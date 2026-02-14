using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.Observability;
using RabbitMqBus.Services;
using RabbitMqBus.SubscriptionManager.Interfaces;

namespace RabbitMqBus.EventBus.Implementations;

public class RabbitMqMessageDispatcher : IRabbitMqMessageDispatcher
{
    private readonly IRabbitMqChannelManager _channelManager;
    private readonly IRabbitMqRetryHandler _retryHandler;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _options;
    private readonly EventBusMetrics? _metrics;
    private readonly IIdempotencyService? _idempotencyService;
    private readonly ILogger<RabbitMqMessageDispatcher> _logger;

    private readonly Dictionary<string, string> _consumerTags = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IResponse>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _consumerSemaphores = new();
    private bool _disposed;

    public RabbitMqMessageDispatcher(
        IRabbitMqChannelManager channelManager,
        IRabbitMqRetryHandler retryHandler,
        ISubscriptionManager subscriptionManager,
        IServiceProvider serviceProvider,
        RabbitMqOptions options,
        ILogger<RabbitMqMessageDispatcher> logger,
        EventBusMetrics? metrics = null,
        IIdempotencyService? idempotencyService = null)
    {
        _channelManager = channelManager;
        _retryHandler = retryHandler;
        _subscriptionManager = subscriptionManager;
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _metrics = metrics;
        _idempotencyService = idempotencyService;
    }

    public async Task StartConsumerAsync(string queueName)
    {
        if (_consumerTags.ContainsKey(queueName))
            return;

        var channel = await _channelManager.GetChannelAsync();
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        var consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _consumerTags[queueName] = consumerTag;

        if (_options.Concurrency.Enabled)
        {
            _consumerSemaphores.TryAdd(
                consumerTag,
                new SemaphoreSlim(
                    _options.Concurrency.MaxDegreeOfParallelism,
                    _options.Concurrency.MaxDegreeOfParallelism));
        }

        _logger.LogInformation(
            "Consumer запущен для очереди {QueueName}, concurrency: {MaxParallelism}",
            queueName,
            _options.Concurrency.Enabled ? _options.Concurrency.MaxDegreeOfParallelism : -1);
    }

    public async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        var stopwatch = Stopwatch.StartNew();
        var eventName = eventArgs.BasicProperties.Type ?? eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        var messageId = eventArgs.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
        var channel = await _channelManager.GetChannelAsync();

        SemaphoreSlim? semaphore = null;
        if (_options.Concurrency.Enabled &&
            _consumerSemaphores.TryGetValue(eventArgs.ConsumerTag, out semaphore))
        {
            await semaphore.WaitAsync();
        }

        try
        {
            if (await TryHandleIdempotencyAsync(eventName, messageId, eventArgs.DeliveryTag, channel))
                return;

            if (await TryHandlePendingRequestAsync(eventName, message, eventArgs, channel))
                return;

            await DispatchToHandlerAsync(eventName, message, eventArgs, channel, stopwatch, messageId);
        }
        finally
        {
            semaphore?.Release();
        }
    }

    public void RegisterPendingRequest(string correlationId, TaskCompletionSource<IResponse> tcs)
    {
        _pendingRequests.TryAdd(correlationId, tcs);
    }

    public bool RemovePendingRequest(string correlationId)
    {
        return _pendingRequests.TryRemove(correlationId, out _);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        var channel = await _channelManager.GetChannelAsync();

        foreach (var (queueName, consumerTag) in _consumerTags)
        {
            try
            {
                await channel.BasicCancelAsync(consumerTag);
                _logger.LogDebug($"Консьюмер для очереди {queueName} остановлен");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Ошибка остановки консьюмера для {queueName}");
            }
        }

        foreach (var semaphore in _consumerSemaphores.Values)
        {
            semaphore.Dispose();
        }
    }

    private async Task<bool> TryHandleIdempotencyAsync(
        string eventName,
        string messageId,
        ulong deliveryTag,
        IChannel channel)
    {
        if (!_options.Idempotency.Enabled || _idempotencyService == null)
            return false;

        if (!_idempotencyService.IsDuplicate(messageId))
            return false;

        _metrics?.RecordDuplicate(eventName);
        _logger.LogWarning($"Дубликат сообщения {messageId}");
        await channel.BasicAckAsync(deliveryTag, multiple: false);
        return true;
    }

    private async Task<bool> TryHandlePendingRequestAsync(
        string eventName,
        string message,
        BasicDeliverEventArgs eventArgs,
        IChannel channel)
    {
        if (eventArgs.BasicProperties.CorrelationId == null)
            return false;

        if (!_pendingRequests.TryGetValue(eventArgs.BasicProperties.CorrelationId, out var tcs))
            return false;

        var responseEventType = _subscriptionManager.GetEventTypeByName(eventName);
        IResponse? response = null;
        try
        {
            response = JsonSerializer.Deserialize(message, responseEventType) as IResponse;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Ошибка десериализации события {eventName}");
        }
        
        tcs.TrySetResult(response);
        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        return true;
    }

    private async Task DispatchToHandlerAsync(
        string eventName,
        string message,
        BasicDeliverEventArgs eventArgs,
        IChannel channel,
        Stopwatch stopwatch,
        string messageId)
    {
        if (!_subscriptionManager.HasSubscriptionsForEvent(eventName))
        {
            _logger.LogWarning($"Нет подписки на событие: {eventName}");
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            return;
        }

        var subscriptions = _subscriptionManager.GetHandlersForEvent(eventName).ToList();
        if (subscriptions.Count == 0)
        {
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, false);
            return;
        }

        var subscription = subscriptions.First();
        var eventType = _subscriptionManager.GetEventTypeByName(eventName);
        var integrationEvent = JsonSerializer.Deserialize(message, eventType) as IEvent;

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
        var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

        try
        {
            await (Task)concreteType.GetMethod("HandleAsync")!.Invoke(handler, new object[] { integrationEvent, CancellationToken.None })!;

            if (_options.Idempotency.Enabled && _idempotencyService != null)
            {
                _idempotencyService.MarkAsProcessed(messageId);
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);

            stopwatch.Stop();
            _metrics?.RecordConsumed(eventName, stopwatch.Elapsed.TotalMilliseconds, true);
            _logger.LogDebug($"Сообщение {eventName} обработано за {stopwatch.Elapsed.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordConsumed(eventName, stopwatch.Elapsed.TotalMilliseconds, false);
            _logger.LogError(ex, $"Ошибка обработки сообщения {eventName}");

            if (await _retryHandler.TryRetryAsync(eventArgs, eventName))
            {
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            }
            else
            {
                await channel.BasicNackAsync(eventArgs.DeliveryTag, false, false);
            }
        }
    }
}

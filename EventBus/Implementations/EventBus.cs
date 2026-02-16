using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.SubscriptionManager.Interfaces;

namespace RabbitMqBus.EventBus.Implementations;

public class EventBusRabbitMq : IEventBus, IAsyncDisposable
{
    private readonly IRabbitMqChannelManager _channelManager;
    private readonly IRabbitMqPublisher _publisher;
    private readonly IRabbitMqMessageDispatcher _dispatcher;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILogger<EventBusRabbitMq> _logger;
    private bool _disposed;

    public EventBusRabbitMq(
        IRabbitMqChannelManager channelManager,
        IRabbitMqPublisher publisher,
        IRabbitMqMessageDispatcher dispatcher,
        ISubscriptionManager subscriptionManager,
        ILogger<EventBusRabbitMq> logger)
    {
        _channelManager = channelManager;
        _publisher = publisher;
        _dispatcher = dispatcher;
        _subscriptionManager = subscriptionManager;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var eventName = typeof(TEvent).Name;
        var exchangeType = _channelManager.GetExchangeType(eventName);

        if (exchangeType == null)
        {
            _logger.LogWarning("Событие {EventName} не зарегистрировано. Используется Fanout по умолчанию.", eventName);
            exchangeType = EventExchangeType.Fanout;
            await _channelManager.EnsureExchangeAsync<TEvent>(exchangeType.Value, cancellationToken);
        }

        var routingKey = exchangeType == EventExchangeType.Direct ? eventName : string.Empty;
        await PublishToEventExchangeAsync(@event, eventName, exchangeType.Value, routingKey, cancellationToken);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, string customRoutingKey, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var eventName = typeof(TEvent).Name;
        var exchangeType = _channelManager.GetExchangeType(eventName);

        if (exchangeType == null)
        {
            _logger.LogWarning("Событие {EventName} не зарегистрировано. Используется Direct по умолчанию для кастомного routing key.", eventName);
            exchangeType = EventExchangeType.Direct;
            await _channelManager.EnsureExchangeAsync<TEvent>(exchangeType.Value, cancellationToken);
        }

        await PublishToEventExchangeAsync(@event, eventName, exchangeType.Value, customRoutingKey, cancellationToken);
    }

    public async Task PublishToExchangeAsync<TEvent>(TEvent @event, string customExchangeName, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var eventName = typeof(TEvent).Name;
        await _publisher.PublishAsync(@event, customExchangeName, routingKey, eventName, cancellationToken);
    }

    private async Task PublishToEventExchangeAsync<TEvent>(TEvent @event, string eventName, EventExchangeType exchangeType, string routingKey, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        var exchangeName = $"exchange.{eventName}";
        await _publisher.PublishAsync(@event, exchangeName, routingKey, eventName, cancellationToken);
    }

    public async Task SubscribeAsync<TEvent, THandler>(EventExchangeType exchangeType = EventExchangeType.Fanout)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var routingKey = exchangeType == EventExchangeType.Direct ? eventName : string.Empty;

        await SubscribeInternalAsync<TEvent, THandler>(routingKey, exchangeType, customQueueName: null);
    }

    public async Task SubscribeAsync<TEvent, THandler>(EventExchangeType exchangeType, string customQueueName)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var routingKey = exchangeType == EventExchangeType.Direct ? eventName : string.Empty;

        await SubscribeInternalAsync<TEvent, THandler>(routingKey, exchangeType, customQueueName);
    }

    public async Task SubscribeAsync<TEvent, THandler>(string customRoutingKey, EventExchangeType exchangeType = EventExchangeType.Direct)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        await SubscribeInternalAsync<TEvent, THandler>(customRoutingKey, exchangeType, customQueueName: null);
    }

    public async Task SubscribeToCustomExchangeAsync<TEvent, THandler>(string customExchangeName, string routingKey, string queueName, EventExchangeType? exchangeType = null)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var handlerType = typeof(THandler);
        var channel = await _channelManager.GetChannelAsync();

        if (exchangeType.HasValue)
        {
            var rabbitExchangeType = exchangeType.Value switch
            {
                EventExchangeType.Direct => ExchangeType.Direct,
                EventExchangeType.Topic => ExchangeType.Topic,
                _ => ExchangeType.Fanout
            };

            await channel.ExchangeDeclareAsync(
                exchange: customExchangeName,
                type: rabbitExchangeType,
                durable: true,
                autoDelete: false);

            _logger.LogInformation($"Exchange {customExchangeName} создан с типом {exchangeType}");
        }

        await BindQueueToExchangeAsync<TEvent, THandler>(queueName, customExchangeName, routingKey, eventName, handlerType.Name, null);
    }

    public void Unsubscribe<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        _subscriptionManager.RemoveSubscription<TEvent, THandler>();
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMs = 30000, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        where TResponse : IResponse
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
        request.CorrelationId = correlationId;

        var replyQueueName = $"reply.{correlationId}";
        request.ReplyTo = replyQueueName;

        var tcs = new TaskCompletionSource<IResponse>();
        _dispatcher.RegisterPendingRequest(correlationId, tcs);

        try
        {
            var channel = await _channelManager.GetChannelAsync(cancellationToken);

            await channel.QueueDeclareAsync(
                queue: replyQueueName,
                durable: false,
                exclusive: true,
                autoDelete: true,
                cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += _dispatcher.OnMessageReceivedAsync;

            await channel.BasicConsumeAsync(
                queue: replyQueueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: cancellationToken);

            await PublishAsync(request, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));

            if (completedTask != tcs.Task)
            {
                throw new TimeoutException($"Request/Response превысил таймаут {timeoutMs}ms");
            }

            var response = await tcs.Task;
            return (TResponse)response;
        }
        finally
        {
            _dispatcher.RemovePendingRequest(correlationId);

            try
            {
                var channel = await _channelManager.GetChannelAsync(cancellationToken);
                await channel.QueueDeleteAsync(replyQueueName, ifUnused: false, ifEmpty: false, cancellationToken: cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Произошла ошибка");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger.LogInformation("Начинается мягкая остановка EventBus...");

        await _dispatcher.DisposeAsync();

        await Task.Delay(1000);

        await _channelManager.DisposeAsync();

        _logger.LogInformation("EventBus мягко остановлен");
    }

    private async Task SubscribeInternalAsync<TEvent, THandler>(string routingKey, EventExchangeType exchangeType, string? customQueueName = null)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var handlerType = typeof(THandler);
        
        var queueName = !string.IsNullOrEmpty(customQueueName)
            ? customQueueName
            : GenerateQueueName(eventName, handlerType.Name, routingKey);
        
        var exchangeName = $"exchange.{eventName}";
        var dlxName = $"exchange.{eventName}.dlx";

        await _channelManager.EnsureExchangeAsync<TEvent>(exchangeType);

        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", dlxName }
        };

        await BindQueueToExchangeAsync<TEvent, THandler>(queueName, exchangeName, routingKey, eventName, handlerType.Name, queueArgs);
    }

    private async Task BindQueueToExchangeAsync<TEvent, THandler>(string queueName, string exchangeName, string routingKey, string eventName, string handlerName, Dictionary<string, object>? queueArgs)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        _subscriptionManager.AddSubscription<TEvent, THandler>(queueName, routingKey);

        var channel = await _channelManager.GetChannelAsync();

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        _logger.LogInformation(
            $"Подписка на exchange {exchangeName} с routing key '{routingKey}', событие {eventName}, обработчик {handlerName}, очередь {queueName}");

        await _dispatcher.StartConsumerAsync(queueName);
    }

    private static string GenerateQueueName(string eventName, string handlerName, string routingKey)
    {
        var routingKeySuffix = string.IsNullOrEmpty(routingKey)
            ? "default"
            : routingKey.Replace(".", "_").Replace("*", "any").Replace("#", "all");
        
        return $"queue.{eventName}.{handlerName}.{routingKeySuffix}";
    }
}
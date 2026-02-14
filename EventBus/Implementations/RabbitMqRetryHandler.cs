using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.Observability;

namespace RabbitMqBus.EventBus.Implementations;

public class RabbitMqRetryHandler : IRabbitMqRetryHandler
{
    private readonly IRabbitMqChannelManager _channelManager;
    private readonly RabbitMqOptions _options;
    private readonly EventBusMetrics? _metrics;
    private readonly ILogger<RabbitMqRetryHandler> _logger;

    public RabbitMqRetryHandler(
        IRabbitMqChannelManager channelManager,
        RabbitMqOptions options,
        ILogger<RabbitMqRetryHandler> logger,
        EventBusMetrics? metrics = null)
    {
        _channelManager = channelManager;
        _options = options;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<bool> TryRetryAsync(
        BasicDeliverEventArgs eventArgs, 
        string eventName, 
        CancellationToken cancellationToken = default)
    {
        if (!_options.RetryPolicy.Enabled)
            return false;

        var retryCount = GetRetryCount(eventArgs.BasicProperties);
        
        if (retryCount >= _options.RetryPolicy.MaxRetryAttempts)
        {
            _logger.LogError($"Сообщение {eventName} превысило лимит повторов ({_options.RetryPolicy.MaxRetryAttempts})");
            return false;
        }

        var nextAttempt = retryCount + 1;
        var retryExchange = $"exchange.{eventName}.retry-{nextAttempt}";
        
        await PublishToRetryQueueAsync(eventArgs, retryExchange, nextAttempt, eventName, cancellationToken);
        
        _metrics?.RecordRetry(eventName, nextAttempt);
        _logger.LogWarning($"Сообщение {eventName} отправлено на повтор {nextAttempt}/{_options.RetryPolicy.MaxRetryAttempts}");
        
        return true;
    }

    public int GetRetryCount(IReadOnlyBasicProperties? properties)
    {
        if (properties?.Headers != null && properties.Headers.TryGetValue("x-retry-count", out var value))
        {
            return value is byte[] bytes ? BitConverter.ToInt32(bytes, 0) : 0;
        }
        return 0;
    }

    private async Task PublishToRetryQueueAsync(
        BasicDeliverEventArgs originalArgs, 
        string retryExchange, 
        int attemptNumber, 
        string eventName,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Type = originalArgs.BasicProperties?.Type ?? eventName,
            MessageId = originalArgs.BasicProperties?.MessageId,
            CorrelationId = originalArgs.BasicProperties?.CorrelationId,
            ContentType = originalArgs.BasicProperties?.ContentType ?? "application/json",
            Headers = new Dictionary<string, object>
            {
                { "x-retry-count", attemptNumber },
                { "x-original-queue", originalArgs.RoutingKey },
                { "x-first-death-reason", "rejected" }
            }!
        };

        var channel = await _channelManager.GetChannelAsync(cancellationToken);
        
        await channel.BasicPublishAsync(
            exchange: retryExchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: originalArgs.Body.ToArray(),
            cancellationToken: cancellationToken);
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.Observability;

namespace RabbitMqBus.EventBus.Implementations;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IRabbitMqChannelManager _channelManager;
    private readonly RabbitMqOptions _options;
    private readonly EventBusMetrics? _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IRabbitMqChannelManager channelManager,
        RabbitMqOptions options,
        ILogger<RabbitMqPublisher> logger,
        EventBusMetrics? metrics = null)
    {
        _channelManager = channelManager;
        _options = options;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event, 
        string exchangeName, 
        string routingKey, 
        string eventName,
        CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var stopwatch = Stopwatch.StartNew();
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = BuildMessageProperties(@event, eventName);

        try
        {
            var channel = await _channelManager.GetChannelAsync(cancellationToken);

            if (@event.EventId == null || @event.EventId == Guid.Empty)
            {
                @event.EventId = Guid.NewGuid();
            }

            if (@event.CreatedOn == null || @event.CreatedOn == DateTime.MinValue)
            {
                @event.CreatedOn = DateTime.UtcNow;
            }
            
            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            stopwatch.Stop();
            _metrics?.RecordPublished(eventName, stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogDebug($"Опубликовано событие {eventName} в {exchangeName} (routing: '{routingKey}'), Id: {@event.EventId}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, $"Ошибка публикации {eventName}, длительность: {stopwatch.Elapsed.TotalMilliseconds}ms");
            throw;
        }
    }

    private BasicProperties BuildMessageProperties<TEvent>(TEvent @event, string eventName) where TEvent : IEvent
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Type = eventName,
            MessageId = @event.EventId.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json"
        };

        if (_options.MessageTtl.Enabled)
        {
            properties.Expiration = _options.MessageTtl.DefaultTtlMs.ToString();
        }

        if (@event is IRequest request)
        {
            properties.CorrelationId = request.CorrelationId;
            properties.ReplyTo = request.ReplyTo;
        }

        return properties;
    }
}

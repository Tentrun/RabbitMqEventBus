using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.Rmq.Interfaces;

namespace RabbitMqBus.EventBus.Implementations;

public class RabbitMqChannelManager : IRabbitMqChannelManager
{
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqChannelManager> _logger;
    
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;
    private readonly Dictionary<string, EventExchangeType> _eventExchangeTypes = new();
    private readonly HashSet<string> _declaredExchanges = new();
    private bool _disposed;

    public RabbitMqChannelManager(
        IRabbitMqConnection connection,
        RabbitMqOptions options,
        ILogger<RabbitMqChannelManager> logger)
    {
        _connection = connection;
        _options = options;
        _logger = logger;
    }

    public async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken = default)
    {
        if (_channel != null && _channel.IsOpen)
        {
            return _channel;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel != null && _channel.IsOpen)
                return _channel;

            if (!_connection.IsConnected)
                await _connection.TryConnectAsync(cancellationToken);

            var connection = _connection.GetConnection();
            _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            if (_options.Prefetch.Enabled)
            {
                await _channel.BasicQosAsync(
                    prefetchSize: 0, 
                    prefetchCount: _options.Prefetch.PrefetchCount, 
                    global: _options.Prefetch.GlobalQos, 
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation($"Prefetch Count активен: {_options.Prefetch.PrefetchCount}");
            }

            _logger.LogInformation("RabbitMQ подключение установлено");
            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task EnsureExchangeAsync<TEvent>(EventExchangeType exchangeType, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var channel = await GetChannelAsync(cancellationToken);
        var eventName = typeof(TEvent).Name;
        var exchangeName = $"exchange.{eventName}";
        var dlxName = $"exchange.{eventName}.dlx";
        var dlqName = $"queue.{eventName}.dlq";

        if (_declaredExchanges.Contains(exchangeName))
        {
            if (_eventExchangeTypes.TryGetValue(eventName, out var existingType) && existingType != exchangeType)
            {
                _logger.LogWarning($"Exchange {exchangeName} уже создан с типом {existingType}, запрошен {exchangeType}");
            }
            return;
        }

        var rabbitExchangeType = exchangeType switch
        {
            EventExchangeType.Direct => ExchangeType.Direct,
            EventExchangeType.Topic => ExchangeType.Topic,
            _ => ExchangeType.Fanout
        };

        await channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        if (_options.RetryPolicy.Enabled)
        {
            await CreateRetryInfrastructureAsync(channel, eventName, exchangeName, cancellationToken);
        }

        try
        {
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: rabbitExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex)
            when (ex.ShutdownReason?.ReplyCode == 406)
        {
            _logger.LogWarning(
                "Exchange {ExchangeName} существует с другим типом. Удаление и пересоздание с типом {ExchangeType}...",
                exchangeName, exchangeType);

            _channel = null;
            channel = await GetChannelAsync(cancellationToken);

            await channel.ExchangeDeleteAsync(exchangeName, ifUnused: false, cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: rabbitExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        _eventExchangeTypes[eventName] = exchangeType;
        _declaredExchanges.Add(exchangeName);
        _declaredExchanges.Add(dlxName);

        _logger.LogInformation($"Exchange {exchangeName} создан ({exchangeType}), DLX {dlxName}, DLQ {dlqName}, Retry: {_options.RetryPolicy.Enabled}");
    }

    public EventExchangeType? GetExchangeType(string eventName)
    {
        return _eventExchangeTypes.TryGetValue(eventName, out var type) ? type : null;
    }

    private async Task CreateRetryInfrastructureAsync(IChannel channel, string eventName, string exchangeName, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _options.RetryPolicy.MaxRetryAttempts; attempt++)
        {
            var delay = CalculateRetryDelay(attempt);
            var retryQueueName = $"queue.{eventName}.retry-{attempt}";
            var retryExchangeName = $"exchange.{eventName}.retry-{attempt}";

            await channel.ExchangeDeclareAsync(
                exchange: retryExchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var retryQueueArgs = new Dictionary<string, object>
            {
                { "x-message-ttl", delay },
                { "x-dead-letter-exchange", exchangeName }
            };

            await channel.QueueDeclareAsync(
                queue: retryQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retryQueueArgs!,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: retryQueueName,
                exchange: retryExchangeName,
                routingKey: string.Empty,
                cancellationToken: cancellationToken);

            _declaredExchanges.Add(retryExchangeName);
        }
    }

    private int CalculateRetryDelay(int attemptNumber)
    {
        var delay = _options.RetryPolicy.InitialDelayMs * Math.Pow(_options.RetryPolicy.BackoffMultiplier, attemptNumber - 1);
        return (int)Math.Min(delay, _options.RetryPolicy.MaxDelayMs);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_channel is { IsOpen: true })
        {
            try
            {
                await _channel.CloseAsync();
                _logger.LogInformation("RabbitMQ подключение закрыто");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка закрытия подключения к RabbitMq");
            }
        }

        _channel?.Dispose();
        await _connection.DisposeAsync();
        _channelLock.Dispose();
    }
}

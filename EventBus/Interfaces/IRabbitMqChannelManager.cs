using RabbitMQ.Client;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.Models;

namespace RabbitMqBus.EventBus.Interfaces;

public interface IRabbitMqChannelManager : IAsyncDisposable
{
    Task<IChannel> GetChannelAsync(CancellationToken cancellationToken = default);

    Task EnsureExchangeAsync<TEvent>(EventExchangeType exchangeType, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    EventExchangeType? GetExchangeType(string eventName);
}

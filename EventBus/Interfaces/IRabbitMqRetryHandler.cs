using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqBus.EventBus.Interfaces;

public interface IRabbitMqRetryHandler
{
    Task<bool> TryRetryAsync(
        BasicDeliverEventArgs eventArgs,
        string eventName,
        CancellationToken cancellationToken = default);

    int GetRetryCount(IReadOnlyBasicProperties properties);
}

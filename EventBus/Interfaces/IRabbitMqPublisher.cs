using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.EventBus.Interfaces;

public interface IRabbitMqPublisher
{
    Task PublishAsync<TEvent>(
        TEvent @event,
        string exchangeName,
        string routingKey,
        string eventName,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}

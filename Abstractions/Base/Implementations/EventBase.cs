using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.Abstractions.Base.Implementations;

public abstract class EventBase : IEvent
{
    private Guid EventId { get; set; } = Guid.NewGuid();

    Guid IEvent.EventId
    {
        get => EventId;
        set => EventId = value;
    }

    private DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    DateTime IEvent.CreatedOn
    {
        get => CreatedOn;
        set => CreatedOn = value;
    }
}
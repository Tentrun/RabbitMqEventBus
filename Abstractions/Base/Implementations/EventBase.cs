using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.Abstractions.Base.Implementations;

public abstract class EventBase : IEvent
{
    private Guid Id { get; set; } = Guid.NewGuid();

    Guid IEvent.Id
    {
        get => Id;
        set => Id = value;
    }

    private DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    DateTime IEvent.CreatedOn
    {
        get => CreatedOn;
        set => CreatedOn = value;
    }
}
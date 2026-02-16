namespace RabbitMqBus.Abstractions.Base.Interfaces;

public interface IEvent
{
    Guid EventId { get; set; }
    DateTime CreatedOn { get; set; }
}
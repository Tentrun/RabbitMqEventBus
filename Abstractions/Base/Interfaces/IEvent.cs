namespace RabbitMqBus.Abstractions.Base.Interfaces;

public interface IEvent
{
    Guid Id { get; set; }
    DateTime CreatedOn { get; set; }
}
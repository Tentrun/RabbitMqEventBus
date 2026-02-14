namespace RabbitMqBus.Abstractions.Base.Interfaces;

public interface IRequest : IEvent
{
    string? CorrelationId { get; set; }
    string? ReplyTo { get; set; }
}

public interface IResponse : IEvent
{
    string? CorrelationId { get; set; }
}

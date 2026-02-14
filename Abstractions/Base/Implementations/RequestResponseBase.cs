using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.Abstractions.Base.Implementations;

public abstract class RequestBase : IRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? ReplyTo { get; set; }
}

public abstract class ResponseBase : IResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}

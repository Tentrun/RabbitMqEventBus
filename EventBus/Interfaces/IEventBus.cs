using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.Models;

namespace RabbitMqBus.EventBus.Interfaces;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken token = default) 
        where T : IEvent;
    
    Task PublishAsync<T>(T @event, string customRoutingKey, CancellationToken token = default) 
        where T : IEvent;
    
    Task PublishToExchangeAsync<T>(T @event, string customExchangeName, string routingKey, CancellationToken token = default)
        where T : IEvent;
    
    Task SubscribeAsync<T, THandler>(EventExchangeType exchangeType = EventExchangeType.Fanout)
        where T : IEvent
        where THandler : IEventHandler<T>;
    
    Task SubscribeAsync<T, THandler>(EventExchangeType exchangeType, string customQueueName)
        where T : IEvent
        where THandler : IEventHandler<T>;
    
    Task SubscribeAsync<T, THandler>(string customRoutingKey, EventExchangeType exchangeType = EventExchangeType.Direct)
        where T : IEvent
        where THandler : IEventHandler<T>;
    
    Task SubscribeToCustomExchangeAsync<T, THandler>(string customExchangeName, string routingKey, string queueName, EventExchangeType? exchangeType = null)
        where T : IEvent
        where THandler : IEventHandler<T>;
    
    void Unsubscribe<T, THandler>()
        where T : IEvent
        where THandler : IEventHandler<T>;
    
    Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMs = 30000, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        where TResponse : IResponse;
}
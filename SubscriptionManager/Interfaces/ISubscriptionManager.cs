using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.SubscriptionManager.Interfaces;

public interface ISubscriptionManager
{
    bool IsEmpty { get; }
    event EventHandler<string> OnEventRemoved;
    
    void AddSubscription<TEvent, THandler>(string queueName)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>;
    
    void AddSubscription<TEvent, THandler>(string queueName, string routingKey)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>;
    
    void RemoveSubscription<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>;
    
    bool HasSubscriptionsForEvent<TEvent>() where TEvent : IEvent;
    bool HasSubscriptionsForEvent(string eventName);
    
    Type GetEventTypeByName(string eventName);
    string GetEventKey<TEvent>();
    
    IEnumerable<SubscriptionInfo> GetHandlersForEvent<TEvent>() where TEvent : IEvent;
    IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName);
    void Clear();
}

public record SubscriptionInfo(Type HandlerType, string QueueName, string RoutingKey);
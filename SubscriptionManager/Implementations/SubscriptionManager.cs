using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.SubscriptionManager.Interfaces;

namespace RabbitMqBus.SubscriptionManager.Implementations;

public class SubscriptionManager : ISubscriptionManager
{
    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers = new();
    private readonly List<Type> _eventTypes = new();
    private readonly Lock _lock = new();

    public event EventHandler<string> OnEventRemoved;

    public bool IsEmpty => !_handlers.Keys.Any();

    public void AddSubscription<TEvent, THandler>(string queueName)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        AddSubscription<TEvent, THandler>(queueName, string.Empty);
    }

    public void AddSubscription<TEvent, THandler>(string queueName, string routingKey)
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = GetEventKey<TEvent>();
        var handlerType = typeof(THandler);

        lock (_lock)
        {
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
                _eventTypes.Add(typeof(TEvent));
            }

            if (_handlers[eventName].Any(s => s.HandlerType == handlerType && s.QueueName == queueName))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}' with queue '{queueName}'", nameof(handlerType));
            }

            _handlers[eventName].Add(new SubscriptionInfo(handlerType, queueName, routingKey));
        }
    }

    public void RemoveSubscription<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = GetEventKey<TEvent>();
        var handlerType = typeof(THandler);

        lock (_lock)
        {
            if (_handlers.TryGetValue(eventName, out var handlers))
            {
                var toRemove = handlers.FirstOrDefault(s => s.HandlerType == handlerType);
                if (toRemove != null)
                {
                    handlers.Remove(toRemove);
                    if (!handlers.Any())
                    {
                        _handlers.Remove(eventName);
                        _eventTypes.Remove(typeof(TEvent));
                        OnEventRemoved?.Invoke(this, eventName);
                    }
                }
            }
        }
    }

    public bool HasSubscriptionsForEvent<TEvent>() where TEvent : IEvent
        => HasSubscriptionsForEvent(GetEventKey<TEvent>());

    public bool HasSubscriptionsForEvent(string eventName)
        => _handlers.ContainsKey(eventName);

    public Type GetEventTypeByName(string eventName)
        => _eventTypes.SingleOrDefault(t => t.Name == eventName);

    public string GetEventKey<TEvent>()
        => typeof(TEvent).Name;

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<TEvent>() where TEvent : IEvent
        => GetHandlersForEvent(GetEventKey<TEvent>());

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName)
        => _handlers.TryGetValue(eventName, out var handlers) 
            ? handlers 
            : Enumerable.Empty<SubscriptionInfo>();

    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _eventTypes.Clear();
        }
    }
}
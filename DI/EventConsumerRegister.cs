using System.Reflection;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;

namespace RabbitMqBus.DI;

public class EventConsumerRegister
{
    private readonly List<ConsumerRegistration> _registrations = new();

    public void AddConsumer<THandler>(EventExchangeType exchangeType, string? customQueueName = null) 
        where THandler : class
    {
        var (eventType, handlerType) = ResolveTypes<THandler>();
        var action = BuildStandardSubscribeAction(eventType, handlerType, exchangeType, customQueueName);
        _registrations.Add(new ConsumerRegistration(eventType, handlerType, action));
    }

    public void AddConsumer<TEvent, THandler>(EventExchangeType exchangeType, string? customQueueName = null) 
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        Func<IEventBus, Task> action = customQueueName != null
            ? bus => bus.SubscribeAsync<TEvent, THandler>(exchangeType, customQueueName)
            : bus => bus.SubscribeAsync<TEvent, THandler>(exchangeType);

        _registrations.Add(new ConsumerRegistration(typeof(TEvent), typeof(THandler), action));
    }

    public void AddConsumer<THandler>(string exchangeName, string routingKey, string queueName)
        where THandler : class
    {
        var (eventType, handlerType) = ResolveTypes<THandler>();
        var action = BuildCustomSubscribeAction(eventType, handlerType, exchangeName, routingKey, queueName);
        _registrations.Add(new ConsumerRegistration(eventType, handlerType, action));
    }

    public void AddConsumer<TEvent, THandler>(string exchangeName, string routingKey, string queueName) 
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        Func<IEventBus, Task> action = bus => 
            bus.SubscribeToCustomExchangeAsync<TEvent, THandler>(exchangeName, routingKey, queueName);

        _registrations.Add(new ConsumerRegistration(typeof(TEvent), typeof(THandler), action));
    }

    public IEnumerable<ConsumerRegistration> GetRegistrations() => _registrations;

    private static (Type EventType, Type HandlerType) ResolveTypes<THandler>() where THandler : class
    {
        var handlerType = typeof(THandler);
        var eventHandlerInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            ?? throw new ArgumentException($"{handlerType.Name} должен реализовывать IEventHandler<T>!");

        return (eventHandlerInterface.GetGenericArguments()[0], handlerType);
    }

    private static Func<IEventBus, Task> BuildStandardSubscribeAction(
        Type eventType, Type handlerType, EventExchangeType exchangeType, string? customQueueName)
    {
        var paramCount = customQueueName != null ? 2 : 1;

        var method = typeof(IEventBus)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(IEventBus.SubscribeAsync)
                        && m.IsGenericMethodDefinition
                        && m.GetGenericArguments().Length == 2
                        && m.GetParameters().Length == paramCount
                        && m.GetParameters()[0].ParameterType == typeof(EventExchangeType))
            .MakeGenericMethod(eventType, handlerType);

        return customQueueName != null
            ? bus => (Task)method.Invoke(bus, [exchangeType, customQueueName])!
            : bus => (Task)method.Invoke(bus, [exchangeType])!;
    }

    private static Func<IEventBus, Task> BuildCustomSubscribeAction(
        Type eventType, Type handlerType, string exchangeName, string routingKey, string queueName)
    {
        var method = typeof(IEventBus)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(IEventBus.SubscribeToCustomExchangeAsync)
                        && m.IsGenericMethodDefinition
                        && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(eventType, handlerType);

        return bus => (Task)method.Invoke(bus, [exchangeName, routingKey, queueName, null])!;
    }

    public record ConsumerRegistration(Type EventType, Type HandlerType, Func<IEventBus, Task> SubscribeAction);
}
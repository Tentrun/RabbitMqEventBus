using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.Models;

namespace RabbitMqBus.DI;

public class EventConsumerRegister
{
    private readonly List<ConsumerRegistration> _registrations = new();

    public void AddConsumer<THandler>(EventExchangeType exchangeType = EventExchangeType.Fanout) where THandler : class
    {
        var handlerType = typeof(THandler);
        var eventHandlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .ToList();

        if (!eventHandlerInterfaces.Any())
            throw new ArgumentException($"{handlerType.Name} должен наследоваться и реализовывать от IEventHandler<T>!");

        foreach (var iface in eventHandlerInterfaces)
        {
            var eventType = iface.GetGenericArguments()[0];
            _registrations.Add(new ConsumerRegistration(eventType, handlerType, exchangeType));
        }
    }

    public IEnumerable<ConsumerRegistration> GetRegistrations() => _registrations;

    public record ConsumerRegistration(Type EventType, Type HandlerType, EventExchangeType ExchangeType);
}
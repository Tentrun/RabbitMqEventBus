using Microsoft.Extensions.DependencyInjection;
using RabbitMqBus.Abstractions.Base.Interfaces;
using RabbitMqBus.DI;
using RabbitMqBus.Models;

namespace RabbitMqBus;

public class RabbitMqEventBusBuilder
{
    private readonly IServiceCollection _services;
    private readonly EventConsumerRegister _register = new();

    internal RabbitMqEventBusBuilder(IServiceCollection services)
    {
        _services = services;
        _services.AddSingleton(_register);
    }

    public RabbitMqEventBusBuilder AddConsumer<THandler>(EventExchangeType exchangeType) 
        where THandler : class
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<THandler>(exchangeType);
        return this;
    }

    public RabbitMqEventBusBuilder AddConsumer<THandler>(EventExchangeType exchangeType, string customQueueName) 
        where THandler : class
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<THandler>(exchangeType, customQueueName);
        return this;
    }

    public RabbitMqEventBusBuilder AddConsumer<THandler>(string customExchangeName, string routingKey, string queueName)
        where THandler : class
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<THandler>(customExchangeName, routingKey, queueName);
        return this;
    }

    public RabbitMqEventBusBuilder AddConsumer<TEvent, THandler>(EventExchangeType exchangeType) 
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<TEvent, THandler>(exchangeType);
        return this;
    }

    public RabbitMqEventBusBuilder AddConsumer<TEvent, THandler>(EventExchangeType exchangeType, string customQueueName) 
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<TEvent, THandler>(exchangeType, customQueueName);
        return this;
    }

    public RabbitMqEventBusBuilder AddConsumer<TEvent, THandler>(string customExchangeName, string routingKey, string queueName)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        _services.AddScoped<THandler>();
        _register.AddConsumer<TEvent, THandler>(customExchangeName, routingKey, queueName);
        return this;
    }
}
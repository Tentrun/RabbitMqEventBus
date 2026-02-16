using Microsoft.Extensions.DependencyInjection;
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
}
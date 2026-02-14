using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMqBus.BackgroundServices;
using RabbitMqBus.EventBus.Implementations;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.HealthChecks;
using RabbitMqBus.Models;
using RabbitMqBus.Observability;
using RabbitMqBus.Rmq.Implementations;
using RabbitMqBus.Rmq.Interfaces;
using RabbitMqBus.Services;
using RabbitMqBus.SubscriptionManager.Interfaces;

namespace RabbitMqBus.DI;

public static class AddRmqBusToDi
{
    public static RabbitMqEventBusBuilder AddRabbitMqEventBus(this IServiceCollection services,
        Action<RabbitMqOptions> configureOptions)
    {
        var options = new RabbitMqOptions();
        configureOptions(options);
        services.AddSingleton(options);

        services.AddSingleton<IConnectionFactory>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };
            return factory;
        });

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();

        services.AddSingleton<ISubscriptionManager, SubscriptionManager.Implementations.SubscriptionManager>();

        if (options.Observability.MetricsEnabled)
        {
            services.AddSingleton<EventBusMetrics>();
        }

        if (options.Idempotency.Enabled)
        {
            services.AddSingleton<IIdempotencyService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IdempotencyService>>();
                return new IdempotencyService(
                    options.Idempotency.CacheDurationMs,
                    options.Idempotency.MaxCacheSize,
                    logger);
            });
        }

        services.AddSingleton<IRabbitMqChannelManager>(sp => new RabbitMqChannelManager(
            sp.GetRequiredService<IRabbitMqConnection>(),
            options,
            sp.GetRequiredService<ILogger<RabbitMqChannelManager>>()));

        services.AddSingleton<IRabbitMqPublisher>(sp => new RabbitMqPublisher(
            sp.GetRequiredService<IRabbitMqChannelManager>(),
            options,
            sp.GetRequiredService<ILogger<RabbitMqPublisher>>(),
            sp.GetService<EventBusMetrics>()));

        services.AddSingleton<IRabbitMqRetryHandler>(sp => new RabbitMqRetryHandler(
            sp.GetRequiredService<IRabbitMqChannelManager>(),
            options,
            sp.GetRequiredService<ILogger<RabbitMqRetryHandler>>(),
            sp.GetService<EventBusMetrics>()));

        services.AddSingleton<IRabbitMqMessageDispatcher>(sp => new RabbitMqMessageDispatcher(
            sp.GetRequiredService<IRabbitMqChannelManager>(),
            sp.GetRequiredService<IRabbitMqRetryHandler>(),
            sp.GetRequiredService<ISubscriptionManager>(),
            sp,
            options,
            sp.GetRequiredService<ILogger<RabbitMqMessageDispatcher>>(),
            sp.GetService<EventBusMetrics>(),
            sp.GetService<IIdempotencyService>()));

        services.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(
            sp.GetRequiredService<IRabbitMqChannelManager>(),
            sp.GetRequiredService<IRabbitMqPublisher>(),
            sp.GetRequiredService<IRabbitMqMessageDispatcher>(),
            sp.GetRequiredService<ISubscriptionManager>(),
            sp.GetRequiredService<ILogger<EventBusRabbitMq>>()));

        services.AddHostedService<EventBusBackgroundService>();

        return new RabbitMqEventBusBuilder(services);
    }
    
    public static IServiceCollection AddRabbitMqHealthCheck(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<RabbitMqHealthCheck>("rabbitmq");
        
        return services;
    }
}
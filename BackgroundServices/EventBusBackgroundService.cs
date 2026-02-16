using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMqBus.DI;
using RabbitMqBus.EventBus.Interfaces;
using RabbitMqBus.Models;
using RabbitMqBus.Rmq.Interfaces;

namespace RabbitMqBus.BackgroundServices;

public class EventBusBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventConsumerRegister _registrar;
    private readonly ILogger<EventBusBackgroundService> _logger;
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(30);

    public EventBusBackgroundService(
        IServiceProvider serviceProvider,
        EventConsumerRegister registrar,
        ILogger<EventBusBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _registrar = registrar;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<IRabbitMqConnection>();

        _logger.LogInformation("Подключение к RabbitMQ...");
        
        var startTime = DateTime.UtcNow;
        var connected = false;
        
        while (!connected && DateTime.UtcNow - startTime < _connectionTimeout)
        {
            try
            {
                connected = await connection.TryConnectAsync(stoppingToken);
                if (!connected)
                {
                    _logger.LogWarning("Не удалось подключиться к RabbitMQ, повтор через 2 секунды...");
                    await Task.Delay(2000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка подключения к RabbitMQ");
                await Task.Delay(2000, stoppingToken);
            }
        }

        if (!connected)
        {
            _logger.LogError("Не удалось подключиться к RabbitMQ за {TimeoutSeconds} секунд.", _connectionTimeout.TotalSeconds);
            return;
        }

        _logger.LogInformation("RabbitMQ подключен. Подписка на события...");

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        foreach (var reg in _registrar.GetRegistrations())
        {
            try
            {
                MethodInfo? method;
                
                if (!string.IsNullOrEmpty(reg.CustomQueueName))
                {
                    method = typeof(IEventBus)
                        .GetMethod(nameof(IEventBus.SubscribeAsync), 
                            2, BindingFlags.Public | BindingFlags.Instance, types: new[] { typeof(EventExchangeType), typeof(string) })?
                        .MakeGenericMethod(reg.EventType, reg.HandlerType);
                    
                    if (method != null)
                    {
                        await (Task)method.Invoke(eventBus, new object[] { reg.ExchangeType, reg.CustomQueueName })!;
                        _logger.LogInformation("Подписка на {EventType} с обработчиком {HandlerType} (тип exchange: {ExchangeType}, очередь: {QueueName})",
                            reg.EventType.Name, reg.HandlerType.Name, reg.ExchangeType, reg.CustomQueueName);
                    }
                }
                else
                {
                    method = typeof(IEventBus)
                        .GetMethod(nameof(IEventBus.SubscribeAsync), 
                            2, BindingFlags.Public | BindingFlags.Instance, types: new[] { typeof(EventExchangeType) })?
                        .MakeGenericMethod(reg.EventType, reg.HandlerType);
                    
                    if (method != null)
                    {
                        await (Task)method.Invoke(eventBus, new object[] { reg.ExchangeType })!;
                        _logger.LogInformation("Подписка на {EventType} с обработчиком {HandlerType} (тип exchange: {ExchangeType})",
                            reg.EventType.Name, reg.HandlerType.Name, reg.ExchangeType);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка подписки на {EventType} с обработчиком {HandlerType}",
                    reg.EventType.Name, reg.HandlerType.Name);
            }
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
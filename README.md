[![NuGet](https://img.shields.io/nuget/v/Tentrun.RabbitMqEventBus)](https://www.nuget.org/packages/Tentrun.RabbitMqEventBus/)
[![License](https://img.shields.io/github/license/Tentrun/RabbitMqEventBus)](LICENSE)

## Обзор

Библиотека для работы с RabbitMQ, предоставляющая все критичные функции:

 **Retry Policy** - автоматические повторные попытки после падения

 **Prefetch Count** - контроль нагрузки на консьюмеры  

 **Health Checks** - мониторинг работоспособности

 **Graceful Shutdown** - мягкое завершение обработки  

 **Message TTL** - время жизни сообщений  

 **Observability** - метрики

 **Request/Response** - стратегия запрос/ответ  

 **Idempotency** - защита от дубликатов  

 **Concurrency Control** - параллельная обработка  

---

## Архитектура событий

Библиотека использует разделение интерфейсов для разных паттернов обмена сообщениями:

###  IEvent - Pub/Sub - Fire-and-Forget
Используйте для **асинхронных событий**, когда не нужен ответ:
```csharp
public class OrderCreatedEvent : IEvent
{
    public Guid Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public string OrderNumber { get; set; }
}
```

###  IRequest / IResponse - Request-Reply
Используйте для **синхронных запросов**, когда нужен ответ:
```csharp
public class GetUserRequest : RequestBase
{
    public int UserId { get; set; }
}

public class GetUserResponse : ResponseBase
{
    public string UserName { get; set; }
}
```

---

## Быстрый старт

### 1. Регистрация в DI

```csharp
// Program.cs
services.AddRabbitMqEventBus(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.VirtualHost = "/";
    
    // Необязательные настройки
});

// Опционально: добавить Health Check
services.AddRabbitMqHealthCheck();
```

### 2. Создание событий

```csharp
// Простое событие
public class OrderCreatedEvent : IEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    
    public string OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
}

// Request/Response
public class GetUserRequest : RequestBase
{
    public int UserId { get; set; }
}

public class GetUserResponse : ResponseBase
{
    public string UserName { get; set; }
    public string Email { get; set; }
}
```

### 3. Публикация событий

```csharp
public class OrderController : ControllerBase
{
    private readonly IEventBus _eventBus;
    
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var @event = new OrderCreatedEvent 
        { 
            OrderNumber = order.Number,
            TotalAmount = order.Total
        };
        
        await _eventBus.PublishAsync(@event);
        
        return Ok();
    }
}
```

### 4. Обработка событий

```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Обработка заказа {OrderNumber}", @event.OrderNumber);
        
        // Бизнес логика
    }
}

// Регистрация в DI
services.AddScoped<OrderCreatedHandler>();

// Program.cs
services.AddRabbitMqEventBus(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.VirtualHost = "/";
    
    // Необязательные настройки
})
.AddConsumer<OrderCreatedHandler>(EventExchangeType.Direct); //Можно настроить exchangeType
```

---

## Детальная конфигурация всех 9 функций

### 1.  Retry Policy - Автоматические повторные попытки

**Что делает:** При ошибке обработки сообщение автоматически отправляется в retry queue с задержкой, увеличивающейся экспоненциально.

**Конфигурация:**

```csharp
options.RetryPolicy.Enabled = true;                  // Вкл/выкл (по умолчанию: true)
options.RetryPolicy.MaxRetryAttempts = 3;            // Макс. попыток (по умолчанию: 3)
options.RetryPolicy.InitialDelayMs = 1000;           // Начальная задержка (по умолчанию: 1000 мс)
options.RetryPolicy.MaxDelayMs = 60000;              // Макс. задержка (по умолчанию: 60 сек)
options.RetryPolicy.BackoffMultiplier = 2.0;         // Множитель (по умолчанию: 2.0)
```

**Как работает:**

- Попытка 1: задержка = 1000 мс
- Попытка 2: задержка = 2000 мс
- Попытка 3: задержка = 4000 мс
- После 3 попыток → отправка в DLQ (Dead Letter Queue)

**Пример использования:**

```csharp
public class PaymentHandler : IEventHandler<PaymentEvent>
{
    public async Task HandleAsync(PaymentEvent @event, CancellationToken ct)
    {
        // Если здесь exception, сообщение автоматически попадёт в retry
        await ProcessPaymentAsync(@event.Amount);
    }
}
```

---

### 2. Prefetch Count - Контроль нагрузки

**Что делает:** Ограничивает количество необработанных сообщений на одном консюмере, предотвращая перегрузку.

**Конфигурация:**

```csharp
options.Prefetch.Enabled = true;                     // Вкл/выкл (по умолчанию: true)
options.Prefetch.PrefetchCount = 10;                 // Кол-во сообщений (по умолчанию: 10)
options.Prefetch.GlobalQos = false;                  // Глобальный QoS (по умолчанию: false)
```

---

### 3. Health Checks - Мониторинг

**Что делает:** Предоставляет endpoint для проверки подключения к RabbitMQ.

**Регистрация:**

```csharp
// Program.cs
builder.Services.AddRabbitMqHealthCheck();

app.MapHealthChecks("/health");
```

**Ответ:**

```json
{
  "status": "Healthy",
  "results": {
    "rabbitmq": {
      "status": "Healthy",
      "description": "RabbitMQ работает"
    }
  }
}
```

---

### 4. Graceful Shutdown - Корректное завершение

**Что делает:** При остановке приложения:
1.  Прекращает принимать новые сообщения (отменяет все consumer'ы)
2.  Ожидает 1 секунду для завершения обработки текущих сообщений
3.  Закрывает канал и соединение

**Конфигурация:** Работает автоматически через `IAsyncDisposable`. Координируется через `EventBusRabbitMq`.

---

### 5.  Message TTL - Время жизни

**Что делает:** Автоматически удаляет сообщения, которые не были обработаны за указанное время.

**Конфигурация:**

```csharp
options.MessageTtl.Enabled = false;                  // Вкл/выкл (по умолчанию: false)
options.MessageTtl.DefaultTtlMs = 3600000;           // 1 час (по умолчанию)
```

---

### 6. Observability - Метрики

**Что делает:** Собирает метрики о работе event bus для мониторинга.

**Конфигурация:**

```csharp
options.Observability.MetricsEnabled = true;         // Метрики (по умолчанию: true)
```

**Доступные метрики:**

| Метрика | Описание |
|---------|----------|
| `eventbus_messages_published_total` | Общее количество опубликованных сообщений |
| `eventbus_messages_consumed_total` | Успешно обработанных сообщений |
| `eventbus_messages_failed_total` | Ошибок обработки |
| `eventbus_messages_retried_total` | Повторных попыток |
| `eventbus_publish_duration_ms` | Гистограмма длительности публикации |
| `eventbus_consume_duration_ms` | Гистограмма длительности обработки |
| `eventbus_duplicates_detected_total` | Количество обнаруженных дубликатов |

**Использование с Prometheus:**

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("EventBus Meter");
        metrics.AddPrometheusExporter();
    });

app.MapPrometheusScrapingEndpoint();
```

**Пример запроса в Grafana:**

```promql
# Количество обработанных событий за последний час
increase(eventbus_messages_consumed_total{event_name="OrderCreatedEvent"}[1h])

# Средняя длительность обработки
rate(eventbus_consume_duration_ms_sum[5m]) / rate(eventbus_consume_duration_ms_count[5m])

# Процент ошибок
100 * (rate(eventbus_messages_failed_total[5m]) / rate(eventbus_messages_consumed_total[5m]))
```

---

### 7. Request/Response - Запрос/ответ

**Что делает:** Позволяет посылать запрос и ждать ответ через RabbitMQ.

**Создание Request/Response:**

```csharp
public class GetUserDataRequest : RequestBase
{
    public int UserId { get; set; }
}

public class GetUserDataResponse : ResponseBase
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
}
```

**Handler для Response:**

```csharp
public class GetUserDataHandler : IEventHandler<GetUserDataRequest>
{
    private readonly IEventBus _eventBus;
    private readonly IUserRepository _userRepo;
    
    public async Task HandleAsync(GetUserDataRequest request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId);
        
        var response = new GetUserDataResponse
        {
            CorrelationId = request.CorrelationId,
            UserName = user.Name,
            Email = user.Email,
            IsActive = user.IsActive
        };
        
        // Отправляем response в reply queue
        await _eventBus.PublishAsync(response, request.ReplyTo!, ct);
    }
}
```

**Отправка Request:**

```csharp
public class UserService
{
    private readonly IEventBus _eventBus;
    
    public async Task<GetUserDataResponse> GetUserAsync(int userId)
    {
        var request = new GetUserDataRequest { UserId = userId };
        
        var response = await _eventBus.RequestAsync<GetUserDataRequest, GetUserDataResponse>(
            request, 
            timeoutMs: 30000);
        
        return response;
    }
}
```

---

### 8. Idempotency - Защита от дубликатов

**Что делает:** Отслеживает обработанные `MessageId` и пропускает дубликаты.

**Конфигурация:**

```csharp
options.Idempotency.Enabled = true;                  // Вкл/выкл (по умолчанию: true)
options.Idempotency.CacheDurationMs = 300000;        // 5 минут (по умолчанию)
options.Idempotency.MaxCacheSize = 10000;            // Макс. размер кэша
```

**Как работает:**

1. При получении сообщения проверяется `MessageId`
2. Если `MessageId` уже обработан → пропускается с ACK
3. После успешной обработки `MessageId` сохраняется в cache
4. Кэш очищается автоматически по истечении `CacheDurationMs`

**Метрики:**

```
eventbus_duplicates_detected_total{event_name="PaymentEvent"} = 15
```

---

### 9. Concurrency Control

**Что делает:** Ограничивает количество одновременно обрабатываемых сообщений из одной очереди.

**Конфигурация:**

```csharp
options.Concurrency.Enabled = true;                  // Вкл/выкл (по умолчанию: true)
options.Concurrency.MaxDegreeOfParallelism = 5;      // Макс. параллельных (по умолчанию: 5)
```

---

## Расширенные сценарии

### Множественные консюмеры с разными routing keys

```csharp
// Подписка 1: только критичные уведомления
await eventBus.SubscribeAsync<NotificationEvent, CriticalNotificationHandler>(
    "notification.critical", 
    EventExchangeType.Topic);

// Подписка 2: все уведомления
await eventBus.SubscribeAsync<NotificationEvent, AllNotificationsHandler>(
    "notification.*", 
    EventExchangeType.Topic);

// Подписка 3: уведомления по email
await eventBus.SubscribeAsync<NotificationEvent, EmailNotificationHandler>(
    "notification.email.*", 
    EventExchangeType.Topic);

// Публикация
await eventBus.PublishAsync(notification, "notification.critical.security");
// → Обработают: CriticalNotificationHandler + AllNotificationsHandler
```

---

## FAQ

**Q: Что происходит при падении RabbitMQ?**  
A: Библиотека автоматически переподключается (`AutomaticRecoveryEnabled = true`). Сообщения не теряются благодаря `Persistent = true`.

**Q: Как обрабатывать poison messages (сообщения с постоянными ошибками)?**  
A: После `MaxRetryAttempts` сообщение попадает в DLQ. Настройте мониторинг DLQ и обрабатывайте вручную.

**Q: Что будет если у консьюмера поменять EventExchangeType?**
A: Предыдущий (существующий) эксчейндж удалится.


---

## Поддержка

**GitHub:** [Repository](https://github.com/Tentrun/RabbitMqEventBus)  

---

*Версия документации: 1.0.0*

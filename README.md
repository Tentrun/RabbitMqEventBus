<div align="center">

# 🐰 RabbitMQ Event Bus

### Production-ready библиотека для .NET 10 для работы с RabbitMq

[![NuGet](https://img.shields.io/nuget/v/Tentrun.RabbitMqEventBus?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/Tentrun.RabbitMqEventBus/)
[![Downloads](https://img.shields.io/nuget/dt/Tentrun.RabbitMqEventBus?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/Tentrun.RabbitMqEventBus/)
[![License](https://img.shields.io/github/license/Tentrun/RabbitMqEventBus?style=flat-square)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12-239120?style=flat-square&logo=csharp)

**Полнофункциональная Event Bus для RabbitMQ с фокусом на надёжность, observability и developer experience**

[Быстрый старт](#-быстрый-старт) • [Документация](#-архитектура-событий) • [Примеры](#-примеры-использования) • [FAQ](#-faq)

</div>

---

## 🎯 Обзор

**RabbitMQ Event Bus** — это мощная библиотека для .NET 10, которая предоставляет высокоуровневую абстракцию над RabbitMQ с полным набором enterprise-функций из коробки:

<table>
<tr>
<td width="50%">

### 🔄 Resilience
- **Retry Policy** — Экспоненциальная стратегия повторных попыток
- **Dead Letter Queue** — Автоматическая обработка poison messages
- **Graceful Shutdown** — Корректное завершение работы без потери данных
- **Auto Recovery** — Автоматическое переподключение при сбоях

</td>
<td width="50%">

### 📊 Observability
- **Prometheus Metrics** — Полный набор метрик для мониторинга
- **Health Checks** — Интеграция с ASP.NET Health Checks
- **Distributed Tracing** — Поддержка correlation IDs
- **Structured Logging** — Детальное логирование событий

</td>
</tr>
<tr>
<td width="50%">

### ⚡ Performance
- **Prefetch Control** — Управление нагрузкой на консьюмеры
- **Concurrency Limit** — Ограничение параллельной обработки
- **Persistent Messages** — Гарантия доставки сообщений
- **Message TTL** — Автоматическая очистка устаревших сообщений

</td>
<td width="50%">

### 🛠️ Developer Experience
- **Request/Response Pattern** — Синхронный RPC через RabbitMQ
- **Idempotency** — Защита от дублирующих обработок
- **Flexible Routing** — Topic, Direct, Fanout exchanges
- **Custom Exchanges** — Интеграция со сторонними системами

</td>
</tr>
</table>

---

## 📦 Установка

```bash
dotnet add package Tentrun.RabbitMqEventBus
```

**Требования:**
- .NET 10.0+
- RabbitMQ 3.8+

---

## 🚀 Быстрый старт

### 1️⃣ Регистрация в DI контейнере

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRabbitMqEventBus(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.VirtualHost = "/";
    
    options.RetryPolicy.Enabled = true;
    options.RetryPolicy.MaxRetryAttempts = 3;
    
    options.Prefetch.PrefetchCount = 10;
    options.Idempotency.Enabled = true;
    options.Observability.MetricsEnabled = true;
});

builder.Services.AddRabbitMqHealthCheck();

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

### 2️⃣ Создание события

```csharp
public class OrderCreatedEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    
    public string OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public int CustomerId { get; set; }
}
```

### 3️⃣ Публикация события

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;
    
    public OrderService(IEventBus eventBus) => _eventBus = eventBus;
    
    public async Task CreateOrderAsync(CreateOrderDto dto)
    {
        var @event = new OrderCreatedEvent 
        { 
            OrderNumber = dto.OrderNumber,
            TotalAmount = dto.Total,
            CustomerId = dto.CustomerId
        };
        
        await _eventBus.PublishAsync(@event);
    }
}
```

### 4️⃣ Создание обработчика

```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    private readonly IEmailService _emailService;
    
    public OrderCreatedHandler(
        ILogger<OrderCreatedHandler> logger, 
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation(
            "Обработка заказа {OrderNumber} на сумму {Amount}", 
            @event.OrderNumber, 
            @event.TotalAmount);
        
        await _emailService.SendOrderConfirmationAsync(@event.CustomerId, ct);
    }
}
```

### 5️⃣ Регистрация консюмера

```csharp
builder.Services
    .AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<OrderCreatedEvent, OrderCreatedHandler>(EventExchangeType.Direct);
```

**🎉 Готово!** Теперь события `OrderCreatedEvent` будут автоматически обрабатываться `OrderCreatedHandler` с retry, idempotency и метриками.

---

## 🏗️ Архитектура событий

Библиотека поддерживает два основных паттерна обмена сообщениями:

### 🔥 Fire-and-Forget (IEvent)

Используется для **асинхронных событий**, когда не требуется ответ:

```csharp
public class OrderCreatedEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    
    public string OrderNumber { get; set; }
}

await _eventBus.PublishAsync(new OrderCreatedEvent { OrderNumber = "ORD-123" });
```

**Use cases:**  
✅ Уведомления  
✅ Аудит-логи  
✅ Аналитика  
✅ Интеграционные события  

### 🔄 Request-Reply (IRequest / IResponse)

Используется для **синхронных запросов** с ожиданием ответа (RPC pattern):

```csharp
public class GetUserRequest : RequestBase
{
    public int UserId { get; set; }
}

public class GetUserResponse : ResponseBase
{
    public string UserName { get; set; }
    public string Email { get; set; }
}

var response = await _eventBus.RequestAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 42 },
    timeoutMs: 5000);

Console.WriteLine($"User: {response.UserName}");
```

**Use cases:**  
✅ Микросервисное взаимодействие  
✅ Синхронные запросы данных  
✅ Валидация перед операцией  
✅ Распределённые транзакции  

---

## ⚙️ Конфигурация функций

### 1. 🔄 Retry Policy

Автоматические повторные попытки с экспоненциальной задержкой при ошибках обработки.

```csharp
options.RetryPolicy.Enabled = true;                  
options.RetryPolicy.MaxRetryAttempts = 3;            
options.RetryPolicy.InitialDelayMs = 1000;           
options.RetryPolicy.MaxDelayMs = 60000;              
options.RetryPolicy.BackoffMultiplier = 2.0;
```

| Попытка | Задержка | После макс. попыток |
|---------|----------|---------------------|
| 1 | 1 сек | → DLQ (Dead Letter Queue) |
| 2 | 2 сек | Poison message обрабатывается вручную |
| 3 | 4 сек | Метрика `eventbus_messages_failed_total` |

**События:**
- При каждой повторной попытке увеличивается `eventbus_messages_retried_total`
- После финального отказа → `eventbus_messages_failed_total`

---

### 2. ⚡ Prefetch Count

Управление нагрузкой на консюмеры через QoS (Quality of Service).

```csharp
options.Prefetch.Enabled = true;                     
options.Prefetch.PrefetchCount = 10;                 
options.Prefetch.GlobalQos = false;
```

**Что это даёт:**
- Консюмер получает не более `PrefetchCount` необработанных сообщений
- Предотвращает перегрузку медленных консюмеров
- Балансирует нагрузку между экземплярами сервиса

**Рекомендации:**
- **Быстрая обработка** (<100ms): `PrefetchCount = 50-100`
- **Средняя обработка** (100ms-1s): `PrefetchCount = 10-20`  
- **Медленная обработка** (>1s): `PrefetchCount = 1-5`

---

### 3. 🏥 Health Checks

Интеграция с ASP.NET Core Health Checks для мониторинга состояния подключения.

```csharp
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

**При недоступности RabbitMQ:**
```json
{
  "status": "Unhealthy",
  "results": {
    "rabbitmq": {
      "status": "Unhealthy",
      "description": "Не удалось подключиться к RabbitMQ"
    }
  }
}
```

---

### 4. 🛑 Graceful Shutdown

Автоматическое корректное завершение работы при остановке приложения.

**Процесс:**
1. ✋ Прекращение приёма новых сообщений (отмена всех `BasicConsume`)
2. ⏳ Ожидание завершения обработки текущих сообщений (1 сек)
3. 🔒 Закрытие каналов и соединений
4. ✅ Все сообщения либо обработаны, либо возвращены в очередь

**Поведение:** Работает автоматически через `IAsyncDisposable`.

---

### 5. ⏰ Message TTL

Автоматическое удаление сообщений, не обработанных за указанное время.

```csharp
options.MessageTtl.Enabled = false;                  
options.MessageTtl.DefaultTtlMs = 3600000;
```

**Use cases:**
- 📧 Уведомления, теряющие актуальность (email, push)
- 📊 Метрики и статистика с временными границами
- 🔥 События, критичные только в момент возникновения

---

### 6. 📊 Observability

Сбор метрик в формате Prometheus для мониторинга и алертинга.

```csharp
options.Observability.MetricsEnabled = true;
```

#### Доступные метрики

| Метрика | Тип | Описание |
|---------|-----|----------|
| `eventbus_messages_published_total` | Counter | Всего опубликовано событий |
| `eventbus_messages_consumed_total` | Counter | Успешно обработано событий |
| `eventbus_messages_failed_total` | Counter | Ошибок обработки |
| `eventbus_messages_retried_total` | Counter | Повторных попыток |
| `eventbus_duplicates_detected_total` | Counter | Обнаружено дубликатов |
| `eventbus_publish_duration_ms` | Histogram | Время публикации (ms) |
| `eventbus_consume_duration_ms` | Histogram | Время обработки (ms) |

#### Интеграция с Prometheus

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("EventBus Meter");
        metrics.AddPrometheusExporter();
    });

app.MapPrometheusScrapingEndpoint();
```

#### Grafana PromQL примеры

```promql
# Количество обработанных событий за последний час
increase(eventbus_messages_consumed_total{event_name="OrderCreatedEvent"}[1h])

# Средняя длительность обработки
rate(eventbus_consume_duration_ms_sum[5m]) / rate(eventbus_consume_duration_ms_count[5m])

# Процент ошибок
100 * (
  rate(eventbus_messages_failed_total[5m]) / 
  rate(eventbus_messages_consumed_total[5m])
)

# Top 5 самых медленных событий
topk(5, 
  rate(eventbus_consume_duration_ms_sum[5m]) / 
  rate(eventbus_consume_duration_ms_count[5m])
) by (event_name)
```

---

### 7. 🔄 Request/Response Pattern

Синхронный RPC через RabbitMQ с автоматической маршрутизацией ответов.

#### Создание Request/Response

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

#### Обработчик Request

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
        
        await _eventBus.PublishAsync(response, request.ReplyTo!, ct);
    }
}
```

#### Отправка Request

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

**Механизм работы:**
1. 📤 Отправка request в queue обработчика
2. 🔗 Создание временной reply queue
3. ⏳ Ожидание response с matching `CorrelationId`
4. 📥 Получение response и возврат результата
5. 🗑️ Автоматическое удаление reply queue

---

### 8. 🔐 Idempotency

Защита от дублирующих обработок через кэширование обработанных `MessageId`.

```csharp
options.Idempotency.Enabled = true;                  
options.Idempotency.CacheDurationMs = 300000;        
options.Idempotency.MaxCacheSize = 10000;
```

**Алгоритм работы:**
1. 📝 Получение сообщения с `MessageId`
2. 🔍 Проверка: обработан ли `MessageId` ранее?
3. ✅ Если **ДА** → ACK без обработки + метрика `duplicates_detected`
4. 🆕 Если **НЕТ** → обработка + сохранение `MessageId` в cache
5. 🧹 Автоочистка cache по истечении `CacheDurationMs`

**Use cases:**
- 🔁 Повторная отправка при network retry
- 📡 At-least-once delivery гарантии
- 🔄 Восстановление после сбоев

**Метрики:**
```promql
eventbus_duplicates_detected_total{event_name="PaymentEvent"}
```

---

### 9. ⚙️ Concurrency Control

Ограничение количества одновременно обрабатываемых сообщений из очереди.

```csharp
options.Concurrency.Enabled = true;                  
options.Concurrency.MaxDegreeOfParallelism = 5;
```

**Зачем это нужно:**
- 🔒 Защита от исчерпания ресурсов (DB connections, memory)
- ⚖️ Балансировка нагрузки на зависимые сервисы
- 🎯 Контролируемая throughput для стабильности

**Рекомендации:**
- **I/O-bound** обработка (HTTP calls, DB queries): `10-50`
- **CPU-bound** обработка (вычисления, обработка изображений): `Environment.ProcessorCount`
- **Memory-intensive**: `2-5` (зависит от доступной RAM)

---

## 📘 Примеры использования

### Публикация с кастомным Routing Key

```csharp
await _eventBus.PublishAsync(@event, customRoutingKey: "orders.created.vip");
```

### Публикация в произвольный Exchange

Интеграция со сторонними системами или legacy exchanges:

```csharp
await _eventBus.PublishToExchangeAsync(
    @event: telemetryEvent,
    customExchangeName: "amq.topic",
    routingKey: "sensors.temperature.livingroom",
    token: cancellationToken);
```

**Use cases:**
- 🔗 Интеграция с MQTT bridges (`amq.topic`)
- 🏢 Публикация в корпоративные exchanges
- 🔄 Мультиплексирование между системами

---

### Регистрация консюмеров

#### Стандартная регистрация

```csharp
services.AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<OrderCreatedEvent, OrderCreatedHandler>(EventExchangeType.Direct);
```

#### С кастомным именем очереди

```csharp
services.AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<OrderCreatedEvent, OrderCreatedHandler>(
        EventExchangeType.Direct, 
        "custom.order.processing.queue");
```

**Зачем кастомные имена:**
- 🏛️ Миграция с legacy систем
- 🏢 Интеграция со сторонними сервисами
- 📊 Упрощённые имена для мониторинга
- 👥 Мультитенантность (разные очереди на клиента)

#### Подписка на сторонний Exchange

```csharp
services.AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<TelemetryEvent, TelemetryHandler>(
        customExchangeName: "amq.topic",
        routingKey: "devices.*.telemetry",
        queueName: "telemetry.processor");
```

---

### Topic Exchange с Wildcards

```csharp
await eventBus.SubscribeAsync<NotificationEvent, CriticalNotificationHandler>(
    "notification.critical.*", 
    EventExchangeType.Topic);

await eventBus.SubscribeAsync<NotificationEvent, AllNotificationsHandler>(
    "notification.#", 
    EventExchangeType.Topic);

await _eventBus.PublishAsync(notification, "notification.critical.security");
```

**Routing rules:**
- `*` — ровно одно слово
- `#` — 0 или более слов

---

### Мультитенантность

Разные очереди для разных клиентов:

```csharp
services.AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<PaymentHandler>(EventExchangeType.Direct, "tenant.client1.payments")
    .AddConsumer<PaymentHandler>(EventExchangeType.Direct, "tenant.client2.payments")
    .AddConsumer<PaymentHandler>(EventExchangeType.Direct, "tenant.client3.payments");
```

---

## 📖 API Reference

### IEventBus

#### `PublishAsync<T>(T @event, CancellationToken token = default)`

Публикация в стандартный exchange события (`exchange.{EventName}`).

```csharp
await _eventBus.PublishAsync(new OrderCreatedEvent { OrderId = 123 });
```

---

#### `PublishAsync<T>(T @event, string customRoutingKey, CancellationToken token = default)`

Публикация с кастомным routing key.

```csharp
await _eventBus.PublishAsync(orderEvent, "orders.high-priority");
```

---

#### `PublishToExchangeAsync<T>(T @event, string customExchangeName, string routingKey, CancellationToken token = default)`

Публикация в произвольный exchange.

```csharp
await _eventBus.PublishToExchangeAsync(
    @event: notificationEvent,
    customExchangeName: "amq.topic",
    routingKey: "notifications.email.critical",
    token: cancellationToken);
```

---

#### `SubscribeAsync<TEvent, THandler>(EventExchangeType exchangeType = EventExchangeType.Fanout)`

Подписка с автоматическим созданием exchange и queue.

```csharp
await _eventBus.SubscribeAsync<OrderCreatedEvent, OrderCreatedHandler>(
    EventExchangeType.Direct);
```

---

#### `SubscribeAsync<TEvent, THandler>(EventExchangeType exchangeType, string customQueueName)`

Подписка с кастомным именем очереди.

```csharp
await _eventBus.SubscribeAsync<OrderCreatedEvent, OrderCreatedHandler>(
    EventExchangeType.Direct, 
    "legacy.orders.processing");
```

---

#### `SubscribeToCustomExchangeAsync<TEvent, THandler>(string customExchangeName, string routingKey, string queueName, EventExchangeType? exchangeType = null)`

Подписка на существующий произвольный exchange.

```csharp
await _eventBus.SubscribeToCustomExchangeAsync<TelemetryEvent, TelemetryHandler>(
    customExchangeName: "amq.topic",
    routingKey: "sensors.*.temperature",
    queueName: "telemetry.temperature.processor");
```

**Параметры:**
- `customExchangeName` — имя exchange
- `routingKey` — routing key для binding (поддерживает wildcards: `*`, `#`)
- `queueName` — имя создаваемой очереди
- `exchangeType` — *опционально*, создаст exchange если не существует

⚠️ **Важно:** Для системных exchanges (`amq.*`) не указывайте `exchangeType` — они уже существуют.

---

#### `RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMs = 30000, CancellationToken cancellationToken = default)`

RPC pattern: синхронный запрос с ожиданием ответа.

```csharp
var response = await _eventBus.RequestAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 42 },
    timeoutMs: 5000);
```

**Throws:** `TimeoutException` если ответ не получен за `timeoutMs`.

---

## 🔧 Exchange Types

| Тип | Описание | Use Case |
|-----|----------|----------|
| **Fanout** | Broadcast всем подписчикам | Уведомления, кэш-инвалидация |
| **Direct** | Точное совпадение routing key | Команды, targeted events |
| **Topic** | Wildcard matching (`*`, `#`) | Иерархические события, фильтрация |

---

## ❓ FAQ

### ❓ Что происходит при падении RabbitMQ?

✅ Библиотека автоматически переподключается (`AutomaticRecoveryEnabled = true`).  
✅ Сообщения не теряются благодаря `Persistent = true`.  
✅ Неподтверждённые сообщения возвращаются в очередь.

---

### ❓ Как обрабатывать poison messages?

После `MaxRetryAttempts` сообщение попадает в **Dead Letter Queue (DLQ)**.  

**Рекомендации:**
1. 📊 Настройте мониторинг DLQ алертами
2. 🔍 Анализируйте причины попадания в DLQ
3. 🛠️ Обрабатывайте вручную или автоматизируйте recovery

---

### ❓ Что будет если поменять EventExchangeType у консюмера?

⚠️ Предыдущий exchange **удалится** и создастся новый с новым типом.  
⚠️ Все привязки (bindings) будут потеряны.

**Рекомендация:** Используйте миграционную стратегию:
1. Создайте новый exchange
2. Перенаправьте трафик
3. Удалите старый exchange вручную

---

### ❓ Как масштабировать обработку?

**Горизонтальное масштабирование:**
- Запустите несколько экземпляров сервиса
- RabbitMQ автоматически распределит нагрузку через Round-Robin
- Используйте `PrefetchCount` для балансировки

**Вертикальное масштабирование:**
- Увеличьте `MaxDegreeOfParallelism` для CPU-bound задач
- Увеличьте `PrefetchCount` для I/O-bound задач

---

### ❓ Как обеспечить порядок обработки?

⚠️ RabbitMQ гарантирует порядок **только внутри одной очереди с одним консюмером**.

**Стратегии:**
1. 🔒 Single consumer (не масштабируется)
2. 🗂️ Sharding по ключу (например, `UserId % 10`)
3. 🔐 Pessimistic locking в обработчике

---

## 🛠️ Troubleshooting

### Ошибка подключения к RabbitMQ

```
RabbitMQ.Client.Exceptions.BrokerUnreachableException
```

**Решение:**
- ✅ Проверьте, что RabbitMQ запущен: `docker ps` или `systemctl status rabbitmq-server`
- ✅ Проверьте Host/Port в конфигурации
- ✅ Проверьте firewall rules
- ✅ Проверьте credentials (UserName/Password)

---

### Сообщения не обрабатываются

**Чеклист:**
1. ✅ Handler зарегистрирован в DI: `.AddConsumer<TEvent, THandler>()`
2. ✅ Exchange и Queue созданы (проверьте в RabbitMQ UI: `http://localhost:15672`)
3. ✅ Routing key совпадает между publisher и consumer
4. ✅ Проверьте логи на exceptions в обработчике

---

### Медленная обработка

**Оптимизация:**
- 📈 Увеличьте `MaxDegreeOfParallelism` для параллельной обработки
- 📈 Увеличьте `PrefetchCount` для загрузки пачками
- 🔍 Профилируйте handler через `eventbus_consume_duration_ms` метрики

---

## 📜 Changelog

### [1.1.2] - 2026-02-17

- **Упрощённые имена очередей:** `q.{HandlerName}` вместо `queue.{EventName}.{HandlerName}.{RoutingKeySuffix}`
- **Topic exchange routing:** Для Topic и Direct exchanges routing key теперь устанавливается в имя события автоматически
- **Graceful Shutdown fix:** Исправлена ошибка `ObjectDisposedException` при повторном закрытии канала
- **Единый API `AddConsumer`:** Убрана дублирующая `AddConsumerWithCustomExchange` в `EventConsumerRegister`, заменена на перегрузку `AddConsumer`
- **Рефакторинг `EventConsumerRegister`:** Выделены `ResolveTypes`, `BuildStandardSubscribeAction`, `BuildCustomSubscribeAction`

---

### [1.1.1] - 2026-01-10

#### ✨ Новые возможности
- `PublishToExchangeAsync` для публикации в произвольные exchanges
- `SubscribeToCustomExchangeAsync` для подписки на сторонние exchanges
- Поддержка кастомных имен очередей через `AddConsumer`
- Builder-паттерн для декларативной регистрации консьюмеров

---

### [1.0.0] - 2025-12-01

#### 🎉 Первый релиз
- ✅ Базовый EventBus: Publish/Subscribe, Direct/Fanout/Topic
- ✅ Retry Policy с экспоненциальной задержкой
- ✅ Dead Letter Queue (DLQ)
- ✅ Prefetch Count, Health Checks, Graceful Shutdown
- ✅ Message TTL, Observability (метрики)
- ✅ Request/Response (RPC), Idempotency, Concurrency Control

---

## 📄 Лицензия

Проект распространяется под лицензией **MIT**. Подробности в [LICENSE.txt](LICENSE.txt).

---

## 📞 Поддержка

- 🐛 **Issues**: [GitHub Issues](https://github.com/Tentrun/RabbitMqEventBus/issues)
- 💡 **Discussions**: [GitHub Discussions](https://github.com/Tentrun/RabbitMqEventBus/discussions)
- 📦 **NuGet**: [Tentrun.RabbitMqEventBus](https://www.nuget.org/packages/Tentrun.RabbitMqEventBus/)
- 📖 **Documentation**: [GitHub Repository](https://github.com/Tentrun/RabbitMqEventBus)

---

<div align="center">

⭐ Если библиотека оказалась полезной, поставьте звезду на GitHub!

*Версия документации: 1.1.2*

</div>

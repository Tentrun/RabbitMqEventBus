# Changelog

Все заметные изменения в этом проекте будут документированы в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и этот проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

## [1.1.2] - 2026-02-16

### Добавлено

- **`AddConsumer<THandler>(string customExchangeName, string routingKey, string queueName)`** - декларативная регистрация consumer для кастомных exchanges
  - Позволяет регистрировать подписку на произвольный exchange прямо в Program.cs через builder
  - Автоматическая подписка при старте приложения через EventBusBackgroundService
  - Устраняет необходимость вручную вызывать `SubscribeToCustomExchangeAsync` после создания `app`

- **Перегрузки с явным указанием типа события**: `AddConsumer<TEvent, THandler>` для всех методов

### Улучшения

- ConsumerRegistration хранит `Func<IEventBus, Task>` вместо множества параметров

### Примеры использования

```csharp
// До: ручная подписка в Program.cs
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    await eventBus.SubscribeToCustomExchangeAsync<TelemetryEvent, TelemetryHandler>(
        "amq.topic", "devices.*.telemetry", "mqtt.telemetry.all");
}

// После: декларативная регистрация
services.AddRabbitMqEventBus(options => { /* ... */ })
    .AddConsumer<TelemetryHandler>("amq.topic", "devices.*.telemetry", "mqtt.telemetry.all");
```

## [1.1.1] - 2026-02-16

### Добавлено

- **`SubscribeToCustomExchangeAsync<T, THandler>`** - метод для подписки на произвольные exchanges
  - Позволяет подписываться на любые exchange (в том числе системные как `amq.topic`)
  - **Опциональный параметр `EventExchangeType?`** - если указан, exchange будет создан автоматически при необходимости
  - Полезно для интеграции с внешними системами, которые публикуют в свои exchanges
  - Поддерживает кастомные routing keys и имена очередей

### Улучшения

- Exchange теперь может создаваться автоматически при подписке, если указан тип exchange
- Для системных exchanges (`amq.*`) тип указывать не нужно - они уже существуют

### Примеры использования

```csharp
// Подписка на существующий системный exchange
await eventBus.SubscribeToCustomExchangeAsync<TelemetryEvent, TelemetryHandler>(
    customExchangeName: "amq.topic",
    routingKey: "sensors.*.data",
    queueName: "telemetry.processing");

// Подписка с автоматическим созданием exchange
await eventBus.SubscribeToCustomExchangeAsync<OrderEvent, OrderHandler>(
    customExchangeName: "external.orders",
    routingKey: "order.#",
    queueName: "order.processor",
    exchangeType: EventExchangeType.Topic);
```

## [1.1.0] - 2026-02-16

### Добавлено

- **`PublishToExchangeAsync<T>`** - новый метод для публикации событий в произвольные exchanges
  - Возможность публикации в любой существующий exchange (в том числе системные)
  - Поддержка кастомных routing keys для гибкой маршрутизации
  - Полезно для интеграции с существующими системами обмена сообщениями

- **Кастомные имена очередей при регистрации consumer**
  - Добавлена перегрузка `AddConsumer<THandler>(EventExchangeType, string customQueueName)`
  - Добавлена перегрузка `SubscribeAsync<T, THandler>(EventExchangeType, string customQueueName)`
  - Позволяет задавать пользовательские имена очередей вместо автоматической генерации
  - Use cases: миграция с legacy систем, интеграция со сторонними системами, упрощенные имена для мониторинга

### Примеры использования

```csharp
// Публикация в существующий exchange
var routingKey = $"orders.{orderId}.status";
await _eventBus.PublishToExchangeAsync(
    @event: orderEvent,
    customExchangeName: "legacy.orders.exchange",
    routingKey: routingKey,
    token: cancellationToken);

// Регистрация consumer с кастомным именем очереди
builder.AddConsumer<OrderEventHandler>(EventExchangeType.Topic, "legacy.orders.queue");
```

### Документация

- Добавлен раздел "API Reference" в README.md
- Добавлены примеры для расширенных сценариев (мультитенантность, legacy интеграция)
- Улучшена структура и описание API методов
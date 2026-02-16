# Changelog

Все заметные изменения в этом проекте будут документированы в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и этот проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

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
using System.Diagnostics.Metrics;

namespace RabbitMqBus.Observability;

public class EventBusMetrics
{
    private readonly Counter<long> _publishedCounter;
    private readonly Counter<long> _consumedCounter;
    private readonly Counter<long> _failedCounter;
    private readonly Counter<long> _retriedCounter;
    private readonly Histogram<double> _publishDuration;
    private readonly Histogram<double> _consumeDuration;
    private readonly Counter<long> _duplicatesCounter;

    public EventBusMetrics()
    {
        var meter = new Meter("EventBus Meter");
        
        _publishedCounter = meter.CreateCounter<long>(
            "eventbus_messages_published_total",
            description: "Общее количество опубликованных сообщений");
        
        _consumedCounter = meter.CreateCounter<long>(
            "eventbus_messages_consumed_total",
            description: "Общее количество обработанных сообщений");
        
        _failedCounter = meter.CreateCounter<long>(
            "eventbus_messages_failed_total",
            description: "Общее количество ошибок обработки");
        
        _retriedCounter = meter.CreateCounter<long>(
            "eventbus_messages_retried_total",
            description: "Общее количество повторных попыток");
        
        _publishDuration = meter.CreateHistogram<double>(
            "eventbus_publish_duration_ms",
            "ms",
            "Длительность публикации сообщения");
        
        _consumeDuration = meter.CreateHistogram<double>(
            "eventbus_consume_duration_ms",
            "ms",
            "Длительность обработки сообщения");
        
        _duplicatesCounter = meter.CreateCounter<long>(
            "eventbus_duplicates_detected_total",
            description: "Количество обнаруженных дубликатов");
    }

    public void RecordPublished(string eventName, double durationMs)
    {
        _publishedCounter.Add(1, new KeyValuePair<string, object?>("event_name", eventName));
        _publishDuration.Record(durationMs, new KeyValuePair<string, object?>("event_name", eventName));
    }

    public void RecordConsumed(string eventName, double durationMs, bool success)
    {
        if (success)
        {
            _consumedCounter.Add(1, new KeyValuePair<string, object?>("event_name", eventName));
        }
        else
        {
            _failedCounter.Add(1, new KeyValuePair<string, object?>("event_name", eventName));
        }
        
        _consumeDuration.Record(durationMs, 
            new KeyValuePair<string, object?>("event_name", eventName),
            new KeyValuePair<string, object?>("success", success));
    }

    public void RecordRetry(string eventName, int attemptNumber)
    {
        _retriedCounter.Add(1, 
            new KeyValuePair<string, object?>("event_name", eventName),
            new KeyValuePair<string, object?>("attempt", attemptNumber));
    }

    public void RecordDuplicate(string eventName)
    {
        _duplicatesCounter.Add(1, new KeyValuePair<string, object?>("event_name", eventName));
    }
}

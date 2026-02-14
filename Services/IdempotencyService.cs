using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RabbitMqBus.Services;

public interface IIdempotencyService
{
    bool IsDuplicate(string messageId);
    void MarkAsProcessed(string messageId);
}

public class IdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, DateTime> _processedMessages = new();
    private readonly int _cacheDurationMs;
    private readonly int _maxCacheSize;
    private readonly ILogger<IdempotencyService> _logger;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private DateTime _lastCleanup = DateTime.UtcNow;

    public IdempotencyService(int cacheDurationMs, int maxCacheSize, ILogger<IdempotencyService> logger)
    {
        _cacheDurationMs = cacheDurationMs;
        _maxCacheSize = maxCacheSize;
        _logger = logger;
    }

    public bool IsDuplicate(string messageId)
    {
        CleanupIfNeeded();
        return _processedMessages.ContainsKey(messageId);
    }

    public void MarkAsProcessed(string messageId)
    {
        _processedMessages.TryAdd(messageId, DateTime.UtcNow);
        
        if (_processedMessages.Count > _maxCacheSize)
        {
            CleanupOldEntries();
        }
    }

    private void CleanupIfNeeded()
    {
        if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5)
            return;

        Task.Run(CleanupOldEntries);
    }

    private void CleanupOldEntries()
    {
        if (!_cleanupLock.Wait(0))
            return;

        try
        {
            var cutoff = DateTime.UtcNow.AddMilliseconds(-_cacheDurationMs);
            var keysToRemove = _processedMessages
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _processedMessages.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Очищено {Count} устаревших записей idempotency cache", keysToRemove.Count);
            }

            _lastCleanup = DateTime.UtcNow;
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
}

namespace RabbitMqBus.Models;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    
    public RetryPolicyOptions RetryPolicy { get; set; } = new();
    public PrefetchOptions Prefetch { get; set; } = new();
    public MessageTtlOptions MessageTtl { get; set; } = new();
    public IdempotencyOptions Idempotency { get; set; } = new();
    public ConcurrencyOptions Concurrency { get; set; } = new();
    public ObservabilityOptions Observability { get; set; } = new();
}

public class RetryPolicyOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 60000;
    public double BackoffMultiplier { get; set; } = 2.0;
}

public class PrefetchOptions
{
    public bool Enabled { get; set; } = true;
    public ushort PrefetchCount { get; set; } = 10;
    public bool GlobalQos { get; set; } = false;
}

public class MessageTtlOptions
{
    public bool Enabled { get; set; } = false;
    public int DefaultTtlMs { get; set; } = 3600000;
}

public class IdempotencyOptions
{
    public bool Enabled { get; set; } = true;
    public int CacheDurationMs { get; set; } = 300000;
    public int MaxCacheSize { get; set; } = 10000;
}

public class ConcurrencyOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxDegreeOfParallelism { get; set; } = 5;
}

public class ObservabilityOptions
{
    public bool MetricsEnabled { get; set; } = true;
}
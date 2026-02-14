using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMqBus.Rmq.Interfaces;

namespace RabbitMqBus.HealthChecks;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnection _connection;

    public RabbitMqHealthCheck(IRabbitMqConnection connection)
    {
        _connection = connection;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection.IsConnected)
            {
                return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ работает"));
            }

            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ не работает"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Ошибка проверки RabbitMQ", ex));
        }
    }
}

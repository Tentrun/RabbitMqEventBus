using RabbitMQ.Client;

namespace RabbitMqBus.Rmq.Interfaces;

public interface IRabbitMqConnection : IAsyncDisposable
{
    IConnection GetConnection();
    Task<bool> TryConnectAsync(CancellationToken token = default);
    public bool IsConnected { get; }
}
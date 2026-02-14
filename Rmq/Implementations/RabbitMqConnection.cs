using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqBus.Rmq.Interfaces;

namespace RabbitMqBus.Rmq.Implementations;

public class RabbitMqConnection : IRabbitMqConnection
{
    private readonly IConnectionFactory _factory;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;
    
    public RabbitMqConnection(IConnectionFactory factory, ILogger<RabbitMqConnection> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        
        try
        {
            if (_connection != null)
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
        }
        
        _disposed = true;
        _connectionLock?.Dispose();
    }

    public IConnection GetConnection()
    {
        if (!IsConnected)
            throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            
        return _connection!;
    }

    public async Task<bool> TryConnectAsync(CancellationToken token = default)
    {
        if (IsConnected)
        {
            return true;
        }

        await _connectionLock.WaitAsync(token);
        try
        {
            if (IsConnected)
                return true;

            _logger.LogInformation("RabbitMQ Client is trying to connect");

            var attempt = 0;
            while (!IsConnected && !token.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    _connection = await _factory.CreateConnectionAsync(token);
                    
                    if (IsConnected)
                    {
                        _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                        _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                        _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
                        
                        _logger.LogInformation("RabbitMQ connected successfully");
                        
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error connecting to RabbitMQ (attempt {attempt})");
                    
                    if (attempt == 10)
                        throw;
                        
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), token);
                }
            }

            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        _logger.LogWarning($"RabbitMQ connection closed: {e.ReplyText}");
        _connection?.Dispose();
        return Task.CompletedTask;
    }

    private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "RabbitMQ callback exception");
        return Task.CompletedTask;
    }

    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning($"RabbitMQ connection blocked: {e.Reason}");
        return Task.CompletedTask;
    }
}
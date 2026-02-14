using RabbitMQ.Client.Events;
using RabbitMqBus.Abstractions.Base.Interfaces;

namespace RabbitMqBus.EventBus.Interfaces;

public interface IRabbitMqMessageDispatcher : IAsyncDisposable
{
    Task StartConsumerAsync(string queueName);
    Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs);
    void RegisterPendingRequest(string correlationId, TaskCompletionSource<IResponse> tcs);
    bool RemovePendingRequest(string correlationId);
}

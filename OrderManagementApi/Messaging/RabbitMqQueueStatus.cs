namespace OrderManagement.Messaging;

public sealed record RabbitMqQueueStatus(string Name, bool Exists, uint? ReadyMessages, uint? Consumers);

public sealed record RabbitMqStatus(
    bool IsReachable,
    string? Error,
    IReadOnlyList<RabbitMqQueueStatus> Queues);

public interface IRabbitMqQueueInspector
{
    Task<RabbitMqStatus> GetStatusAsync(CancellationToken cancellationToken);
}

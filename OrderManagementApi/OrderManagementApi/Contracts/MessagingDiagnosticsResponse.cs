namespace OrderManagementApi.Contracts;

/// <summary>Operational status of order processing, outbox publishing, and RabbitMQ queues.</summary>
public sealed record MessagingDiagnosticsResponse(
    DateTimeOffset GeneratedAt,
    long OrdersCreatedLast24Hours,
    IReadOnlyList<OrderStatusCountResponse> OrdersByStatus,
    OutboxSummaryResponse Outbox,
    long ProcessedMessageCount,
    IReadOnlyList<ProcessedMessageResponse> RecentProcessedMessages,
    RabbitMqDiagnosticsResponse RabbitMq);

/// <summary>Order count for a lifecycle status.</summary>
public sealed record OrderStatusCountResponse(string Status, long Count);

/// <summary>Counts and recent persisted outbox events.</summary>
public sealed record OutboxSummaryResponse(
    long PendingCount,
    long RetryScheduledCount,
    long PublishedCount,
    IReadOnlyList<OutboxMessageResponse> RecentMessages);

/// <summary>Persisted event information for an outbox message.</summary>
public sealed record OutboxMessageResponse(
    Guid MessageId,
    string Type,
    string Payload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? NextAttemptAt,
    int Attempts,
    string? LastError);

/// <summary>Information about a message already handled by a consumer.</summary>
public sealed record ProcessedMessageResponse(
    Guid MessageId,
    string Consumer,
    DateTimeOffset ProcessedAt);

/// <summary>RabbitMQ reachability and ready-message counts for order event queues.</summary>
public sealed record RabbitMqDiagnosticsResponse(
    bool IsReachable,
    string? Error,
    IReadOnlyList<RabbitMqQueueResponse> Queues);

/// <summary>Ready messages and active consumers for a RabbitMQ queue.</summary>
public sealed record RabbitMqQueueResponse(
    string Name,
    bool Exists,
    uint? ReadyMessages,
    uint? Consumers);

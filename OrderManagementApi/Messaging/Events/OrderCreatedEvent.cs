namespace OrderManagement.Messaging.Events;

public sealed record OrderCreatedEvent(Guid MessageId, Guid OrderId, DateTime CreatedAt);

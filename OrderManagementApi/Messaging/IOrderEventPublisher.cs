using OrderManagement.Messaging.Events;

namespace OrderManagement.Messaging;

public interface IOrderEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent message, CancellationToken cancellationToken);
}

using RabbitMQ.Client;

namespace OrderManagement.Messaging;

public static class RabbitMqTopology
{
    public const string EventsExchange = "orders.events";
    public const string RetryExchange = "orders.retries";
    public const string DeadLetterExchange = "orders.dead-letter";
    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderCreatedQueue = "orders.order-created";
    public const string DeadLetterQueue = "orders.order-created.dead-letter";

    public static readonly (string RoutingKey, string QueueName, int DelayMilliseconds)[] RetryQueues =
    [
        ("retry.5s", "orders.order-created.retry.5s", 5_000),
        ("retry.30s", "orders.order-created.retry.30s", 30_000),
        ("retry.2m", "orders.order-created.retry.2m", 120_000)
    ];

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            EventsExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            OrderCreatedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = OrderCreatedRoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            OrderCreatedQueue, EventsExchange, OrderCreatedRoutingKey,
            cancellationToken: cancellationToken);

        foreach (var retry in RetryQueues)
        {
            await channel.QueueDeclareAsync(
                retry.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = retry.DelayMilliseconds,
                    ["x-dead-letter-exchange"] = EventsExchange,
                    ["x-dead-letter-routing-key"] = OrderCreatedRoutingKey
                },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                retry.QueueName, RetryExchange, retry.RoutingKey,
                cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(
            DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DeadLetterQueue, DeadLetterExchange, OrderCreatedRoutingKey,
            cancellationToken: cancellationToken);
    }
}

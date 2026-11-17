using RabbitMQ.Client;

namespace OrderManagement.Messaging;

public static class RabbitMqConnectionFactory
{
    public static ConnectionFactory Create(RabbitMqOptions options) => new()
    {
        HostName = options.HostName,
        Port = options.Port,
        UserName = options.UserName,
        Password = options.Password,
        VirtualHost = options.VirtualHost,
        AutomaticRecoveryEnabled = false
    };
}

namespace OrderManagement.Messaging;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "devuser";
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
}

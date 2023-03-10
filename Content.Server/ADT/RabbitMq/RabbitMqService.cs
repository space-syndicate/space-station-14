using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Content.Server.ADT.RabbitMq;

public sealed class RabbitMqService : IRabbitMqService
{
    public void SendMessage(object obj)
    {
        var message = JsonSerializer.Serialize(obj);
        SendMessage(message);
    }

    public void SendMessage(string message)
    {
        ConnectionFactory factory = new ConnectionFactory() { Uri = new Uri("amqps://rlzzrent:9rvGAG53rSYH4NZMDFpel4pJSQbpjbmr@cow.rmq2.cloudamqp.com/rlzzrent") };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: "ExternalNetwork",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: "",
                routingKey: "ExternalNetwork",
                basicProperties: null,
                body: body);
        }
    }
}

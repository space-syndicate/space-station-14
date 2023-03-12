using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Content.Server.ADT.RabbitMq;

public sealed class RabbitMqService : IRabbitMqService
{
    private ConnectionFactory factory { get; set; }
    private RabbitMqService()
    {
        factory = new ConnectionFactory() { Uri = new Uri("amqps://rlzzrent:9rvGAG53rSYH4NZMDFpel4pJSQbpjbmr@cow.rmq2.cloudamqp.com/rlzzrent") };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.ExchangeDeclare(exchange:"SS14",type: ExchangeType.Fanout, durable: false, autoDelete: false, arguments: null);
        }
    }

    public void SendMessage(object obj)
    {
        var message = JsonSerializer.Serialize(obj);
        SendMessage(message);
    }

    public void SendMessage(string message)
    {

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: "SS14",
                routingKey: "all",
                basicProperties: null,
                body: body);
        }
    }
}

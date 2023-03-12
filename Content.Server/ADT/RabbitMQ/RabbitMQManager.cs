using System.Text;
using System.Threading.Tasks;
using Content.Server.ADT.ExternalNetwork;
using Content.Shared.ADT.ACCVars;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Robust.Shared.Configuration;


namespace Content.Server.ADT.RabbitMQ;

public sealed class RabbitMQManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private ConnectionFactory factory = default!;

    public void Initialize()
    {
        factory = new ConnectionFactory() {DispatchConsumersAsync = true, Uri = new Uri(_cfg.GetCVar(ACCVars.RabbitMQConnectionString)) };
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange:"SS14", type: ExchangeType.Fanout, durable: false, autoDelete: false, arguments: null);

        var queueId = Guid.NewGuid().ToString();
        channel.QueueDeclare(queue: queueId, durable: false, exclusive: true, autoDelete: true, arguments: null);
        channel.QueueBind(queue: queueId, exchange: "SS14", routingKey: "all", arguments: null );

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += ReceivedMessage;
        channel.BasicConsume(queue: queueId, autoAck: true, consumer: consumer);

    }

    private async Task ReceivedMessage(object sender, BasicDeliverEventArgs @event)
    {
        var jsonString = Encoding.UTF8.GetString(@event.Body.ToArray());
        if (!string.IsNullOrEmpty(jsonString))
        {
            var package = JsonConvert.DeserializeObject<NetworkPackage>(jsonString);
            if (package != null)
            {
                _entityManager.EventBus.RaiseEvent(EventSource.Local, package);
            }
        }
    }

    public void SendMessage(object obj)
    {
        var message = JsonConvert.SerializeObject(obj);
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

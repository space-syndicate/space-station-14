using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.ADT.RabbitMq;
using Content.Server.Chat.Managers;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Server.Popups;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Robust.Shared.Player;

namespace Content.Server.ADT.ExternalNetwork;

public sealed class ExternalNetworkSystem: EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;
    [Dependency] private readonly IRabbitMqService _mqService = default!;

    private IModel channel = default!;

    public override void Initialize()
    {
        base.Initialize();

        var factory = new ConnectionFactory() { DispatchConsumersAsync = true, Uri = new Uri("amqps://rlzzrent:9rvGAG53rSYH4NZMDFpel4pJSQbpjbmr@cow.rmq2.cloudamqp.com/rlzzrent") };
        var connection = factory.CreateConnection();
        var queueId = Guid.NewGuid().ToString();

        channel = connection.CreateModel();
        channel.QueueDeclare(queue: queueId,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null);

        channel.QueueBind(queue: queueId, exchange: "SS14", routingKey: "all", arguments: null );

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += ReceivedMessage;
        channel.BasicConsume(queue: queueId, autoAck: true, consumer: consumer);

        SubscribeLocalEvent<ExternalNetworkComponent, ComponentStartup>(OnNetworkStartup);
        SubscribeLocalEvent<ExternalNetworkComponent, ComponentShutdown>(OnNetworkShutdown);
        SubscribeLocalEvent<NetworkPackage>(OnRecivedNetworkObject);

    }

    private void OnRecivedNetworkObject(NetworkPackage args)
    {
        var devices = EntityManager.EntityQuery<ExternalNetworkComponent>();
        if (args.PackageType == DeviceTypes.Fax)
        {
            if (args.Command == NetworkCommand.Register)
            {
                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(args.SenderAddress) && !device.KnownHosts.Any(x => x == args.SenderAddress) && device.Address != args.SenderAddress)
                    {
                        device.KnownHosts.Add(args.SenderAddress);
                    }
                }
            }
            else if (args.Command == NetworkCommand.UnRegister)
            {
                foreach (var device in devices)
                {
                    if(!string.IsNullOrEmpty(args.SenderAddress) && device.KnownHosts.Any(x=>x == args.SenderAddress))
                        device.KnownHosts.Remove(args.SenderAddress);
                }
            }
            else if (args.Command == NetworkCommand.Ping)
            {
                foreach (var device in devices)
                {
                    _mqService.SendMessage(new NetworkPackage()
                    {
                        Command = NetworkCommand.Register,
                        Sender = (int) device.Owner,
                        SenderAddress = device.Address,
                        PackageType = device.DeviceType
                    });
                }
            }
            else if (args.Command == NetworkCommand.Transfer)
            {
                if(args.Data == null) return;

                var device = devices.FirstOrDefault(x => x.Address == args.Address);
                if (device == null) return;

                TryComp<FaxMachineComponent>(device.Owner, out var _faxMachineComponent);
                if(_faxMachineComponent == null) return;

                string? _paperName = string.Empty;
                string? _paperContent = string.Empty;

                if(!args.Data.TryGetValue(FaxConstants.FaxPaperNameData, out var paperName)) return;
                if(string.IsNullOrEmpty(paperName?.ToString())) return;
                else _paperName = paperName?.ToString();

                if(!args.Data.TryGetValue(FaxConstants.FaxPaperContentData, out var paperContent)) return;
                if(string.IsNullOrEmpty(paperContent?.ToString())) return;
                else _paperContent = paperContent?.ToString();


                args.Data.TryGetValue(FaxConstants.FaxPaperStampStateData, out var stampState);
                args.Data.TryGetValue(FaxConstants.FaxPaperStampedByData, out List<string>? stampedBy);
                args.Data.TryGetValue(FaxConstants.FaxPaperPrototypeData, out var prototypeId);

                if (_paperName != null && _paperContent != null)
                {
                    var printout = new FaxPrintout(_paperContent, _paperName, prototypeId?.ToString(),
                        stampState?.ToString(), stampedBy);


                    _audioSystem.PlayGlobal("/Audio/Machines/high_tech_confirm.ogg", Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false);
                    _faxMachineComponent.PrintingQueue.Enqueue(printout);
                }
            }
        }
    }

    private async Task ReceivedMessage(object sender, BasicDeliverEventArgs @event)
    {
        var memoryStream = new MemoryStream(@event.Body.ToArray());
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.WriteAsString,
        };

        var networkObject = await JsonSerializer.DeserializeAsync<NetworkPackage>(memoryStream, options);
        if (networkObject != null)
        {
            RaiseLocalEvent(networkObject);
        }
    }


    private void OnNetworkStartup(EntityUid uid, ExternalNetworkComponent component, ComponentStartup args)
    {
        //Тут надо регистрировать девайс во внешней системе
        if (component.AutoConnect && string.IsNullOrEmpty(component.Address))
        {
            component.Address = Guid.NewGuid().ToString();

            _mqService.SendMessage(new NetworkPackage()
            {
                Command = NetworkCommand.Register,
                Sender = (int) uid,
                SenderAddress = component.Address,
                PackageType = DeviceTypes.Fax
            });
        }
    }

    private void OnNetworkShutdown(EntityUid uid, ExternalNetworkComponent component, ComponentShutdown args)
    {
        //Удаляем девайс из внешней системы
        _mqService.SendMessage(new NetworkPackage()
        {
            Command = NetworkCommand.UnRegister,
            Sender = (int)uid,
            SenderAddress = component.Address,
            PackageType = DeviceTypes.Fax
        });
    }
}

public static class NetworkCommand
{
    public static string Register = "Register";
    public static string UnRegister = "UnRegister";
    public static string Transfer = "Transfer";
    public static string Ping = "Ping";
}

public sealed class NetworkPackage : EntityEventArgs
{
    public string? Command { get; set; }
    public string? Address { get; set; }
    public int? Sender { get; set; }
    public string? SenderAddress { get; set; }
    public string? PackageType { get; set; }
    public NetworkPayload? Data { get; set;}
}

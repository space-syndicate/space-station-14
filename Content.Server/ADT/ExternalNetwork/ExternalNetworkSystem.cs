using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.ADT.RabbitMq;
using Content.Server.ADT.RabbitMQ;
using Content.Server.Chat.Managers;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Server.Popups;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Robust.Shared.Player;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Content.Server.ADT.ExternalNetwork;

public sealed class ExternalNetworkSystem: EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RabbitMQManager _mqManager = default!;

    public override void Initialize()
    {
        base.Initialize();

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
                    _mqManager.SendMessage(new NetworkPackage()
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

                if(!args.Data.TryGetValue(FaxConstants.FaxPaperNameData, out string? paperName)) return;
                if(!args.Data.TryGetValue(FaxConstants.FaxPaperContentData, out string? paperContent)) return;

                args.Data.TryGetValue(FaxConstants.FaxPaperStampStateData, out string? stampState);
                args.Data.TryGetValue(FaxConstants.FaxPaperStampedByData, out JArray? stampedBy);
                args.Data.TryGetValue(FaxConstants.FaxPaperPrototypeData, out string? prototypeId);

                string? faxName = args.SenderAddress?.Substring(args.SenderAddress.Length - 6);
                string faxPaperName = $"{paperName} от {faxName}";
                var printout = new FaxPrintout(paperContent, faxPaperName, prototypeId, stampState, stampedBy?.ToObject<List<string>>());

                //Нет доступа к UID
                //_faxSystem.Receive(_faxMachineComponent.Owner,printout,args.SenderAddress,_faxMachineComponent);

                _audioSystem.PlayGlobal("/Audio/Machines/high_tech_confirm.ogg", Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false);
                _faxMachineComponent.PrintingQueue.Enqueue(printout);

            }
        }
    }

    private void OnNetworkStartup(EntityUid uid, ExternalNetworkComponent component, ComponentStartup args)
    {
        //Тут надо регистрировать девайс во внешней системе
        if (component.AutoConnect && string.IsNullOrEmpty(component.Address))
        {
            component.Address = Guid.NewGuid().ToString();

            _mqManager.SendMessage(new NetworkPackage()
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
        _mqManager.SendMessage(new NetworkPackage()
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

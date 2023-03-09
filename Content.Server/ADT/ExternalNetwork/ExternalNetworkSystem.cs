using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using JetBrains.Annotations;

namespace Content.Server.ADT.ExternalNetwork;

[UsedImplicitly]
public sealed class ExternalNetworkSystem: EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _networkSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExternalNetworkComponent, ComponentStartup>(OnNetworkStartup);
        SubscribeLocalEvent<ExternalNetworkComponent, ComponentShutdown>(OnNetworkShutdown);

    }

    private void OnNetworkStartup(EntityUid uid, ExternalNetworkComponent component, ComponentStartup args)
    {
        //Тут надо регистрировать девайс во внешней системе

    }

    private void OnNetworkShutdown(EntityUid uid, ExternalNetworkComponent component, ComponentShutdown args)
    {
        //Удаляем девайс из внешней системы
    }
}

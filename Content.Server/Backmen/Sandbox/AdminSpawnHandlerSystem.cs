using Content.Server.Sandbox;
using Robust.Server.Player;
using Robust.Shared.Network.Messages;

namespace Content.Server.Backmen.Sandbox;

public sealed class AdminSpawnHandler:EntitySystem
{
    private ISawmill _log = default!;
    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill($"ecs.systems.{nameof(AdminSpawnHandler)}");
        _sandboxSystem.OnAdminPlacement += SandboxSystemOnOnAdminPlacement;
    }

    private void SandboxSystemOnOnAdminPlacement(object? sender, MsgPlacement placement)
    {
        var channel = placement.MsgChannel;
        var session =  _playerManager.GetSessionByChannel(channel);
        _log.Info($"Admin {ToPrettyString(session.AttachedEntity?? EntityUid.Invalid):user} spawned {placement.EntityTemplateName} at {placement.EntityCoordinates:targetlocation}");
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _sandboxSystem.OnAdminPlacement -= SandboxSystemOnOnAdminPlacement;
    }
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SandboxSystem _sandboxSystem = default!;
}

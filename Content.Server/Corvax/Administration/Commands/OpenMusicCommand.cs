using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Content.Server.Administration;

namespace Content.Server.Corvax.Administration.MusicPlayer;

[AdminCommand(AdminFlags.Spawn)]
public sealed partial class OpenMusicCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "openmusic";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var eui = new MusicPlayerEui();
        _euiManager.OpenEui(eui, player);
    }
}

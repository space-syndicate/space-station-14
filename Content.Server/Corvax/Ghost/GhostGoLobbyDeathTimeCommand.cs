using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Corvax.Ghost;

[AdminCommand(AdminFlags.Server)]
public sealed partial class GhostGoLobbyDeathTimeCommand : LocalizedCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "ghost_golobby_deathtime";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
        {
            shell.WriteError(Help);
            return;
        }

        _cfg.SetCVar(CCCVars.GhostGoLobbyDeathTimeMinutes, minutes);
        shell.WriteLine($"ghost.go_lobby.death_time = {minutes}");
    }
}

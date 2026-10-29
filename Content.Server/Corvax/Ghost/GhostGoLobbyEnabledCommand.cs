using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Corvax.Ghost;

[AdminCommand(AdminFlags.Server)]
public sealed partial class GhostGoLobbyEnabledCommand : LocalizedCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "ghost_golobby";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!bool.TryParse(args[0], out var enabled))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        _cfg.SetCVar(CCCVars.GhostGoLobbyEnabled, enabled);
        shell.WriteLine($"ghost.go_lobby.enabled = {enabled}");
    }
}

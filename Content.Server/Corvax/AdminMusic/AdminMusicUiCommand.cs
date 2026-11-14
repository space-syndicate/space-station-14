using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Corvax.AdminMusic;

/// <summary>
///     Открывает UI музыкального сопровождения. Флаг такой же, как у <c>playglobalsound</c>.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class AdminMusicUiCommand : LocalizedEntityCommands
{
    [Dependency] private EuiManager _euiManager = default!;

    public override string Command => "musicui";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euiManager.OpenEui(new AdminMusicEui(), player);
    }
}

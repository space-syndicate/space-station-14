using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CorvaxNext.Administration.UI.Audio.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class OpenAudioPanelCommand : IConsoleCommand
{
    public string Command => "audiopanel";
    public string Description => "Opens the admin audio panel panel.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        var ui = new AdminAudioPanelEui();
        eui.OpenEui(ui, player);
    }
}

using Content.Server.Administration;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Server.Icarus.Commands;

[UsedImplicitly]
[AdminCommand(AdminFlags.Fun)]
public sealed class SpawnIcarusCommand : IConsoleCommand
{
    public string Command => "spawnicarus";
    public string Description => "Spawn Icarus beam.";
    public string Help => "spawnicarus <gridId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Incorrect number of arguments. " + Help);
            return;
        }
    }
}

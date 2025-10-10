using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class PlayLocalSoundCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public string Command => "playlocalsound";
    public string Description => Loc.GetString("play-local-sound-command-description");
    public string Help => Loc.GetString("play-local-sound-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        Filter filter;
        var audio = AudioParams.Default;

        switch (args.Length)
        {
            case 2:
                break;
            case 3:
                if (int.TryParse(args[2], out var volume))
                    audio = audio.WithVolume(volume);
                else
                {
                    shell.WriteError(Loc.GetString("play-local-sound-command-volume-parse", ("volume", args[2])));
                    return;
                }

                break;
            default:
                shell.WriteLine(Loc.GetString("play-local-sound-command-help"));
                return;
        }

        if (!NetEntity.TryParse(args[0], out var netid) || !_entManager.TryGetEntity(netid, out var uid))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (_entManager.TryGetComponent<MapComponent>(uid, out var map))
            filter = Filter.Empty().AddInMap(map.MapId, _entManager);
        else if (_entManager.HasComponent<MapGridComponent>(uid))
            filter = Filter.Empty().AddInGrid(uid.Value, _entManager);
        else
        {
            shell.WriteError(Loc.GetString("play-local-sound-command-uid"));
            return;
        }

        audio = audio.AddVolume(-8);
        _entManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(filter, new ResolvedPathSpecifier(args[1]), audio);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint(Loc.GetString("play-local-sound-command-arg-uid"));

        if (args.Length == 2)
        {
            var hint = Loc.GetString("play-local-sound-command-arg-path");

            var options = CompletionHelper.AudioFilePath(args[1], _protoManager, _res);

            return CompletionResult.FromHintOptions(options, hint);
        }

        if (args.Length == 3)
            return CompletionResult.FromHint(Loc.GetString("play-local-sound-command-arg-volume"));

        return CompletionResult.Empty;
    }
}

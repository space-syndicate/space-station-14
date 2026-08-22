using System.Numerics;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Administration;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Runtime.Versioning;

namespace Content.Server.Corvax.Shuttle.Commands;

[AdminCommand(AdminFlags.Fun)]

public sealed partial class FTLTargetSetCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private DockingSystem _dockSys = default!;
    [Dependency] private SharedTransformSystem _transformSys = default!;
    [Dependency] private SharedMapSystem _mapSys = default!;

    public override string Command => "ftltargetset";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var x = 0f;
        var y = 0f;
        var angle = 0f;
        string? tag = null;

        if (args.Length < 2 || args.Length > 6)
        {
            shell.WriteLine(Loc.GetString($"shell-need-between-arguments", ("lower", 2), ("upper", 6)));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEnt) || !_entManager.TryGetEntity(netEnt, out var uid))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid"));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var netTarget) || !_entManager.TryGetEntity(netTarget, out var target) || (!_entManager.HasComponent<MapComponent>(target) && !_entManager.HasComponent<MapGridComponent>(target)))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (args.Length > 3 && (!float.TryParse(args[2], out x) || !float.TryParse(args[3], out y)))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        if (args.Length > 4)
        {
            if (!float.TryParse(args[4], out angle) || angle < -360f || angle > 360f)
            {
                shell.WriteError(Loc.GetString("shell-argument-number-must-be-between", ("index", args[4]), ("lower", -360), ("upper", 360)));
                return;
            }
        }

        if (args.Length > 5)
        {
            tag = args[5];
        }

        if (!_entManager.TryGetComponent<FTLComponent>(uid, out var comp))
        {
            shell.WriteError(Loc.GetString("shell-entity-target-lacks-component",("componentName", nameof(FTLComponent)))); 
            return;
        }

        if (_entManager.HasComponent<ArrivalsShuttleComponent>(uid))
        {
            return;
        }

        if (comp.State != FTLState.Travelling)
        {
            return;
        }

        
        if (tag != null)
        {
            var config = _dockSys.GetDockingConfig(uid.Value, target.Value, tag);
            if (config != null)
            {
                comp.TargetCoordinates = config.Coordinates;
                comp.TargetAngle = config.Angle;
                comp.PriorityTag = tag;
            }
        } 
        else
        {
            comp.TargetCoordinates = new EntityCoordinates(target.Value, new Vector2(x, y));
            comp.TargetAngle = angle;
            comp.PriorityTag = tag;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<FTLComponent>(args[0], EntityManager), Loc.GetString("cmd-hint-ftltargetset-id")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Components<MapComponent>(args[1], EntityManager).Concat(CompletionHelper.Components<MapGridComponent>(args[1], EntityManager)), Loc.GetString("cmd-hint-ftltargetset-target")),
            3 => CompletionResult.FromHint(Loc.GetString("cmd-hint-ftltargetset-x")),
            4 => CompletionResult.FromHint(Loc.GetString("cmd-hint-ftltargetset-y")),
            5 => CompletionResult.FromHint(Loc.GetString("cmd-hint-ftltargetset-angle")),
            6 => CompletionResult.FromHint(Loc.GetString("cmd-hint-ftltargetset-tag")),
            _ => CompletionResult.Empty,
        };
    }
}

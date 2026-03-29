using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Timing;
using Content.Shared.Examine;
using Robust.Shared.Console;
using Robust.Shared.Timing;
using System;

namespace Content.Server.Corvax.Shuttle.Commands;

[AdminCommand(AdminFlags.Fun)]

public sealed class FTLTravelTimeSetCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!; 

    public override string Command => "ftltraveltimeset";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
            {
                shell.WriteLine(Loc.GetString($"shell-wrong-arguments-number-need-specific",
                    ("properAmount", 2),
                    ("currentAmount", args.Length)));
                return;
            }

        if (!NetEntity.TryParse(args[0], out var netEnt) || !_entManager.TryGetEntity(netEnt, out var uid))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid"));
            return;
        }

        if (!float.TryParse(args[1], out var time))
        {
            shell.WriteError(Loc.GetString("cmd-delaystart-invalid-seconds"));
            return;
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

        var startTime = comp.StateTime.Start;
        var newEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(time);

        comp.StateTime = new StartEndTime(startTime, newEndTime);
    }
}

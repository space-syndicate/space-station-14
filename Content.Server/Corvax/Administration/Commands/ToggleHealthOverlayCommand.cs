using Content.Server.Administration;
using Content.Server.Ghost;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Overlays;
using Robust.Shared.Console;

namespace Content.Server.Corvax.Administration.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed class ToggleHealthOverlayCommand : LocalizedEntityCommands
    {
        public override string Command => "togglehealthoverlay";
        public override string Description => Loc.GetString("command-togglehealthoverlay-description");
        public override string Help => $"Usage: {Command}";

        [Dependency] private readonly IEntityManager _entityManager = default!;

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            EntityUid entityUid;
            if (args.Length > 1)
            {
                shell.WriteError(Loc.GetString("shell-need-between-arguments", ("lower", 0),  ("upper", 1)));
                return;
            }

            if (args.Length == 1 && EntityUid.TryParse(args[0], out entityUid))
            {
                shell.WriteError("shell-invalid-entity-id");
                return;
            }
            else
            {
                entityUid = shell.Player!.AttachedEntity!.Value;

            }

            if (!_entityManager.EntityExists(entityUid))
            {
                shell.WriteError(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            if (!_entityManager.TryGetComponent<GhostComponent>(entityUid, out _))
            {
                shell.WriteError(Loc.GetString("shell-entity-must-be-ghost"));
                return;
            }

            if (_entityManager.TryGetComponent<ShowHealthBarsComponent>(entityUid, out var health))
            {

                _entityManager.RemoveComponent<ShowHealthBarsComponent>(entityUid);
                _entityManager.RemoveComponent<ShowHealthIconsComponent>(entityUid);
                return;
            }
            _entityManager.EnsureComponent<ShowHealthBarsComponent>(entityUid);
            _entityManager.EnsureComponent<ShowHealthIconsComponent>(entityUid);

        }
    }
}

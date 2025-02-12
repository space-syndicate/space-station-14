using System.Linq;
using Content.Server.Administration;
using Content.Shared._CorvaxNext.Skills;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CorvaxNext.Skills.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class GrantAllSkillsCommand : IConsoleCommand
{
    [Dependency] private readonly ILocalizationManager _localization = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    public string Command => "grantallskills";

    public string Description => "Grants all skills to given entity.";

    public string Help => "grantallskills <entityuid>";

    public void Execute(IConsoleShell shell, string arg, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(_localization.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var id))
        {
            shell.WriteError(_localization.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entity.TryGetEntity(new(id), out var entity))
        {
            shell.WriteError(_localization.GetString("shell-invalid-entity-id"));
            return;
        }

        var component = _entity.EnsureComponent<SkillsComponent>(entity.Value);

        component.Skills.UnionWith(Enum.GetValues<Shared._CorvaxNext.Skills.Skills>());

        _entity.Dirty(entity.Value, component);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(_entity.GetEntities()
                .Select(entity => entity.Id.ToString())
                .Where(str => str.StartsWith(args[0]))
                .Select(entity => new CompletionOption(entity)));

        return new([], null);
    }
}

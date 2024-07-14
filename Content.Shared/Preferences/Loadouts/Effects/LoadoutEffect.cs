using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

[ImplicitDataDefinitionForInheritors]
public abstract partial class LoadoutEffect
{
    /// <summary>
    /// Tries to validate the effect.
    /// </summary>
    public abstract bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        LoadoutPrototype proto, // Corvax-Sponsors
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason);

    public virtual void Apply(RoleLoadout loadout) {}
}

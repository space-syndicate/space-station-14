using Content.Shared.Weapons.Mod.Events;
using Content.Shared.Weapons.Mod.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Mod.Components;

[RegisterComponent]
public sealed partial class ModComponent : Component
{
    [DataField]
    public string CareMods = string.Empty;

    [DataField("soundEject")]
    public SoundSpecifier? EjectSound = new SoundCollectionSpecifier("ModEject");

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public SoundSpecifier? SoundInsert = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/bullet_insert.ogg");
}



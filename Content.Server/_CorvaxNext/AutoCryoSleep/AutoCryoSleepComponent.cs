/*
 * Author: TornadoTech
 * License: AGPL
 */

using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.AutoCryoSleep;

[RegisterComponent]
public sealed partial class AutoCryoSleepComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Disconnected;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? EffectId = "JetpackEffect";
}

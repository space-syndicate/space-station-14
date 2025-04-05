using Content.Shared._CorvaxNext.BattleRoyale.RangeFinder;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;

namespace Content.Client._CorvaxNext.BattleRoyale.RangeFinder;

public sealed class RangeFinderVisualizerSystem : VisualizerSystem<RangeFinderComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, RangeFinderComponent component, ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(PinpointerVisuals.IsActive, out var isActiveObj)
            || !args.AppearanceData.TryGetValue(PinpointerVisuals.TargetDistance, out var distanceObj)
            || isActiveObj is not bool isActive
            || distanceObj is not Distance distance)
        {
            return;
        }

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!isActive)
        {
            sprite.LayerSetState(PinpointerLayers.Screen, "pinonnull");
            sprite.LayerSetRotation(PinpointerLayers.Screen, Angle.Zero);
            return;
        }

        var state = distance switch
        {
            Distance.Reached => "pinondirect",
            Distance.Close => "pinonclose",
            Distance.Medium => "pinonmedium",
            Distance.Far => "pinonfar",
            _ => "pinonnull"
        };

        sprite.LayerSetState(PinpointerLayers.Screen, state);
    }
}

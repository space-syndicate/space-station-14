using Content.Shared._CorvaxNext.Medical.SmartFridge;
using SmartFridgeComponent = Content.Shared._CorvaxNext.Medical.SmartFridge.SmartFridgeComponent;

namespace Content.Client._CorvaxNext.Medical.SmartFridge;

public sealed class SmartFridgeSystem : SharedSmartFridgeSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartFridgeComponent, AfterAutoHandleStateEvent>(OnVendingAfterState);
    }

    private void OnVendingAfterState(EntityUid uid, SmartFridgeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_uiSystem.TryGetOpenUi<SmartFridgeBoundUserInterface>(uid, SmartFridgeUiKey.Key, out var bui))
        {
            bui.Refresh();
        }
    }
}

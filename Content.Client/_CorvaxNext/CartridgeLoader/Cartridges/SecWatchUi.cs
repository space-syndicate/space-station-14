using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._CorvaxNext.CartridgeLoader.Cartridges;

public sealed partial class SecWatchUi : UIFragment
{
    private SecWatchUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface ui, EntityUid? owner)
    {
        _fragment = new SecWatchUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is SecWatchUiState cast)
            _fragment?.UpdateState(cast);
    }
}
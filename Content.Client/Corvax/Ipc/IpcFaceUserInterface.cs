using System;
using Content.Shared.Corvax.Ipc;
using Robust.Client.GameObjects;
using Content.Shared.UserInterface;

namespace Content.Client.Corvax.Ipc;

public sealed class IpcFaceUserInterface : BoundUserInterface
{
    private IpcFaceMenu? _menu;

    public IpcFaceUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = new IpcFaceMenu();
        _menu.OnClose += Close;
        _menu.FaceSelected += state =>
        {
            SendPredictedMessage(new IpcFaceSelectMessage(state));
            Close();
        };
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state != null)
            base.UpdateState(state);
        if (_menu == null || state is not IpcFaceBuiState msg)
            return;
        _menu.Populate(msg.Profile, msg.Selected);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _menu?.Dispose();
        _menu = null;
    }
}

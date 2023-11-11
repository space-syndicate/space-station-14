using Content.Shared.Backmen.StationAI.Events;

namespace Content.Client.Backmen.StationAI.UI;

/// <summary>
///     Initializes a <see cref="AICameraList"/> and updates it when new server messages are received.
/// </summary>
public sealed class AICameraListBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private AICameraList _window = new AICameraList();

    public AICameraListBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        var netId = _entityManager.GetNetEntity(owner);
        _window.TryUpdateCameraList += () => SendMessage(new AICameraListMessage(netId));
        _window.WarpToCamera += (uid) => SendMessage(new AICameraWarpMessage(netId, _entityManager.GetNetEntity(uid)));
    }

    protected override void Open()
    {
        base.Open();

        if (State != null) UpdateState(State);

        _window.OpenCentered();
    }

    /// <summary>
    ///     Update the UI state based on server-sent info
    /// </summary>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not AIBoundUserInterfaceState cast)
            return;

        _window.UpdateCameraList(_entityManager.GetEntityList(cast.Cameras));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window.Dispose();
    }
}

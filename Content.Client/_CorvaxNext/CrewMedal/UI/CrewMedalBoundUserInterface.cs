using Content.Shared._CorvaxNext.CrewMedal;
using Robust.Client.UserInterface;

namespace Content.Client._CorvaxNext.CrewMedal.UI;

/// <summary>
/// A wrapper class for the Crew Medal user interface.
/// Initializes the <see cref="CrewMedalWindow"/> and updates it when new data is received from the server.
/// </summary>
public sealed class CrewMedalBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    /// <summary>
    /// The main interface window.
    /// </summary>
    [ViewVariables]
    private CrewMedalWindow? _window;

    public CrewMedalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CrewMedalWindow>();
        _window.OnReasonChanged += HandleReasonChanged;

        Reload();
    }

    /// <summary>
    /// Called when the reason is changed in the <see cref="CrewMedalWindow"/>.
    /// Sends a message to the server with the new reason if it differs from the current one.
    /// </summary>
    private void HandleReasonChanged(string newReason)
    {
        if (!_entityManager.TryGetComponent<CrewMedalComponent>(Owner, out var component))
            return;

        if (!component.Reason.Equals(newReason))
        {
            SendPredictedMessage(new CrewMedalReasonChangedMessage(newReason));
        }
    }

    /// <summary>
    /// Updates the data in the window to reflect the current state of the <see cref="CrewMedalComponent"/>.
    /// </summary>
    public void Reload()
    {
        if (_window is null)
            return;

        if (!_entityManager.TryGetComponent<CrewMedalComponent>(Owner, out var component))
            return;

        _window.SetCurrentReason(component.Reason);
        _window.SetAwarded(component.Awarded);
        _window.SetMaxCharacters(component.MaxCharacters);
    }
}

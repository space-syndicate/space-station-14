namespace Content.Server.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public sealed class IcarusTerminalSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    public void OnKeyInsert()
    {

    }

    public void Fire()
    {
        // TODO: Fire Icarus beam
    }

    public bool IsUnlocked(IcarusTerminalComponent comp)
    {
        return comp.ActivatedKeys == comp.TotalKeys;
    }
}

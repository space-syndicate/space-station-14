using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Maths;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public List<string> Prototypes { get; }
    public bool PriorityJoin { get; }
    public Color? OocColor { get; }
    public int ExtraCharSlots { get; }
}

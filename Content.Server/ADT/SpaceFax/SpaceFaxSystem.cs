using Content.Server.Fax;
using Robust.Shared.Prototypes;

namespace Content.Server.ADT.SpaceFax;
/// <summary>
/// Send Fax to Another Server
/// </summary>
public sealed class SpaceFaxSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;
}

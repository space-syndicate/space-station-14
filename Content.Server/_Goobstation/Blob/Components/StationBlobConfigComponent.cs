using System.ComponentModel.DataAnnotations;
//using Content.Server.SpecForces;
using Robust.Shared.Prototypes;

namespace Content.Server._Goobstation.Blob.Components;

[RegisterComponent]
public sealed partial class StationBlobConfigComponent : Component
{
    public const int DefaultStageBegin = 40;
    public const int DefaultStageCritical = 1000;
    public const int DefaultStageEnd = 1600;

    [DataField]
    public int StageBegin { get; set; } = DefaultStageBegin;

    [DataField]
    public int StageCritical { get; set; } = DefaultStageCritical;

    [DataField]
    public int StageTheEnd { get; set; } = DefaultStageEnd;

    /*[DataField("specForceTeam")]  //Goobstation - Disabled automatic ERT
    public ProtoId<SpecForceTeamPrototype> SpecForceTeam { get; set; } = "RXBZZBlobDefault";*/
}

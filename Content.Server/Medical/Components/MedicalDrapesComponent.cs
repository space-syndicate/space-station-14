namespace Content.Server.Medical.Components;
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(MedicalDrapesSystem), typeof(CryoPodSystem))]
public sealed partial class MedicalDrapesComponent : Component
{
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(0.8);
    [DataField]
    public EntityUid? UsedOnEntity;
    [DataField]
    public float MaxUseRange = 2.5f;

}

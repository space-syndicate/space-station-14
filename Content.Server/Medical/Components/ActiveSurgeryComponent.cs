namespace Content.Server.Medical.Components
{
    [RegisterComponent]
    public sealed partial class ActiveSurgeryComponent : Component
    {
        [DataField]
        public bool? IsActive = false;
    }
}

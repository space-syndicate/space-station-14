namespace Content.Server.Remotes
{
    [RegisterComponent]
    [Access(typeof(DoorRemoteSystem))]
    public sealed partial class DoorRemoteComponent : Component
    {
        [DataField("mode"), ViewVariables(VVAccess.ReadWrite)]
        public OperatingMode Mode = OperatingMode.OpenClose;

        public enum OperatingMode : byte
        {
            OpenClose,
            ToggleBolts,
            ToggleEmergencyAccess
        }
    }
}

using Content.Shared.Atmos;

namespace Content.Server.Atmos.Piping.Trinary.Components
{
    [RegisterComponent]
    public sealed partial class GasFilterComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField("inlet")]
        public string InletName = "inlet";

        [DataField("filter")]
        public string FilterName = "filter";

        [DataField("outlet")]
        public string OutletName = "outlet";

        [DataField]
        public float TransferRate = Atmospherics.MaxTransferRate;

        [DataField]
        public float MaxTransferRate = Atmospherics.MaxTransferRate;

        [DataField, ViewVariables(VVAccess.ReadWrite)] // Corvax-Next-AutoPipes
        public Gas? FilteredGas;
		
		/// Corvax-Next-AutoPipes-Start
        [DataField]
        public bool StartOnMapInit { get; set; } = false;
		/// Corvax-Next-AutoPipes-End
    }
}

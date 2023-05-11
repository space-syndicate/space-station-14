using Content.Shared.Sirena;

namespace Content.Server.DetailExaminable
{
    [RegisterComponent]
    public sealed class DetailExaminableComponent : Component
    {
        [DataField("content", required: true)] [ViewVariables(VVAccess.ReadWrite)]
        public string Content = "";

        [DataField("ERPStatus", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public EnumERPStatus ERPStatus = EnumERPStatus.NO;

        public string GetERPStatusName()
        {
            switch (ERPStatus)
            {
                case EnumERPStatus.HALF:
                    return Loc.GetString("humanoid-erp-status-half");
                case EnumERPStatus.FULL:
                    return Loc.GetString("humanoid-erp-status-full");
                default:
                    return Loc.GetString("humanoid-erp-status-no");
            }
        }
    }
}

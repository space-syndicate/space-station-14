using Robust.Shared.Configuration;

namespace Content.Shared._CorvaxNext.NextVars;

/// <summary>
///     Corvax modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class NextVars
{
    /// <summary>
    /// Offer item.
    /// </summary>
    public static readonly CVarDef<bool> OfferModeIndicatorsPointShow =
        CVarDef.Create("hud.offer_mode_indicators_point_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// _CorvaxNext Surgery cvars
    /// </summary>
    #region Surgery

    public static readonly CVarDef<bool> CanOperateOnSelf =
        CVarDef.Create("surgery.can_operate_on_self", false, CVar.SERVERONLY);

    #endregion
}

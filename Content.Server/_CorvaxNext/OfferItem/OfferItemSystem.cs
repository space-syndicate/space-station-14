using Content.Shared.Alert;
using Content.Shared._CorvaxNext.OfferItem;
using Content.Shared.Hands.Components;

namespace Content.Server._CorvaxNext.OfferItem;

public sealed class OfferItemSystem : SharedOfferItemSystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;

    private float _offerAcc = 0;
    private const float OfferAccMax = 3f;

    public override void Update(float frameTime)
    {
        _offerAcc += frameTime;

        if (_offerAcc >= OfferAccMax)
            _offerAcc -= OfferAccMax;
        else
            return;

        var query = EntityQueryEnumerator<OfferItemComponent, HandsComponent>();
        while (query.MoveNext(out var uid, out var offerItem, out var hands))
        {
            if (hands.ActiveHand is null)
                continue;

            if (offerItem.Hand is not null && hands.Hands[offerItem.Hand].HeldEntity is null)
                if (offerItem.Target is not null)
                {
                    UnReceive(offerItem.Target.Value, offerItem: offerItem);
                    offerItem.IsInOfferMode = false;
                    Dirty(uid, offerItem);
                }
                else
                    UnOffer(uid, offerItem);

            if (!offerItem.IsInReceiveMode)
            {
                _alertsSystem.ClearAlert(uid, OfferAlert);
                continue;
            }

            _alertsSystem.ShowAlert(uid, OfferAlert);
        }
    }
}

using Content.Server.Documents;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Lathe;
using Content.Shared.Paper;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.Documents
{
    public sealed partial class DocumentPrinterSystem : EntitySystem
    {
        [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
        [Dependency] private readonly PaperSystem _paper = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DocumentPrinterComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<DocumentPrinterComponent, LatheGetResultEvent>(SetContentDocument);
        }

        private void OnInit(EntityUid uid, DocumentPrinterComponent comp, ComponentInit args)
        {
            _itemSlots.AddItemSlot(uid, "idSlot", comp.IdSlot);
        }

        private void SetContentDocument(EntityUid uid, DocumentPrinterComponent comp, LatheGetResultEvent result)
        {
            var paperComp = EnsureComp<PaperComponent>(result.ResultItem);
            var stationName = GetStation(result.ResultItem);

            if (comp.IdSlot.Item is { Valid: true } idCardEntity &&
                TryComp<IdCardComponent>(idCardEntity, out var idCard))
            {
                _paper.SetContent(result.ResultItem, FormatString(Loc.GetString(paperComp.Content), stationName, idCard));
            }
            else
                _paper.SetContent(result.ResultItem, FormatString(Loc.GetString(paperComp.Content), stationName));
        }

        public string FormatString(string content, string? station, IdCardComponent? idCard = null)
        {
            var stationTime = GetTimeStation();

            content = content
                .Replace(":DATE:", stationTime)
                .Replace(":STATION:", station ?? "Station XX-000");

            content = content
                .Replace(":NAME:", idCard?.FullName ?? "(ФИО)")
                .Replace(":JOB:", idCard?.LocalizedJobTitle ?? "(полное наименование должности)");

            return content;
        }

        private string GetStation(EntityUid document)
        {
            var station = _station.GetOwningStation(document);
            var stationName = station != null
                ? Name(station.Value)
                : null;
            return stationName ?? "";
        }

        private string GetTimeStation()
        {
            var curTime = _timing.CurTime;
            var formattedTime = $"{(int)curTime.TotalHours:D2}:{curTime.Minutes:D2}:{curTime.Seconds:D2}";
            return DateTime.Now.AddYears(1000).ToShortDateString() + " " + formattedTime;
        }

    }
}

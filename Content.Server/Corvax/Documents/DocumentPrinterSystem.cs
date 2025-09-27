using Content.Shared.Station;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Lathe;
using Content.Shared.Paper;
using Robust.Shared.Timing;
using Content.Shared.Corvax.Documents;

namespace Content.Server.Corvax.Documents;

public sealed partial class DocumentPrinterSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DocumentPrinterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DocumentPrinterComponent, LatheGetResultEvent>(SetContentDocument);
    }

    private void OnInit(Entity<DocumentPrinterComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, "idSlot", ent.Comp.IdSlot);
    }

    private void SetContentDocument(Entity<DocumentPrinterComponent> ent, ref LatheGetResultEvent result)
    {
        var paperComp = EnsureComp<PaperComponent>(result.ResultItem);
        var stationName = GetStation(result.ResultItem);

        if (ent.Comp.IdSlot.Item is { Valid: true } idCardEntity &&
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
            .Replace(":STATION:", station ?? Loc.GetString("doc-text-printer-default-station"));

        content = content
            .Replace(":NAME:", idCard?.FullName ?? Loc.GetString("doc-text-printer-default-name"))
            .Replace(":JOB:", idCard?.LocalizedJobTitle ?? Loc.GetString("doc-text-printer-default-job"));

        return content;
    }

    private string? GetStation(EntityUid document)
    {
        var station = _station.GetOwningStation(document);
        var stationName = station != null
            ? Name(station.Value)
            : null;
        return stationName;
    }

    private string GetTimeStation()
    {
        var curTime = _timing.CurTime;
        var formattedTime = $"{(int)curTime.TotalHours:D2}:{curTime.Minutes:D2}:{curTime.Seconds:D2}";
        return DateTime.UtcNow.AddYears(1000).ToShortDateString() + " " + formattedTime;
    }

}

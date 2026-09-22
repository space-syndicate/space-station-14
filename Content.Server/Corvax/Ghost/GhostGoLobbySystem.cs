using Content.Server._Corvax.Events;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.Events;
using Content.Shared.Corvax.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.Ghost;

public sealed partial class GhostGoLobbySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private bool _enabled;
    private TimeSpan _requiredPlaytime;
    private TimeSpan _deathTime;

    private readonly HashSet<int> _usedCharacters = new();

    public bool TryTakeCharacter(HumanoidCharacterProfile profile)
    {
        return _usedCharacters.Add(GetCharacterHash(profile));
    }

    private static int GetCharacterHash(HumanoidCharacterProfile profile)
    {
        return HashCode.Combine(profile.Name, profile.Sex, profile.Age, profile.Species);
    }

    public override void Initialize()
    {
        SubscribeNetworkEvent<GhostGoLobbyEvent>(OnGhostGoLobby);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnGhostAttached);

        Subs.CVar(_cfg, CCCVars.GhostGoLobbyEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCCVars.GhostGoLobbyTimeHours, value => _requiredPlaytime = TimeSpan.FromHours(value), true);
        Subs.CVar(_cfg, CCCVars.GhostGoLobbyDeathTimeMinutes, OnDeathTimeChanged, true);
    }

    private void OnDeathTimeChanged(float minutes)
    {
        _deathTime = TimeSpan.FromMinutes(minutes);

        var clampTo = _timing.CurTime + _deathTime;
        var query = EntityQueryEnumerator<GhostGoLobbyComponent>();
        while (query.MoveNext(out var uid, out var lobby))
        {
            if (lobby.AvailableAt <= clampTo)
                continue;

            lobby.AvailableAt = clampTo;
            Dirty(uid, lobby);
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PreRoundLobby)
            _usedCharacters.Clear();
    }

    private void OnGhostAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        if (HasComp<GhostGoLobbyComponent>(uid))
            return;

        var lobby = AddComp<GhostGoLobbyComponent>(uid);
        lobby.AvailableAt = _timing.CurTime + _deathTime;
        Dirty(uid, lobby);
    }

    private void OnGhostGoLobby(GhostGoLobbyEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } attached)
            return;

        TryGhostGoLobby(attached, args.SenderSession);
    }

    private void TryGhostGoLobby(EntityUid uid, ICommonSession session)
    {
        if (!_enabled)
            return;

        var all = _playTime.GetOverallPlaytime(session);
        if (all < _requiredPlaytime)
        {
            var remaining = (int)Math.Ceiling((_requiredPlaytime - all).TotalHours);
            _popup.PopupEntity(Loc.GetString("ghost-go-lobby-playtime", ("hours", remaining)), uid, uid);
            return;
        }

        if (TryComp<GhostGoLobbyComponent>(uid, out var lobby) && _timing.CurTime < lobby.AvailableAt)
        {
            var remaining = (int)Math.Ceiling((lobby.AvailableAt - _timing.CurTime).TotalMinutes);
            _popup.PopupEntity(Loc.GetString("ghost-go-lobby-deathtime", ("minutes", remaining)), uid, uid);
            return;
        }

        _mind.WipeMind(session);

        RaiseLocalEvent(new GhostJoinLobbyRequestEvent(session));
    }
}

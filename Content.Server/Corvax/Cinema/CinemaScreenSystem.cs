using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Server.Administration.Managers;
using Content.Server.Popups;
using Robust.Server.GameObjects;
using Content.Shared.Corvax.Cinema;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.Cinema;

/// <summary>
/// Server-side authority for cinema screens.
///
/// Holds <see cref="CinemaScreenComponent.VideoUrl"/>, <see cref="CinemaScreenComponent.Playing"/>,
/// <see cref="CinemaScreenComponent.ServerStartTime"/> and <see cref="CinemaScreenComponent.PausePosition"/>
/// so that every client derives the same playback position from server time instead of running its own timer.
/// </summary>
public sealed partial class CinemaScreenSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CinemaScreenComponent, CinemaScreenControlMessage>(OnControlMessage);
        SubscribeLocalEvent<CinemaScreenComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeNetworkEvent<CinemaAudioChunkRequestEvent>(OnAudioRequest);
        SubscribeNetworkEvent<CinemaVideoChunkRequestEvent>(OnVideoRequest);
        SubscribeLocalEvent<CinemaScreenComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CinemaScreenComponent, ComponentStartup>(OnCinemaStartup);
        SubscribeLocalEvent<CinemaScreenComponent, ComponentShutdown>(OnCinemaShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        InitializeAudioExtraction();
    }

    private void OnCinemaStartup(EntityUid uid, CinemaScreenComponent comp, ComponentStartup args)
    {
        // Screens restored with an existing URL must regenerate their audio manifest after a server restart.
        ResetAudioMetadata(comp);
        PrepareAudio(uid, comp);
    }

    private void OnCinemaShutdown(EntityUid uid, CinemaScreenComponent comp, ComponentShutdown args)
    {
        _episodeRequests.Remove(uid);
        ReleaseAudioRequest(uid);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        ResetAudioExtractionCache();
    }

    public override void Shutdown()
    {
        ShutdownAudioExtraction();
        base.Shutdown();
    }

    private void OnUiOpened(Entity<CinemaScreenComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateScreen(ent, ent.Comp);
    }

    private void UpdateScreen(EntityUid uid, CinemaScreenComponent comp)
    {
        Dirty(uid, comp);
        UpdateScreenUi(uid, comp);
    }

    private void UpdateScreenUi(EntityUid uid, CinemaScreenComponent comp)
    {
        var status = comp.Broken ? "cinema-status-broken" : comp.AudioStatus;
        if (!comp.Broken && comp.AudioCacheKey != null && !comp.Buffering && !comp.StreamFailed)
            status = comp.Playing ? "cinema-status-playing" : "cinema-status-paused";
        _ui.SetUiState(uid, CinemaScreenUiKey.Key, new CinemaScreenState(comp.VideoUrl, comp.Volume, status, comp.Films.Keys.ToArray(), comp.PreparationStage, comp.PreparationPercent));
    }

    private void OnControlMessage(Entity<CinemaScreenComponent> ent, ref CinemaScreenControlMessage msg)
    {
        var screenUid = ent.Owner;
        var comp = ent.Comp;
        if (!_players.TryGetSessionByEntity(msg.Actor, out var session))
            return;

        var isAdmin = _admin.IsAdmin(session);
        if (!isAdmin)
        {
            // Input validation is disabled on the BUI to support admin ghosts. Apply ordinary interaction
            // checks here for players, and never accept a custom URL from them.
            if (!_actionBlocker.CanInteract(msg.Actor, screenUid) ||
                !_interaction.InRangeUnobstructed(msg.Actor, screenUid))
                return;

            if (msg.Action == CinemaScreenAction.SetUrl || !string.IsNullOrEmpty(msg.Url))
            {
                _popup.PopupEntity(Loc.GetString("cinema-admin-only"), screenUid, msg.Actor);
                return;
            }
        }

        // Loading is shared by all viewers; reject controls from stale or already-open panels too.
        if (comp.Broken || comp.AudioStatus is "cinema-status-loading" or "cinema-status-resolving" or "cinema-status-buffering")
            return;

        msg.Url = msg.Url.Trim();
        if ((msg.Action == CinemaScreenAction.SetUrl ||
             msg.Action == CinemaScreenAction.Play && msg.Url.Length > 0) && !IsUrlAllowed(msg.Url))
        {
            _popup.PopupEntity(Loc.GetString("cinema-invalid-url"), screenUid, msg.Actor);
            return;
        }

        switch (msg.Action)
        {
            case CinemaScreenAction.SelectFilm:
                if (!comp.Films.TryGetValue(msg.Film, out var filmUrl) || !IsUrlAllowed(filmUrl))
                {
                    _popup.PopupEntity(Loc.GetString("cinema-invalid-film"), screenUid, msg.Actor);
                    return;
                }
                Play(screenUid, comp, filmUrl);
                break;
            case CinemaScreenAction.SetUrl:
                SetUrl(screenUid, comp, msg.Url);
                break;
            case CinemaScreenAction.Play:
                Play(screenUid, comp, msg.Url);
                break;
            case CinemaScreenAction.Pause:
                Pause(screenUid, comp);
                break;
            case CinemaScreenAction.Stop:
                Stop(screenUid, comp);
                break;
            case CinemaScreenAction.Seek:
                Seek(screenUid, comp, msg.SeekSeconds);
                break;
            case CinemaScreenAction.SetVolume:
                SetVolume(screenUid, comp, msg.Volume);
                break;
        }
    }

    private void SetUrl(EntityUid uid, CinemaScreenComponent comp, string? url)
    {
        if (!IsUrlAllowed(url))
        {
            Log.Debug($"Cinema SetUrl REJECTED: url='{url}' (must be HTTPS and whitelisted)");
            return;
        }

        _episodeRequests.Remove(uid);
        ReleaseAudioRequest(uid);
        comp.VideoUrl = url;
        comp.ResolvedVideoUrl = null;
        comp.Playing = false;
        comp.PausePosition = 0;
        comp.ServerStartTime = default;
        ResetAudioMetadata(comp);
        UpdateScreen(uid, comp);
        PrepareAudio(uid, comp);
        StartPlaybackWhenReady(uid, comp);
        Log.Debug($"Cinema SetUrl OK: '{url}'");
    }

    private void Play(EntityUid uid, CinemaScreenComponent comp, string? url)
    {
        // Allow the client to pass a URL along with Play (paste URL + Play in one step).
        if (!string.IsNullOrWhiteSpace(url) && url != comp.VideoUrl)
        {
            SetUrl(uid, comp, url);
        }

        if (string.IsNullOrWhiteSpace(comp.VideoUrl))
        {
            Log.Debug($"Cinema Play REJECTED: no video URL set");
            return;
        }

        PrepareAudio(uid, comp);
        StartPlaybackWhenReady(uid, comp);
    }

    internal void StartPlaybackWhenReady(EntityUid uid, CinemaScreenComponent comp)
    {
        if (comp.StreamPreparing)
        {
            if (comp.Playing)
                return;
            comp.PlayWhenPrepared = true;
            UpdateStreamingPlayback(uid, comp);
            return;
        }
        if (comp.AudioStatus is "cinema-status-loading" or "cinema-status-resolving")
        {
            comp.PlayWhenPrepared = true;
            comp.Playing = false;
            UpdateScreen(uid, comp);
            return;
        }

        comp.PlayWhenPrepared = false;
        if (comp.Playing)
            return;

        comp.ServerStartTime = _timing.RealTime;
        comp.Playing = true;
        UpdateScreen(uid, comp);
        Log.Debug($"Cinema Play OK: '{comp.VideoUrl}'");
    }

    private void Pause(EntityUid uid, CinemaScreenComponent comp)
    {
        comp.PlayWhenPrepared = false;
        if (!comp.Playing)
            return;

        comp.PausePosition = CurrentPosition(comp);
        comp.Playing = false;
        UpdateScreen(uid, comp);
    }

    private void Stop(EntityUid uid, CinemaScreenComponent comp)
    {
        comp.PlayWhenPrepared = false;
        comp.Buffering = false;
        comp.Playing = false;
        comp.PausePosition = 0;
        comp.ServerStartTime = default;
        UpdateScreen(uid, comp);
    }

    private void Seek(EntityUid uid, CinemaScreenComponent comp, double seconds)
    {
        if (!double.IsFinite(seconds))
            return;

        comp.PausePosition = Math.Clamp(seconds, 0, TimeSpan.MaxValue.TotalSeconds / 2);
        if (comp.StreamDuration > 0)
            comp.PausePosition = Math.Min(comp.PausePosition, Math.Max(0, comp.StreamDuration - 0.05));

        // Re-anchor the play clock so the seek takes effect immediately for every client.
        if (comp.Playing)
            comp.ServerStartTime = _timing.RealTime;
        if (comp.StreamPreparing)
        {
            if (comp.PausePosition >= PreparedStreamEnd(comp))
            {
                comp.PlayWhenPrepared |= comp.Playing;
                comp.Playing = false;
                comp.Buffering = true;
                comp.AudioStatus = "cinema-status-buffering";
            }
            UpdateStreamingPlayback(uid, comp);
        }

        UpdateScreen(uid, comp);
    }

    private void SetVolume(EntityUid uid, CinemaScreenComponent comp, float volume)
    {
        if (!float.IsFinite(volume))
            return;

        comp.Volume = Math.Clamp(volume, 0f, 1f);
        UpdateScreen(uid, comp);
    }

    /// <summary>The position (seconds) the video should currently be at, derived from server time.</summary>
    private double CurrentPosition(CinemaScreenComponent comp)
    {
        if (!comp.Playing || comp.ResolvedVideoUrl != null && !comp.HasVideoSegments)
            return comp.PausePosition;

        var elapsed = (_timing.RealTime - comp.ServerStartTime).TotalSeconds;
        return comp.PausePosition + Math.Max(0, elapsed);
    }

    private void OnDamageChanged(Entity<CinemaScreenComponent> entity, ref DamageChangedEvent args)
    {
        if (entity.Comp.Broken)
            return;

        if (_damageable.GetTotalDamage(entity.Owner).Float() < entity.Comp.BreakDamage)
            return;

        ReleaseAudioRequest(entity.Owner);
        entity.Comp.Broken = true;
        entity.Comp.Playing = false;
        UpdateScreen(entity.Owner, entity.Comp);
    }

    private bool IsUrlAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps &&
            !(uri.Scheme == Uri.UriSchemeHttp && _cfg.GetCVar(CinemaCVars.AllowHttp)))
            return false;

        if (uri.Host.Length == 0 || uri.UserInfo.Length != 0 ||
            !(IsDirectMediaUrl(url) || TryGetAniLibertyEpisode(url, out _) || IsAniLibertyMedia(uri, ".m3u8")))
            return false;

        return IsHostAllowed(uri);
    }

    private bool IsHostAllowed(Uri uri)
    {
        // Domain whitelist can be turned off for local/dev media servers.
        if (!_cfg.GetCVar(CinemaCVars.WhitelistEnabled))
            return true;

        var whitelist = _cfg.GetCVar(CinemaCVars.UrlWhitelist);
        var entries = whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry))
                continue;

            if (string.Equals(uri.Host, entry, StringComparison.OrdinalIgnoreCase))
                return true;

            // A suffix entry like "archive.org" also allows "*.archive.org".
            if (uri.Host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

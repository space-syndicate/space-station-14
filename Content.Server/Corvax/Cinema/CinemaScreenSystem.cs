using Content.Server.Administration.Managers;
using Content.Shared.Corvax.Cinema;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
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
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<MsgCinemaScreenControl>(OnControlMessage);
        SubscribeNetworkEvent<CinemaAudioChunkRequestEvent>(OnAudioRequest);
        SubscribeLocalEvent<CinemaScreenComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CinemaScreenComponent, ComponentStartup>(OnCinemaStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        InitializeAudioExtraction();
    }

    private void OnCinemaStartup(EntityUid uid, CinemaScreenComponent comp, ComponentStartup args)
    {
        // Screens restored with an existing URL must regenerate their audio manifest after a server restart.
        ResetAudioMetadata(comp);
        PrepareAudio(uid, comp);
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

    private void OnControlMessage(MsgCinemaScreenControl msg)
    {
        var session = _players.GetSessionByChannel(msg.MsgChannel);

        Log.Info($"Cinema control: session={session.Name}, action={msg.Action}, entity={msg.Entity}, url='{msg.Url}', seek={msg.SeekSeconds}, volume={msg.Volume}");

        // Only admins may change playback state.
        if (!_admin.IsAdmin(session))
        {
            Log.Info($"Cinema control REJECTED: {session.Name} is not an admin");
            return;
        }

        if (!TryGetEntity(msg.Entity, out var uid) || uid is not { } screenUid ||
            !TryComp<CinemaScreenComponent>(screenUid, out var comp))
        {
            Log.Info($"Cinema control REJECTED: entity {msg.Entity} not found / not a CinemaScreen");
            return;
        }

        if (comp.Broken)
        {
            Log.Info($"Cinema control REJECTED: screen {ToPrettyString(screenUid)} is broken");
            return;
        }

        switch (msg.Action)
        {
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
            Log.Info($"Cinema SetUrl REJECTED: url='{url}' (must be HTTPS and whitelisted)");
            return;
        }

        comp.VideoUrl = url;
        comp.Playing = false;
        comp.PausePosition = 0;
        comp.ServerStartTime = default;
        ResetAudioMetadata(comp);
        Dirty(uid, comp);
        PrepareAudio(uid, comp);
        Log.Info($"Cinema SetUrl OK: '{url}'");
    }

    private void Play(EntityUid uid, CinemaScreenComponent comp, string? url)
    {
        // Allow the client to pass a URL along with Play (paste URL + Play in one step).
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!IsUrlAllowed(url))
            {
                Log.Info($"Cinema Play REJECTED: url='{url}' (must be HTTPS and whitelisted)");
                return;
            }

            comp.VideoUrl = url;
            comp.PausePosition = 0;
            ResetAudioMetadata(comp);
            PrepareAudio(uid, comp);
        }

        if (string.IsNullOrWhiteSpace(comp.VideoUrl))
        {
            Log.Info($"Cinema Play REJECTED: no video URL set");
            return;
        }

        if (comp.Playing)
            return;

        comp.ServerStartTime = _timing.RealTime;
        comp.Playing = true;
        Dirty(uid, comp);
        PrepareAudio(uid, comp);
        Log.Info($"Cinema Play OK: '{comp.VideoUrl}'");
    }

    private void Pause(EntityUid uid, CinemaScreenComponent comp)
    {
        if (!comp.Playing)
            return;

        comp.PausePosition = CurrentPosition(comp);
        comp.Playing = false;
        Dirty(uid, comp);
    }

    private void Stop(EntityUid uid, CinemaScreenComponent comp)
    {
        comp.Playing = false;
        comp.PausePosition = 0;
        comp.ServerStartTime = default;
        Dirty(uid, comp);
    }

    private void Seek(EntityUid uid, CinemaScreenComponent comp, double seconds)
    {
        comp.PausePosition = Math.Max(0, seconds);

        // Re-anchor the play clock so the seek takes effect immediately for every client.
        if (comp.Playing)
            comp.ServerStartTime = _timing.RealTime;

        Dirty(uid, comp);
    }

    private void SetVolume(EntityUid uid, CinemaScreenComponent comp, float volume)
    {
        comp.Volume = Math.Clamp(volume, 0f, 1f);
        Dirty(uid, comp);
    }

    /// <summary>The position (seconds) the video should currently be at, derived from server time.</summary>
    private double CurrentPosition(CinemaScreenComponent comp)
    {
        if (!comp.Playing)
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

        entity.Comp.Broken = true;
        entity.Comp.Playing = false;
        Dirty(entity.Owner, entity.Comp);
    }

    private bool IsUrlAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // HTTPS by default; http is opt-in for local/dev media servers.
        if (!_cfg.GetCVar(CinemaCVars.AllowHttp) &&
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            return false;

        if (uri.Host.Length == 0)
            return false;

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

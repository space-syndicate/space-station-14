using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server.DynamicHostname;


/// <summary>
/// This handles dynamically updating hostnames.
/// </summary>
public sealed class DynamicHostnameSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameMapManager _mapManager = default!;

    private string OriginalHostname { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        OriginalHostname = _configuration.GetCVar(CVars.GameHostName);
        AttemptUpdateHostname();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev) => AttemptUpdateHostname();
    private void OnRoundStarted(RoundStartedEvent ev) => AttemptUpdateHostname();

    private void OnValueChanged(bool newValue)
    {
        if (!newValue)
            _configuration.SetCVar(CVars.GameHostName, OriginalHostname);

        AttemptUpdateHostname();
    }

    private void AttemptUpdateHostname()
    {
        var currentMapName = _mapManager.GetSelectedMap()?.MapName;
        var currentPresetName = _gameTicker.CurrentPreset?.ModeTitle;

        UpdateHostname(currentMapName, currentPresetName);
    }

    private string GetLocId()
    {
        switch (_gameTicker.RunLevel)
        {
            case GameRunLevel.InRound:
                return "in-round";
            case GameRunLevel.PostRound:
                return "post-round";
            default:
                return "in-lobby";
        }
    }

    private void UpdateHostname(string? currentMapName = null, string? currentPresetName = null)
    {
        var locId = GetLocId();
        var presetName = "No preset";

        if (currentPresetName != null)
            presetName = Loc.GetString(currentPresetName);

        var hostname = Loc.GetString($"dynamic-hostname-{locId}-hostname",
            ("originalHostName", OriginalHostname),
            ("preset", presetName),
            ("mapName", currentMapName ?? "No Map"));

        _configuration.SetCVar(CVars.GameHostName, hostname);
    }
}

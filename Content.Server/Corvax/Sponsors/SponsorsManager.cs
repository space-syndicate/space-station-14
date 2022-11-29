using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Corvax.Sponsors;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;

    private readonly Dictionary<NetUserId, SponsorInfo> _cachedSponsors = new();
    private List<SponsorInfo> _SponsorsList = new();

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _cfg.OnValueChanged(CCVars.SponsorsApiUrl, s => _apiUrl = s, true);

        _netMgr.RegisterNetMessage<MsgSponsorInfo>();
        _netMgr.RegisterNetMessage<MsgSponsorListInfo>();

        _netMgr.Connecting += OnConnecting;
        _netMgr.Connected += OnConnected;
        _netMgr.Disconnect += OnDisconnect;
    }

    public bool TryGetInfo(NetUserId userId, [NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        return _cachedSponsors.TryGetValue(userId, out sponsor);
    }

    private async Task OnConnecting(NetConnectingArgs e)
    {
        var info = await LoadSponsorInfo(e.UserId);
        var sponsors = await _dbManager.GetSponsorList();

        if (sponsors != null)
        {
            foreach (var x in sponsors)
            {
                var userName = await _dbManager.GetPlayerRecordByUserId(new NetUserId(x.UserId));
                _SponsorsList.Add(new SponsorInfo()
                {
                    Tier = x.Tier,
                    AllowedMarkings = x.AllowedMarkings.Split(";",StringSplitOptions.RemoveEmptyEntries),
                    CharacterName = userName != null ? userName.LastSeenUserName : string.Empty,
                    ExtraSlots = x.ExtraSlots,
                    HavePriorityJoin = x.HavePriorityJoin,
                    OOCColor = x.OOCColor
                });
            }
        }

        if (info?.Tier == null)
        {
            _cachedSponsors.Remove(e.UserId); // Remove from cache if sponsor expired
            return;
        }

        DebugTools.Assert(!_cachedSponsors.ContainsKey(e.UserId), "Cached data was found on client connect");

        _cachedSponsors[e.UserId] = info;
    }

    private void OnConnected(object? sender, NetChannelArgs e)
    {
        var info = _cachedSponsors.TryGetValue(e.Channel.UserId, out var sponsor) ? sponsor : null;
        var msg = new MsgSponsorInfo() { Info = info };
        _netMgr.ServerSendMessage(msg, e.Channel);

        var msgList = new MsgSponsorListInfo() { Sponsors = _SponsorsList.ToArray()};
        _netMgr.ServerSendMessage(msgList, e.Channel);

    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedSponsors.Remove(e.Channel.UserId);
    }

    private async Task<SponsorInfo?> LoadSponsorInfo(NetUserId userId)
    {
        if (!string.IsNullOrEmpty(_apiUrl))
        {
            var url = $"{_apiUrl}/sponsors/{userId.ToString()}";
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                _sawmill.Error(
                    "Failed to get player sponsor OOC color from API: [{StatusCode}] {Response}",
                    response.StatusCode,
                    errorText);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SponsorInfo>();
        }
        else
        {
            var sponsorInfo = await _dbManager.GetSponsorInfo(userId);
            if (sponsorInfo != null)
            {
                return new SponsorInfo()
                {
                    Tier = sponsorInfo.Tier,
                    AllowedMarkings = sponsorInfo.AllowedMarkings.Split(";",StringSplitOptions.RemoveEmptyEntries),
                    CharacterName = string.Empty,
                    ExtraSlots = sponsorInfo.ExtraSlots,
                    HavePriorityJoin = sponsorInfo.HavePriorityJoin,
                    OOCColor = sponsorInfo.OOCColor
                };
            }
            else
            {
                return null;
            }
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpExternalApiSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || !_corvaxApi.IsConnected)
            return;

        foreach (var snapshot in _bwoinkAdapter.GetRelaySnapshots())
        {
            var descriptionHash = SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description));
            if (!_seenRelays.TryGetValue(snapshot.UserId, out var seen))
                seen = new RelaySeenState(null, Array.Empty<byte>());

            if (snapshot.RootMessageId != null && snapshot.RootMessageId != seen.RootMessageId)
            {
                _seenRelays[snapshot.UserId] = seen with { RootMessageId = snapshot.RootMessageId };
                _ = SendConversationUpsertAsync(snapshot);
            }

            if (!descriptionHash.SequenceEqual(seen.DescriptionHash))
            {
                var current = _seenRelays.GetValueOrDefault(snapshot.UserId) ?? seen;
                _seenRelays[snapshot.UserId] = current with { DescriptionHash = descriptionHash };
                _ = SendAsync(new AHelpApiOutbound.AHelpMessage(
                    snapshot.UserId.ToString(),
                    snapshot.UserId.ToString(),
                    "transcript",
                    snapshot.Description,
                    DateTimeOffset.UtcNow));
            }
        }
    }

    private async Task OnApiConnectedAsync()
    {
        if (!_enabled)
            return;

        var payloads = await RunOnMainThread(BuildConnectedPayloads);
        foreach (var payload in payloads)
        {
            await SendAsync(payload);
        }
    }

    private void OnApiDisconnected()
    {
        _taskManager.RunOnMainThread(() => _seenRelays.Clear());
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        _ = HandlePlayerStatusChangedAsync(e);
    }

    private async Task HandlePlayerStatusChangedAsync(SessionStatusEventArgs e)
    {
        var snapshot = await RunOnMainThread(() =>
            _seenRelays.ContainsKey(e.Session.UserId)
                ? new PlayerStatusSnapshot(e.Session.UserId, e.Session.Name, e.NewStatus)
                : null);

        if (snapshot == null)
            return;

        var status = snapshot.Status.ToString();
        if (snapshot.Status == SessionStatus.Disconnected &&
            await _dbManager.GetBanAsync(null, snapshot.UserId, null, null) != null)
        {
            status = "Banned";
        }

        _ = SendAsync(new AHelpApiOutbound.PlayerStatus(
            snapshot.UserId.ToString(),
            snapshot.UserId.ToString(),
            snapshot.Ckey,
            status,
            DateTimeOffset.UtcNow));
    }

    private object[] BuildConnectedPayloads()
    {
        _seenRelays.Clear();

        var payloads = new List<object>
        {
            new AHelpApiOutbound.Hello(
                ProtocolVersion,
                _cfg.GetCVar(CVars.GameHostName),
                _gameTicker.RoundId,
                _gameTicker.RunLevel.ToString()),
        };

        foreach (var snapshot in _bwoinkAdapter.GetRelaySnapshots())
        {
            if (snapshot.RootMessageId == null)
                continue;

            _seenRelays[snapshot.UserId] = new RelaySeenState(
                snapshot.RootMessageId,
                SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description)));

            payloads.Add(BuildConversationUpsert(snapshot));

            if (!string.IsNullOrWhiteSpace(snapshot.Description))
            {
                payloads.Add(new AHelpApiOutbound.AHelpMessage(
                    snapshot.UserId.ToString(),
                    snapshot.UserId.ToString(),
                    "transcript",
                    snapshot.Description,
                    DateTimeOffset.UtcNow));
            }
        }

        return payloads.ToArray();
    }

    private AHelpApiOutbound.ConversationUpsert BuildConversationUpsert(AHelpRelaySnapshot snapshot)
    {
        _bwoinkAdapter.TryGetAHelpWebhookChannelId(out var webhookChannelId);
        return new AHelpApiOutbound.ConversationUpsert(
            snapshot.UserId.ToString(),
            snapshot.UserId.ToString(),
            snapshot.Username,
            snapshot.CharacterName,
            snapshot.RootMessageId,
            webhookChannelId == 0 ? null : webhookChannelId,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            snapshot.LastRunLevel.ToString());
    }

    private async Task SendConversationUpsertAsync(AHelpRelaySnapshot snapshot)
    {
        await SendAsync(BuildConversationUpsert(snapshot));
    }

    private sealed record RelaySeenState(ulong? RootMessageId, byte[] DescriptionHash);

    private sealed record PlayerStatusSnapshot(NetUserId UserId, string Ckey, SessionStatus Status);
}

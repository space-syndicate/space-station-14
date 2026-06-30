using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_apiClient.IsConnected)
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
        _seenRelays.Clear();
        await SendHelloAsync();
        await SendCurrentConversationsAsync();
    }

    private void OnApiDisconnected()
    {
        _seenRelays.Clear();
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!_seenRelays.ContainsKey(e.Session.UserId))
            return;

        var status = e.NewStatus.ToString();
        if (e.NewStatus == SessionStatus.Disconnected &&
            await _dbManager.GetBanAsync(null, e.Session.UserId, null, null) != null)
        {
            status = "Banned";
        }

        _ = SendAsync(new AHelpApiOutbound.PlayerStatus(
            e.Session.UserId.ToString(),
            e.Session.UserId.ToString(),
            e.Session.Name,
            status,
            DateTimeOffset.UtcNow));
    }

    private async Task SendHelloAsync()
    {
        await SendAsync(new AHelpApiOutbound.Hello(
            ProtocolVersion,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            _gameTicker.RunLevel.ToString()));
    }

    private async Task SendCurrentConversationsAsync()
    {
        foreach (var snapshot in _bwoinkAdapter.GetRelaySnapshots())
        {
            if (snapshot.RootMessageId == null)
                continue;

            _seenRelays[snapshot.UserId] = new RelaySeenState(
                snapshot.RootMessageId,
                SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description)));

            await SendConversationUpsertAsync(snapshot);

            if (!string.IsNullOrWhiteSpace(snapshot.Description))
            {
                await SendAsync(new AHelpApiOutbound.AHelpMessage(
                    snapshot.UserId.ToString(),
                    snapshot.UserId.ToString(),
                    "transcript",
                    snapshot.Description,
                    DateTimeOffset.UtcNow));
            }
        }
    }

    private async Task SendConversationUpsertAsync(AHelpRelaySnapshot snapshot)
    {
        _bwoinkAdapter.TryGetAHelpWebhookChannelId(out var webhookChannelId);
        await SendAsync(new AHelpApiOutbound.ConversationUpsert(
            snapshot.UserId.ToString(),
            snapshot.UserId.ToString(),
            snapshot.Username,
            snapshot.CharacterName,
            snapshot.RootMessageId,
            webhookChannelId == 0 ? null : webhookChannelId,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            snapshot.LastRunLevel.ToString()));
    }

    private sealed record RelaySeenState(ulong? RootMessageId, byte[] DescriptionHash);
}

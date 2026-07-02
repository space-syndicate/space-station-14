using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration.Systems;
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

        foreach (var snapshot in _bwoinkSystem.CorvaxGetAHelpRelaySnapshots())
        {
            var descriptionHash = SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description));
            if (!_seenRelays.TryGetValue(snapshot.UserId, out var seen))
                seen = new RelaySeenState(null, Array.Empty<byte>(), false);

            var sendUpsert = !seen.ConversationSent || snapshot.RootMessageId != seen.RootMessageId;
            var sendTranscript = !string.IsNullOrWhiteSpace(snapshot.Description) &&
                                 !descriptionHash.SequenceEqual(seen.DescriptionHash);

            if (!sendUpsert && !sendTranscript)
                continue;

            var current = seen with
            {
                RootMessageId = sendUpsert ? snapshot.RootMessageId : seen.RootMessageId,
                DescriptionHash = sendTranscript ? descriptionHash : seen.DescriptionHash,
                ConversationSent = true,
            };
            _seenRelays[snapshot.UserId] = current;

            RunLoggedAsync(SendRelayUpdateAsync(snapshot, sendUpsert, sendTranscript), "send relay update");
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
        RunLoggedAsync(HandlePlayerStatusChangedAsync(e), "handle player status change");
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

        await SendAsync(new AHelpApiOutbound.PlayerStatus(
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

        var sentConversations = new HashSet<NetUserId>();
        foreach (var userId in _knownExternalConversations)
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
                continue;

            _seenRelays[userId] = new RelaySeenState(null, Array.Empty<byte>(), true);
            payloads.Add(BuildConversationUpsert(session));
            sentConversations.Add(userId);
        }

        foreach (var snapshot in _bwoinkSystem.CorvaxGetAHelpRelaySnapshots())
        {
            var conversationAlreadySent = !sentConversations.Add(snapshot.UserId);
            _seenRelays[snapshot.UserId] = new RelaySeenState(
                snapshot.RootMessageId,
                SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description)),
                true);

            if (!conversationAlreadySent || snapshot.RootMessageId != null)
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

    private AHelpApiOutbound.ConversationUpsert BuildConversationUpsert(CorvaxAHelpRelaySnapshot snapshot)
    {
        var webhookChannelId = _bwoinkSystem.CorvaxGetAHelpWebhookChannelId();
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

    private AHelpApiOutbound.ConversationUpsert BuildConversationUpsert(ICommonSession session)
    {
        var webhookChannelId = _bwoinkSystem.CorvaxGetAHelpWebhookChannelId();
        return new AHelpApiOutbound.ConversationUpsert(
            session.UserId.ToString(),
            session.UserId.ToString(),
            session.Name,
            _minds.GetCharacterName(session.UserId),
            null,
            webhookChannelId == 0 ? null : webhookChannelId,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            _gameTicker.RunLevel.ToString());
    }

    private async Task SendRelayUpdateAsync(CorvaxAHelpRelaySnapshot snapshot, bool sendUpsert, bool sendTranscript)
    {
        if (sendUpsert)
            await SendAsync(BuildConversationUpsert(snapshot));

        if (!sendTranscript)
            return;

        await SendAsync(new AHelpApiOutbound.AHelpMessage(
            snapshot.UserId.ToString(),
            snapshot.UserId.ToString(),
            "transcript",
            snapshot.Description,
            DateTimeOffset.UtcNow));
    }

    private sealed record RelaySeenState(ulong? RootMessageId, byte[] DescriptionHash, bool ConversationSent);

    private sealed record PlayerStatusSnapshot(NetUserId UserId, string Ckey, SessionStatus Status);
}

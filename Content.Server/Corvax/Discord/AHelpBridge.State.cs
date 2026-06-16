using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpDiscordThreadBridgeSystem
{
    private readonly object _stateLock = new();
    private readonly Dictionary<NetUserId, ulong> _userThreads = new();
    private readonly Dictionary<ulong, NetUserId> _threadUsers = new();
    private readonly HashSet<ulong> _createdRootMessages = new();
    private readonly Dictionary<NetUserId, PendingThreadRequest> _pendingThreadRequests = new();
    private readonly HashSet<NetUserId> _creatingThreads = new();

    private void ClearState()
    {
        lock (_stateLock)
        {
            _userThreads.Clear();
            _threadUsers.Clear();
            _createdRootMessages.Clear();
            _pendingThreadRequests.Clear();
            _creatingThreads.Clear();
        }
    }

    private bool TryGetThreadForUser(NetUserId userId, out ulong threadId)
    {
        lock (_stateLock)
        {
            return _userThreads.TryGetValue(userId, out threadId);
        }
    }

    private bool TryGetUserForThread(ulong threadId, out NetUserId userId)
    {
        lock (_stateLock)
        {
            return _threadUsers.TryGetValue(threadId, out userId);
        }
    }

    private bool IsRootMessageProcessed(ulong rootMessageId)
    {
        lock (_stateLock)
        {
            return _createdRootMessages.Contains(rootMessageId);
        }
    }

    private void MarkRootMessageProcessed(ulong rootMessageId)
    {
        lock (_stateLock)
        {
            _createdRootMessages.Add(rootMessageId);
        }
    }

    private void RegisterPendingThreadRequest(ICommonSession target, string relayUsername)
    {
        lock (_stateLock)
        {
            RemoveExpiredPendingThreadRequests();
            _pendingThreadRequests[target.UserId] = new PendingThreadRequest(
                target.UserId,
                target.Name,
                relayUsername,
                _minds.GetCharacterName(target.UserId),
                DateTime.UtcNow);
        }
    }

    private bool TryTakePendingThreadRequest(string authorUsername, [NotNullWhen(true)] out PendingThreadRequest? request)
    {
        lock (_stateLock)
        {
            RemoveExpiredPendingThreadRequests();

            var exactMatch = _pendingThreadRequests.Values
                .FirstOrDefault(pending => string.Equals(pending.RelayUsername, authorUsername, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                _pendingThreadRequests.Remove(exactMatch.UserId);
                request = exactMatch;
                return true;
            }

            if (_pendingThreadRequests.Count == 1)
            {
                var pending = _pendingThreadRequests.Values.First();
                _pendingThreadRequests.Remove(pending.UserId);
                request = pending;
                return true;
            }
        }

        request = null;
        return false;
    }

    private void RemoveExpiredPendingThreadRequests()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        foreach (var pending in _pendingThreadRequests.Values.Where(pending => pending.CreatedAt < cutoff).ToArray())
        {
            _pendingThreadRequests.Remove(pending.UserId);
        }
    }

    private bool TryBeginThreadCreation(NetUserId userId)
    {
        lock (_stateLock)
        {
            if (_userThreads.ContainsKey(userId) || _creatingThreads.Contains(userId))
                return false;

            _creatingThreads.Add(userId);
            return true;
        }
    }

    private void EndThreadCreation(NetUserId userId)
    {
        lock (_stateLock)
        {
            _creatingThreads.Remove(userId);
        }
    }

    private sealed record PendingThreadRequest(
        NetUserId UserId,
        string Username,
        string RelayUsername,
        string? CharacterName,
        DateTime CreatedAt);
}

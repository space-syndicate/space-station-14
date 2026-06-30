using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetCord;
using NetCord.Gateway;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpDiscordThreadBridgeSystem
{
    private async Task<bool> TryHandleThreadCommandAsync(Message message)
    {
        var content = message.Content.Trim();
        if (!content.StartsWith('!'))
            return false;

        if (content.Equals("!ckey", StringComparison.OrdinalIgnoreCase))
        {
            await SendPlayerListAsync(message.ChannelId);
            return true;
        }

        if (content.StartsWith("!ah ", StringComparison.OrdinalIgnoreCase))
        {
            var args = content["!ah ".Length..].Trim();
            await HandleOpenAHelpCommandAsync(message.ChannelId, message, args);
            return true;
        }

        return false;
    }

    private async Task SendPlayerListAsync(ulong channelId)
    {
        var sessions = _playerManager.Sessions
            .OrderBy(session => session.Name)
            .ToArray();

        if (sessions.Length == 0)
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, "На сервере нет подключенных игроков.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("```");
        builder.AppendLine("ckey | статус | персонаж | должность | роли | антаг");

        foreach (var session in sessions)
        {
            var characterName = _minds.GetCharacterName(session.UserId) ?? "-";
            var job = "-";
            var roleNames = "-";
            var antagonist = "нет";

            if (_minds.TryGetMind(session.UserId, out var mind))
            {
                var mindEntity = mind.Value;
                var roles = _roles.MindGetAllRoleInfo((mindEntity.Owner, mindEntity.Comp));
                var jobRole = roles.FirstOrDefault(role => !role.Antagonist);
                var namedRoles = roles
                    .Select(role => Loc.GetString(role.Name))
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .ToArray();

                if (!string.IsNullOrWhiteSpace(jobRole.Name))
                    job = Loc.GetString(jobRole.Name);

                if (namedRoles.Length != 0)
                    roleNames = string.Join(", ", namedRoles);

                antagonist = roles.Any(role => role.Antagonist) ? "да" : "нет";
            }

            builder.AppendLine($"{session.Name} | {GetSessionStatusName(session.Status)} | {characterName} | {job} | {roleNames} | {antagonist}");
        }

        builder.AppendLine("```");

        foreach (var chunk in SplitDiscordMessage(builder.ToString()))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, chunk);
        }
    }

    private async Task HandleOpenAHelpCommandAsync(ulong channelId, Message message, string args)
    {
        if (!TryParseAHelpCommand(args, out var ckey, out var ahelpText))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, "Использование: `!ah ckey сообщение`");
            return;
        }

        if (!TryGetSessionByCkey(ckey, out var target))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, $"Игрок `{ckey}` не найден на сервере.");
            return;
        }

        var authorName = await GetDiscordAuthorNameAsync(message);
        var relayName = AHelpDiscordRelayHelper.GetDiscordRelayName(authorName);
        var plainText = ahelpText.ReplaceLineEndings(" ");
        var bwoinkText = AHelpDiscordRelayHelper.BuildDiscordBwoinkText(authorName, plainText);

        _relayService.SendAHelpToGame(target.UserId, bwoinkText);
        RegisterPendingThreadRequest(target, relayName);
        _relayService.QueueWebhookMessage(
            target.UserId,
            relayName,
            plainText,
            isAdmin: true);

        _ = EnsureThreadForUserFromRelayAsync(target.UserId);

        await SendDiscordThreadWebhookMessageAsync(channelId, $"AH для `{target.Name}` отправлен");
    }

    private static bool TryParseAHelpCommand(string args, out string ckey, out string message)
    {
        ckey = string.Empty;
        message = string.Empty;

        var split = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
            return false;

        ckey = split[0];
        message = split[1];
        return true;
    }

    private static string GetSessionStatusName(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.InGame => "в игре",
            SessionStatus.Connected => "подключен",
            SessionStatus.Disconnected => "отключен",
            _ => status.ToString(),
        };
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        if (_playerManager.TryGetSessionByUsername(ckey, out session))
            return true;

        session = _playerManager.Sessions.FirstOrDefault(player =>
            string.Equals(player.Name, ckey, StringComparison.OrdinalIgnoreCase));

        return session != null;
    }
}

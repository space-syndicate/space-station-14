using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetCord;
using NetCord.Gateway;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpDiscordThreadBridgeSystem
{
    private async Task<bool> TryHandleThreadCommandAsync(Message message, string authorName)
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
            await HandleOpenAHelpCommandAsync(message.ChannelId, authorName, args);
            return true;
        }

        return false;
    }

    private async Task SendPlayerListAsync(ulong channelId)
    {
        var chunks = await _taskManager.RunOnMainThreadAsync(BuildPlayerListChunks);

        foreach (var chunk in chunks)
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, chunk);
        }
    }

    private async Task HandleOpenAHelpCommandAsync(ulong channelId, string authorName, string args)
    {
        if (!TryParseAHelpCommand(args, out var ckey, out var ahelpText))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, "Использование: `!ah ckey сообщение`");
            return;
        }

        var relayName = AHelpDiscordRelayHelper.GetDiscordRelayName(authorName);
        var plainText = ahelpText.ReplaceLineEndings(" ");
        var result = await _taskManager.RunOnMainThreadAsync(() =>
        {
            if (!TryGetSessionByCkey(ckey, out var target))
                return OpenAHelpCommandResult.NotFound();

            var bwoinkText = AHelpDiscordRelayHelper.BuildDiscordBwoinkText(authorName, plainText);
            _relayService.SendAHelpToGame(target.UserId, bwoinkText);
            RegisterPendingThreadRequest(target, relayName);
            _relayService.QueueWebhookMessage(
                target.UserId,
                relayName,
                plainText,
                isAdmin: true);

            return OpenAHelpCommandResult.Sent(target.UserId, target.Name);
        });

        if (result.UserId == null)
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, $"Игрок `{ckey}` не найден на сервере.");
            return;
        }

        _ = EnsureThreadForUserFromRelayAsync(result.UserId.Value);

        await SendDiscordThreadWebhookMessageAsync(channelId, $"AH для `{result.Ckey}` отправлен");
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

    private string[] BuildPlayerListChunks()
    {
        var sessions = _playerManager.Sessions
            .OrderBy(session => session.Name)
            .ToArray();

        if (sessions.Length == 0)
            return new[] { "На сервере нет подключенных игроков." };

        var builder = new StringBuilder();
        builder.AppendLine("```");
        builder.AppendLine("ckey | статус | персонаж | должность | роли | антаг");

        foreach (var session in sessions)
        {
            var info = AHelpPlayerInfoHelper.BuildPlayerInfo(session, _minds, _roles);
            var characterName = info.CharacterName ?? "-";
            var roleNames = info.Roles.Length == 0 ? "-" : string.Join(", ", info.Roles);
            var antagonist = info.Antagonist ? "да" : "нет";
            builder.AppendLine($"{info.Ckey} | {AHelpPlayerInfoHelper.GetLocalizedStatusName(info.Status)} | {characterName} | {info.Job} | {roleNames} | {antagonist}");
        }

        builder.AppendLine("```");
        return SplitDiscordMessage(builder.ToString()).ToArray();
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        return AHelpPlayerInfoHelper.TryGetSessionByCkey(_playerManager, ckey, out session);
    }

    private sealed record OpenAHelpCommandResult(NetUserId? UserId, string Ckey)
    {
        public static OpenAHelpCommandResult NotFound()
        {
            return new OpenAHelpCommandResult(null, string.Empty);
        }

        public static OpenAHelpCommandResult Sent(NetUserId userId, string ckey)
        {
            return new OpenAHelpCommandResult(userId, ckey);
        }
    }
}

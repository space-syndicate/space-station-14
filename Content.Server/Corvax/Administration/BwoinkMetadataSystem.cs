using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Administration.Managers;
using Content.Server.Preferences.Managers;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Administration;

public sealed class BwoinkMetadataSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

    private static readonly Regex AdminColorRegex = new(@"\[color=(red|purple)\]", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformBwoinkTextEvent>(OnTransformBwoinkText);
    }

    private void OnTransformBwoinkText(ref TransformBwoinkTextEvent ev)
    {
        var adminData = _adminManager.GetAdminData(ev.SenderSession);

        if (adminData is not null)
        {
            var prefix = GetAdminPrefix(ev.Text, adminData);
            if (prefix != null)
                ev.Text = InsertPrefix(ev.Text, ev.SenderSession.Name, prefix);

            if (_adminManager.HasAdminFlag(ev.SenderSession, AdminFlags.NameColor))
            {
                var prefs = _preferencesManager.GetPreferences(ev.SenderSession.UserId);
                var hex = prefs.AdminOOCColor.ToHex();
                ev.Text = AdminColorRegex.Replace(ev.Text, $"[color={hex}]");
            }
        }
        else
        {
            var prefix = GetAntagPrefix(ev.SenderSession);
            if (prefix != null)
                ev.Text = InsertPrefix(ev.Text, ev.SenderSession.Name, prefix);
        }
    }

    private string? GetAdminPrefix(string text, AdminData adminData)
    {
        if (adminData.Title is not { } title)
            return null;

        var prefix = $"[bold]\\[{title}\\][/bold]";
        if (text.Contains($"\\[{title}\\]"))
            return null;

        return prefix;
    }

    private string? GetAntagPrefix(ICommonSession session)
    {
        var mindId = _mind.GetMind(session.UserId);
        if (mindId is not { } mind)
            return null;

        var roles = _role.MindGetAllRoleInfo(mind);
        var antagRoles = roles.Where(r => r.Antagonist).ToList();

        if (antagRoles.Count == 0)
            return null;

        var color = AntagPrototype.GroupColor.ToHex();
        var names = string.Join(", ", antagRoles.Select(r => Loc.GetString(r.Name)));
        return $"[color={color}]\\[{names}\\][/color]";
    }

    private static string InsertPrefix(string text, string playerName, string prefix)
    {
        return text.Replace(playerName, $"{prefix} {playerName}");
    }
}

[ByRefEvent]
public struct TransformBwoinkTextEvent
{
    public string Text;
    public ICommonSession SenderSession;

    public TransformBwoinkTextEvent(string text, ICommonSession senderSession)
    {
        Text = text;
        SenderSession = senderSession;
    }
}
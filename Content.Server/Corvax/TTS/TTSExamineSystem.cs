using Content.Server.VoiceMask;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.TTS;

public sealed class DetailExaminableSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] protected readonly IConfigurationManager _configManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TTSComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, TTSComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        // Don't show verb if no TTS enabled
        if (!_configManager.GetCVar(CCCVars.TTSEnabled))
            return;

        string? voiceId = string.Empty;

        // If user is wearing a voice mask, we will take its voice
        if (TryComp<VoiceMaskComponent>(uid, out var voiceMask))
            voiceId = voiceMask.VoiceId;
        else
            voiceId = component.VoicePrototypeId;

        // Get the voice name
        string voiceName = string.Empty;
        if (_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId ?? string.Empty, out var protoVoice))
        {
            voiceName = Loc.GetString(protoVoice.Name);
        }

        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkup(Loc.GetString("tts-examine-voice", ("name", voiceName)));
                _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
            },
            Text = Loc.GetString("tts-examine"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("tts-examine-disabled"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/Emotes/vocal.png"))
        };

        args.Verbs.Add(verb);
    }
}

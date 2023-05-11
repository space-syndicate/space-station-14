using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.Sirena;
using Robust.Shared.Utility;
using Robust.Shared.Configuration;
using Content.Shared.ADT.ACCVars;

namespace Content.Server.DetailExaminable
{
    public sealed class DetailExaminableSystem : EntitySystem
    {
        [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DetailExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        }

        private void OnGetExamineVerbs(EntityUid uid, DetailExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            // TODO: Hide if identity isn't visible (when identity is merged)
            var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

            var verb = new ExamineVerb()
            {
                Act = () =>
                {
                    var markup = new FormattedMessage();
                    markup.AddMarkup(component.Content);
                    if (_configurationManager.GetCVar(ACCVars.IsERP))
                    {
                        // Sirena-ERPStatus-Start
                        if (component.ERPStatus == EnumERPStatus.FULL)
                            markup.PushColor(Color.Green);
                        else if (component.ERPStatus == EnumERPStatus.HALF)
                            markup.PushColor(Color.Yellow);
                        else
                            markup.PushColor(Color.Red);
                        markup.AddMarkup("\n" + component.GetERPStatusName());
                        // Sirena-ERPStatus-End
                    }

                    _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
                },
                Text = Loc.GetString("detail-examinable-verb-text"),
                Category = VerbCategory.Examine,
                Disabled = !detailsRange,
                Message = Loc.GetString("detail-examinable-verb-disabled"),
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
            };

            args.Verbs.Add(verb);
        }
    }
}

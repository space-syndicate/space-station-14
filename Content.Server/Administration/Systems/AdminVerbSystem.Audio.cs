using System.Linq;
using Content.Server.Corvax.Audio.UI;
using Content.Shared.Administration;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Administration.Systems;

/// <remarks>
///  CorvaxGoob JukeboxControls
/// </remarks>>
public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private void AddAudioVerbs(GetVerbsEvent<Verb> args)
    {

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!_adminManager.HasAdminFlag(args.User, AdminFlags.Admin))
            return;

        if (!_entityManager.TryGetComponent<JukeboxComponent>(args.Target, out var jukebox))
        {
            return;
        }

        Verb manageAudio = new Verb()
        {
            Text = Loc.GetString("admin-verb-manage-audio"),
            Category = VerbCategory.Admin,
            Act = () =>
            {
                _euiManager.OpenEui(new AudioControlsEui(args.Target), actor.PlayerSession);
            }
        };
        args.Verbs.Add(manageAudio);
    }

}

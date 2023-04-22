using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Server.Players;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Administration.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    sealed class FixPlayerCommand : IConsoleCommand
    {

        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        public string Command => "fixplayerchat";

        public string Description => Loc.GetString("set-fix-player-chat-description", ("requiredComponent", nameof(MindComponent)));

        public string Help => Loc.GetString("set-fix-player-chat-help-text", ("command", Command));



        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!IoCManager.Resolve<IPlayerManager>().TryGetSessionByUsername(args[0], out var session))
            {
                shell.WriteLine(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }

            if (session?.AttachedEntity == null)
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            var eUid = session.AttachedEntity.Value;

            if (!eUid.IsValid() || !_entityManager.EntityExists(eUid))
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            if (!_entityManager.HasComponent<MindComponent>(eUid) || !_entityManager.HasComponent<ActorComponent>(eUid))
            {
                shell.WriteLine(Loc.GetString("set-mind-command-target-has-no-mind-message"));
                return;
            }

            _entityManager.RemoveComponent<ActorComponent>(eUid);
            _entityManager.RemoveComponent<MindComponent>(eUid);

            // hm, does player have a mind? if not we may need to give them one
            var playerCData = session.ContentData();
            if (playerCData == null)
            {
                shell.WriteLine(Loc.GetString("set-mind-command-target-has-no-content-data-message"));
                return;
            }

            var mind = playerCData.Mind;

            if(mind == null){
                mind = new Mind.Mind(session.UserId)
                {
                    CharacterName = _entityManager.GetComponent<MetaDataComponent>(eUid).EntityName
                };
                mind.ChangeOwningPlayer(session.UserId);
            }

            //mind.TransferTo(null);
            Timer.Spawn(1_000, ()=>{
                if(eUid.IsValid() && _entityManager.HasComponent<MetaDataComponent>(eUid)){
                    mind.TransferTo(eUid);
                }
            });
            _adminLogger.Add(LogType.Mind, LogImpact.High, $"{(shell.Player != null ? shell.Player.Name : "An administrator")} fixplayerchat {_entityManager.ToPrettyString(eUid)}");
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args){
            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                    Loc.GetString("cmd-fixplayerchat-hint-1")),
                _ => CompletionResult.Empty
            };
        }
    }
}

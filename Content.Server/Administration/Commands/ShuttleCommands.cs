using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Content.Shared.Localizations;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Round)]
    public sealed partial class CallShuttleCommand : LocalizedEntityCommands
    {
        [Dependency] private RoundEndSystem _roundEndSystem = default!;

        public override string Command => "callshuttle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            bool cantRecall = false; //Corvax-Start

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (args.Length >= 1 && TimeSpan.TryParseExact(args[0], ContentLocalizationManager.TimeSpanMinutesFormats, LocalizationManager.DefaultCulture, out var timeSpan))
            {
                if (args.Length >= 2 && !bool.TryParse(args[1], out cantRecall))
                    return;
            
                _roundEndSystem.RequestRoundEnd(timeSpan, shell.Player?.AttachedEntity, checkCooldown: false, cantRecall: cantRecall);
            }

            else if (args.Length >= 1) // Corvax-End
                shell.WriteLine(Loc.GetString("shell-timespan-minutes-must-be-correct"));

            else
                _roundEndSystem.RequestRoundEnd(shell.Player?.AttachedEntity, checkCooldown: false);
        }

        // Corvax-Start
        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) 
        {
            return args.Length switch
            {
                2 => CompletionResult.FromHintOptions(["false", "true"], Loc.GetString("[bool]")),
                _ => CompletionResult.Empty,
            };
        }
        // Corvax-End
    }

    [AdminCommand(AdminFlags.Round)]
    public sealed partial class RecallShuttleCommand : LocalizedEntityCommands
    {
        [Dependency] private RoundEndSystem _roundEndSystem = default!;

        public override string Command => "recallshuttle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            _roundEndSystem.CancelRoundEndCountdown(shell.Player?.AttachedEntity, forceRecall: true);
        }
    }
}

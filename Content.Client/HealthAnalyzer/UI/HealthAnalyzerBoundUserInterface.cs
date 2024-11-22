using Content.Shared.MedicalScanner;
using Content.Shared._CorvaxNext.Targeting;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.HealthAnalyzer.UI
{
    [UsedImplicitly]
    public sealed class HealthAnalyzerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private HealthAnalyzerWindow? _window;

        public HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = new HealthAnalyzerWindow
            {
                Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName,
            };
            _window.OnClose += Close;
            _window.OnBodyPartSelected += SendBodyPartMessage;
            _window.OpenCentered();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            if (message is not HealthAnalyzerScannedUserMessage cast)
                return;

            _window.Populate(cast);
        }

        private void SendBodyPartMessage(TargetBodyPart? part, EntityUid target) => SendMessage(new HealthAnalyzerPartMessage(EntMan.GetNetEntity(target), part ?? null));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_window != null)
            {
                _window.OnClose -= Close;
                _window.OnBodyPartSelected -= SendBodyPartMessage;
            }

            _window?.Dispose();
        }
    }
}

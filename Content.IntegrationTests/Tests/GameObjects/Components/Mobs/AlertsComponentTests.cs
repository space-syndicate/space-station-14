using System.Linq;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared.Alert;
using Content.Shared.Mobs; // Corvax-species-without-classic-HumanHealth
using Content.Shared.Mobs.Components; // Corvax-species-without-classic-HumanHealth
using Robust.Client.UserInterface;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates; // Corvax-species-without-classic-HumanHealth

namespace Content.IntegrationTests.Tests.GameObjects.Components.Mobs
{
    [TestFixture]
    [TestOf(typeof(AlertsComponent))]
    public sealed class AlertsComponentTests
    {
        [Test]
        public async Task AlertsTest()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings
            {
                Connected = true,
                DummyTicker = false
            });
            var server = pair.Server;
            var client = pair.Client;

            var clientUIMgr = client.ResolveDependency<IUserInterfaceManager>();
            var clientEntManager = client.ResolveDependency<IEntityManager>();

            var entManager = server.ResolveDependency<IEntityManager>();
            var serverPlayerManager = server.ResolveDependency<IPlayerManager>();
            var alertsSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AlertsSystem>();

            EntityUid playerUid = default;
            await server.WaitAssertion(() =>
            {
                playerUid = serverPlayerManager.Sessions.Single().AttachedEntity.GetValueOrDefault();
#pragma warning disable NUnit2045 // Interdependent assertions.
                Assert.That(playerUid, Is.Not.EqualTo(default(EntityUid)));
                // Making sure it exists
                Assert.That(entManager.HasComponent<AlertsComponent>(playerUid));
#pragma warning restore NUnit2045

                var alerts = alertsSystem.GetActiveAlerts(playerUid);
                Assert.That(alerts, Is.Not.Null);
                var alertCount = alerts.Count;

                alertsSystem.ShowAlert(playerUid, "Debug1");
                alertsSystem.ShowAlert(playerUid, "Debug2");

                Assert.That(alerts, Has.Count.EqualTo(alertCount + 2));
            });

            await pair.RunTicksSync(5);

            AlertsUI clientAlertsUI = default;
            await client.WaitAssertion(() =>
            {
                var local = client.Session;
                Assert.That(local, Is.Not.Null);
                var controlled = local.AttachedEntity;
#pragma warning disable NUnit2045 // Interdependent assertions.
                Assert.That(controlled, Is.Not.Null);
                // Making sure it exists
                Assert.That(clientEntManager.HasComponent<AlertsComponent>(controlled.Value));
#pragma warning restore Nunit2045

                // find the alertsui

                clientAlertsUI = FindAlertsUI(clientUIMgr.ActiveScreen);
                Assert.That(clientAlertsUI, Is.Not.Null);

                static AlertsUI FindAlertsUI(Control control)
                {
                    if (control is AlertsUI alertUI)
                        return alertUI;
                    foreach (var child in control.Children)
                    {
                        var found = FindAlertsUI(child);
                        if (found != null)
                            return found;
                    }

                    return null;
                }

                // we should be seeing 3 alerts - our health, and the 2 debug alerts, in a specific order.
                Assert.That(clientAlertsUI.AlertContainer.ChildCount, Is.GreaterThanOrEqualTo(3));
                var alertControls = clientAlertsUI.AlertContainer.Children.Select(c => (AlertControl) c);
                var alertIDs = alertControls.Select(ac => ac.Alert.ID).ToArray();
                // Corvax-species-without-classic-HumanHealth-start
                var healthAlert = "NoHealthAlertFinded"; // if species doesn't have health alert
                if (clientEntManager.TryGetComponent<MobThresholdsComponent>(controlled.Value, out var thresholds))
                {
                    var getStateEvent = new ComponentGetState();
                    clientEntManager.EventBus.RaiseComponentEvent(controlled.Value, thresholds, ref getStateEvent);

                    if (getStateEvent.State is MobThresholdsComponentState state)
                    {
                        if (state.StateAlertDict.TryGetValue(MobState.Alive, out var aliveAlert))
                        {
                            healthAlert = aliveAlert.Id;
                        }
                    }
                }
                var expectedIDs = new[] { healthAlert, "Debug1", "Debug2" }; // "HumanHealth" -> healthAlert
                // Corvax-species-without-classic-HumanHealth-end
                Assert.That(alertIDs, Is.SupersetOf(expectedIDs));
            });

            await server.WaitAssertion(() =>
            {
                alertsSystem.ClearAlert(playerUid, "Debug1");
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                // we should be seeing 2 alerts now because one was cleared
                Assert.That(clientAlertsUI.AlertContainer.ChildCount, Is.GreaterThanOrEqualTo(2));
                var alertControls = clientAlertsUI.AlertContainer.Children.Select(c => (AlertControl) c);
                var alertIDs = alertControls.Select(ac => ac.Alert.ID).ToArray();
                // Corvax-species-without-classic-HumanHealth-start
                var healthAlert = "NoHealthAlertFinded"; // if species doesn't have health alert
                if (clientEntManager.TryGetComponent<MobThresholdsComponent>(client.Session.AttachedEntity.Value, out var thresholds))
                {
                    var getStateEvent = new ComponentGetState();
                    clientEntManager.EventBus.RaiseComponentEvent(client.Session.AttachedEntity.Value, thresholds, ref getStateEvent);

                    if (getStateEvent.State is MobThresholdsComponentState state)
                    {
                        if (state.StateAlertDict.TryGetValue(MobState.Alive, out var aliveAlert))
                        {
                            healthAlert = aliveAlert.Id;
                        }
                    }
                }
                var expectedIDs = new[] { healthAlert, "Debug2" }; // "HumanHealth" -> healthAlert
                // Corvax-species-without-classic-HumanHealth-end
                Assert.That(alertIDs, Is.SupersetOf(expectedIDs));
            });

            await pair.CleanReturnAsync();
        }
    }
}

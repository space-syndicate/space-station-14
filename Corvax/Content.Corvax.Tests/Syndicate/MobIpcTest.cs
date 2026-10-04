using Content.IntegrationTests.Fixtures;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Sound.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Corvax.Tests.Syndicate;

[TestFixture]
public sealed class MobIpcTest : GameTest
{
    [Test]
    public async Task EmpPulseDamagesIpc()
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var damageableSystem = server.System<DamageableSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var uid = entManager.SpawnEntity("MobIpc", coords);
            entManager.EnsureComponent<IpcComponent>(uid);

            var before = damageableSystem.GetTotalDamage(uid);

            var ev = new EmpPulseEvent();
            server.EntMan.EventBus.RaiseLocalEvent(uid, ref ev);

            var after = damageableSystem.GetTotalDamage(uid);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Affected, Is.True, "IPC must be marked as affected by an EMP pulse.");
                Assert.That(after, Is.GreaterThan(before), "IPC must take Shock damage from an EMP pulse.");
            });

            entManager.DeleteEntity(uid);
            mapSystem.DeleteMap(mapId);
        });
    }

    [Test]
    public async Task MobStateCriticalAddsWarningSound()
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var uid = entManager.SpawnEntity("MobIpc", coords);
            entManager.EnsureComponent<IpcComponent>(uid);
            var mobState = entManager.EnsureComponent<MobStateComponent>(uid);

            Assert.That(entManager.HasComponent<SpamEmitSoundComponent>(uid), Is.False,
                "Setup sanity: no warning sound before entering Critical.");

            var ev = new MobStateChangedEvent(uid, mobState, MobState.Alive, MobState.Critical);
            server.EntMan.EventBus.RaiseLocalEvent(uid, ev);

            Assert.That(entManager.HasComponent<SpamEmitSoundComponent>(uid), Is.True,
                "IPC must emit a warning sound when entering Critical state.");

            entManager.DeleteEntity(uid);
            mapSystem.DeleteMap(mapId);
        });
    }

    [Test]
    public async Task MobStateAliveRemovesWarningSound()
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var uid = entManager.SpawnEntity("MobIpc", coords);
            entManager.EnsureComponent<IpcComponent>(uid);
            var mobState = entManager.EnsureComponent<MobStateComponent>(uid);
            entManager.EnsureComponent<SpamEmitSoundComponent>(uid);

            var ev = new MobStateChangedEvent(uid, mobState, MobState.Critical, MobState.Alive);
            server.EntMan.EventBus.RaiseLocalEvent(uid, ev);

            Assert.That(entManager.HasComponent<SpamEmitSoundComponent>(uid), Is.False,
                "IPC must stop the warning sound when leaving Critical state.");

            entManager.DeleteEntity(uid);
            mapSystem.DeleteMap(mapId);
        });
    }
}

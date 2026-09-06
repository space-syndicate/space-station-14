using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Content.Server.Administration.Managers;
using Content.Shared.Corvax.Cinema;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Corvax;

[TestFixture]
public sealed class CinemaTests
{
    [Test]
    public async Task PlaybackControls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var admins = server.ResolveDependency<IAdminManager>();

        await server.WaitAssertion(() =>
        {
            var extraction = config.GetCVar(CinemaCVars.AudioExtractionEnabled);
            var allowHttp = config.GetCVar(CinemaCVars.AllowHttp);
            var whitelist = config.GetCVar(CinemaCVars.WhitelistEnabled);
            var attached = pair.Player!.AttachedEntity;
            Assert.That(admins.IsAdmin(pair.Player), Is.True);
            config.SetCVar(CinemaCVars.AudioExtractionEnabled, false);
            var screen = entities.SpawnEntity("CinemaScreen", MapCoordinates.Nullspace);
            var actor = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            server.PlayerMan.SetAttachedEntity(pair.Player!, actor);
            var comp = entities.GetComponent<CinemaScreenComponent>(screen);

            void Send(CinemaScreenAction action, string url = "", double seek = 0, float volume = 1, string film = "")
            {
                entities.EventBus.RaiseLocalEvent(screen, new CinemaScreenControlMessage
                {
                    Actor = actor,
                    Action = action,
                    Film = film,
                    Url = url,
                    SeekSeconds = seek,
                    Volume = volume,
                });
            }

            const string first = "https://media.w3.org/first.webm";
            const string second = "https://media.w3.org/second.mp4";
            Send(CinemaScreenAction.Play, first);
            Assert.That(comp.Playing, Is.True);
            Send(CinemaScreenAction.Seek, seek: 42);
            Send(CinemaScreenAction.Pause);
            Send(CinemaScreenAction.Play, first);
            Assert.That(comp.PausePosition, Is.EqualTo(42).Within(0.1), "Play must resume the same URL.");
            Send(CinemaScreenAction.Play, second);
            Assert.That(comp.VideoUrl, Is.EqualTo(second));
            Assert.That(comp.PausePosition, Is.Zero, "A different film must start at zero.");
            Assert.That(comp.Playing, Is.True);
            Send(CinemaScreenAction.Seek, seek: double.NaN);
            Send(CinemaScreenAction.SetVolume, volume: float.PositiveInfinity);
            Assert.That(comp.PausePosition, Is.Zero);
            Assert.That(comp.Volume, Is.EqualTo(1));

            foreach (var url in new[]
                     {
                         "https://www.youtube.com/watch?v=123456",
                         "https://media.w3.org/index.html",
                         "https://media.w3.org.evil.example/film.webm",
                         "http://media.w3.org/film.webm",
                     })
            {
                Send(CinemaScreenAction.SetUrl, url);
                Assert.That(comp.VideoUrl, Is.EqualTo(second), url);
            }

            config.SetCVar(CinemaCVars.AllowHttp, true);
            config.SetCVar(CinemaCVars.WhitelistEnabled, false);
            Send(CinemaScreenAction.SetUrl, "ftp://example.org/film.webm");
            Assert.That(comp.VideoUrl, Is.EqualTo(second), "AllowHttp must not allow other schemes.");
            Send(CinemaScreenAction.SetUrl, "http://example.org/film.webm");
            Assert.That(comp.VideoUrl, Is.EqualTo("http://example.org/film.webm"));
            Send(CinemaScreenAction.Play);
            Send(CinemaScreenAction.Stop);
            Assert.That(comp.Playing, Is.False);
            Assert.That(comp.PausePosition, Is.Zero);

            admins.DeAdmin(pair.Player!);
            Send(CinemaScreenAction.Play, first);
            Assert.That(comp.Playing, Is.False, "Non-admins must not control playback.");
            Assert.That(comp.Films.Count, Is.EqualTo(10));
            var listed = comp.Films.First();
            Send(CinemaScreenAction.SelectFilm, film: listed.Key);
            Assert.That(comp.Playing, Is.True, "Players can start a film from the prototype.");
            Assert.That(comp.VideoUrl, Is.EqualTo(listed.Value));
            Send(CinemaScreenAction.SelectFilm, film: "unlisted-film");
            Assert.That(comp.VideoUrl, Is.EqualTo(listed.Value), "An unknown film ID must be rejected.");
            Send(CinemaScreenAction.SetUrl, first);
            Assert.That(comp.VideoUrl, Is.EqualTo(listed.Value), "Players must not set custom URLs.");
            Send(CinemaScreenAction.Pause);
            Assert.That(comp.Playing, Is.False);
            Send(CinemaScreenAction.Play);
            Assert.That(comp.Playing, Is.True, "Players can resume without submitting a URL.");
            admins.ReAdmin(pair.Player!);
            Send(CinemaScreenAction.Play, first);
            Assert.That(comp.VideoUrl, Is.EqualTo(first), "Admins retain custom URL playback.");
            server.PlayerMan.SetAttachedEntity(pair.Player!, attached);
            config.SetCVar(CinemaCVars.AudioExtractionEnabled, extraction);
            config.SetCVar(CinemaCVars.AllowHttp, allowHttp);
            config.SetCVar(CinemaCVars.WhitelistEnabled, whitelist);
            entities.DeleteEntity(actor);
            entities.DeleteEntity(screen);
        });
        await pair.CleanReturnAsync();
    }
    [Test]
    public async Task ScreenCollisionAndDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var physics = entities.System<SharedPhysicsSystem>();
        var damage = entities.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        foreach (var (prototype, width, height) in new[]
                 {
                     ("CinemaScreen", 10f, 5.625f),
                     ("CinemaScreenMini", 4f, 2.25f),
                     ("CinemaScreenMini2", 2f, 1.125f),
                 })
        {
            EntityUid screen = default;
            EntityUid bullet = default;
            await server.WaitAssertion(() =>
            {
                screen = entities.SpawnEntity(prototype, new MapCoordinates(Vector2.Zero, map.MapId));
                var fixture = entities.GetComponent<FixturesComponent>(screen).Fixtures["fix1"];
                var bounds = fixture.Shape.ComputeAABB(Robust.Shared.Physics.Transform.Empty, 0);
                Assert.That(bounds.Width, Is.EqualTo(width).Within(0.03), prototype);
                Assert.That(bounds.Height, Is.EqualTo(height).Within(0.03), prototype);
                Assert.That(fixture.Hard, Is.True);
                Assert.That(fixture.CollisionLayer & (int) CollisionGroup.MobMask, Is.Not.Zero);
                Assert.That(fixture.CollisionLayer & (int) CollisionGroup.BulletImpassable, Is.Not.Zero);

                // Hit the outer edge, far outside the original inherited one-tile fixture.
                bullet = entities.SpawnEntity("BulletLightRifle",
                    new MapCoordinates(new Vector2(width / 2 + 1, 0), map.MapId));
                physics.SetLinearVelocity(bullet, new Vector2(-10, 0));
            });

            await server.WaitRunTicks(30);
            await server.WaitAssertion(() =>
            {
                Assert.That(damage.GetTotalDamage(screen).Float(), Is.GreaterThan(0),
                    $"{prototype} must receive actual projectile impact damage.");
                Assert.That(entities.Deleted(bullet), Is.True);
                damage.TryChangeDamage(screen, new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } },
                    ignoreResistances: true);
                Assert.That(entities.GetComponent<CinemaScreenComponent>(screen).Broken, Is.True);
                entities.DeleteEntity(screen);
            });
        }

        await pair.CleanReturnAsync();
    }

}

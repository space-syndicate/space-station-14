using System.Numerics;
 using Content.Server.Shuttles.Components;
 using Robust.Server.GameObjects;
 using Content.Shared.Audio;
 using Robust.Shared.Audio;
 using Robust.Shared.Map;
 using Robust.Shared.Physics.Events;
 using Robust.Shared.Map.Components;
 using Content.Shared.Damage;

 namespace Content.Server.Shuttles.Systems;

 public sealed partial class ShuttleSystem
 {
     [Dependency] private readonly MapSystem _mapSys = default!; // Theta
     [Dependency] private readonly DamageableSystem _damageSys = default!; // Theta

     /// <summary>
     /// Minimum velocity difference between 2 bodies for a shuttle "impact" to occur.
     /// </summary>
     private const int MinimumImpactVelocity = 10;

     // Theta: collision damage
     /// <summary>
     /// Kinetic energy required to dismantle a single tile
     /// </summary>
     private const float TileBreakEnergy = 6700;

     /// <summary>
     /// Kinetic energy required to spawn sparks
     /// </summary>
     private const float SparkEnergy = 5000;
     // End Theta

     private readonly SoundCollectionSpecifier _shuttleImpactSound = new("ShuttleImpactSound");

     private void InitializeImpact()
     {
         SubscribeLocalEvent<ShuttleComponent, StartCollideEvent>(OnShuttleCollide);
     }

     private void OnShuttleCollide(EntityUid uid, ShuttleComponent component, ref StartCollideEvent args)
     {
         // Theta: change check from "if we're a shuttle" to "both must be grids"
         if (!TryComp<MapGridComponent>(uid, out var ourGrid) ||
             !TryComp<MapGridComponent>(args.OtherEntity, out var otherGrid))
             return;

         var ourBody = args.OurBody;
         var otherBody = args.OtherBody;

         // TODO: Would also be nice to have a continuous sound for scraping.
         var ourXform = Transform(uid);

         if (ourXform.MapUid == null)
             return;

         var otherXform = Transform(args.OtherEntity);

         var ourPoint = Vector2.Transform(args.WorldPoint, _transform.GetInvWorldMatrix(ourXform));
         var otherPoint = Vector2.Transform(args.WorldPoint, _transform.GetInvWorldMatrix(otherXform));

         var ourVelocity = _physics.GetLinearVelocity(uid, ourPoint, ourBody, ourXform);
         var otherVelocity = _physics.GetLinearVelocity(args.OtherEntity, otherPoint, otherBody, otherXform);
         var jungleDiff = (ourVelocity - otherVelocity).Length();

         if (jungleDiff < MinimumImpactVelocity)
         {
             return;
         }

         // Theta: ship collisions
         var energy = ourBody.Mass * Math.Pow(jungleDiff, 2) / 2;
         var dir = (ourVelocity.Length() > otherVelocity.Length() ? ourVelocity : -otherVelocity).Normalized();
         ProcessTile(uid, ourGrid, (Vector2i) ourPoint, (float) energy, -dir);
         ProcessTile(args.OtherEntity, otherGrid, (Vector2i) otherPoint, (float) energy, dir);
         // End Theta

         var coordinates = new EntityCoordinates(ourXform.MapUid.Value, args.WorldPoint);
         var volume = MathF.Min(10f, 1f * MathF.Pow(jungleDiff, 0.5f) - 5f);
         var audioParams = AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(volume);

         _audio.PlayPvs(_shuttleImpactSound, coordinates, audioParams);
     }

     // Theta: function to destroy tiles
     private void ProcessTile(EntityUid uid, MapGridComponent grid, Vector2i tile, float energy, Vector2 dir)
     {
         DamageSpecifier damage = new();
         damage.DamageDict = new() { { "Blunt", energy } };

         foreach (EntityUid localUid in _lookup.GetLocalEntitiesIntersecting(uid, tile, gridComp: grid))
         {
             _damageSys.TryChangeDamage(localUid, damage);

             TransformComponent form = Transform(localUid);
             if (!form.Anchored)
                 _transform.Unanchor(localUid, form);
             _throwing.TryThrow(localUid, dir);
         }

         if (energy > TileBreakEnergy)
             _mapSys.SetTile(new Entity<MapGridComponent>(uid, grid), tile, Tile.Empty);

         if (energy > SparkEnergy)
             SpawnAtPosition("EffectSparks", new EntityCoordinates(uid, tile));
     }
     // End Theta
 }

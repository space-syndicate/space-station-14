using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.NightDayMapLight
{
    public sealed class NightDayMapLightSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<NightDayMapLightComponent, MapLightComponent>();
            while (query.MoveNext(out var uid, out var nightDayMapLight, out var mapLight))
            {
                var dayDuration = nightDayMapLight.DayDuration;
                var transitionDuration = dayDuration / 2f;

                var t = (float)_gameTiming.CurTime.TotalSeconds % dayDuration / dayDuration;

                if (t <= 0.5f)
                {
                    var dayColor = nightDayMapLight.DayColor;
                    if (t >= 0.5f - (transitionDuration / dayDuration))
                    {
                        var transitionT = (0.5f - t) / (transitionDuration / dayDuration);
                        dayColor = Color.InterpolateBetween(nightDayMapLight.NightColor, nightDayMapLight.DayColor, transitionT);
                    }
                    mapLight.AmbientLightColor = dayColor;
                }
                else
                {
                    var nightColor = nightDayMapLight.NightColor;
                    if (t <= 0.5f + (transitionDuration / dayDuration))
                    {
                        var transitionT = (t - 0.5f) / (transitionDuration / dayDuration);
                        nightColor = Color.InterpolateBetween(nightDayMapLight.NightColor, nightDayMapLight.DayColor, transitionT);
                    }
                    mapLight.AmbientLightColor = nightColor;
                }

                Dirty(uid, mapLight);
            }
        }
    }
}
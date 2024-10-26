namespace Content.Server._Sunrise.NightDayMapLight
{
    [RegisterComponent]
    public sealed partial class NightDayMapLightComponent : Component
    {
        [ViewVariables]
        [DataField]
        public Color DayColor = Color.FromHex("#666666");

        [ViewVariables]
        [DataField]
        public Color NightColor = Color.FromHex("#000000");

        [ViewVariables]
        [DataField]
        public float DayDuration = 1200;
    }
}
namespace Content.Shared.Corvax.TTS;

[Flags]
public enum RadioChannelFlag : int
{
    None = 0,
    Common = 1 << 0,
    Command = 1 << 1,
    Engineering = 1 << 2,
    Medical = 1 << 3,
    Science = 1 << 4,
    Security = 1 << 5,
    Service = 1 << 6,
    Supply = 1 << 7,
    Syndicate = 1 << 8,
    Binary = 1 << 9,
    Handheld = 1 << 10,
    Freelance = 1 << 11,
    CentCom = 1 << 12,
    Xenoborg = 1 << 13,
    Mothership = 1 << 14,

    /// <summary>
    /// All channels except Common.
    /// </summary>
    AllExceptCommon = Command | Engineering | Medical | Science | Security | Service | Supply | Syndicate | Binary | Handheld | Freelance | CentCom | Xenoborg | Mothership,

    /// <summary>
    /// All channels including Common.
    /// </summary>
    All = Common | AllExceptCommon,
}

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
    Legal = 1 << 8,
    Syndicate = 1 << 9,
    Binary = 1 << 10,
    Handheld = 1 << 11,
    Freelance = 1 << 12,
    CentCom = 1 << 13,
    Xenoborg = 1 << 14,
    Mothership = 1 << 15,

    /// <summary>
    /// All channels except Common.
    /// </summary>
    AllExceptCommon = Command | Engineering | Medical | Science | Security | Service | Supply | Legal | Syndicate | Binary | Handheld | Freelance | CentCom | Xenoborg | Mothership,

    /// <summary>
    /// All channels including Common.
    /// </summary>
    All = Common | AllExceptCommon,
}

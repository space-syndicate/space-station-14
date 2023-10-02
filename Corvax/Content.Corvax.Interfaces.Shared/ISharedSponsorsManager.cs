namespace Content.Corvax.Interfaces.Shared;

public interface ISharedSponsorsManager
{
    public void Initialize();
}

public interface ISponsorInfo
{
    public int? Tier { get; set; }
    public string? OOCColor { get; set; }
    public bool HavePriorityJoin { get; set; }
    public int ExtraSlots { get; set; }
    public string[] AllowedMarkings { get; set; }
}

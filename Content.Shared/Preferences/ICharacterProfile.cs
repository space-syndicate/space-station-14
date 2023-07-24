using Content.Shared.Corvax.Sponsors;
using Content.Shared.Humanoid;

namespace Content.Shared.Preferences
{
    public interface ICharacterProfile
    {
        string Name { get; }

        ICharacterAppearance CharacterAppearance { get; }

        bool MemberwiseEquals(ICharacterProfile other);

        /// <summary>
        ///     Makes this profile valid so there's no bad data like negative ages.
        /// </summary>
        void EnsureValid(SponsorInfo? sponsorInfo); // Corvax-Sponsors: Integrated filtering for sponsor things (markings/species/etc)
    }
}

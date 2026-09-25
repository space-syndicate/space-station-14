using System;
using Content.Server.Corvax.Cinema;
using NUnit.Framework;

namespace Content.Tests.Server.Corvax;

[TestFixture]
public sealed class CinemaUrlTest
{
    private const string Episode = "a22f8718-8325-4b41-beb6-017c17a164cc";

    [TestCase("https://aniliberty.top/anime/video/episode/", "", true)]
    [TestCase("https://www.aniliberty.top/anime/video/episode/", "/?from=share#video", true)]
    [TestCase("http://aniliberty.top/anime/video/episode/", "", false)]
    [TestCase("https://aniliberty.top.evil.example/anime/video/episode/", "", false)]
    [TestCase("https://user@aniliberty.top/anime/video/episode/", "", false)]
    [TestCase("https://aniliberty.top:444/anime/video/episode/", "", false)]
    [TestCase("https://aniliberty.top/anime/video/episode/", "/extra", false)]
    [TestCase("https://aniliberty.top/anime/", "", false)]
    public void EpisodeLinks(string prefix, string suffix, bool valid)
    {
        Assert.That(CinemaScreenSystem.TryGetAniLibertyEpisode(prefix + Episode + suffix, out var id), Is.EqualTo(valid));
        if (valid)
            Assert.That(id, Is.EqualTo(Guid.Parse(Episode)));
    }

    [TestCase("https://aniliberty.top/anime/video/episode/not-an-id")]
    [TestCase("https://aniliberty.top/anime/video/episode/")]
    [TestCase("not a URL")]
    public void InvalidEpisode(string url)
    {
        Assert.That(CinemaScreenSystem.TryGetAniLibertyEpisode(url, out _), Is.False);
    }
}

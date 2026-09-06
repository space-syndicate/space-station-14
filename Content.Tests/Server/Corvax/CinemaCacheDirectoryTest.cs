using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Content.Server.Corvax.Cinema;
using NUnit.Framework;

namespace Content.Tests.Server.Corvax;

[TestFixture]
public sealed class CinemaCacheDirectoryTest
{
    [Test]
    public void RemovesDeadOwnersAndPreservesLiveOrUnidentifiedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cinema-cache-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var active = CinemaCacheDirectory.NewPath(root);
            var dead = Path.Combine(root, $"covax-cinema-audio-{int.MaxValue}-1-{Guid.NewGuid():N}");
            using var process = Process.GetCurrentProcess();
            var reusedPid = Path.Combine(root,
                $"covax-cinema-audio-{process.Id}-{process.StartTime.ToUniversalTime().Ticks - 1}-{Guid.NewGuid():N}");
            var legacy = Path.Combine(root, $"covax-cinema-audio-{Guid.NewGuid():N}");
            var malformed = Path.Combine(root, "covax-cinema-audio-0-0-invalid");
            var unrelated = Path.Combine(root, "other-cache");
            foreach (var path in new[] { active, dead, reusedPid, legacy, malformed, unrelated })
            {
                Directory.CreateDirectory(Path.Combine(path, "segments"));
                File.WriteAllText(Path.Combine(path, "segments", "video.webm"), "cached data");
            }
            var warnings = new List<string>();
            CinemaCacheDirectory.RemoveStale(root, warnings.Add);
            Assert.That(warnings, Is.Empty);
            Assert.That(Directory.Exists(dead), Is.False);
            Assert.That(Directory.Exists(reusedPid), Is.False, "Reused process IDs must not retain orphaned caches.");
            foreach (var path in new[] { active, legacy, malformed, unrelated })
                Assert.That(File.Exists(Path.Combine(path, "segments", "video.webm")), Is.True, path);
            CinemaCacheDirectory.RemoveStale(root, warnings.Add);
            Assert.That(warnings, Is.Empty, "Repeated cleanup must be harmless.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

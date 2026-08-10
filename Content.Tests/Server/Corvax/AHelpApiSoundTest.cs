using System;
using System.Text.Json;
using Content.Server.Administration.Systems;
using Content.Server.Corvax.Api.AHelp;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Server.Corvax;

[TestFixture]
public sealed class AHelpApiSoundTest
{
    [Test]
    public void LegacyCommandPlaysSoundByDefault()
    {
        const string json = """
            {
              "commandId": "legacy",
              "type": "send_ahelp_message",
              "text": "Message"
            }
            """;

        var command = JsonSerializer.Deserialize<AHelpApiCommand>(json);

        Assert.That(command, Is.Not.Null);
        Assert.That(command!.PlaySound, Is.True);
        Assert.That(command.AdminOnly, Is.False);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void MessageSoundRespectsApiFlags(bool playSound, bool adminOnly, bool expectedPlaySound)
    {
        var message = BwoinkSystem.BuildCorvaxAHelpMessage(
            new NetUserId(Guid.NewGuid()),
            "Message",
            playSound,
            adminOnly);

        Assert.That(message.PlaySound, Is.EqualTo(expectedPlaySound));
        Assert.That(message.AdminOnly, Is.EqualTo(adminOnly));
    }
}

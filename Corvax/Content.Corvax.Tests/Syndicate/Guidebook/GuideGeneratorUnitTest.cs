using System.Collections.Generic;
using Content.Server.Corvax.GuideGenerator;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Corvax.Tests.Syndicate.Guidebook;

[TestFixture]
public sealed class TextToolsTest
{
    [TestCase("test", "Test")]
    [TestCase("a", "A")]
    [TestCase("hello world", "Hello world")]
    public void CapitalizeString(string input, string expected)
    {
        Assert.That(TextTools.CapitalizeString(input), Is.EqualTo(expected));
    }

    [TestCase("Test", "test")]
    [TestCase("A", "a")]
    [TestCase("HELLO WORLD", "hELLO WORLD")]
    public void DecapitalizeString(string input, string expected)
    {
        Assert.That(TextTools.DecapitalizeString(input), Is.EqualTo(expected));
    }

    [TestCase("  test  ", "test")]
    [TestCase(",test,", "test")]
    [TestCase("  ,; test ;,  ", "test")]
    public void NormalizeSuffixToken(string input, string expected)
    {
        Assert.That(TextTools.NormalizeSuffixToken(input), Is.EqualTo(expected));
    }

    [Test]
    public void GetEditorSuffixFiltersIgnoredTokens()
    {
        var ignored = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "do not map" };
        var result = TextTools.GetEditorSuffix("test, do not map", ignored, TextTools.NormalizeSuffixToken);
        Assert.That(result, Is.EqualTo("test"));
    }

    [Test]
    public void GetEditorSuffixLowercasesResult()
    {
        var result = TextTools.GetEditorSuffix("Test, OTHER", new HashSet<string>(), TextTools.NormalizeSuffixToken);
        Assert.That(result, Is.EqualTo("test, other"));
    }
}

[TestFixture]
public sealed class FieldEntryTest
{
    [Test]
    public void ConvertValueInt()
    {
        Assert.That(FieldEntry.ConvertNode(new ValueDataNode("42")), Is.EqualTo(42));
    }

    [Test]
    public void ConvertValueDouble()
    {
        Assert.That(FieldEntry.ConvertNode(new ValueDataNode("3.14")), Is.EqualTo(3.14));
    }

    [Test]
    public void ConvertMappingProducesDictionary()
    {
        var node = new MappingDataNode
        {
            { "a", new ValueDataNode("1") },
            { "b", new ValueDataNode("two") },
        };

        var result = FieldEntry.ConvertNode(node) as Dictionary<string, object?>;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!["a"], Is.EqualTo(1));
        Assert.That(result["b"], Is.EqualTo("two"));
    }

    [Test]
    public void ConvertSequenceProducesList()
    {
        var node = new SequenceDataNode("a", "b", "c");
        var result = FieldEntry.ConvertNode(node) as List<object?>;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("a"));
        Assert.That(result[2], Is.EqualTo("c"));
    }

    [Test]
    public void DeduplicateAgainstDefaultWrapsInDefaultAndIdKeys()
    {
        var defaults = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
        var values = new Dictionary<string, object?>
        {
            ["ent1"] = new Dictionary<string, object?> { ["a"] = 1 },
        };

        var result = FieldEntry.DeduplicateAgainstDefault(defaults, values);

        Assert.That(result, Does.ContainKey(FieldEntry.DefaultField));
        Assert.That(result[FieldEntry.DefaultField], Is.EqualTo(defaults));
        Assert.That(result, Does.ContainKey(FieldEntry.PrototypeId));
        Assert.That(result[FieldEntry.PrototypeId], Is.EqualTo(values));
    }
}

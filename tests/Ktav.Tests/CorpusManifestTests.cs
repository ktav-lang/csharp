using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Ktav.Tests;

[TestFixture]
public sealed class CorpusManifestTests
{
    private static readonly IReadOnlyDictionary<string, int> s_oneEach = new Dictionary<string, int>
    {
        ["valid"] = 1,
        ["invalid"] = 1,
        ["unrepresentable"] = 1,
        ["parseable-unrepresentable"] = 1,
        ["strict-lossy"] = 1,
    };

    private static readonly IReadOnlyDictionary<string, int> s_twoInvalid = new Dictionary<string, int>
    {
        ["valid"] = 1,
        ["invalid"] = 2,
        ["unrepresentable"] = 1,
        ["parseable-unrepresentable"] = 1,
        ["strict-lossy"] = 1,
    };

    [Test]
    public void AcceptsCompleteMiniCorpusWithPinnedManifestShape()
    {
        WithCorpus(root =>
        {
            var manifest = CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest), root, s_oneEach);
            Assert.That(manifest.CategoriesByName["invalid"][0].Flags, Does.Contain("raw_bytes"));
            Assert.That(manifest.CategoriesByName["valid"][0].Flags, Is.Empty);
        });
    }

    [TestCase("\"schema_version\":0", "schema_version")]
    [TestCase("\"schema_version\":2", "schema_version")]
    [TestCase("\"extra\":true", "unknown field")]
    [TestCase("\"valid\":{\"count\":0}", "expected 1")]
    [TestCase("\"flags\":[\"raw_bytes\",\"raw_bytes\"]", "duplicate flag")]
    [TestCase("\"flags\":[\"mystery\"]", "unknown flag")]
    [TestCase("\"flags\":null", "fixture flags must be an array")]
    public void RejectsMalformedManifest(string replacement, string expectedMessage)
    {
        WithCorpus(root =>
        {
            string modified = MiniManifest;
            if (replacement.StartsWith("\"schema_version\"", StringComparison.Ordinal))
                modified = modified.Replace("\"schema_version\":1", replacement, StringComparison.Ordinal);
            else if (replacement.StartsWith("\"extra\"", StringComparison.Ordinal))
                modified = modified.Replace("\"schema_version\":1", "\"schema_version\":1," + replacement, StringComparison.Ordinal);
            else if (replacement.StartsWith("\"valid\"", StringComparison.Ordinal))
                modified = modified.Replace("\"valid\":{\"count\":1}", replacement, StringComparison.Ordinal);
            else
                modified = modified.Replace("\"flags\":[\"raw_bytes\"]", replacement, StringComparison.Ordinal);

            var ex = Assert.Throws<InvalidDataException>(() =>
                CorpusManifest.Parse(Encoding.UTF8.GetBytes(modified), root, s_oneEach));
            Assert.That(ex!.Message, Does.Contain(expectedMessage));
        });
    }

    [Test]
    public void RejectsUnknownOrMissingCategoriesAndRootEntries()
    {
        WithCorpus(root =>
        {
            string unknownCategory = MiniManifest.Replace("\"valid\":{\"count\":1}",
                "\"other\":{\"count\":1}", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(unknownCategory), root, s_oneEach));

            string missingCategory = MiniManifest.Replace(",\"strict-lossy\":{\"count\":1}", "", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(missingCategory), root, s_oneEach));

            File.WriteAllText(Path.Combine(root, "surprise.txt"), "x");
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest), root, s_oneEach));
            File.Delete(Path.Combine(root, "surprise.txt"));

            Directory.Delete(Path.Combine(root, "strict-lossy"), true);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest), root, s_oneEach));
        });
    }

    [Test]
    public void RejectsTrailingJsonAndMissingCompanions()
    {
        WithCorpus(root =>
        {
            Assert.Catch<JsonException>(() =>
                CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest + " {}"), root, s_oneEach));
            File.Delete(Path.Combine(root, "valid", "a.canonical.ktav"));
            Assert.Throws<InvalidDataException>(() =>
                CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest), root, s_oneEach));
        });
    }

    [Test]
    public void RejectsTruncatedNonemptyCategory()
    {
        WithCorpus(root =>
        {
            Assert.Throws<InvalidDataException>(() =>
                CorpusManifest.Parse(Encoding.UTF8.GetBytes(MiniManifest.Replace("\"invalid\":{\"count\":1}",
                    "\"invalid\":{\"count\":2}", StringComparison.Ordinal)), root, s_twoInvalid));
        });
    }

    [Test]
    public void RejectsDanglingOrMisappliedFixtureFlags()
    {
        WithCorpus(root =>
        {
            string dangling = MiniManifest.Replace("invalid_utf8/bad", "invalid_utf8/missing", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(dangling), root, s_oneEach));

            string misapplied = MiniManifest.Replace("\"category\":\"invalid\"", "\"category\":\"valid\"", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(misapplied), root, s_oneEach));
        });
    }

    [Test]
    public void RejectsMalformedUtf8WithoutRawBytesFlag()
    {
        WithCorpus(root =>
        {
            var manifest = JsonNode.Parse(MiniManifest)!;
            manifest["fixture_flags"] = new JsonArray();
            string withoutFlags = manifest.ToJsonString();
            using var modifiedManifest = JsonDocument.Parse(withoutFlags);
            var flags = modifiedManifest.RootElement.GetProperty("fixture_flags");
            Assert.That(flags.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(flags.GetArrayLength(), Is.Zero);
            Assert.Throws<InvalidDataException>(() => CorpusManifest.Parse(Encoding.UTF8.GetBytes(withoutFlags), root, s_oneEach));
        });
    }

    [Test]
    public void InvalidOracleRequiresExactExpectedErrorClass()
    {
        using var oracle = JsonDocument.Parse("{\"expected_error\":\"WantedClass\"}");
        var error = KtavException.FromEnvelope(Encoding.UTF8.GetBytes("{\"error\":\"DifferentClass\"}"));
        Assert.Throws<AssertionException>(() =>
            SpecConformance.AssertInvalidOracle(error, oracle.RootElement, "mini.ktav"));
    }

    [Test]
    public void InvalidOracleChecksPresentNullPayloadButIgnoresAbsentPayload()
    {
        using var oracle = JsonDocument.Parse("{\"expected_error\":\"ParseError\",\"reason\":null,\"body\":null}");
        var nullPayloadError = KtavException.FromEnvelope(Encoding.UTF8.GetBytes("{\"error\":\"ParseError\",\"reason\":null,\"body\":null}"));
        Assert.DoesNotThrow(() => SpecConformance.AssertInvalidOracle(nullPayloadError, oracle.RootElement, "mini.ktav"));

        var presentPayloadError = KtavException.FromEnvelope(Encoding.UTF8.GetBytes("{\"error\":\"ParseError\",\"body\":\"present\"}"));
        using var absentOracle = JsonDocument.Parse("{\"expected_error\":\"ParseError\"}");
        Assert.DoesNotThrow(() => SpecConformance.AssertInvalidOracle(presentPayloadError, absentOracle.RootElement, "mini.ktav"));
        Assert.Throws<AssertionException>(() =>
            SpecConformance.AssertInvalidOracle(presentPayloadError, oracle.RootElement, "mini.ktav"));
    }

    private static string MiniManifest => """
        {"$comment":"mini corpus","schema_version":1,"categories":{
          "valid":{"count":1},"invalid":{"count":1},"unrepresentable":{"count":1},
          "parseable-unrepresentable":{"count":1},"strict-lossy":{"count":1}},
          "fixture_flags":[{"category":"invalid","fixture":"invalid_utf8/bad","flags":["raw_bytes"],"note":"raw source"}]}
        """;

    private static void WithCorpus(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "ktav-corpus-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "manifest.json"), MiniManifest);
            File.WriteAllText(Path.Combine(root, "boundary-fixtures.json"),
                "{\"$comment\":\"mini metadata\",\"boundary_dependent_leaves\":[{\"fixture\":\"a\",\"path\":\"/value\",\"boundary_class\":\"integer_range\"}]}");
            foreach (string category in CorpusManifest.Categories)
                Directory.CreateDirectory(Path.Combine(root, category));
            File.WriteAllText(Path.Combine(root, "valid", "a.ktav"), "null");
            File.WriteAllText(Path.Combine(root, "valid", "a.canonical.ktav"), "null");
            File.WriteAllText(Path.Combine(root, "valid", "a.json"), "null");
            Directory.CreateDirectory(Path.Combine(root, "invalid", "invalid_utf8"));
            File.WriteAllBytes(Path.Combine(root, "invalid", "invalid_utf8", "bad.ktav"), new byte[] { 0xff });
            File.WriteAllText(Path.Combine(root, "invalid", "invalid_utf8", "bad.json"), "{}");
            File.WriteAllText(Path.Combine(root, "unrepresentable", "value.json"), "{}");
            File.WriteAllText(Path.Combine(root, "parseable-unrepresentable", "parsed.ktav"), "null");
            File.WriteAllText(Path.Combine(root, "parseable-unrepresentable", "parsed.json"), "{}");
            File.WriteAllText(Path.Combine(root, "strict-lossy", "lossy.ktav"), "null");
            File.WriteAllText(Path.Combine(root, "strict-lossy", "lossy.json"), "{}");
            action(root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

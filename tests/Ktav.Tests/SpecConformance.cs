using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Walks the Ktav spec conformance suite and replays every fixture
/// against the loaded native parser across the five manifest categories:
/// <list type="bullet">
/// <item><c>valid</c> — parses, compared against a plain-JSON oracle;
/// each fixture's <c>.canonical.ktav</c> companion is also checked
/// byte-for-byte against <c>EmitCanonical</c>'s output (spec § 5.9.10,
/// § 5.9.8).</item>
/// <item><c>invalid</c> — must fail to parse.</item>
/// <item><c>unrepresentable</c> — JSON-only Values a writer (Dumps and
/// EmitCanonical) must refuse; there is deliberately no .ktav source.</item>
/// <item><c>parseable-unrepresentable</c> — parses fine, but canonical
/// emit must refuse.</item>
/// </list>
/// A guard test hard-fails when the suite is missing or when an unknown
/// fixture category directory appears — an empty submodule or a newly
/// added category must not silently keep CI green.
/// </summary>
[TestFixture]
public class SpecConformance
{
    private static readonly string s_validDir = Path.Combine(TestPaths.Spec, "valid");
    private static readonly string s_invalidDir = Path.Combine(TestPaths.Spec, "invalid");
    private static readonly string s_unrepresentableDir = Path.Combine(TestPaths.Spec, "unrepresentable");
    private static readonly string s_parseableUnrepresentableDir = Path.Combine(TestPaths.Spec, "parseable-unrepresentable");
    private static readonly string s_strictLossyDir = Path.Combine(TestPaths.Spec, "strict-lossy");
    private static CorpusManifest Manifest() => TestPaths.Corpus;

    private static List<string> Paths(string category) => Manifest().CategoriesByName[category]
        .Select(f => Path.Combine(TestPaths.Spec, category, f.Path.Replace('/', Path.DirectorySeparatorChar)))
        .ToList();

    private static CorpusFixture Fixture(string category, string path) => Manifest().CategoriesByName[category]
        .Single(f => Path.GetFullPath(Path.Combine(TestPaths.Spec, category,
            f.Path.Replace('/', Path.DirectorySeparatorChar))) == Path.GetFullPath(path));

    private static List<string> ValidPaths()
    {
        return Paths("valid");
    }

    private static List<string> InvalidPaths()
    {
        return Paths("invalid");
    }

    private static List<string> UnrepresentablePaths()
    {
        return Paths("unrepresentable");
    }

    private static List<string> ParseableUnrepresentablePaths()
    {
        return Paths("parseable-unrepresentable");
    }

    private static List<string> StrictLossyPaths()
    {
        return Paths("strict-lossy");
    }

    public static IEnumerable<TestCaseData> StrictLossyCases()
    {
        Manifest();
        foreach (var ktavPath in StrictLossyPaths())
        {
            var name = Path.GetRelativePath(s_strictLossyDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"strict-lossy:{name}");
        }
    }

    public static IEnumerable<TestCaseData> ValidCases()
    {
        Manifest();
        foreach (var ktavPath in ValidPaths())
        {
            var name = Path.GetRelativePath(s_validDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"valid:{name}");
        }
    }

    /// <summary>Every non-canonical fixture in <c>valid/</c> ships a
    /// <c>.canonical.ktav</c> companion (spec § 5.9.10, § 5.9.8) — this
    /// derives that companion's path.</summary>
    private static string CanonicalCompanionPath(string ktavPath) =>
        Path.ChangeExtension(ktavPath, ".canonical.ktav");

    public static IEnumerable<TestCaseData> ValidCanonicalCases()
    {
        Manifest();
        foreach (var ktavPath in ValidPaths())
        {
            var name = Path.GetRelativePath(s_validDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"canonical:{name}");
        }
    }

    public static IEnumerable<TestCaseData> InvalidCases()
    {
        Manifest();
        foreach (var ktavPath in InvalidPaths())
        {
            var name = Path.GetRelativePath(s_invalidDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"invalid:{name}");
        }
    }

    public static IEnumerable<TestCaseData> UnrepresentableCases()
    {
        Manifest();
        foreach (var jsonPath in UnrepresentablePaths())
        {
            var name = Path.GetRelativePath(s_unrepresentableDir, jsonPath).Replace('\\', '/');
            yield return new TestCaseData(jsonPath).SetName($"unrepresentable:{name}");
        }
    }

    public static IEnumerable<TestCaseData> ParseableUnrepresentableCases()
    {
        Manifest();
        foreach (var ktavPath in ParseableUnrepresentablePaths())
        {
            var name = Path.GetRelativePath(s_parseableUnrepresentableDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"parseable-unrepresentable:{name}");
        }
    }

    [TestCaseSource(nameof(ValidCases))]
    public void Valid(string ktavPath)
    {
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        Assert.That(File.Exists(oraclePath), $"oracle JSON missing: {oraclePath}");

        var src = File.ReadAllText(ktavPath);
        var oracleBytes = File.ReadAllBytes(oraclePath);

        var got = Ktav.Loads(src);

        // Decode the oracle through the same wire reader so bare numbers
        // become KtavInteger / KtavFloat — matching what cabi emits via
        // its $i / $f wrappers.
        var want = WireJson.Decode(oracleBytes);

        Assert.That(ValueEquals(want, got),
            $"mismatch for {ktavPath}\nsrc:\n{src}\nwant: {want}\ngot:  {got}");
    }

    /// <summary>
    /// The canonical writer must produce the exact spelling in the
    /// fixture's <c>.canonical.ktav</c> companion (spec § 5.9.10, § 5.9.8)
    /// — compared byte-for-byte, not just structurally. Idempotence
    /// (re-canonicalising the companion yields itself) is implied but
    /// not re-checked here, since every valid fixture is already run
    /// through this same comparison.
    /// </summary>
    [TestCaseSource(nameof(ValidCanonicalCases))]
    public void ValidCanonical(string ktavPath)
    {
        var canonicalPath = CanonicalCompanionPath(ktavPath);
        Assert.That(File.Exists(canonicalPath), $"canonical companion missing: {canonicalPath}");

        var src = File.ReadAllText(ktavPath);
        var expectedBytes = File.ReadAllBytes(canonicalPath);

        var value = Ktav.Loads(src);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(Ktav.EmitCanonical(value));

        Assert.That(actualBytes, Is.EqualTo(expectedBytes),
            $"canonical mismatch for {ktavPath}\n" +
            $"want: {System.Text.Encoding.UTF8.GetString(expectedBytes)}\n" +
            $"got:  {System.Text.Encoding.UTF8.GetString(actualBytes)}");
    }

    [TestCaseSource(nameof(InvalidCases))]
    public void Invalid(string ktavPath)
    {
        var raw = File.ReadAllBytes(ktavPath);
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        using var oracle = JsonDocument.Parse(File.ReadAllText(oraclePath));
        var expected = oracle.RootElement;

        if (Fixture("invalid", ktavPath).Flags.Contains("raw_bytes"))
        {
            var error = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(raw),
                $"expected invalid-UTF-8 rejection for {ktavPath}")!;
            Assert.That(error.Error, Is.EqualTo("InvalidUtf8"), $"error class for {ktavPath}");
            Assert.That(error.SpecSection, Is.EqualTo("§6.15"), $"spec section for {ktavPath}");
            Assert.That(error.Span.HasValue, Is.True, $"UTF-8 error span missing for {ktavPath}");
            AssertInvalidOracle(error, expected, ktavPath);
            return;
        }

        Assert.That(TryDecodeStrictUtf8(raw, out var src), Is.True,
            $"malformed UTF-8 fixture missing raw_bytes flag: {ktavPath}");
        var parseError = Assert.Throws<KtavException>(() => Ktav.Loads(src),
            $"expected parse error for {ktavPath}")!;
        AssertInvalidOracle(parseError, expected, ktavPath);
    }

    internal static void AssertInvalidOracle(KtavException error, JsonElement expected, string path)
    {
        Assert.That(expected.ValueKind, Is.EqualTo(JsonValueKind.Object), $"invalid oracle object for {path}");
        Assert.That(expected.TryGetProperty("expected_error", out var expectedError), Is.True,
            $"expected_error missing for {path}");
        Assert.That(expectedError.ValueKind, Is.EqualTo(JsonValueKind.String), $"expected_error must be a string for {path}");
        string expectedClass = expectedError.GetString()!;
        Assert.That(expectedClass, Is.Not.Empty, $"expected_error must be nonempty for {path}");
        Assert.That(error.Error, Is.EqualTo(expectedClass), $"error class for {path}");
        AssertJsonString(expected, "reason", error.Reason, path);
        AssertJsonInt(expected, "line", error.Line, path);
        AssertJsonString(expected, "line_text", error.LineText, path);
        AssertJsonString(expected, "body", error.Body, path);
        AssertJsonString(expected, "canonical", error.Canonical, path);
        AssertJsonString(expected, "spec_section", error.SpecSection, path);
        if (expected.TryGetProperty("span", out var span))
        {
            if (span.ValueKind == JsonValueKind.Null) Assert.That(error.Span.HasValue, Is.False, $"span for {path}");
            else
            {
                Assert.That(error.Span.HasValue, Is.True, $"span missing for {path}");
                Assert.That(error.Span!.Value.Start, Is.EqualTo(span.GetProperty("start").GetInt64()), $"span.start for {path}");
                Assert.That(error.Span.Value.End, Is.EqualTo(span.GetProperty("end").GetInt64()), $"span.end for {path}");
            }
        }
        if (expected.TryGetProperty("path", out var segments))
        {
            if (segments.ValueKind == JsonValueKind.Null) Assert.That(error.Path, Is.Null, $"path for {path}");
            else Assert.That(error.Path, Is.EqualTo(segments.EnumerateArray().Select(x => x.GetString()).ToArray()), $"path for {path}");
        }
    }

    private static void AssertJsonString(JsonElement expected, string name, string? actual, string path)
    {
        if (!expected.TryGetProperty(name, out var value)) return;
        Assert.That(value.ValueKind == JsonValueKind.Null ? (string?)null : value.GetString(), Is.EqualTo(actual), $"{name} for {path}");
    }

    private static void AssertJsonInt(JsonElement expected, string name, int? actual, string path)
    {
        if (!expected.TryGetProperty(name, out var value)) return;
        Assert.That(value.ValueKind == JsonValueKind.Null ? (int?)null : value.GetInt32(), Is.EqualTo(actual), $"{name} for {path}");
    }

    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (System.Text.DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    [TestCaseSource(nameof(UnrepresentableCases))]
    public void Unrepresentable(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var value = FixtureValueToKtav(root.GetProperty("value"));
        var reason = root.GetProperty("unrepresentable_reason").GetString();
        var (expectedReason, expectedSection) = ReasonContract(reason!);

        var dumpsError = Assert.Throws<KtavException>(() => Ktav.Dumps(value),
            $"Dumps should refuse unrepresentable value ({reason}): {jsonPath}");
        var canonicalError = Assert.Throws<KtavException>(() => Ktav.EmitCanonical(value),
            $"EmitCanonical should refuse unrepresentable value ({reason}): {jsonPath}");
        AssertUnrepresentableError(dumpsError!, expectedReason, expectedSection, jsonPath);
        AssertUnrepresentableError(canonicalError!, expectedReason, expectedSection, jsonPath);
    }

    [TestCaseSource(nameof(ParseableUnrepresentableCases))]
    public void ParseableUnrepresentable(string ktavPath)
    {
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        Assert.That(File.Exists(oraclePath), $"oracle JSON missing: {oraclePath}");

        var src = File.ReadAllText(ktavPath);
        var got = Ktav.Loads(src);

        using var doc = JsonDocument.Parse(File.ReadAllText(oraclePath));
        var root = doc.RootElement;
        var want = FixtureValueToKtav(root.GetProperty("value"));
        var reason = root.GetProperty("unrepresentable_reason").GetString();

        Assert.That(ValueEquals(want, got),
            $"mismatch for {ktavPath}\nsrc:\n{src}\nwant: {want}\ngot:  {got}");

        var dumpsError = Assert.Throws<KtavException>(() => Ktav.Dumps(got),
            $"Dumps should refuse unrepresentable value ({reason}): {ktavPath}");
        var canonicalError = Assert.Throws<KtavException>(() => Ktav.EmitCanonical(got),
            $"EmitCanonical should refuse unrepresentable value ({reason}): {ktavPath}");
        var (expectedReason, expectedSection) = ReasonContract(reason!);
        AssertUnrepresentableError(dumpsError!, expectedReason, expectedSection, ktavPath);
        AssertUnrepresentableError(canonicalError!, expectedReason, expectedSection, ktavPath);
    }

    /// <summary>
    /// Spec § 8.1 <c>strict-lossy/</c>: <see cref="Ktav.Loads"/> accepts the
    /// fixture and yields <c>lax_value</c>; <see cref="Ktav.LoadsStrict"/>
    /// refuses it with <c>LossyScalar</c> naming the exact body/canonical.
    /// </summary>
    [TestCaseSource(nameof(StrictLossyCases))]
    public void StrictLossy(string ktavPath)
    {
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        Assert.That(File.Exists(oraclePath), $"oracle JSON missing: {oraclePath}");

        var src = File.ReadAllText(ktavPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(oraclePath));
        var root = doc.RootElement;
        var want = WireJson.Decode(System.Text.Encoding.UTF8.GetBytes(root.GetProperty("lax_value").GetRawText()));

        var got = Ktav.Loads(src);
        Assert.That(ValueEquals(want, got),
            $"lax mismatch for {ktavPath}\nsrc:\n{src}\nwant: {want}\ngot:  {got}");

        var ex = Assert.Throws<KtavException>(() => Ktav.LoadsStrict(src),
            $"LoadsStrict should refuse {ktavPath}")!;
        Assert.That(ex.Error, Is.EqualTo(root.GetProperty("expected_error").GetString()), $"error class for {ktavPath}");
        Assert.That(ex.Body, Is.EqualTo(root.GetProperty("body").GetString()), $"body for {ktavPath}");
        Assert.That(ex.Canonical, Is.EqualTo(root.GetProperty("canonical").GetString()), $"canonical for {ktavPath}");
    }

    /// <summary>
    /// The shared manifest guard validates the five categories, exact
    /// counts, companions, and known root metadata before discovery.
    /// </summary>
    /// <remarks>
    /// This test confirms the discovered categories match that validated
    /// manifest instance.
    /// </remarks>
    [Test]
    public void SuiteCoversEveryFixtureCategory()
    {
        Assert.That(Manifest().CategoriesByName.Keys, Is.EquivalentTo(CorpusManifest.PinnedCounts.Keys));
    }

    private static (string Reason, string Section) ReasonContract(string reason) => reason switch
    {
        "ScalarRoot" => ("ScalarRoot", "§5.9.0"),
        "NonFiniteFloat" => ("NonFiniteFloat", "§5.9.0"),
        "EmptyKeyName" => ("EmptyKeyName", "§5.9.0"),
        "CRByte" => ("CRByte", "§5.9.7"),
        "BothFormsRequired" => ("BothFormsRequired", "§5.9.7"),
        "LeadingWhitespaceCollision" => ("LeadingWhitespaceCollision", "§5.9.7"),
        "TrailingWhitespaceCollision" => ("TrailingWhitespaceCollision", "§5.9.7"),
        _ => throw new AssertionException($"unknown unrepresentable_reason: {reason}"),
    };

    private static void AssertUnrepresentableError(KtavException error, string reason, string section, string path)
    {
        Assert.That(error.Error, Is.EqualTo("UnrepresentableAt"), $"error class for {path}");
        Assert.That(error.Reason, Is.EqualTo(reason), $"reason for {path}");
        Assert.That(error.SpecSection, Is.EqualTo(section), $"spec section for {path}");
    }

    /// <summary>
    /// Converts a plain-JSON fixture oracle value to a KtavValue.
    /// A JSON object whose only property is <c>$float</c> is the spec's
    /// encoding for a non-finite float (payload is a string like "NaN")
    /// — no JSON number can be non-finite, so this is unambiguous.
    /// Numbers keep their raw text, split into Integer/Float the same
    /// way the wire reader does.
    /// </summary>
    private static KtavValue FixtureValueToKtav(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (el.EnumerateObject().Count() == 1)
                {
                    var prop = el.EnumerateObject().First();
                    if (prop.Name == "$float")
                        return new KtavFloat(prop.Value.GetString()!);
                }
                return new KtavObject(el.EnumerateObject()
                    .Select(p => new KeyValuePair<string, KtavValue>(p.Name, FixtureValueToKtav(p.Value)))
                    .ToList());
            }
            case JsonValueKind.Array:
                return new KtavArray(el.EnumerateArray().Select(FixtureValueToKtav).ToList());
            case JsonValueKind.String:
                return new KtavString(el.GetString()!);
            case JsonValueKind.Number:
                // Use the raw JSON text; '.'/'e'/'E' implies a float.
                var text = el.GetRawText();
                if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
                    return new KtavFloat(text);
                return new KtavInteger(text);
            case JsonValueKind.True:
                return KtavBool.True;
            case JsonValueKind.False:
                return KtavBool.False;
            case JsonValueKind.Null:
                return KtavNull.Instance;
            default:
                throw new KtavException($"unsupported fixture JSON kind: {el.ValueKind}");
        }
    }

    /// <summary>
    /// Structural equality with one subtlety: floats compare by numeric
    /// value (the spec oracle JSON may use a canonical text form like
    /// <c>2.5e+8</c> where the source Ktav had <c>2.5E+8</c>). Integers
    /// compare by <see cref="BigInteger"/>.
    /// </summary>
    private static bool ValueEquals(KtavValue a, KtavValue b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is KtavFloat fa && b is KtavFloat fb)
        {
            var da = double.Parse(fa.Text, System.Globalization.CultureInfo.InvariantCulture);
            var db = double.Parse(fb.Text, System.Globalization.CultureInfo.InvariantCulture);
            return da.Equals(db);
        }
        if (a is KtavInteger ia && b is KtavInteger ib)
            return BigInteger.Parse(ia.Text) == BigInteger.Parse(ib.Text);
        if (a is KtavArray aa && b is KtavArray ab)
        {
            if (aa.Items.Count != ab.Items.Count) return false;
            for (int i = 0; i < aa.Items.Count; i++)
                if (!ValueEquals(aa.Items[i], ab.Items[i])) return false;
            return true;
        }
        if (a is KtavObject oa && b is KtavObject ob)
        {
            if (oa.Entries.Count != ob.Entries.Count) return false;
            for (int i = 0; i < oa.Entries.Count; i++)
            {
                if (oa.Entries[i].Key != ob.Entries[i].Key) return false;
                if (!ValueEquals(oa.Entries[i].Value, ob.Entries[i].Value)) return false;
            }
            return true;
        }
        return a.Equals(b);
    }
}

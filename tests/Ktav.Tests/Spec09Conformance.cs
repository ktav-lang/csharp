using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Spec 0.9 conformance (412 fixtures: valid 228, invalid 162,
/// unrepresentable 5, parseable-unrepresentable 4, strict-lossy 13).
/// Reuses the 0.8 runner cores against the 0.9 corpus and adds the
/// diagnostics-0.9 contract:
/// <list type="bullet">
/// <item>72 new invalid diagnostics (30 EOF + 42 malformed UTF-8) ship
/// <c>expected_location</c> metadata: <c>line</c>/<c>start</c> plus exactly
/// one of <c>end</c>/<c>minimum_end</c> — validated, never ignored.</item>
/// <item>Every invalid fixture is replayed through BOTH entry points
/// (string API and the raw-bytes API) and both backends where available,
/// so raw-byte invalid-UTF-8 coverage reaches every byte boundary.</item>
/// <item>Valid fixtures are additionally executed through the formatter
/// with its fixed-point property checked.</item>
/// </list>
/// </summary>
[TestFixture]
public class Spec09Conformance
{
    private static CorpusManifest Manifest() => TestPaths.Corpus09;

    private static string FixturePath(string category, CorpusFixture f) =>
        Path.Combine(TestPaths.Spec09, category, f.Path.Replace('/', Path.DirectorySeparatorChar));

    // ------------------------------------------------------------------
    // Guards
    // ------------------------------------------------------------------

    [Test]
    public void CorpusIsPresentWithPinnedCounts()
    {
        var manifest = Manifest();
        Assert.That(manifest.CategoriesByName.Keys,
            Is.EquivalentTo(CorpusManifest.PinnedCounts09.Keys));
        foreach (var (category, expected) in CorpusManifest.PinnedCounts09)
            Assert.That(manifest.CategoriesByName[category].Count, Is.EqualTo(expected),
                $"pinned count for {category}");
    }

    [Test]
    public void Diagnostics09ShipsExpectedLocationMetadata()
    {
        Manifest();
        int withLocation = 0;
        foreach (var f in Manifest().CategoriesByName["invalid"])
        {
            var oracle = Path.ChangeExtension(FixturePath("invalid", f), ".json");
            using var doc = JsonDocument.Parse(File.ReadAllText(oracle));
            if (doc.RootElement.TryGetProperty("expected_location", out _))
                withLocation++;
        }
        Assert.That(withLocation, Is.EqualTo(72),
            "the 0.9 diagnostics contract is 30 EOF + 42 malformed-UTF-8 fixtures with expected_location");
    }

    // ------------------------------------------------------------------
    // Case sources
    // ------------------------------------------------------------------

    private static IEnumerable<TestCaseData> Cases(string category)
    {
        Manifest();
        foreach (var f in Manifest().CategoriesByName[category])
        {
            var name = f.Path.Replace('\\', '/');
            yield return new TestCaseData(f).SetName($"{category}-09:{name}");
        }
    }

    public static IEnumerable<TestCaseData> ValidCases() => Cases("valid");
    public static IEnumerable<TestCaseData> InvalidCases() => Cases("invalid");
    public static IEnumerable<TestCaseData> UnrepresentableCases() => Cases("unrepresentable");
    public static IEnumerable<TestCaseData> ParseableUnrepresentableCases() => Cases("parseable-unrepresentable");
    public static IEnumerable<TestCaseData> StrictLossyCases() => Cases("strict-lossy");

    // ------------------------------------------------------------------
    // Runners
    // ------------------------------------------------------------------

    [TestCaseSource(nameof(ValidCases))]
    public void Valid(CorpusFixture fixture)
    {
        var ktavPath = FixturePath("valid", fixture);
        bool native = TestPaths.CabiBuilt();
        SpecConformance.ValidCore(ktavPath, native ? KtavBackend.Native : KtavBackend.Pure);
        SpecConformance.ValidCore(ktavPath, KtavBackend.Pure);

        // Formatter exercise (fixed point) on both backends.
        var src = File.ReadAllText(ktavPath);
        if (native)
        {
            string formatted = Ktav.Format(src, KtavBackend.Native);
            Assert.That(Ktav.Format(formatted, KtavBackend.Native), Is.EqualTo(formatted),
                $"format fixed point for {ktavPath}");
        }
        string formattedPure = Ktav.Format(src, KtavBackend.Pure);
        Assert.That(Ktav.Format(formattedPure, KtavBackend.Pure), Is.EqualTo(formattedPure),
            $"format fixed point (pure) for {ktavPath}");

        var canonicalPath = Path.ChangeExtension(ktavPath, ".canonical.ktav");
        if (File.Exists(canonicalPath))
            SpecConformance.ValidCanonicalCore(ktavPath, KtavBackend.Pure);
    }

    [TestCaseSource(nameof(InvalidCases))]
    public void Invalid(CorpusFixture fixture)
    {
        var ktavPath = FixturePath("invalid", fixture);
        var raw = File.ReadAllBytes(ktavPath);
        using var oracle = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(ktavPath, ".json")));
        var expected = oracle.RootElement;

        if (fixture.Flags.Contains("raw_bytes"))
        {
            // Malformed UTF-8: only the byte API can see it; the raw UTF-8
            // bytes go to BOTH backends' file-bytes entry points.
            foreach (var backend in Backends())
            {
                var error = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(raw, backend),
                    $"expected invalid-UTF-8 rejection for {ktavPath} ({backend})")!;
                Assert.That(error.Error, Is.EqualTo("InvalidUtf8"), $"error class for {ktavPath}");
                Assert.That(error.SpecSection, Is.EqualTo("§6.15"), $"spec section for {ktavPath}");
                Assert.That(error.Span.HasValue, Is.True, $"UTF-8 error span missing for {ktavPath}");
                SpecConformance.AssertInvalidOracle(error, expected, ktavPath);
                AssertLocation(error, expected, ktavPath, invalidUtf8: true);
            }
            return;
        }

        Assert.That(TryDecodeStrictUtf8(raw, out var src), Is.True,
            $"malformed UTF-8 fixture missing raw_bytes flag: {ktavPath}");
        foreach (var backend in Backends())
        {
            // String entry point…
            var err1 = Assert.Throws<KtavException>(() => Ktav.Loads(src, backend),
                $"expected parse error for {ktavPath} ({backend})")!;
            SpecConformance.AssertInvalidOracle(err1, expected, ktavPath);
            AssertLocation(err1, expected, ktavPath, invalidUtf8: false);
            // …and the byte entry point on the same re-encoded bytes.
            var err2 = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(Encoding.UTF8.GetBytes(src), backend),
                $"expected parse error (bytes) for {ktavPath} ({backend})")!;
            SpecConformance.AssertInvalidOracle(err2, expected, ktavPath);
            AssertLocation(err2, expected, ktavPath, invalidUtf8: false);
        }
    }

    [TestCaseSource(nameof(UnrepresentableCases))]
    public void Unrepresentable(CorpusFixture fixture) =>
        SpecConformance.UnrepresentableCore(FixturePath("unrepresentable", fixture),
            TestPaths.CabiBuilt() ? KtavBackend.Native : KtavBackend.Pure);

    [TestCaseSource(nameof(ParseableUnrepresentableCases))]
    public void ParseableUnrepresentable(CorpusFixture fixture) =>
        SpecConformance.ParseableUnrepresentableCore(FixturePath("parseable-unrepresentable", fixture),
            TestPaths.CabiBuilt() ? KtavBackend.Native : KtavBackend.Pure);

    [TestCaseSource(nameof(StrictLossyCases))]
    public void StrictLossy(CorpusFixture fixture) =>
        SpecConformance.StrictLossyCore(FixturePath("strict-lossy", fixture),
            TestPaths.CabiBuilt() ? KtavBackend.Native : KtavBackend.Pure);

    // ------------------------------------------------------------------
    // expected_location validation (line/start + exactly one end/minimum_end)
    // ------------------------------------------------------------------

    internal static void AssertLocation(KtavException error, JsonElement expected, string path, bool invalidUtf8)
    {
        if (!expected.TryGetProperty("expected_location", out var loc))
        {
            Assert.That(expected.TryGetProperty("expected_error", out _), Is.True,
                $"expected_error missing for {path}");
            return;
        }
        Assert.That(loc.ValueKind, Is.EqualTo(JsonValueKind.Object), $"expected_location type for {path}");

        Assert.That(loc.TryGetProperty("line", out var lineEl), Is.True, $"expected_location.line missing for {path}");
        Assert.That(lineEl.ValueKind, Is.EqualTo(JsonValueKind.Number), $"expected_location.line type for {path}");
        int line = lineEl.GetInt32();
        Assert.That(line, Is.GreaterThanOrEqualTo(1), $"expected_location.line for {path}");

        Assert.That(loc.TryGetProperty("start", out var startEl), Is.True, $"expected_location.start missing for {path}");
        long start = startEl.GetInt64();
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"expected_location.start for {path}");

        bool hasEnd = loc.TryGetProperty("end", out var endEl);
        bool hasMinimumEnd = loc.TryGetProperty("minimum_end", out var minEndEl);
        Assert.That(hasEnd ^ hasMinimumEnd, Is.True,
            $"expected_location must carry exactly one of end/minimum_end for {path}");
        Assert.That(error.Span.HasValue, Is.True, $"span missing for {path}");
        var span = error.Span!.Value;
        Assert.That(span.Start, Is.EqualTo(start), $"span.start for {path}");
        Assert.That(span.End, Is.GreaterThan(span.Start), $"span must be nonempty for {path}");
        if (hasEnd)
        {
            Assert.That(span.End, Is.EqualTo(endEl.GetInt64()), $"span.end for {path}");
        }
        else
        {
            Assert.That(span.End, Is.GreaterThanOrEqualTo(minEndEl.GetInt64()),
                $"span.end must reach minimum_end for {path}");
        }

        // Invalid UTF-8 never reports line text (no replacement-char
        // decoding of bytes that are not valid source).
        if (invalidUtf8)
            Assert.That(error.LineText, Is.Null, $"line_text must be null for invalid UTF-8: {path}");
    }

    private static IEnumerable<KtavBackend> Backends()
    {
        // The pure backend ALWAYS runs every fixture (no skips in the
        // pure-backend CI job); the native backend joins whenever a cabi
        // binary exists (full test job).
        if (TestPaths.CabiBuilt()) yield return KtavBackend.Native;
        yield return KtavBackend.Pure;
    }

    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

namespace Ktav.Tests;

[TestFixture]
public class Task39RegressionTests
{
    private const string Blocked = "a: 1\na.\"\\q\": 2\n";

    [Test]
    [Category("PureOnly")]
    public void PureMalformedLaterQuotedSegmentLosesToPathConflict() => AssertBlocked(KtavBackend.Pure);

    [Test]
    [Category("NativeOnly")]
    public void NativeMalformedLaterQuotedSegmentLosesToPathConflict() => AssertBlocked(KtavBackend.Native);

    private static void AssertBlocked(KtavBackend backend)
    {
        AssertConflict(() => Ktav.Loads(Blocked, backend));
        AssertConflict(() => Ktav.LoadsStrict(Blocked, backend));
        AssertConflict(() => Ktav.Format(Blocked, backend));
        AssertConflict(() => Ktav.CanonicalFromSource(Blocked, backend));
    }

    private static void AssertConflict(TestDelegate action)
    {
        var error = Assert.Throws<KtavException>(action)!;
        Assert.That(error.Error, Is.EqualTo("KeyPathConflict"));
        Assert.That(error.Path, Is.EqualTo(new[] { "a", "\"\\q\"" }));
    }

    [Test]
    [Category("PureOnly")]
    public void UnblockedMalformedQuotedKeyReportsBadEscapeAcrossPureOperations()
    {
        var src = "a.\"\\q\": 2\n";
        AssertBadEscape(() => Ktav.Loads(src, KtavBackend.Pure));
        AssertBadEscape(() => Ktav.LoadsStrict(src, KtavBackend.Pure));
        AssertBadEscape(() => Ktav.Format(src, KtavBackend.Pure));
        AssertBadEscape(() => Ktav.CanonicalFromSource(src, KtavBackend.Pure));
    }

    private static void AssertBadEscape(TestDelegate action)
    {
        var error = Assert.Throws<KtavException>(action)!;
        Assert.That(error.Error, Is.EqualTo("BadEscapeSequence"));
    }

    [TestCaseSource(nameof(RawUtf8Cases))]
    [Category("PureOnly")]
    public void PureRawUtf8FixturesUseCorpusOracles(string path) => AssertRawUtf8(path, KtavBackend.Pure);

    [TestCaseSource(nameof(RawUtf8Cases))]
    [Category("NativeOnly")]
    public void NativeRawUtf8FixturesUseCorpusOracles(string path) => AssertRawUtf8(path, KtavBackend.Native);

    private static void AssertRawUtf8(string path, KtavBackend backend)
    {
        var bytes = File.ReadAllBytes(path);
        var error = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend), path)!;
        Assert.That(error.Error, Is.EqualTo("InvalidUtf8"), path);
        Assert.That(error.Line, Is.Not.Null.And.GreaterThan(0), path);
        Assert.That(error.Body, Is.Not.Null.And.Not.Empty, path);
        Assert.That(error.Body, Does.StartWith("input is not valid UTF-8:"), path);
        Assert.That(error.Message, Does.Contain("first invalid byte sequence at byte offset "), path);
        Assert.That(error.Span.HasValue, Is.True, path);
        Assert.That(error.Span!.Value.Start, Is.LessThan(error.Span.Value.End), path);
        Assert.That(error.Span.Value.Start, Is.GreaterThanOrEqualTo(0), path);
        Assert.That(error.Span.Value.End, Is.LessThanOrEqualTo(bytes.Length), path);
        SpecConformance.InvalidCore(path, backend);
    }

    public static IEnumerable<TestCaseData> RawUtf8Cases()
    {
        var manifest = TestPaths.Corpus;
        var fixtures = manifest.CategoriesByName["invalid"]
            .Where(f => f.Flags.Contains("raw_bytes")).ToArray();
        Assert.That(fixtures, Has.Length.EqualTo(9), "raw-byte invalid fixture count");
        foreach (var fixture in fixtures)
            yield return new TestCaseData(Path.Combine(TestPaths.Spec, "invalid", fixture.Path.Replace('/', Path.DirectorySeparatorChar)))
                .SetName($"raw9:{fixture.Path}");
    }
}

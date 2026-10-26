using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>Exact-text native regressions ported from Rust reference
/// tests/suite/api/text/edit_mutate.rs (commit 861c64a8378242b87b66b7d7bfeb119996ce3643).
/// These explicitly pin Native: absence of the required ABI must fail, never skip/fallback.</summary>
[TestFixture]
[Category("NativeOnly")]
public sealed class NativeMutationRegressionTests
{
    private static KtavValue S(string value) => new KtavString(value);

    [TestCase(false)]
    [TestCase(true)]
    public void InsertTextualAndLiteralSegmentsPreserveExactOutput(bool segments)
    {
        Assert.That(segments
            ? Ktav.InsertValueSegments("a: 1\n## tail\n", new[] { "b" }, S("v"), KtavBackend.Native)
            : Ktav.InsertValue("a: 1\n## tail\n", "b", S("v"), KtavBackend.Native),
            Is.EqualTo("a: 1\n## tail\nb: v\n"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InsertCollisionReportsExactError(bool segments)
    {
        var error = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.InsertValueSegments("a: 1\n", new[] { "a" }, S("x"), KtavBackend.Native);
            else Ktav.InsertValue("a: 1\n", "a", S("x"), KtavBackend.Native);
        });
        Assert.That(error!.Error, Is.EqualTo("EditAlreadyExists"));
        Assert.That(error.Body, Is.Null);
        Assert.That(error.Line, Is.Null);
        Assert.That(error.Span, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemovePreservesCommentsAndExactOutput(bool segments)
    {
        var source = "## top\na: 1\n## mid\nb: 2\n## end\n";
        Assert.That(segments
            ? Ktav.RemoveValueSegments(source, new[] { "a" }, KtavBackend.Native)
            : Ktav.RemoveValue(source, "a", KtavBackend.Native),
            Is.EqualTo("## top\n## mid\nb: 2\n## end\n"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveMissingReportsExactError(bool segments)
    {
        var error = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.RemoveValueSegments("b: 1\n", new[] { "a" }, KtavBackend.Native);
            else Ktav.RemoveValue("b: 1\n", "a", KtavBackend.Native);
        });
        Assert.That(error!.Error, Is.EqualTo("EditNotFound"));
        Assert.That(error.Body, Is.Null);
        Assert.That(error.Line, Is.Null);
        Assert.That(error.Span, Is.Null);
    }

    [Test]
    public void RenameTextualPreservesOrderCommentsAndValue()
    {
        Assert.That(Ktav.RenameKey("## head\na: 1\nb: 2\n", "b", "x", KtavBackend.Native),
            Is.EqualTo("## head\na: 1\nx: 2\n"));
    }

    [Test]
    public void RenameSegmentsTreatsNewKeyAsLiteralDecodedText()
    {
        Assert.That(Ktav.RenameKeySegments("a: 1\n", new[] { "a" }, "x.y\\z", KtavBackend.Native),
            Is.EqualTo("\"x.y\\\\z\": 1\n"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameCollisionAndMissingUseExactErrors(bool segments)
    {
        KtavException? Collision() => Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.RenameKeySegments("a: 1\nb: 2\n", new[] { "a" }, "b", KtavBackend.Native);
            else Ktav.RenameKey("a: 1\nb: 2\n", "a", "b", KtavBackend.Native);
        });
        var collision = Collision();
        Assert.That(collision!.Error, Is.EqualTo("EditAlreadyExists"));
        Assert.That(collision.Body, Is.Null);
        Assert.That(collision.Line, Is.Null);
        Assert.That(collision.Span, Is.Null);

        var missing = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.RenameKeySegments("a: 1\n", new[] { "missing" }, "x", KtavBackend.Native);
            else Ktav.RenameKey("a: 1\n", "missing", "x", KtavBackend.Native);
        });
        Assert.That(missing!.Error, Is.EqualTo("EditNotFound"));
        Assert.That(missing.Body, Is.Null);
        Assert.That(missing.Line, Is.Null);
        Assert.That(missing.Span, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InsertMissingParentIsNotFoundAndArrayTargetUnsupported(bool segments)
    {
        var missing = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.InsertValueSegments("a: 1\n", new[] { "missing", "child" }, S("v"), KtavBackend.Native);
            else Ktav.InsertValue("a: 1\n", "missing.child", S("v"), KtavBackend.Native);
        });
        Assert.That(missing!.Error, Is.EqualTo("EditNotFound"));

        var unsupported = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.InsertValueSegments("list: [x]\n", new[] { "list", "1" }, S("v"), KtavBackend.Native);
            else Ktav.InsertValue("list: [x]\n", "list.1", S("v"), KtavBackend.Native);
        });
        Assert.That(unsupported!.Error, Is.EqualTo("EditUnsupported"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveArrayElementHasReferenceExactOutput(bool segments)
    {
        var output = segments
            ? Ktav.RemoveValueSegments("a: [1, 2, 3]\n", new[] { "a", "1" }, KtavBackend.Native)
            : Ktav.RemoveValue("a: [1, 2, 3]\n", "a.1", KtavBackend.Native);
        Assert.That(output, Is.EqualTo("a: [1, 3]\n"));
    }

    [Test]
    public void InvalidSourceErrorPrecedesMalformedPathAndReplacement()
    {
        var error = Assert.Throws<KtavException>(() =>
            Ktav.RenameKey("a: {\n b: [", "bad..path", "x", KtavBackend.Native));
        Assert.That(error!.Error, Is.EqualTo("UnclosedCompound"));
        Assert.That(error.LineText, Is.EqualTo(" b: ["));
    }

    [Test]
    public void TextualEscapedPathAddressesDottedPrefixSegment()
    {
        Assert.That(Ktav.RenameKey("x.y\\.z: 1\n", @"x.y\.z", "m", KtavBackend.Native),
            Is.EqualTo("x.m: 1\n"));
    }

    [Test]
    public void RenameSameKeyIsByteIdentical()
    {
        Assert.That(Ktav.RenameKey("k:: 042\n", "k", "k", KtavBackend.Native),
            Is.EqualTo("k:: 042\n"));
    }
}

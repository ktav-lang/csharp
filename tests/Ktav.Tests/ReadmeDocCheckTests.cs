// The README's examples, executed.
//
// rust puts its README snippets under test (#265); this does the same
// for .NET. The equivalent Go examples were both WRONG when first
// written — an error's spec_section guessed as §5.5 where the envelope
// reports §6.2, and a formatting example claiming a respelling the
// formatter does not perform. Prose that is never run drifts silently.
using System;
using NUnit.Framework;

namespace Ktav.Tests;

public class ReadmeDocCheckTests
{
    [Test]
    public void StructuredErrorExampleMatchesTheReadme()
    {
        var e = Assert.Throws<KtavException>(() => global::Ktav.Ktav.Loads("a: 1\na: 2\n"));
        Assert.That(e.Error, Is.EqualTo("DuplicateKey"));
        Assert.That(e.Line, Is.EqualTo(2));
        Assert.That(e.SpecSection, Is.EqualTo("§6.2"));
        Assert.That(e.Message, Is.Not.Empty);
    }

    // Task #303: Message must be the core's own rendering, taken verbatim
    // from the envelope's `message` field — not this binding's old
    // locally-reconstructed sentence (KtavException.cs used to build
    // "<error>: <body> at [<path>] ..."). Distinguishes the two by their
    // actual, different wording rather than just asserting non-empty.
    [Test]
    public void MessageIsTheCoresOwnRenderingNotAReconstructedSentence()
    {
        var e = Assert.Throws<KtavException>(() => global::Ktav.Ktav.LoadsStrict("version: 1.10\n"));
        Assert.That(e.Message, Does.Contain("would be inferred as a number and silently canonicalised"));
        Assert.That(e.Message, Does.Not.StartWith("LossyScalar:"));
    }

    [Test]
    public void FormatterExampleMatchesTheReadmeAndIsAFixedPoint()
    {
        const string input = "## why\na:   {x: 1}\n";
        const string expected = "## why\na: {\n    x: 1\n}\n";
        var once = global::Ktav.Ktav.Format(input);
        Assert.That(once, Is.EqualTo(expected));
        Assert.That(global::Ktav.Ktav.Format(once), Is.EqualTo(once));
    }

    // Task #311: the native library already exported ktav_canonical_from_source
    // (part of the standard nine-symbol set every cabi crate carries); this
    // binding never surfaced it. Proves it agrees with the two-step
    // EmitCanonical(Loads(src)) path it replaces, and drops trivia the way
    // canonical form must.
    [Test]
    public void CanonicalFromSourceAgreesWithEmitCanonicalOfLoads()
    {
        foreach (var src in new[]
                 {
                     "x: 1.0\n",
                     "x: 1e400\n",
                     "a.b: 1\nc: [1, 2]\n",
                     "x: 1.23456789012345678901\n",
                 })
        {
            var direct = global::Ktav.Ktav.CanonicalFromSource(src);
            var twoStep = global::Ktav.Ktav.EmitCanonical(global::Ktav.Ktav.Loads(src));
            Assert.That(direct, Is.EqualTo(twoStep), $"diverged for {src}");
        }
    }

    [Test]
    public void CanonicalFromSourceDropsCommentsAndBlankLines()
    {
        var output = global::Ktav.Ktav.CanonicalFromSource("## why\na: 1\n\n\nb: 2\n");
        Assert.That(output, Does.Not.Contain("##"));
        Assert.That(output, Does.Not.Contain("\n\n"));
    }
}

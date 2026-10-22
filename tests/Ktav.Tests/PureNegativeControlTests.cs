using System;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Negative controls for the major pure-parser states and boundaries,
/// all through the pure backend per-call overloads. Exact error
/// categories and byte spans are asserted wherever the spec pins them;
/// these complements — they do not replace — the corpus invalid fixtures.
/// </summary>
[TestFixture]
public class PureNegativeControlTests
{
    private const KtavBackend Pure = KtavBackend.Pure;

    private static KtavException Error(string src, bool strict = false) =>
        strict ? Assert.Throws<KtavException>(() => Ktav.LoadsStrict(src, Pure))!
               : Assert.Throws<KtavException>(() => Ktav.Loads(src, Pure))!;

    // -------- unclosed compounds (§6.1) --------

    [TestCase("a: [")]
    [TestCase("a: {")]
    [TestCase("a:\n    b: [")]
    public void UnclosedCompoundsSpanOpenerThroughEof(string src)
    {
        var e = Error(src);
        Assert.That(e.Error, Is.EqualTo("UnclosedCompound"), $"class for {src}");
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.1"), $"section for {src}");
        Assert.That(e.Span, Is.Not.Null, $"span for {src}");
        Assert.That(e.Span!.Value.Start, Is.LessThan(e.Span.Value.End));
        Assert.That(e.Span.Value.End, Is.EqualTo(Encoding.UTF8.GetByteCount(src)), $"span end at EOF for {src}");
    }

    [Test]
    public void ExplicitRootObjectAtEofIsRejected()
    {
        var e = Error("{\n");
        Assert.That(e.Error, Is.EqualTo("UnclosedCompound"));
        Assert.That(e.Span, Is.Not.Null);
    }

    // -------- bad escapes (§6.13) --------

    [TestCase(@"a: {k: \q}")]
    [TestCase(@"a: {k: \u12")]
    [TestCase(@"a: [1, \q]")]
    public void BadEscapeSequencesAreRejectedWithExactClass(string src)
    {
        var e = Error(src);
        Assert.That(e.Error, Is.EqualTo("BadEscapeSequence"), $"class for {src}");
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.13"), $"section for {src}");
        // PureError.BadEscapeSequence carries the offending sequence in the
        // BODY field (Reason is writer-time only and stays null at parse
        // time — see src/Ktav/Pure/PureError.cs).
        Assert.That(e.Body, Is.Not.Null.And.Not.Empty, $"sequence payload for {src}");
        Assert.That(e.Reason, Is.Null, $"reason must stay null at parse time for {src}");
    }

    [Test]
    public void LoneSurrogateEscapeIsRejected()
    {
        var e = Error(@"a: {k: \uD800}");
        Assert.That(e.Error, Is.EqualTo("BadEscapeSequence"));
    }

    // -------- duplicate keys / dotted-key conflicts (§6.2/§6.3) --------

    [Test]
    public void DuplicateKeyReportsClassSectionPathAndSpan()
    {
        var e = Error("a: 1\na: 2\n");
        Assert.That(e.Error, Is.EqualTo("DuplicateKey"));
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.2"));
        Assert.That(e.Line, Is.EqualTo(2));
        Assert.That(e.Span, Is.Not.Null);
    }

    [Test]
    public void InlineDuplicateKeyIsRejected()
    {
        var e = Error("a: {x: 1, x: 2}\n");
        Assert.That(e.Error, Is.EqualTo("DuplicateKey"));
    }

    [Test]
    public void DottedKeyLeafOccupiedIsKeyPathConflict()
    {
        var e = Error("a.b: 1\na.b.c: 2\n");
        Assert.That(e.Error, Is.EqualTo("KeyPathConflict"), $"message: {e.Message}");
    }

    [Test]
    public void DottedKeyLeafOccupiedIsDuplicateKey()
    {
        // Rust insert.rs:249–257: занятый конечный сегмент — DuplicateKey.
        var e = Error("a.b.c: 1\na.b: 2\n");
        Assert.That(e.Error, Is.EqualTo("DuplicateKey"));
        Assert.That(e.Path, Is.EqualTo(new[] { "a", "b" }));
    }

    // -------- inline depth limit (128) --------

    [Test]
    public void InlineDepthBeyondLimitIsRejectedButLimitIsNot()
    {
        var ok = "a: " + new string('[', 128) + new string(']', 128) + "\n";
        Assert.DoesNotThrow(() => Ktav.Loads(ok, Pure),
            "nesting exactly at the limit must parse");

        var tooDeep = "a: " + new string('[', 129) + new string(']', 129) + "\n";
        var e = Error(tooDeep);
        Assert.That(e.Error, Is.EqualTo("MalformedInlineCompound"));
        Assert.That(e.Message, Does.Contain("nesting depth exceeds limit (128)"));
    }

    // -------- root shapes (§5.0.1/§5.9.0) --------

    // Spec §5.0: "The root Value produced by parsing a document is either
    // an Object or an Array." §5.0.1 has NO rule that produces a bare
    // scalar root: a scalar first content line falls to §5.0.1 rule 7 and
    // becomes the first item of an implicit top-level ARRAY. A scalar root
    // is therefore only ever reachable from a CONSTRUCTED KtavValue via
    // the writer, never from source text.

    [Test]
    public void ScalarSourceParsesAsImplicitTopLevelArray()
    {
        var v = Ktav.Loads("42\n", Pure);
        Assert.That(v, Is.InstanceOf<KtavArray>(),
            "§5.0.1 rule 7: a bare scalar first line opens an implicit top-level Array");
        var items = ((KtavArray)v).Items;
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0], Is.EqualTo(new KtavInteger("42")),
            "array items are typed (§5.2 rule 13)");
    }

    [Test]
    public void ConstructedScalarRootIsRefusedByWriters()
    {
        // The ScalarRoot refusal is a WRITER-side check (§5.9.0: every
        // representable document root is a compound); it fires on a
        // constructed scalar Value, not on any parse of source text.
        KtavValue scalar = KtavInteger.Of(42);
        var dumpError = Assert.Throws<KtavException>(() => Ktav.Dumps(scalar, Pure))!;
        Assert.That(dumpError.Reason, Is.EqualTo("ScalarRoot"));
        var canonError = Assert.Throws<KtavException>(() => Ktav.EmitCanonical(scalar, Pure))!;
        Assert.That(canonError.Reason, Is.EqualTo("ScalarRoot"));
    }

    // -------- byte-level IO (BOM / CRLF / raw invalid UTF-8) --------

    [Test]
    public void BomIsSkippedButOffsetsStayOriginal()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF };
        bytes = Concat(bytes, Encoding.UTF8.GetBytes("a: 1\n"));
        var v = (KtavObject)Ktav.LoadsUtf8(bytes, Pure);
        Assert.That(v.TryGet("a"), Is.EqualTo(KtavInteger.Of(1)));

        // An error in a BOM'd document still reports ORIGINAL byte
        // offsets (BOM included), per §3.1.
        var bad = Concat(new byte[] { 0xEF, 0xBB, 0xBF }, Encoding.UTF8.GetBytes("a: 1\na: 2\n"));
        var e = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bad, Pure))!;
        Assert.That(e.Span, Is.Not.Null);
        Assert.That(e.Span!.Value.Start, Is.GreaterThanOrEqualTo(3),
            "span must be an original-byte offset, BOM not subtracted");
    }

    [Test]
    public void CrlfDocumentsParseAndLineTextsStripTrailingCr()
    {
        var e = Error("a: 1\r\na: [");
        Assert.That(e.Error, Is.EqualTo("UnclosedCompound"));
        Assert.That(e.LineText, Is.EqualTo("a: ["),
            "line_text must strip the trailing CR of the preceding CRLF");
    }

    [Test]
    public void RawInvalidUtf8RejectsBeforeGrammarWithNonemptySpan()
    {
        var bytes = Encoding.UTF8.GetBytes("a: ");
        bytes = Concat(bytes, new byte[] { 0xFF, 0xFE });
        var e = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, Pure))!;
        Assert.That(e.Error, Is.EqualTo("InvalidUtf8"));
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.15"));
        Assert.That(e.Span, Is.Not.Null);
        Assert.That(e.Span!.Value.Start, Is.LessThan(e.Span.Value.End),
            "span over malformed bytes must be nonempty");
        Assert.That(e.Span.Value.End, Is.LessThanOrEqualTo(bytes.Length));
    }

    [Test]
    public void TruncatedMultibyteAtEofIsInvalidUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("a: caf");
        bytes = Concat(bytes, new byte[] { 0xC3 }); // truncated é
        var e = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, Pure))!;
        Assert.That(e.Error, Is.EqualTo("InvalidUtf8"));
        Assert.That(e.Span, Is.Not.Null);
    }

    // -------- numeric boundaries (§3.6) --------

    [Test]
    public void OversizedIntegersFallToString()
    {
        var v = (KtavObject)Ktav.Loads("n: 9223372036854775808\n", Pure);
        Assert.That(v.TryGet("n"), Is.EqualTo(new KtavString("9223372036854775808")));
    }

    [Test]
    public void NegativeZeroFloatKeepsItsSpelling()
    {
        var v = (KtavObject)Ktav.Loads("z: -0.0\n", Pure);
        Assert.That(v.TryGet("z"), Is.EqualTo(new KtavFloat("-0.0")));
    }

    [Test]
    public void LeadingZerosForceString()
    {
        var v = (KtavObject)Ktav.Loads("a: 007\n", Pure);
        Assert.That(v.TryGet("a"), Is.EqualTo(new KtavString("007")));
    }

    [Test]
    public void StrictLossyBodyAndCanonicalAreExact()
    {
        var e = Error("version: 1.10\n", strict: true);
        Assert.That(e.Error, Is.EqualTo("LossyScalar"));
        Assert.That(e.Body, Is.EqualTo("1.10"));
        Assert.That(e.Canonical, Is.EqualTo("1.1"));

        // The i64-overflowing literal is NOT LossyScalar: per §5.2 rule 13
        // it falls through to rule 15 (String carrying the body as
        // written), and Rust's classify.rs only raises LossyScalar when a
        // successfully parsed Int/Float canonicalises to text different
        // from the source — an unparsable numeral never reaches that
        // comparison. Strict mode therefore ACCEPTS it as a String.
        var bigValue = (KtavObject)Ktav.LoadsStrict("n: 10000000000000000000\n", Pure);
        Assert.That(bigValue.TryGet("n"), Is.EqualTo(new KtavString("10000000000000000000")));
    }

    [Test]
    public void StrictRejectsCanonicalisingIntegerSpellings()
    {
        // The genuinely lossy integer spellings: hex/underscored forms
        // whose canonical decimal differs from the body (§5.2 rule 13 via
        // strict comparison).
        var e = Error("n: 0xFF\n", strict: true);
        Assert.That(e.Error, Is.EqualTo("LossyScalar"));
        Assert.That(e.Body, Is.EqualTo("0xFF"));
        Assert.That(e.Canonical, Is.EqualTo("255"));

        var u = Error("n: 1_000\n", strict: true);
        Assert.That(u.Error, Is.EqualTo("LossyScalar"));
        Assert.That(u.Body, Is.EqualTo("1_000"));
        Assert.That(u.Canonical, Is.EqualTo("1000"));
    }

    [Test]
    public void StrictAcceptsCanonicalSpellings()
    {
        var v = (KtavObject)Ktav.LoadsStrict("a: 1.5\nb: 1e10\n", Pure);
        Assert.That(v.TryGet("a"), Is.EqualTo(new KtavFloat("1.5")));
        Assert.That(v.TryGet("b"), Is.EqualTo(new KtavFloat("10000000000.0")));
    }

    // -------- implicit-root FIRST content line (§5.0.1 rules 7/8) --------
    //
    // The first content line of an implicit-root document IS the first
    // pair/array item — it must be consumed exactly once by the normal
    // dispatch, never swallowed by root classification. Each check asserts
    // the EXACT structure and EXACTLY one entry/item, so a dropped first
    // line (empty object/array) fails.

    private static void AssertExactlyOneEntry(KtavValue v, string key, KtavValue expected,
        string because)
    {
        var o = (KtavObject)v;
        Assert.That(o.Entries, Has.Count.EqualTo(1), because);
        Assert.That(o.Entries[0].Key, Is.EqualTo(key), because);
        Assert.That(o.Entries[0].Value, Is.EqualTo(expected), because);
    }

    private static void AssertExactlyOneItem(KtavValue v, KtavValue expected, string because)
    {
        var items = ((KtavArray)v).Items;
        Assert.That(items, Has.Count.EqualTo(1), because);
        Assert.That(items[0], Is.EqualTo(expected), because);
    }

    [Test]
    public void ImplicitRootFirstLineBecomesTheFirstPairOrItem()
    {
        // a: 1 → {a: 1} (implicit object; NOT an empty object)
        AssertExactlyOneEntry(Ktav.Loads("a: 1\n", Pure), "a", KtavInteger.Of(1),
            "implicit object root keeps its first pair");

        // foo → ["foo"] (implicit array; NOT an empty array)
        AssertExactlyOneItem(Ktav.Loads("foo\n", Pure), new KtavString("foo"),
            "implicit array root keeps its first item");

        // a.b: leaf → nested a→b→"leaf"
        var nested = (KtavObject)Ktav.Loads("a.b: leaf\n", Pure);
        Assert.That(nested.Entries, Has.Count.EqualTo(1));
        var inner = (KtavObject)nested.Entries[0].Value;
        Assert.That(nested.Entries[0].Key, Is.EqualTo("a"));
        Assert.That(inner.Entries, Has.Count.EqualTo(1));
        Assert.That(inner.Entries[0].Key, Is.EqualTo("b"));
        Assert.That(inner.Entries[0].Value, Is.EqualTo(new KtavString("leaf")));
    }

    [TestCase("a: 1\n", "a", "1")]
    [TestCase("foo\n", null, "foo")]
    [TestCase("a.b: leaf\n", "a", "leaf")]
    public void ImplicitRootSurvivesBomCommentAndMissingFinalNewline(
        string src, string? key, string body)
    {
        bool isArray = key == null;
        var utf8 = Encoding.UTF8.GetBytes(src);

        // (a) UTF-8 BOM prefix via LoadsUtf8.
        var bom = Concat(new byte[] { 0xEF, 0xBB, 0xBF }, utf8);
        AssertFirstLineIntact(Ktav.LoadsUtf8(bom, Pure), isArray, key, body, "BOM: " + src);

        // (b) a ## comment line before.
        AssertFirstLineIntact(Ktav.Loads("## c\n" + src, Pure), isArray, key, body,
            "comment: " + src);

        // (c) no trailing newline (LoadsUtf8 of bytes without the final LF).
        var noNl = Encoding.UTF8.GetBytes(src.TrimEnd('\n'));
        AssertFirstLineIntact(Ktav.LoadsUtf8(noNl, Pure), isArray, key, body,
            "no trailing newline: " + src);
    }

    private static void AssertFirstLineIntact(KtavValue v, bool isArray,
        string? key, string body, string because)
    {
        if (isArray)
        {
            var items = ((KtavArray)v).Items;
            Assert.That(items, Has.Count.EqualTo(1), because);
            Assert.That(items[0], Is.EqualTo(new KtavString(body)), because);
        }
        else if (body == "1")
        {
            AssertExactlyOneEntry(v, key!, KtavInteger.Of(1), because);
        }
        else
        {
            // dotted a.b: leaf — one entry whose value is a one-pair object
            var o = (KtavObject)v;
            Assert.That(o.Entries, Has.Count.EqualTo(1), because);
            Assert.That(o.Entries[0].Key, Is.EqualTo(key), because);
            var inner = (KtavObject)o.Entries[0].Value;
            Assert.That(inner.Entries, Has.Count.EqualTo(1), because);
            Assert.That(inner.Entries[0].Key, Is.EqualTo("b"), because);
            Assert.That(inner.Entries[0].Value, Is.EqualTo(new KtavString(body)), because);
        }
    }

    // -------- multiline dedent (§5.6 collecting.rs) --------
    //
    // The stripped form removes the longest COMMON leading-whitespace
    // prefix over non-blank lines, compared CODE POINT by code point —
    // NOT the minimum byte length of the leading runs. Every expectation
    // below is the exact resulting string (byte-compared), so both
    // under- and over-stripping fail.

    [Test]
    public void MultilineDedentCommonPrefixOnly()
    {
        // Both lines share the two-space prefix → stripped.
        AssertDedent("k: (\n  a\n  b\n)\n", "a\nb");
    }

    [Test]
    public void MultilineDedentEqualByteLengthsDifferentCodePointsStripsNothing()
    {
        // "   x" (3 ASCII spaces, 3 bytes) vs one U+3000 (3 bytes): equal
        // byte-run lengths but NO common whitespace code point, so the
        // dedent must strip NOTHING. The old min-byte-length algorithm
        // stripped 3 bytes from both lines — caught byte-for-byte here.
        AssertDedent("k: (\n   x\n\u3000y\n)\n", "   x\n\u3000y");
    }

    [Test]
    public void MultilineDedentMixedTabsAndSpacesDivergingPrefixStripsNothing()
    {
        // "\t\tx" (2 tabs) vs "  y" (2 spaces): common code-point prefix
        // is empty → keep both prefixes verbatim.
        AssertDedent("k: (\n\t\tx\n  y\n)\n", "\t\tx\n  y");
    }

    [Test]
    public void MultilineDedentBlankLinesDoNotConstrainThePrefix()
    {
        // The blank line contributes nothing and does not shrink the
        // common prefix; it stays an empty line in the result.
        AssertDedent("k: (\n  a\n\n  b\n)\n", "a\n\nb");
    }

    private static void AssertDedent(string doc, string expected)
    {
        var v = (KtavObject)Ktav.Loads(doc, Pure);
        var s = (KtavString)v.Entries[0].Value!;
        Assert.That(v.Entries, Has.Count.EqualTo(1));
        // Byte-for-byte: over-stripping (min byte length) and
        // under-stripping both change the encoded bytes.
        Assert.That(Encoding.UTF8.GetBytes(s.Value),
            Is.EqualTo(Encoding.UTF8.GetBytes(expected)),
            $"stripped multiline of {doc.Replace("\n", "\\n")}");
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }
}

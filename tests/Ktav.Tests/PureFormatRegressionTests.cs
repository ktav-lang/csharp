using System;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Formatter-focused (Ktav.Format → Pure) regressions for the round-G
/// fixes, which the round-G tests covered only through Loads/LoadsUtf8.
/// The formatter duplicates both the implicit-root fall-through
/// (PureFormatter.HandleLine) and the code-point dedent
/// (PureFmtTree.FinishMultiline), so these tests pin the same behaviour
/// through the public Format(string, KtavBackend) entry point.
///
/// Expected outputs are derived independently from the documented writer
/// rules, not from execution:
///  - an implicit root OBJECT emits its pairs at indent 0 with NO closer
///    (PureFormatter.EmitFormatted pushes KPairs(isRoot: true) and the
///    KPairs task never enqueues a KClose) → "a: 1\n" formats to itself;
///  - an implicit root ARRAY whose first item is not a compound is
///    unwrapped (ArrayRoot: needsWrap false → KItems(rootFirst: true));
///    the scalar item "foo" needs no raw marker and is not pair-shaped,
///    so it is emitted bare → "foo\n";
///  - a dotted key inserts nested block objects; a non-empty nested
///    object value emits ": {\n", pairs at indent+1 (4 spaces), and a
///    "}" closer at the opener's indent → "a: {\n    b: leaf\n}\n";
///  - comments are never dropped: the leading comment line is kept as
///    doc-leading trivia, re-emitted verbatim (trimmed text) at indent 0.
/// Every test also asserts the documented fixed point Format(out) == out.
/// </summary>
[TestFixture]
public class PureFormatRegressionTests
{
    private const KtavBackend Pure = KtavBackend.Pure;

    // -------- implicit-root first line through FORMAT (round-G item 1) ----

    [Test]
    public void FormatImplicitRootObjectFirstLineIsPreserved()
    {
        var formatted = Ktav.Format("a: 1\n", Pure);
        Assert.That(formatted, Is.EqualTo("a: 1\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
    }

    [Test]
    public void FormatImplicitRootArrayFirstItemIsPreserved()
    {
        var formatted = Ktav.Format("foo\n", Pure);
        Assert.That(formatted, Is.EqualTo("foo\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
    }

    [Test]
    public void FormatDottedKeyNestedBlockMatchesCanonicalShape()
    {
        var formatted = Ktav.Format("a.b: leaf\n", Pure);
        Assert.That(formatted, Is.EqualTo("a: {\n    b: leaf\n}\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
    }

    [Test]
    public void FormatPreservesLeadingCommentVerbatim()
    {
        // Leading trivia path: "## c" is doc-leading trivia (trimmed text
        // kept verbatim), emitted at indent 0 before the first pair; the
        // first line is then the round-G implicit-root fall-through case.
        var formatted = Ktav.Format("## c\na: 1\n", Pure);
        Assert.That(formatted, Is.EqualTo("## c\na: 1\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
    }

    // -------- multiline dedent through FORMAT (round-G item 5) ------------
    //
    // The formatter parses the multiline string with the SAME code-point
    // common-prefix dedent as the parser, then re-emits via §5.6
    // ChooseMultilineForm(preferStripped: false):
    //   StringNeedsMultiline is TRUE for both inputs below (the value
    //   starts with a whitespace code point).
    //   MlStripped is unsafe in BOTH cases: the chooser scans lines of the
    //   VALUE and finds every non-blank line indented (indentedLine),
    //   so strippedSafe = false.
    //   MlVerbatim is safe (no line's trimmed text is "))"), so the form
    //   is MlVerbatim → the pair emitter writes ": ((\n" + value + "\n"
    //   + indent + "))\n" (indent 0 here → no padding before "))").
    // The dedent strips nothing in either case, so the re-emitted body is
    // byte-identical to the source body.

    [Test]
    public void FormatMultilineEqualByteLengthDifferentCodePointsIsVerbatim()
    {
        // Dedent at parse: prefixes are "   " (3 ASCII spaces, 3 bytes) vs
        // one U+3000 (3 bytes) — different code points → common prefix is
        // 0 code points, content stays "   x\n\u3000y".
        var formatted = Ktav.Format("k: (\n   x\n\u3000y\n)\n", Pure);
        Assert.That(formatted, Is.EqualTo("k: ((\n   x\n\u3000y\n))\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
        // Content preservation: the decoded value of both documents is
        // byte-identical (the verbatim re-emission must not change it).
        var expected = Encoding.UTF8.GetBytes("   x\n\u3000y");
        var actual = Encoding.UTF8.GetBytes(
            ((KtavString)((KtavObject)Ktav.Loads(formatted, Pure)).Entries[0].Value).Value);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void FormatMultilineMixedTabsAndSpacesIsVerbatim()
    {
        // Dedent at parse: prefixes "\t\t" vs "  " diverge at the first
        // code point → common prefix 0, content stays "\t\tx\n  y".
        var formatted = Ktav.Format("k: (\n\t\tx\n  y\n)\n", Pure);
        Assert.That(formatted, Is.EqualTo("k: ((\n\t\tx\n  y\n))\n"));
        Assert.That(Ktav.Format(formatted, Pure), Is.EqualTo(formatted), "fixed point");
        var expected = Encoding.UTF8.GetBytes("\t\tx\n  y");
        var actual = Encoding.UTF8.GetBytes(
            ((KtavString)((KtavObject)Ktav.Loads(formatted, Pure)).Entries[0].Value).Value);
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------- BOM-prefixed MALFORMED source through FORMAT -----------------
    //
    // Formatter path, original-byte spans, BOM counted (never subtracted).
    // Ktav.Format(string) UTF-8-encodes (Ktav.cs:229-241); PureFormatter.
    // FormatCore skips the 3-byte BOM only when choosing the FIRST line
    // start (lineStart = bom, PureFormatter.cs:55-57), but every offset it
    // passes to the parser is an ORIGINAL byte offset, so errors surface
    // with BOM bytes counted. PureFormatter.Format converts PureError via
    // ToExceptionWithDerivedLine (original-byte spans; line_text via
    // PureUtf8.LineTextAt, which strips ONE trailing CR; Line via
    // PureUtf8.LineOfOffset for the EOF UnclosedCompound whose line is
    // null, and directly from the factory otherwise).

    [Test]
    public void FormatBomPrefixedUnclosedArraySpansOpenerToEofInOriginalBytes()
    {
        // formatter path, original-byte spans, BOM counted
        // Bytes: EF BB BF (0-2) | "a: 1" (3-6) | "\n" (7) | "a: [" (8-11).
        // Total = 3 + 4 + 1 + 4 = 12 bytes.
        // Line 1 [3,7): "a: 1" → implicit object root (PushObj(0)).
        // Line 2 [8,12): "a: [" → OpenArr → PushArr(tEnd - 1) = PushArr(11),
        //   so the recorded opener offset is the '[' at byte 11.
        // EOF = 12. Finish: stack depth 2 → UnclosedCompound("array", 11, 12)
        //   (PureFormatter.cs Finish). Line is null → ToExceptionWithDerivedLine
        //   computes Line = LineOfOffset(src, 11): the only LF is at byte 7
        //   (< 11) → line 2.
        var e = Assert.Throws<KtavException>(() => Ktav.Format("\uFEFFa: 1\na: [", Pure))!;
        Assert.That(e.Error, Is.EqualTo("UnclosedCompound"));
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.1"));
        Assert.That(e.Span, Is.Not.Null);
        Assert.That(e.Span!.Value.Start, Is.EqualTo(11), "opener '[' at 3(BOM)+5+3");
        Assert.That(e.Span!.Value.Start, Is.GreaterThanOrEqualTo(3), "BOM bytes counted, not subtracted");
        Assert.That(e.Span!.Value.End, Is.EqualTo(12), "EOF == 3+5+4 == total byte length");
        Assert.That(e.Line, Is.EqualTo(2), "LineOfOffset(11) after the single LF at byte 7");
        // LineTextAt(src, 11): line start after the LF at 7 → 8; no later LF;
        // no trailing CR → exactly "a: [".
        Assert.That(e.LineText, Is.EqualTo("a: ["));
    }

    [Test]
    public void FormatBomPrefixedCrlfMissingSeparatorUsesTrimmedOriginalByteWindow()
    {
        // formatter path, original-byte spans, BOM counted
        // Bytes: EF BB BF (0-2) | "a: 1" (3-6) | "\r" (7) "\n" (8)
        //        | "justword" (9-16) | "\r" (17) | "\n" (18). Total 19.
        // Line 1 [3,7): "a: 1" → implicit object root; lineNum = 1.
        // Line 2 starts at 3+4+2 = 9; the CRLF-aware scanner stops at the CR
        //   at byte 17, so the raw line window is [9,17); Trim is a no-op
        //   ("justword" has no leading/trailing whitespace) → tStart = 9,
        //   tEnd = 17 (the trimmed window; the trailing CR is NOT included).
        // ScanUnescapedColon over [9,17) finds no unescaped ':' →
        //   HandleObjectPair throws MissingSeparator(lineNum=2, 9, 17)
        //   (PureFormatter.cs:343, factory PureError.cs:119: §6.6, Line set
        //   directly from the formatter's 1-based lineNum — lineNum is the
        //   formatter loop counter starting at bom, so BOM does not shift it).
        var e = Assert.Throws<KtavException>(() => Ktav.Format("\uFEFFa: 1\r\njustword\r\n", Pure))!;
        Assert.That(e.Error, Is.EqualTo("MissingSeparator"));
        Assert.That(e.SpecSection, Is.EqualTo("\u00a76.6"));
        Assert.That(e.Line, Is.EqualTo(2));
        Assert.That(e.Span, Is.Not.Null);
        Assert.That(e.Span!.Value.Start, Is.EqualTo(9), "line-2 start = 3(BOM)+4+2(CRLF)");
        Assert.That(e.Span!.Value.End, Is.EqualTo(17), "9 + 8 chars of 'justword'");
        // LineTextAt(src, 9): line start after the LF at 8 → 9; next LF at 18;
        // one trailing CR at 17 stripped → exactly "justword".
        Assert.That(e.LineText, Is.EqualTo("justword"));
    }
}

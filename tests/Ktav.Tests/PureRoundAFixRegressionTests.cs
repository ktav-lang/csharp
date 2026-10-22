using System.Text;

using Ktav.Pure;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// CI proof for the fix-round-A changes (see plan "## fix round A"):
/// the ryu-pretty golden branch table, overflow-tolerant numeral
/// classification, and the iterative dotted-path materialiser. All
/// assertions are exact; every error expectation is a KtavException with
/// asserted fields — never a catch-all.
/// </summary>
[TestFixture]
public class PureRoundAFixRegressionTests
{
    private const KtavBackend Pure = KtavBackend.Pure;

    // -------- RyuFormat golden vectors (internal table in PureNum) --------

    [Test]
    public void RyuGoldenVectorsFormatExactly()
    {
        Assert.That(PureNum.RyuGoldenVectors, Is.Not.Empty,
            "the golden table must stay populated");
        foreach (var (name, value, expected) in PureNum.RyuGoldenVectors)
            Assert.That(PureNum.RyuFormat(value), Is.EqualTo(expected),
                $"RyuFormat golden vector '{name}'");
    }

    // -------- overflow numerals classify to String, never throw --------

    [TestCase("n: 99999999999999999999999999999\n")]
    [TestCase("n: 0xFFFFFFFFFFFFFFFFFF\n")]
    [TestCase("n: 0b1111111111111111111111111111111111111111111111111111111111111111111\n")]
    [TestCase("n: 0o777777777777777777777777\n")]
    [TestCase("n: 123456789012345678901234567890abc\n")]
    public void HugeNumericTokensParseAsStrings(string src)
    {
        KtavValue v = null!;
        Assert.DoesNotThrow(() => v = Ktav.Loads(src, Pure),
            "an i64-overflowing numeral must fall through to String, never OverflowException");
        var top = (KtavObject)v;
        var n = top.TryGet("n");
        Assert.That(n, Is.InstanceOf<KtavString>(), $"string classification for {src.Trim()}");
        Assert.That(((KtavString)n!).Value,
            Is.EqualTo(src.Substring(3).TrimEnd()), "the body must be carried verbatim");
    }

    [Test]
    public void HugeIntegersStillRoundTripThroughTheWriters()
    {
        const string huge = "99999999999999999999999999999";
        var text = Ktav.Dumps(new KtavObject(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, KtavValue>("v", new KtavString(huge)),
        }), Pure);
        Assert.That(text, Does.Contain(huge));
        var back = (KtavObject)Ktav.Loads(text, Pure);
        Assert.That(back.TryGet("v"), Is.EqualTo(new KtavString(huge)));
    }

    // -------- deep dotted key through the iterative materialiser --------

    [Test]
    public void VeryDeepDottedKeyMaterialisesIteratively()
    {
        // 50,000 segments — far beyond any host stack — through the PUBLIC
        // API: parse and exact structure walk only. NO Dumps here: the
        // writer's block form indents 4 spaces per nesting level, so a
        // 50,000-deep chain would produce ~10 billion output characters
        // (output size is inherently quadratic in depth). The bounded
        // writer-shape proof lives in BoundedChainDumpsExactNestedShape.
        const int depth = 50_000;
        var sb = new StringBuilder("root");
        for (int i = 0; i < depth; i++) sb.Append(".l").Append(i);
        sb.Append(": leaf\n");

        KtavValue parsed = null!;
        Assert.DoesNotThrow(() => parsed = Ktav.Loads(sb.ToString(), Pure),
            "deep dotted keys must materialise iteratively — no StackOverflowException");

        KtavValue cur = parsed;
        int walked = 0;
        while (cur is KtavObject o && o.Entries.Count == 1)
        {
            cur = o.Entries[0].Value;
            walked++;
        }
        Assert.That(walked, Is.EqualTo(depth + 1));
        Assert.That(((KtavString)cur).Value, Is.EqualTo("leaf"));
    }

    /// <summary>
    /// Writer-shape proof at a BOUNDED depth (the deep-parse test above
    /// covers the materialiser). Expected output derived independently
    /// from the documented writer rules (PureWriter.EmitPair / KClose /
    /// PushIndent): a non-empty block object emits
    /// `key: {` + newline, its pairs at indent+1, and a closer `}` line
    /// at the opener's indent; the 4-space indent unit means the output
    /// size is QUADRATIC in nesting depth (sum of 4*i per level) — that
    /// is inherent to the block format, not a writer defect, and this
    /// test keeps the case bounded (depth 40 → EXACTLY 6,732 chars; full
    /// arithmetic in the comment inside the test body).
    /// </summary>
    [Test]
    public void BoundedChainDumpsExactNestedShape()
    {
        // Tiny literal pin first: exact text of a 2-segment dotted chain.
        Assert.That(Ktav.Dumps(Ktav.Loads("a.b: leaf\n", Pure), Pure),
            Is.EqualTo("a: {\n    b: leaf\n}\n"));

        const int depth = 40; // dotted segments after "root"
        var sb = new StringBuilder("root");
        for (int i = 0; i < depth; i++) sb.Append(".l").Append(i);
        sb.Append(": leaf\n");
        var v = Ktav.Loads(sb.ToString(), Pure);

        // Independent expected construction from the writer rules.
        // Hand-derived structure of "root.l0...l{depth-1}: leaf": root,
        // l0, ..., l{depth-2} are BLOCK OBJECTS (depth openers) and the
        // innermost segment l{depth-1} maps DIRECTLY to the leaf String,
        // so there is NO `l{depth-1}: {` opener. Level j opener
        // (j = 0..depth-1): 4*j spaces + key_j + ": {" (key_0 = "root",
        // key_j = "l{j-1}"); the leaf line "l{depth-1}: leaf" sits at
        // 4*depth spaces; then `depth` closer lines "}" from j = depth-1
        // down to 0.
        //
        // Traces pinned by this construction:
        //   depth 0: "root: leaf\n"  (no openers, no closers)
        //   depth 1: "root: {\n    l0: leaf\n}\n"
        //   depth 2: "root: {\n    l0: {\n        l1: leaf\n    }\n}\n"
        //
        // Exact size at depth 40 (chars = bytes, ASCII only):
        //   openers j=0..39: indent 4*j sums to 4*(0+1+...+39) = 3,120;
        //     keys "root" (4) + "l0".."l9" (10 × 2) + "l10".."l38" (29 × 3);
        //     ": {\n" × 40 = 160          → 3,120 + 4 + 20 + 87 + 160 = 3,391
        //   leaf line: 4*40 = 160 spaces + "l39" (3) + ": leaf\n" (7)
        //                                                    =       170
        //   closers j=39..0: indent 3,120 + "}\n" × 40 = 80
        //                                                    =     3,200
        //   TOTAL = 3,391 + 170 + 3,200 = 6,761 (< the 10,000 bound).
        var expected = new StringBuilder();
        for (int j = 0; j < depth; j++)
        {
            expected.Append(' ', 4 * j).Append(j == 0 ? "root" : "l" + (j - 1)).Append(": {\n");
        }
        expected.Append(' ', 4 * depth).Append("l").Append(depth - 1).Append(": leaf\n");
        for (int j = depth - 1; j >= 0; j--)
        {
            expected.Append(' ', 4 * j).Append("}\n");
        }

        var dumped = Ktav.Dumps(v, Pure);
        Assert.That(dumped, Is.EqualTo(expected.ToString()),
            "the nested writer shape must match the documented indent/closer layout exactly");
        Assert.That(dumped.Length, Is.LessThan(10_000),
            "depth 40 must stay a bounded case (output is quadratic in depth by design)");

        // The dump re-parses back to the same chain structure.
        var reparsed = Ktav.Loads(dumped, Pure);
        KtavValue cur = reparsed;
        int walked = 0;
        while (cur is KtavObject o && o.Entries.Count == 1)
        {
            cur = o.Entries[0].Value;
            walked++;
        }
        Assert.That(walked, Is.EqualTo(depth + 1), "round trip must preserve depth");
        Assert.That(((KtavString)cur).Value, Is.EqualTo("leaf"));
    }

    // -------- parser errors are always KtavException, never internal --------

    [TestCase("a: [")]
    [TestCase("a: {k: \\q}")]
    [TestCase("a: 1\na: 2\n")]
    [TestCase("{\n")]
    public void ParseFailuresSurfaceAsKtavExceptionOnly(string src)
    {
        var e = Assert.Throws<KtavException>(() => Ktav.Loads(src, Pure))!;
        Assert.That(e.Error, Is.Not.Empty, "structured class must be present");
        Assert.That(e.Message.TrimStart(), Does.Not.StartWith("{"),
            "the message is the Rust Display text, not a JSON envelope");
    }

    [Test]
    public void FormatterFailuresAlsoSurfaceAsKtavException()
    {
        var e = Assert.Throws<KtavException>(() => Ktav.Format("a: {x: 1, x: 2}\n", Pure))!;
        Assert.That(e.Error, Is.EqualTo("DuplicateKey"));
    }
}

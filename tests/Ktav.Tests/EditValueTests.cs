using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Spec 0.9 lossless source edit: exact-output semantics and the full set
/// of layout regressions ported from the reference implementation
/// (tests/suite/api/text/edit_value/{mod,layout}.rs). Every case runs the
/// textual-path and the segments API against the pure backend and, when a
/// native library is present, the native backend too — both must agree
/// byte-for-byte.
/// </summary>
[TestFixture]
public class EditValueTests
{
    private static KtavValue S(string s) => new KtavString(s);

    private static KtavValue ParsePure(string src) => Ktav.Loads(src, KtavBackend.Pure);

    private delegate string EditCall(string src);

    /// <summary>Build the textual path from decoded segments (escape '.' and
    /// '\', mirror the Rust test helper).</summary>
    private static string Textual(IReadOnlyList<string> segments)
    {
        var parts = new List<string>();
        foreach (var s in segments)
            parts.Add(s.Replace("\\", "\\\\").Replace(".", "\\."));
        return string.Join(".", parts);
    }

    /// <summary>Expected tree: the original with exactly the target replaced.</summary>
    private static KtavValue ExpectedTree(string source, IReadOnlyList<string> path, KtavValue replacement)
    {
        var root = ParsePure(source);
        // Rebuild along the path (KtavValue is immutable).
        return Replace(root, path, 0, replacement)!;
    }

    private static KtavValue? Replace(KtavValue v, IReadOnlyList<string> path, int depth, KtavValue replacement)
    {
        string seg = path[depth];
        if (depth + 1 == path.Count)
        {
            if (v is KtavObject o)
            {
                var entries = new List<System.Collections.Generic.KeyValuePair<string, KtavValue>>();
                bool hit = false;
                foreach (var e in o.Entries)
                {
                    if (e.Key == seg) { entries.Add(new(seg, replacement)); hit = true; }
                    else entries.Add(e);
                }
                return hit ? new KtavObject(entries) : null;
            }
            if (v is KtavArray a)
            {
                int idx = int.Parse(seg);
                if (idx < 0 || idx >= a.Items.Count) return null;
                var items = new List<KtavValue>(a.Items);
                items[idx] = replacement;
                return new KtavArray(items);
            }
            return null;
        }
        if (v is KtavObject po)
        {
            var entries = new List<System.Collections.Generic.KeyValuePair<string, KtavValue>>();
            bool hit = false;
            foreach (var e in po.Entries)
            {
                if (e.Key == seg)
                {
                    var inner = Replace(e.Value, path, depth + 1, replacement);
                    if (inner == null) return null;
                    entries.Add(new(seg, inner));
                    hit = true;
                }
                else entries.Add(e);
            }
            return hit ? new KtavObject(entries) : null;
        }
        if (v is KtavArray pa)
        {
            int idx = int.Parse(seg);
            if (idx < 0 || idx >= pa.Items.Count) return null;
            var inner2 = Replace(pa.Items[idx], path, depth + 1, replacement);
            if (inner2 == null) return null;
            var items = new List<KtavValue>(pa.Items);
            items[idx] = inner2;
            return new KtavArray(items);
        }
        return null;
    }

    /// <summary>Run `edit` on both APIs; assert the exact expected text and
    /// that the result parses to the original tree with the target replaced.
    /// Pure first, then native when the cabi binary is available.</summary>
    private static void Check(string source, IReadOnlyList<string> path, KtavValue replacement, string expected)
    {
        var wanted = ExpectedTree(source, path, replacement);
        Assert.That(wanted, Is.Not.Null, "test path must address an existing value");
        var textual = Textual(path);

        RunBackend(KtavBackend.Pure, Ktav.EditValue(source, textual, replacement, KtavBackend.Pure));
        RunBackend(KtavBackend.Pure, Ktav.EditValueSegments(source, path, replacement, KtavBackend.Pure));
        if (TestPaths.CabiBuilt())
        {
            RunBackend(KtavBackend.Native, Ktav.EditValue(source, textual, replacement, KtavBackend.Native));
            RunBackend(KtavBackend.Native, Ktav.EditValueSegments(source, path, replacement, KtavBackend.Native));
        }

        void RunBackend(KtavBackend backend, string result)
        {
            Assert.That(result, Is.EqualTo(expected),
                $"edit output mismatch ({backend})\nsource:\n{source}");
            var reparsed = Ktav.Loads(result, backend);
            Assert.That(SpecConformance.ValueEquals(reparsed, wanted),
                $"reparsed tree mismatch ({backend})\nsource:\n{source}\nresult:\n{result}");
        }
    }

    // ------------------------------------------------------------------
    // Deep-leaf form preservation (regressions 1–2)
    // ------------------------------------------------------------------

    [Test]
    public void DeepLeafPreservesEachSourceForm()
    {
        foreach (var source in new[]
        {
            "targets.storagehost.address: old\ntargets.storagehost.user: mcp-agent\n",
            "targets: {storagehost: {address: old, user: mcp-agent}}\n",
            "targets: {\n  storagehost: {\n    address: old\n    user: mcp-agent\n  }\n}\n",
        })
        {
            Check(source,
                new[] { "targets", "storagehost", "address" },
                S("192.168.0.88:22"),
                source.Replace("old", "192.168.0.88:22"));
        }
    }

    [Test]
    public void DeepArrayLeafPreservesMixedForms()
    {
        foreach (var source in new[]
        {
            "targets: [{storagehost: [old, keep]}, other]\n",
            "targets: [\n  {storagehost: [old, keep]}\n  other\n]\n",
            "targets: [\n  {\n    storagehost: [\n      old\n      keep\n    ]\n  }\n  other\n]\n",
        })
        {
            Check(source,
                new[] { "targets", "0", "storagehost", "0" },
                S("new"),
                source.Replace("old", "new"));
        }
    }

    // ------------------------------------------------------------------
    // Whole inline containers / emptied block containers (3–4)
    // ------------------------------------------------------------------

    [Test]
    public void WholeInlineContainersRemainInline()
    {
        Check("a: {x: 1}\n", new[] { "a" }, ParsePure("{y: 2}"), "a: {y: 2}\n");
        Check("a: [1, 2]\n", new[] { "a" }, ParsePure("[3, 4]"), "a: [3, 4]\n");
        Check("a: {}\n", new[] { "a" }, ParsePure("{y: 2}"), "a: {y: 2}\n");
        Check("a: []\n", new[] { "a" }, ParsePure("[3, 4]"), "a: [3, 4]\n");
        Check("a: {}\n", new[] { "a" }, ParsePure("[3, 4]"), "a: [3, 4]\n");
    }

    [Test]
    public void EmptyingBlockContainersDoesNotCollapseThem()
    {
        Check("a: {\n  x: 1\n}\n", new[] { "a" }, ParsePure("{}"), "a: {\n}\n");
        Check("a: [\n  1\n]\n", new[] { "a" }, ParsePure("[]"), "a: [\n]\n");
    }

    // ------------------------------------------------------------------
    // Inline expansion (5–8)
    // ------------------------------------------------------------------

    [Test]
    public void NewlineUnfoldsOnlyInlineAncestorChain()
    {
        Check(
            "## top\na: [{x: old, untouched: [1, 2]}, {keep: yes}]\nz: keep\n",
            new[] { "a", "0", "x" }, S("one\ntwo"),
            "## top\na: [\n    {\n        x: ((\none\ntwo\n        ))\n        untouched: [1, 2]\n    }\n    {keep: yes}\n]\nz: keep\n");
    }

    [Test]
    public void NewlineUnderDottedKeyDoesNotExpandDottedAncestors()
    {
        Check(
            "a.b: [old, keep]\na.c: other\n",
            new[] { "a", "b", "0" }, S("one\ntwo"),
            "a.b: [\n    ((\none\ntwo\n    ))\n    keep\n]\na.c: other\n");
    }

    [Test]
    public void DenseEscapesUnfoldButSparseEscapesDoNot()
    {
        Check("a: [old, keep]\n", new[] { "a", "0" }, S("a,b,c,d,e"),
            "a: [\n    a,b,c,d,e\n    keep\n]\n");
        Check("a: [old]\n", new[] { "a", "0" }, S("a,b,c,d"),
            "a: [a\\,b\\,c\\,d]\n");
        string sparse = new string('x', 30) + "a,b,c,d,e";
        Check("a: [old]\n", new[] { "a", "0" }, S(sparse),
            "a: [" + sparse.Replace(",", "\\,") + "]\n");
    }

    [Test]
    public void WidthBoundaryIsInCharactersNotBytes()
    {
        foreach (var (size, expanded) in new[] { (95, false), (96, true) })
        {
            var value = new string('я', size);
            var expected = expanded
                ? $"a: [\n    {value}\n]\n"
                : $"a: [{value}]\n";
            Check("a: [old]\n", new[] { "a", "0" }, S(value), expected);
        }
    }

    [Test]
    public void AlreadyLongLineOnlyUnfoldsWhenItGrows()
    {
        var source = $"a: [{new string('x', 120)}, keep]\n";
        Check(source, new[] { "a", "0" }, S(new string('y', 120)),
            source.Replace('x', 'y'));
        Check(source, new[] { "a", "0" }, S(new string('y', 121)),
            $"a: [\n    {new string('y', 121)}\n    keep\n]\n");
    }

    [Test]
    public void WidthExpansionStopsWhenInnerLineFits()
    {
        var value = new string('x', 91);
        Check("a: [{x: old}]\n", new[] { "a", "0", "x" }, S(value),
            $"a: [\n    {{x: {value}}}\n]\n");
    }

    [Test]
    public void EscapeOverheadBoundaryIsExact()
    {
        var dense = new string('x', 7) + "a,b,c,d,e";
        var sparse = new string('x', 8) + "a,b,c,d,e";
        Check("a: [old]\n", new[] { "a", "0" }, S(dense), $"a: [\n    {dense}\n]\n");
        Check("a: [old]\n", new[] { "a", "0" }, S(sparse), $"a: [{sparse.Replace(",", "\\,")}]\n");
    }

    [Test]
    public void LongChangedLeafDoesNotUnfoldUnrelatedNestedBranches()
    {
        var value = new string('x', 100);
        Check("a: [old, {untouched: [1, 2]}]\n", new[] { "a", "0" }, S(value),
            $"a: [\n    {value}\n    {{untouched: [1, 2]}}\n]\n");
    }

    // ------------------------------------------------------------------
    // Promoted siblings / pairs / root (9–12)
    // ------------------------------------------------------------------

    [Test]
    public void PromotedSiblingsKeepValuesWhenInlineEscapesLoseMeaning()
    {
        Check(
            "a: [old, a\\,b, \\u00342, :: literal]\n",
            new[] { "a", "0" }, S("x\ny"),
            "a: [\n    ((\nx\ny\n    ))\n    a,b\n    :: 42\n    :: :: literal\n]\n");
    }

    [Test]
    public void PromotedPairsKeepKeySpellingAndRawMarkers()
    {
        Check(
            "a: {\"x.y\": old, nested.key::  42, n: 0007}\n",
            new[] { "a", "x.y" }, S("x\ny"),
            "a: {\n    \"x.y\": ((\nx\ny\n    ))\n    nested.key::  42\n    n: 0007\n}\n");
    }

    [Test]
    public void RootInlinePromotionPreservesBomAndEof()
    {
        Check(
            "\uFEFF[old, keep]",
            new[] { "0" }, S("x\ny"),
            "\uFEFF[\n    ((\nx\ny\n    ))\n    keep\n]");
    }

    [Test]
    public void PromotionPreservesEolCommentsAndNoFinalTerminator()
    {
        foreach (var eol in new[] { "\n", "\r", "\r\n" })
        {
            var source = $"## header{eol}a: [old, keep]";
            var expected =
                $"## header{eol}a: [{eol}    (({eol}x{eol}y{eol}    )){eol}    keep{eol}]";
            Check(source, new[] { "a", "0" }, S("x\ny"), expected);
        }
    }

    [Test]
    public void CommentsOutsidePromotedInlineValueRemainByteExact()
    {
        Check(
            "list: [\n  ## before\n  {x: old}\n  ## after\n]\n",
            new[] { "list", "0", "x" }, S("x\ny"),
            "list: [\n  ## before\n  {\n      x: ((\nx\ny\n      ))\n  }\n  ## after\n]\n");
    }

    [Test]
    public void NoopIgnoresAllReadabilityThresholds()
    {
        var source = $"a: [{new string('x', 140)}, a\\,b\\,c\\,d\\,e]\n";
        Check(source, new[] { "a", "0" }, S(new string('x', 140)), source);
        Check(source, new[] { "a", "1" }, S("a,b,c,d,e"), source);
    }

    [Test]
    public void TypeChangeDoesNotForceUnrelatedLayoutChanges()
    {
        Check("a: {x: [1], keep: [2]}\n", new[] { "a", "x" }, KtavBool.True,
            "a: {x: true, keep: [2]}\n");
        Check("a: [1]\n", new[] { "a" }, S("42"), "a:: 42\n");
    }

    [Test]
    public void ReopenedInlineParentPromotesOnlyItsPhysicalDefinition()
    {
        Check(
            "a: {x: old}\n## gap\na.y: keep\n",
            new[] { "a", "x" }, S("x\ny"),
            "a: {\n    x: ((\nx\ny\n    ))\n}\n## gap\na.y: keep\n");
    }

    [Test]
    public void ImpossibleBlockEncodingKeepsValidInlineForm()
    {
        foreach (var value in new[] { "a\rb", "a\n))\nb" })
        {
            var encoded = value.Replace("\r", "\\r").Replace("\n", "\\n");
            Check("a: [old]\n", new[] { "a", "0" }, S(value), $"a: [{encoded}]\n");
        }
        Check(
            "a: [old, a\\rb]\n",
            new[] { "a", "0" }, S("x\ny"),
            "a: [x\\ny, a\\rb]\n");
    }

    // ------------------------------------------------------------------
    // Fail-closed comment loss (the last layout regression)
    // ------------------------------------------------------------------

    [Test]
    public void UnsafeCommentLossStillFailsClosed()
    {
        foreach (var source in new[] { "a: {\n ## keep\n x: 1\n}\n", "a: [\n ## keep\n 1\n]\n" })
        {
            foreach (var backend in new[] { KtavBackend.Pure, KtavBackend.Native })
            {
                if (backend == KtavBackend.Native && !TestPaths.CabiBuilt()) continue;
                var ex = Assert.Throws<KtavException>(
                    () => Ktav.EditValueSegments(source, new[] { "a" }, KtavNull.Instance, backend),
                    $"expected Unsupported for {source} ({backend})")!;
                Assert.That(ex.Error, Is.EqualTo("EditUnsupported"), $"error class ({backend})");
                Assert.That(ex.Body, Is.Not.Null.And.Not.Empty, $"reason ({backend})");
            }
        }
    }

    // ------------------------------------------------------------------
    // Path / lookup errors
    // ------------------------------------------------------------------

    [Test]
    public void InvalidPathErrorsCarryUtf8ByteOffsets()
    {
        var cases = new (string Path, int Offset)[]
        {
            ("", 0),              // empty path
            (".", 0),             // empty first segment
            ("a..b", 2),          // empty middle segment
            ("a.", 2),            // trailing dot
            ("a\\qb", 1),         // bad escape
            ("a\\", 1),           // trailing lone backslash
            ("ключ\\qb", 8),       // offset counts UTF-8 bytes ('ключ' = 8 bytes)
        };
        foreach (var (path, offset) in cases)
        {
            var ex = Assert.Throws<KtavException>(
                () => Ktav.EditValue("a: 1\n", path, S("x"), KtavBackend.Pure),
                $"expected EditInvalidPath for '{path}'")!;
            Assert.That(ex.Error, Is.EqualTo("EditInvalidPath"), $"error class for '{path}'");
            Assert.That(ex.Body, Is.EqualTo(offset.ToString()), $"body offset for '{path}'");
            Assert.That(ex.Line.HasValue, Is.False, $"line for '{path}'");
            Assert.That(ex.Span.HasValue, Is.False, $"span for '{path}'");
        }
    }

    [Test]
    public void NotFoundErrorsAreDistinct()
    {
        foreach (var (source, segments) in new (string, string[])[]
        {
            ("a: 1\n", new[] { "b" }),
            ("a: 1\n", new[] { "a", "b" }),   // scalar descent
            ("a: [1]\n", new[] { "a", "1" }), // index overflow
            ("a: [1]\n", new[] { "a", "-1" }),// signs are not indexes
            ("a: [1]\n", new[] { "a", "x" }), // non-index in array
            ("{}\n", Array.Empty<string>()),  // empty segment list
        })
        {
            var ex = Assert.Throws<KtavException>(
                () => Ktav.EditValueSegments(source, segments, S("x"), KtavBackend.Pure),
                $"expected EditNotFound for {source}")!;
            Assert.That(ex.Error, Is.EqualTo("EditNotFound"), $"error class for {source}");
            Assert.That(ex.Body, Is.Null, $"body for {source}");
        }
    }

    [Test]
    public void NoncontiguousTargetsAreUnsupported()
    {
        // Whole `a` is dotted-key assembled: `a.b` here and `a.c` later.
        var ex = Assert.Throws<KtavException>(
            () => Ktav.EditValueSegments("a.b: 1\na.c: 2\n", new[] { "a" }, ParsePure("{b: 3, c: 4}"),
                KtavBackend.Pure))!;
        Assert.That(ex.Error, Is.EqualTo("EditUnsupported"));
        // Editable leaves inside the same target still work. String "9" is
        // numeric-like: the raw marker preserves its String identity
        // (mirror of the gold `\u00342` → `:: 42` layout regression);
        // an Integer replacement keeps the plain marker.
        Check("a.b: 1\na.c: 2\n", new[] { "a", "b" }, S("9"), "a.b:: 9\na.c: 2\n");
        Check("a.b: 1\na.c: 2\n", new[] { "a", "b" }, new KtavInteger("9"),
            "a.b: 9\na.c: 2\n");
    }

    [Test]
    public void SourceMustParseFirstWithRealEnvelopes()
    {
        var ex = Assert.Throws<KtavException>(
            () => Ktav.EditValue("a: {\n", "a", S("x"), KtavBackend.Pure))!;
        Assert.That(ex.Error, Is.EqualTo("UnclosedCompound"), "parse envelope must surface unchanged");
    }

    /// <summary>
    /// Source-first error priority (contract: UTF-8 validate + parse run
    /// BEFORE path/replacement transport decode): a broken source must win
    /// over an invalid textual path, over a NotFound path, and over the
    /// empty-segments NotFound — with its real 0.9 envelope, not an
    /// Edit* error invented from the path side.
    /// </summary>
    [Test]
    public void BrokenSourceWinsOverBadPathAndEmptySegments()
    {
        const string broken = "a: {\n"; // UnclosedCompound

        // broken source + INVALID textual path → parse envelope, not EditInvalidPath
        var ex1 = Assert.Throws<KtavException>(
            () => Ktav.EditValue(broken, "a\\q", S("x"), KtavBackend.Pure))!;
        Assert.That(ex1.Error, Is.EqualTo("UnclosedCompound"), "source error must precede path decoding");

        // broken source + NotFound path → parse envelope, not EditNotFound
        var ex2 = Assert.Throws<KtavException>(
            () => Ktav.EditValue(broken, "missing.key", S("x"), KtavBackend.Pure))!;
        Assert.That(ex2.Error, Is.EqualTo("UnclosedCompound"));

        // broken source + EMPTY segments → parse envelope, not EditNotFound
        var ex3 = Assert.Throws<KtavException>(
            () => Ktav.EditValueSegments(broken, Array.Empty<string>(), S("x"), KtavBackend.Pure))!;
        Assert.That(ex3.Error, Is.EqualTo("UnclosedCompound"));

        // broken source + numeric payload → parse envelope, not a
        // transport-decode result (pure normalises AFTER the source parse;
        // native must not pre-normalise either — core order is source-first)
        var ex4 = Assert.Throws<KtavException>(
            () => Ktav.EditValue(broken, "a", new KtavInteger("+0001"), KtavBackend.Pure))!;
        Assert.That(ex4.Error, Is.EqualTo("UnclosedCompound"), "source error precedes payload normalisation");
        var ex5 = Assert.Throws<KtavException>(
            () => Ktav.EditValue(broken, "a", new KtavFloat("12x"), KtavBackend.Pure))!;
        Assert.That(ex5.Error, Is.EqualTo("UnclosedCompound"),
            "source error precedes even an INVALID payload's Message envelope");

        // valid source + empty segments → still EditNotFound (order matters
        // both ways: the empty-segments rule survives on a parseable source)
        var ex6 = Assert.Throws<KtavException>(
            () => Ktav.EditValueSegments("a: 1\n", Array.Empty<string>(), S("x"), KtavBackend.Pure))!;
        Assert.That(ex6.Error, Is.EqualTo("EditNotFound"));

        // malformed UTF-8 bytes + empty segments → InvalidUtf8 at the byte
        // boundary (raw-bytes entry point; UTF-8 validation is part of the
        // same source-first phase).
        var raw = new byte[] { (byte)'a', (byte)':', (byte)' ', 0x80, (byte)'\n' };
        var ex7 = Assert.Throws<KtavException>(
            () => global::Ktav.Pure.PureEdit.ApplySegments(raw, Array.Empty<string>(), S("x")))!;
        Assert.That(ex7.Error, Is.EqualTo("InvalidUtf8"), "UTF-8 validation must precede the segments check");
        Assert.That(ex7.Span!.Value.Start, Is.EqualTo(3), "first invalid byte offset");
    }

    /// <summary>
    /// Semantic-verification regressions (parent review): a REAL leaf change
    /// with untouched siblings must pass verification (the verifier compares
    /// the rebuilt, target-masked trees — comparing the untouched originals
    /// rejected every actual replacement), and a replacement whose object
    /// lists the same pairs in reverse order is an equal value, so the edit
    /// is a byte-identical no-op (IndexMap order-insensitive PartialEq).
    /// </summary>
    [Test]
    public void RealLeafChangePassesVerification()
    {
        // String "9" is numeric-like → raw marker keeps String identity;
        // Integer "9" keeps the plain marker (no grammar/encoder change).
        Check("a: 1\nb: 2\nc: 3\n", new[] { "b" }, S("9"),
            "a: 1\nb:: 9\nc: 3\n");
        Check("a: 1\nb: 2\nc: 3\n", new[] { "b" }, new KtavInteger("9"),
            "a: 1\nb: 9\nc: 3\n");
        // Physically inline array item: `::` is literal data there, so the
        // String provenance survives via the \uXXXX escape of the first
        // scalar character (mirror of inline_body(replacement, true)).
        Check("outer: {inner: [1, old, 3], keep: yes}\n",
            new[] { "outer", "inner", "1" }, S("2"),
            "outer: {inner: [1, \\u0032, 3], keep: yes}\n");
        Check("outer: {inner: [1, old, 3], keep: yes}\n",
            new[] { "outer", "inner", "1" }, new KtavInteger("2"),
            "outer: {inner: [1, 2, 3], keep: yes}\n");
    }

    [Test]
    public void ReversedObjectReplacementIsByteIdenticalNoop()
    {
        // Dedicated helper: the tree oracle below is order-sensitive (the
        // CONTRACT is not), so the no-op is asserted byte-for-byte instead.
        void CheckNoop(string source, IReadOnlyList<string> path, KtavValue replacement)
        {
            var textual = Textual(path);
            Assert.That(Ktav.EditValue(source, textual, replacement, KtavBackend.Pure),
                Is.EqualTo(source), "no-op (pure, textual)");
            Assert.That(Ktav.EditValueSegments(source, path, replacement, KtavBackend.Pure),
                Is.EqualTo(source), "no-op (pure, segments)");
            if (TestPaths.CabiBuilt())
            {
                Assert.That(Ktav.EditValue(source, textual, replacement, KtavBackend.Native),
                    Is.EqualTo(source), "no-op (native, textual)");
                Assert.That(Ktav.EditValueSegments(source, path, replacement, KtavBackend.Native),
                    Is.EqualTo(source), "no-op (native, segments)");
            }
        }

        var reversed = ((KtavObject)ParsePure("a: {y: 2, x: 1}")).TryGet("a")!;
        CheckNoop("a: {x: 1, y: 2}\n", new[] { "a" }, reversed);
        // Nested: the order-insensitive equality recurses.
        var reversedNested = ((KtavObject)ParsePure("a: {n: {q: 2, p: 1}, z: 0}")).TryGet("a")!;
        CheckNoop("a: {z: 0, n: {p: 1, q: 2}}\n", new[] { "a" }, reversedNested);
    }

    /// <summary>
    /// Wire-transport parity regressions (parent review): a tagged numeric
    /// payload is canonicalised at the edit boundary exactly like the
    /// native cabi's wire decode (cabi/wire.rs) — "+0001" == "1", "1.50"
    /// == "1.5", signed zero collapses, exponent spellings re-render as
    /// shortest round-trip decimals, and payloads inside the i64 source
    /// domain pass through WITHOUT truncation (9007199254740993 is above
    /// 2^53 yet a legal §5.2 Integer). A noncanonical typed payload
    /// against an already-canonical source is therefore a byte-identical
    /// no-op on BOTH backends (previously: native no-op, pure Unsupported).
    /// </summary>
    [Test]
    public void NoncanonicalTypedPayloadIsByteIdenticalNoop()
    {
        void CheckNoop(string source, string segment, KtavValue replacement)
        {
            Assert.That(Ktav.EditValue(source, segment, replacement, KtavBackend.Pure),
                Is.EqualTo(source), "no-op (pure, textual)");
            Assert.That(Ktav.EditValueSegments(source, new[] { segment }, replacement, KtavBackend.Pure),
                Is.EqualTo(source), "no-op (pure, segments)");
            if (TestPaths.CabiBuilt())
            {
                Assert.That(Ktav.EditValue(source, segment, replacement, KtavBackend.Native),
                    Is.EqualTo(source), "no-op (native, textual)");
                Assert.That(Ktav.EditValueSegments(source, new[] { segment }, replacement, KtavBackend.Native),
                    Is.EqualTo(source), "no-op (native, segments)");
            }
        }

        CheckNoop("n: 1\n", "n", new KtavInteger("+0001"));
        CheckNoop("f: 1.5\n", "f", new KtavFloat("1.50"));
        CheckNoop("z: 0\n", "z", new KtavInteger("-0"));           // signed zero → "0"
        CheckNoop("e: 1500.0\n", "e", new KtavFloat("1.5e3"));     // exponent → "1500.0"
        // Above 2^53, inside i64: exact integer semantics survive the wire
        // round trip (a double-based pipeline would lose the low bits).
        CheckNoop("n: 9007199254740993\n", "n", new KtavInteger("09007199254740993"));
    }

    /// <summary>
    /// §5.2 domain negative control: a 29-digit Integer payload passes the
    /// wire untouched (arbitrary precision is the point of `$i`), but the
    /// source-level Integer domain is i64 — such a value would silently
    /// reclassify as a String on rewrite. The edit guard must refuse with
    /// EditUnsupported (pure) / the native core must fail closed the same
    /// way — never a "successful" edit that turns the Integer into a
    /// String, never a wire-level truncation.
    /// </summary>
    [Test]
    public void BignumIntegerOutsideI64DomainIsUnsupported()
    {
        const string source = "big: 99999999999999999999999999999\n"; // §5.2 rule 5: String
        var replacement = new KtavInteger("09999999999999999999999999999");

        var ex1 = Assert.Throws<KtavException>(
            () => Ktav.EditValue(source, "big", replacement, KtavBackend.Pure))!;
        Assert.That(ex1.Error, Is.EqualTo("EditUnsupported"), "pure textual");
        var ex2 = Assert.Throws<KtavException>(
            () => Ktav.EditValueSegments(source, new[] { "big" }, replacement, KtavBackend.Pure))!;
        Assert.That(ex2.Error, Is.EqualTo("EditUnsupported"), "pure segments");

        if (TestPaths.CabiBuilt())
        {
            var ex3 = Assert.Throws<KtavException>(
                () => Ktav.EditValue(source, "big", replacement, KtavBackend.Native))!;
            Assert.That(ex3.Error, Is.EqualTo("EditUnsupported"), "native textual (core fail-closed)");
            var ex4 = Assert.Throws<KtavException>(
                () => Ktav.EditValueSegments(source, new[] { "big" }, replacement, KtavBackend.Native))!;
            Assert.That(ex4.Error, Is.EqualTo("EditUnsupported"), "native segments (core fail-closed)");
        }
    }

    [Test]
    public void MalformedNumericPayloadsAreMessageEnvelopes()
    {
        foreach (var replacement in new KtavValue[]
        {
            new KtavInteger("12x"),
            new KtavInteger(""),
            new KtavFloat("abc"),
            new KtavFloat("42"),   // no '.' / exponent → not a float wire payload
        })
        {
            var ex1 = Assert.Throws<KtavException>(
                () => Ktav.EditValue("a: 1\n", "a", replacement, KtavBackend.Pure))!;
            Assert.That(ex1.Error, Is.EqualTo("Message"), $"error class for {replacement}");
            Assert.That(ex1.Line.HasValue, Is.False, $"no invented line for {replacement}");
            Assert.That(ex1.Span.HasValue, Is.False, $"no invented span for {replacement}");
            Assert.That(ex1.Body, Is.Null, $"no body for {replacement}");

            var ex2 = Assert.Throws<KtavException>(
                () => Ktav.EditValueSegments("a: 1\n", new[] { "a" }, replacement, KtavBackend.Pure))!;
            Assert.That(ex2.Error, Is.EqualTo("Message"), $"segments API class for {replacement}");

            if (TestPaths.CabiBuilt())
            {
                var ex3 = Assert.Throws<KtavException>(
                    () => Ktav.EditValue("a: 1\n", "a", replacement, KtavBackend.Native))!;
                Assert.That(ex3.Error, Is.EqualTo("Message"), $"native class for {replacement}");
            }
        }
    }

    [Test]
    public void NoopIsByteIdenticalEvenNoncanonical()
    {
        string source = "a:  0007\nb: { x: 1 }\n";
        Check(source, new[] { "a" }, ((KtavObject)ParsePure("a: 0007\n")).TryGet("a")!, source);
    }
}

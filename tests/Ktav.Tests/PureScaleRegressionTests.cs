using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Deterministic algorithmic regression guards for the pure backend:
/// genuine wide/deep/long workloads whose assertions count ACTUAL WORK
/// (entry counts, exact output lengths, exact round-trip structure).
/// There are deliberately NO wall-clock ceilings here — shared CI runners
/// make millisecond gates flaky, and a super-linear blowup trips the CI
/// job timeout anyway. Everything below is deterministic.
/// </summary>
[TestFixture]
public class PureScaleRegressionTests
{
    private const KtavBackend Pure = KtavBackend.Pure;

    [Test]
    public void WideObjectThousandsOfKeys()
    {
        const int keys = 20_000;
        var sb = new StringBuilder();
        for (int i = 0; i < keys; i++) sb.Append("key").Append(i).Append(": ").Append(i).Append('\n');
        var src = sb.ToString();

        var v = (KtavObject)Ktav.Loads(src, Pure);
        Assert.That(v.Entries, Has.Count.EqualTo(keys), "all keys must land");

        var outText = Ktav.Dumps(v, Pure);
        var back = (KtavObject)Ktav.Loads(outText, Pure);
        Assert.That(back.Entries, Has.Count.EqualTo(keys), "round-trip must preserve width");
        // Exact structure at the boundaries; the dotted-prefix merge
        // bookkeeping must not reorder or drop entries.
        Assert.That(back.Entries[0].Key, Is.EqualTo("key0"));
        Assert.That(back.Entries[keys - 1].Key, Is.EqualTo("key" + (keys - 1)));
        Assert.That(back.Entries[keys / 2].Key, Is.EqualTo("key" + (keys / 2)));
        for (int i = 0; i < keys; i++)
            Assert.That(back.Entries[i].Value, Is.EqualTo(KtavInteger.Of(i)),
                $"entry {i} must round-trip as the same integer");
    }

    [Test]
    public void DeepDottedPathsMerge()
    {
        // "root" + `depth` ".lN" segments = depth + 1 key segments, so the
        // document root object descends through depth + 1 single-entry
        // objects (one per segment) before reaching the leaf String.
        const int depth = 2_000;
        var sb = new StringBuilder("root");
        for (int i = 0; i < depth; i++) sb.Append(".l").Append(i);
        sb.Append(": leaf\n");
        var src = sb.ToString();

        var v = (KtavObject)Ktav.Loads(src, Pure);
        KtavValue cur = v;
        int walked = 0;
        while (cur is KtavObject o && o.Entries.Count == 1)
        {
            cur = o.Entries[0].Value;
            walked++;
        }
        Assert.That(walked, Is.EqualTo(depth + 1),
            "dotted path must descend once per key segment (root + .l0..l{depth-1})");
        Assert.That(cur, Is.InstanceOf<KtavString>());
        Assert.That(((KtavString)cur).Value, Is.EqualTo("leaf"));
    }

    [Test]
    public void LongStringsManyMegabytes()
    {
        var body = new string('x', 4 * 1024 * 1024);
        var src = "big: " + body + "\nsmall: 1\n";

        var v = (KtavObject)Ktav.Loads(src, Pure);
        var s = (KtavString)v.TryGet("big")!;
        Assert.That(s.Value.Length, Is.EqualTo(body.Length));

        var dumped = Ktav.Dumps(v, Pure);
        // Expected output built EXPLICITLY from the writer rules, no
        // arithmetic: the body is a single line (no LF), so the string
        // emits in the plain `key: body` form and each pair is
        // `key: value\n` with one space.
        Assert.That(dumped, Is.EqualTo("big: " + body + "\nsmall: 1\n"),
            "Dumps must emit the single-line body plus the small pair verbatim");

        // Multiline INPUT form too: stripping the `(` wrapper yields the
        // same single-line body, so the WRITER output is identical to the
        // first variant's — the multiline spelling exists only in source.
        var ml = "big: (\n" + body + "\n)\n";
        var v2 = (KtavObject)Ktav.Loads(ml, Pure);
        var s2 = (KtavString)v2.TryGet("big")!;
        Assert.That(s2.Value, Is.EqualTo(body));
        Assert.That(Ktav.Dumps(v2, Pure), Is.EqualTo("big: " + body + "\n"));
    }

    [Test]
    public void ExplicitBraceBlockNestingJustUnderTheLimit()
    {
        // 127 levels of REAL block nesting. Per the grammar (spec §5.0.1/
        // §3.2; the parser's ClassifyValueStart/OpenObj path), indentation
        // is ignored and does NOT nest: nesting requires a pair line whose
        // value opens a block (`key: {` alone on the line), the inner
        // pairs at ANY indentation, and one bare `}` closer line per open
        // block. All opener lines sit at the SAME (zero) indentation —
        // what nests is the brace structure, not leading whitespace:
        //
        //     k0: {
        //     k1: {
        //     ...
        //     leaf: 1
        //     }
        //     }
        const int depth = 127;
        var src = new StringBuilder();
        for (int i = 0; i < depth; i++)
            src.Append("k").Append(i).Append(": {\n");
        src.Append("leaf: 1\n");
        for (int i = 0; i < depth; i++)
            src.Append("}\n");

        var v = Ktav.Loads(src.ToString(), Pure);
        KtavValue cur = v;
        int walked = 0;
        while (cur is KtavObject o && o.Entries.Count == 1 && walked < depth)
        {
            cur = o.Entries[0].Value;
            walked++;
        }
        Assert.That(walked, Is.EqualTo(depth),
            "each `kN: {` opener must push one nesting level");
        Assert.That(((KtavObject)cur).Entries[0], Is.EqualTo(
            new KeyValuePair<string, KtavValue>("leaf", KtavInteger.Of(1))));

        // The dump round-trips through the same block form and re-parses
        // to the identical 127-level chain.
        var dumped = Ktav.Dumps(v, Pure);
        var back = Ktav.Loads(dumped, Pure);
        cur = back;
        walked = 0;
        while (cur is KtavObject o && o.Entries.Count == 1 && walked < depth)
        {
            cur = o.Entries[0].Value;
            walked++;
        }
        Assert.That(walked, Is.EqualTo(depth), "round trip must preserve nesting depth");
        Assert.That(((KtavObject)cur).Entries[0], Is.EqualTo(
            new KeyValuePair<string, KtavValue>("leaf", KtavInteger.Of(1))));
    }

    [Test]
    public void ManySmallDocumentsInSequenceStayLinear()
    {
        const int docs = 2_000;
        long total = 0;
        for (int i = 0; i < docs; i++)
        {
            var v = (KtavObject)Ktav.Loads("a: 1\nb: two\nc: [x, y, z]\n", Pure);
            total += v.Entries.Count;
        }
        Assert.That(total, Is.EqualTo(3L * docs));
    }

    [Test]
    public void WideObjectDumpsToExactCanonicalText()
    {
        // Small enough to compare byte-exactly, large enough that a
        // quadratic output builder would be visible to the CI job timeout:
        // 1,000 keys → exactly 1,000 "keyI: I\n" lines.
        const int keys = 1_000;
        var entries = new List<KeyValuePair<string, KtavValue>>(keys);
        for (int i = 0; i < keys; i++) entries.Add(new("key" + i, KtavInteger.Of(i)));
        var expected = new StringBuilder();
        for (int i = 0; i < keys; i++) expected.Append("key").Append(i).Append(": ").Append(i).Append('\n');
        Assert.That(Ktav.Dumps(new KtavObject(entries), Pure), Is.EqualTo(expected.ToString()));
    }
}

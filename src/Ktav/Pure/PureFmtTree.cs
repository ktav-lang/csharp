// The trivia-carrying document tree of the formatter (fmt_parser/doc.rs):
// PObject / PArray / PSlot / PTrivia, the §6.3 insert tables over P slots
// (insert.rs insert_value), the inline-value stamp, and the shared multiline
// finisher (collecting.rs Collecting::finish). Split out of PureFormatter to
// keep every file under the 1000-line budget; the parser and writer halves
// stay there.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

// ------------------------------------------------------------------
// Trivia + trivia-carrying tree (fmt_parser/doc.rs)
// ------------------------------------------------------------------

internal readonly struct PTrivia
{
    public readonly bool Blank;
    public readonly string Text; // comment text, trimmed, verbatim
    public PTrivia(bool blank, string text) { Blank = blank; Text = text; }
    public static readonly PTrivia BlankLine = new(true, "");
}

/// <summary>One pair/item slot: the trivia that preceded it in the source
/// plus the value (KtavValue leaf, PObject, or PArray).</summary>
internal sealed class PSlot
{
    public List<PTrivia> Trivia = new();
    public object? Value;
    public PSlot(List<PTrivia> trivia, object? value) { Trivia = trivia; Value = value; }
    public bool IsObject => Value is PObject;
    public string Label => Value switch
    {
        PObject => "object",
        PArray => "array",
        KtavNull => "null",
        KtavBool => "bool",
        KtavInteger => "integer",
        KtavFloat => "float",
        _ => "string",
    };
}

internal sealed class PObject
{
    public readonly List<KeyValuePair<string, PSlot>> Entries = new();
    private readonly Dictionary<string, int> _index = new();
    public List<PTrivia> Trailing = new();

    /// <summary>insert_leaf: false when occupied (slot untouched).</summary>
    public bool InsertLeaf(string key, PSlot slot)
    {
        if (_index.ContainsKey(key)) return false;
        _index[key] = Entries.Count;
        Entries.Add(new KeyValuePair<string, PSlot>(key, slot));
        return true;
    }

    /// <summary>doc.rs PObject::descend: descend into an existing Object value
    /// (mutating it in place, §6.3), creating an empty one for a fresh key;
    /// null when a non-object value blocks the path.</summary>
    public PObject? Descend(string key)
    {
        if (_index.TryGetValue(key, out int idx))
            return Entries[idx].Value.Value as PObject;
        var slot = new PSlot(new List<PTrivia>(), new PObject());
        _index[key] = Entries.Count;
        Entries.Add(new KeyValuePair<string, PSlot>(key, slot));
        return (PObject)slot.Value;
    }
}

internal sealed class PArray
{
    public readonly List<PSlot> Items = new();
    public List<PTrivia> Trailing = new();
}

internal static class PureFmtTree
{
    /// <summary>doc.rs trim_trailing_blanks: drop trailing Blanks — padding with
    /// nothing after it carries no meaning (right before a close bracket / EOF).</summary>
    public static void TrimTrailingBlanks(List<PTrivia> trivia)
    {
        while (trivia.Count > 0 && trivia[trivia.Count - 1].Blank)
            trivia.RemoveAt(trivia.Count - 1);
    }

    // ------------------------------------------------------------------
    // §6.3 insert tables over P slots (build/insert.rs, P-flavoured)
    // ------------------------------------------------------------------

    public static void InsertValue(PObject table, string rawKey, PSlot slot, long spanS, long spanE)
    {
        var kb = Encoding.UTF8.GetBytes(rawKey);
        if (PureKey.KeyIsSingleSegment(kb, 0, kb.Length))
        {
            var (tlo, thi) = PureUtf8.Trim(kb, 0, kb.Length);
            if (tlo >= thi) throw PureError.EmptyKey(0, spanS, spanE);
            switch (PureKey.CheckKey(kb, tlo, thi))
            {
                case PureKey.KeyValidity.Empty:
                    throw PureError.EmptyKey(0, spanS, spanE);
                case PureKey.KeyValidity.Invalid:
                    throw PureError.InvalidKey(0, rawKey,
                        PureKey.SplitKeyPath(rawKey), spanS, spanE);
            }
            string decoded = PureKey.DecodeKeySegmentChecked(kb, tlo, thi, 0, spanS, spanE);
            if (!table.InsertLeaf(decoded, slot))
            {
                var existing = FindSlot(table, decoded);
                if (existing.IsObject != slot.IsObject)
                    throw PureError.KeyPathConflictOverwrite(0, rawKey,
                        existing.Label, slot.Label, PureError.DecodedPath(rawKey), spanS, spanE);
                throw PureError.DuplicateKey(0, rawKey,
                    PureError.DecodedPath(rawKey), spanS, spanE);
            }
            return;
        }

        // Dotted path: raw byte windows per segment, validated before decode.
        var rawSegs = new List<string>();
        SplitSegments(kb, 0, kb.Length, rawSegs);
        PObject t = table;
        for (int idx = 0; idx < rawSegs.Count; idx++)
        {
            var sb = Encoding.UTF8.GetBytes(rawSegs[idx]);
            var (tlo, thi) = PureUtf8.Trim(sb, 0, sb.Length);
            if (tlo >= thi) throw PureError.EmptyKey(0, spanS, spanE);
            switch (PureKey.CheckKey(sb, tlo, thi))
            {
                case PureKey.KeyValidity.Empty:
                    throw PureError.EmptyKey(0, spanS, spanE);
                case PureKey.KeyValidity.Invalid:
                    throw PureError.InvalidKey(0, rawKey,
                        PureError.RawPath(rawKey), spanS, spanE);
            }
            string decoded = PureKey.DecodeKeySegmentChecked(sb, tlo, thi, 0, spanS, spanE);
            if (idx + 1 < rawSegs.Count)
            {
                var sub = t.Descend(decoded);
                if (sub == null)
                    throw PureError.KeyPathConflictBlocked(0, rawKey,
                        PureError.DecodedPath(rawKey), spanS, spanE);
                t = sub;
            }
            else
            {
                if (!t.InsertLeaf(decoded, slot))
                    throw PureError.DuplicateKey(0, rawKey,
                        PureError.DecodedPath(rawKey), spanS, spanE);
            }
        }
    }

    private static PSlot FindSlot(PObject table, string key)
    {
        foreach (var e in table.Entries)
            if (e.Key == key) return e.Value;
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>Raw dotted segments of a key (same split machine as
    /// PureInline's CollectSegments), materialised as raw text windows.</summary>
    private static void SplitSegments(byte[] b, int lo, int hi, List<string> raw)
    {
        var bounds = new List<(int s, int e)>();
        int start = lo, i = lo;
        bool segStart = true;
        bool hasQ = PureKey.HasQuoteBytes(b, lo, hi);
        while (i < hi)
        {
            if (hasQ && segStart)
            {
                i = PureKey.SkipSegmentWs(b, i, hi);
                if (i < hi && PureKey.IsQuoteByte(b[i]))
                {
                    int end = PureKey.QuotedSpanEnd(b, hi, i);
                    if (end < 0) break;
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= hi) break;
            int by = b[i];
            if (by == (byte)'\\') { i += i + 1 < hi ? 2 : 1; }
            else if (by == (byte)'.')
            {
                bounds.Add((start, i));
                start = i + 1;
                i++;
                segStart = true;
            }
            else i++;
        }
        bounds.Add((start, hi));
        foreach (var t in bounds) raw.Add(PureInline.Text(b, t.s, t.e));
    }

    // ------------------------------------------------------------------
    // doc.rs stamp: wrap a plain inline value (builder/list/leaf) with empty
    // trivia throughout. Sound because a comment can never occur inside an
    // inline compound (§3.4).
    // ------------------------------------------------------------------

    public static object Stamp(object? root)
    {
        // Iterative post-order (explicit stack): a deep dotted key builds a
        // deep builder chain, and stamping must not recurse over it. Each
        // frame fills an object?[] slot array with its children's results
        // (leaves stay KtavValue, compounds become the stamped PObject/PArray);
        // when exhausted it assembles the stamped PObject/PArray into its
        // single parent slot. No casts: PSlot.Value is object?.
        var rootSlot = new object?[1];
        var frames = new List<SFrame>();
        if (!PushStamp(frames, root, rootSlot, 0))
            rootSlot[0] = root; // leaf root: returned as the KtavValue itself
        while (frames.Count > 0)
        {
            var f = frames[frames.Count - 1];
            if (f.Next < f.Count)
            {
                int i = f.Next++;
                object? child = f.IsList ? f.Items![i] : f.Pairs![i].Value;
                if (!PushStamp(frames, child, f.Slots, i))
                    f.Slots[i] = child!; // KtavValue leaf
                continue;
            }
            object done;
            if (f.IsList)
            {
                var pa = new PArray();
                for (int i = 0; i < f.Count; i++)
                    pa.Items.Add(new PSlot(new List<PTrivia>(), f.Slots[i]));
                done = pa;
            }
            else
            {
                var po = new PObject();
                for (int i = 0; i < f.Count; i++)
                    po.Entries.Add(new KeyValuePair<string, PSlot>(
                        f.Pairs![i].Key, new PSlot(new List<PTrivia>(), f.Slots[i])));
                done = po;
            }
            f.ParentSlots![f.ParentIndex] = done;
            frames.RemoveAt(frames.Count - 1);
        }
        return rootSlot[0]!;
    }

    /// <summary>Queue a stamp frame for a compound; false when v is a leaf.</summary>
    private static bool PushStamp(List<SFrame> frames, object? v, object?[] parentSlots, int parentIndex)
    {
        if (v is PureObjBuilder b)
        {
            frames.Add(new SFrame(isList: false, b.Entries, parentSlots, parentIndex));
            return true;
        }
        if (v is List<object?> list)
        {
            frames.Add(new SFrame(isList: true, list, parentSlots, parentIndex));
            return true;
        }
        return false;
    }

    private sealed class SFrame
    {
        public readonly bool IsList;
        public readonly List<KeyValuePair<string, object?>>? Pairs;
        public readonly List<object?>? Items;
        public readonly object?[] Slots;
        public readonly object?[] ParentSlots;
        public readonly int ParentIndex;
        public int Next;

        public SFrame(bool isList, List<KeyValuePair<string, object?>> pairs,
            object?[] parentSlots, int parentIndex)
        {
            IsList = isList; Pairs = pairs; Count = pairs.Count;
            Slots = new object?[pairs.Count];
            ParentSlots = parentSlots; ParentIndex = parentIndex;
        }

        public SFrame(bool isList, List<object?> items, object?[] parentSlots, int parentIndex)
        {
            IsList = isList; Items = items; Count = items.Count;
            Slots = new object?[items.Count];
            ParentSlots = parentSlots; ParentIndex = parentIndex;
        }

        public int Count { get; }
    }

    // ------------------------------------------------------------------
    // collecting.rs Collecting::finish (verbatim join / stripped dedent) —
    // the same computation PureParser uses for the value tree.
    // ------------------------------------------------------------------

    public static string FinishMultiline(byte[] src, int mode, List<(int s, int e)> lines)
    {
        if (mode == 2)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Encoding.UTF8.GetString(src, lines[i].s, lines[i].e - lines[i].s));
            }
            return sb.ToString();
        }
        if (lines.Count == 1)
        {
            var (lo, hi) = PureUtf8.Trim(src, lines[0].s, lines[0].e);
            return lo >= hi ? "" : Encoding.UTF8.GetString(src, lo, hi - lo);
        }
        // Dedent: longest common leading-whitespace prefix over non-blank
        // lines, compared CODE POINT by code point (each member must be the
        // same code point across lines). A minimum byte-RUN length would
        // wrongly strip equal-length runs of different whitespace
        // characters (e.g. three ASCII spaces vs one U+3000), so the runs
        // are walked in parallel and cut at the first code-point mismatch.
        // Mirrors PureParser.FinishMultiline exactly (CpAt is shared).
        int common = -1; // common prefix length in code points
        int refLo = -1, refEnd = 0; // first non-blank line's leading-ws run
        foreach (var (ls, le) in lines)
        {
            var (tlo, thi) = PureUtf8.Trim(src, ls, le);
            if (tlo >= thi) continue; // blank lines don't constrain
            int runEnd = ls + PureUtf8.LeadingWsRun(src, ls, le);
            if (common < 0)
            {
                refLo = ls; refEnd = runEnd;
                common = PureParser.CountCps(src, ls, runEnd);
                continue;
            }
            int matched = 0, pRef = refLo, pCur = ls;
            while (matched < common && pRef < refEnd && pCur < runEnd
                && PureParser.CpAt(src, pRef) == PureParser.CpAt(src, pCur))
            {
                matched++;
                pRef += PureUtf8.InlineWsLenAt(src, pRef, refEnd);
                pCur += PureUtf8.InlineWsLenAt(src, pCur, runEnd);
            }
            common = matched;
            if (common == 0) break;
        }
        if (common < 0) common = 0; // all lines blank
        var sb2 = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb2.Append('\n');
            var (ls, le) = lines[i];
            var (tlo, thi) = PureUtf8.Trim(src, ls, le);
            if (tlo >= thi) continue; // blank line → empty
            // Advance `common` whitespace CODE POINTS (each <= 3 bytes).
            int cLo = ls;
            for (int k = 0; k < common; k++)
                cLo += PureUtf8.InlineWsLenAt(src, cLo, le);
            int cHi = le;
            while (cHi > cLo)
            {
                int back = BackWsLenByte(src, cLo, cHi);
                if (back == 0) break;
                cHi -= back;
            }
            sb2.Append(Encoding.UTF8.GetString(src, cLo, cHi - cLo));
        }
        return sb2.ToString();
    }

    private static int BackWsLenByte(byte[] b, int kLo, int kHi)
    {
        for (int start = Math.Max(kLo, kHi - 3); start < kHi; start++)
        {
            int len = PureUtf8.WsLenAt(b, start, kHi);
            if (len > 0 && start + len == kHi) return len;
        }
        return 0;
    }
}

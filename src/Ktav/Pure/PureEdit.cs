// Lossless source edit (spec 0.9): replace the value at a path by splicing
// the exact target bytes, preserving every other byte of the document —
// comments, whitespace, key spelling/order, BOM, EOL style, final EOL.
// Port of rust/src/edit.rs + src/document/parser/edit/{tree.rs,splice.rs,
// splice/layout.rs}, reusing the existing pure parser invariants
// (PureInline / PureKey / PureClassify / PureUtf8 / PureRender) so the
// positional pass takes its ranges at the same line-dispatch decisions as
// the real parser.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureEdit
{
    // ------------------------------------------------------------------
    // Entry points — byte-exact in, byte-exact out.
    // ------------------------------------------------------------------

    public static string Apply(byte[] src, string textualPath, KtavValue replacement)
    {
        if (textualPath == null) throw new ArgumentNullException(nameof(textualPath));
        // Source-first error priority: UTF-8 validation and the parse run
        // BEFORE the textual path is decoded, so a broken source reports its
        // real 0.9 parser envelope even when the path is also invalid.
        var original = ParseSource(src);
        return ApplyCore(src, ParsePath(textualPath), replacement, original);
    }

    public static string ApplySegments(byte[] src, IReadOnlyList<string> segments, KtavValue replacement)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        // Same source-first order: a broken source wins over the
        // empty-segments NotFound.
        var original = ParseSource(src);
        return ApplyCore(src, segments, replacement, original);
    }

    private static KtavValue ParseSource(byte[] src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        return PureParser.ParseLax(src);
    }

    private static string ApplyCore(byte[] src, IReadOnlyList<string> segments,
        KtavValue replacement, KtavValue original)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        if (segments.Count == 0) throw KtavException.EditNotFound();
        // Transport decode (cabi wire.rs wire_to_any_value): tagged numeric
        // payloads are canonicalised exactly like the native op does, so
        // `{"$i":"+0001"}` and a host-built KtavInteger("+0001") behave
        // identically (a no-op against source `1`). Invalid payloads are a
        // Message envelope — never an Edit* error with invented location.
        replacement = NormalizeWireValue(replacement);
        // §5.2 source domain: a source-level Integer is i64. A wire Integer
        // outside that domain would silently reparse as a String on any
        // rewrite — fail closed here instead, with the arbitrary-precision
        // payload untouched (never truncated).
        if (!WithinSourceIntegerDomain(replacement))
            throw KtavException.EditUnsupported(
                "integer replacement is outside the i64 source domain; it would reclassify as a String");
        var target = LookupValue(original, segments);
        if (target == null) throw KtavException.EditNotFound();
        if (EditValuesEqual(target, replacement)) return PureUtf8.StrictUtf8.GetString(src);

        Node root;
        bool rootExplicit;
        var builder = new Builder(src, insertionMode: false);
        (root, rootExplicit) = builder.Build();

        var (node, ctx) = Descend(root, segments, rootExplicit);
        if (!node.Slot.HasValue)
            throw KtavException.EditUnsupported(
                "target is a noncontiguous value (dotted-key/reopen intermediate); no lossless single-range splice exists");
        var slot = node.Slot.Value;
        var candidate = PureEditSplice.Edit(src, slot, ctx, node, replacement);
        var candidateBytes = PureUtf8.StrictUtf8.GetBytes(candidate);
        var resultBytes = PureEditLayout.Adjust(src, candidateBytes, root, segments, replacement, node, ctx);
        var result = PureUtf8.StrictUtf8.GetString(resultBytes);

        // Mandatory semantic verification: exactly the target replaced, the
        // rest of the tree equal — a silent corruption can never be returned.
        // BOTH trees are compared with the target masked to Null (the
        // rebuilt roots are used, not the untouched originals — masking the
        // target is the only difference a splice may produce).
        KtavValue newDoc;
        try
        {
            newDoc = PureParser.ParseLax(PureUtf8.StrictUtf8.GetBytes(result));
        }
        catch (KtavException ke)
        {
            throw KtavException.EditUnsupported("patch produced an invalid document: " + ke.Message);
        }
        bool targetMatches = TakeTarget(newDoc, segments, out var actual, out var newMasked) &&
                             EditValuesEqual(actual!, replacement);
        if (!TakeTarget(original, segments, out _, out var expectedMasked))
            throw KtavException.EditNotFound();
        if (!targetMatches || !EditValuesEqual(newMasked, expectedMasked))
            throw KtavException.EditUnsupported("semantic mismatch after splice; no lossless patch produced");
        return result;
    }

    // ------------------------------------------------------------------
    // Textual path parsing (edit.rs parse_path): '.' separator, '\.' and
    // '\\' escapes, everything else (incl. trailing lone backslash) is
    // EditInvalidPath at the UTF-8 byte offset of the offender.
    // ------------------------------------------------------------------

    internal static List<string> ParsePath(string path)
    {
        if (path.Length == 0) throw KtavException.EditInvalidPath(0);
        var bytes = Encoding.UTF8.GetBytes(path);
        var segments = new List<string>();
        int segStart = 0;
        StringBuilder? cur = null;
        int i = 0;
        while (i < bytes.Length)
        {
            int b = bytes[i];
            if (b == '.')
            {
                if (segStart == i && cur == null) throw KtavException.EditInvalidPath(i);
                segments.Add(cur != null ? cur.ToString() : Encoding.UTF8.GetString(bytes, segStart, i - segStart));
                cur = null;
                segStart = i + 1;
                i++;
            }
            else if (b == '\\')
            {
                if (i + 1 >= bytes.Length) throw KtavException.EditInvalidPath(i);
                int next = bytes[i + 1];
                if (next != '.' && next != '\\') throw KtavException.EditInvalidPath(i);
                cur ??= new StringBuilder(Encoding.UTF8.GetString(bytes, segStart, i - segStart));
                cur.Append(next == '.' ? '.' : '\\');
                i += 2;
            }
            else
            {
                int len = Utf8Len(b);
                if (cur != null) cur.Append(Encoding.UTF8.GetString(bytes, i, Math.Min(len, bytes.Length - i)));
                i += len;
            }
        }
        if (segStart == bytes.Length && cur == null)
            throw KtavException.EditInvalidPath(bytes.Length);
        segments.Add(cur != null ? cur.ToString() : Encoding.UTF8.GetString(bytes, segStart, bytes.Length - segStart));
        return segments;
    }

    private static int Utf8Len(int b) => b switch
    {
        <= 0x7F => 1,
        >= 0xC0 and <= 0xDF => 2,
        >= 0xE0 and <= 0xEF => 3,
        >= 0xF0 and <= 0xF7 => 4,
        _ => 1,
    };

    // ------------------------------------------------------------------
    // Value lookup / take (splice.rs lookup_value, take_target)
    // ------------------------------------------------------------------

    internal static KtavValue? LookupValue(KtavValue v, IReadOnlyList<string> segments)
    {
        foreach (var seg in segments)
        {
            switch (v)
            {
                case KtavObject o:
                {
                    KtavValue? found = null;
                    foreach (var e in o.Entries)
                        if (e.Key == seg) { found = e.Value; break; }
                    if (found == null) return null;
                    v = found;
                    break;
                }
                case KtavArray a:
                {
                    int idx = ParseIndex(seg);
                    if (idx < 0 || idx >= a.Items.Count) return null;
                    v = a.Items[idx];
                    break;
                }
                default:
                    return null;
            }
        }
        return v;
    }

    /// <summary>Replace the target with Null in a rebuilt (immutable) tree
    /// and report BOTH the removed value and the rebuilt root — KtavObject /
    /// KtavArray are immutable, so the caller must compare the rebuilt
    /// trees, never the untouched originals.</summary>
    private static bool TakeTarget(KtavValue root, IReadOnlyList<string> segments,
        out KtavValue? taken, out KtavValue replacedRoot)
    {
        taken = null;
        replacedRoot = root;
        if (segments.Count == 0) return false;
        return TakeInner(root, segments, 0, ref taken, out replacedRoot);
    }

    private static bool TakeInner(KtavValue v, IReadOnlyList<string> segments, int depth,
        ref KtavValue? taken, out KtavValue replaced)
    {
        // Assigned on EVERY branch (CS0177): failure branches keep the node
        // unchanged, success branches rebuild it.
        replaced = v;
        string seg = segments[depth];
        bool last = depth + 1 == segments.Count;
        switch (v)
        {
            case KtavObject o:
            {
                var entries = new List<KeyValuePair<string, KtavValue>>(o.Entries);
                bool hit = false;
                for (int i = 0; i < entries.Count && !hit; i++)
                {
                    if (entries[i].Key != seg) continue;
                    if (last)
                    {
                        taken = entries[i].Value;
                        entries[i] = new KeyValuePair<string, KtavValue>(seg, KtavNull.Instance);
                        hit = true;
                    }
                    else if (TakeInner(entries[i].Value, segments, depth + 1, ref taken, out var inner))
                    {
                        entries[i] = new KeyValuePair<string, KtavValue>(seg, inner);
                        hit = true;
                    }
                    // Miss below the key: replaced stays `v` (already assigned).
                }
                replaced = new KtavObject(entries);
                return hit;
            }
            case KtavArray a:
            {
                int idx = ParseIndex(seg);
                if (idx < 0 || idx >= a.Items.Count) { replaced = v; return false; }
                var items = new List<KtavValue>(a.Items);
                if (last)
                {
                    taken = items[idx];
                    items[idx] = KtavNull.Instance;
                }
                else if (!TakeInner(items[idx], segments, depth + 1, ref taken, out var inner))
                {
                    replaced = v;
                    return false;
                }
                else items[idx] = inner;
                replaced = new KtavArray(items);
                return true;
            }
            default:
                replaced = v;
                return false;
        }
    }

    // ------------------------------------------------------------------
    // Wire transport normalisation (cabi/wire.rs canonical_integer /
    // canonical_float): string-level, never i64-truncated; non-finite
    // float results pass through verbatim (wire-only capability the
    // §5.9.0 writers reject later, fail closed).
    // ------------------------------------------------------------------

    /// <summary>Canonicalise every tagged numeric payload in the value,
    /// recursively. Mirrors the native wire decode byte-for-byte so the
    /// pure and native backends accept identical replacements.</summary>
    internal static KtavValue NormalizeWireValue(KtavValue v)
    {
        switch (v)
        {
            case KtavInteger i:
                return new KtavInteger(CanonicalWireInteger(i.Text));
            case KtavFloat f:
                return new KtavFloat(CanonicalWireFloat(f.Text));
            case KtavArray a:
            {
                var items = new List<KtavValue>(a.Items.Count);
                foreach (var item in a.Items) items.Add(NormalizeWireValue(item));
                return new KtavArray(items);
            }
            case KtavObject o:
            {
                var entries = new List<KeyValuePair<string, KtavValue>>(o.Entries.Count);
                foreach (var e in o.Entries)
                    entries.Add(new KeyValuePair<string, KtavValue>(e.Key, NormalizeWireValue(e.Value)));
                return new KtavObject(entries);
            }
            default:
                return v;
        }
    }

    /// <summary>wire.rs canonical_integer: optional '-'/'+' sign, ASCII
    /// decimal digits only, no redundant leading zeros, no signed zero;
    /// bignum payloads pass through unchanged (never parsed into i64).</summary>
    internal static string CanonicalWireInteger(string s)
    {
        string sign;
        string digits;
        if (s.Length > 0 && s[0] == '-') { sign = "-"; digits = s.Substring(1); }
        else if (s.Length > 0 && s[0] == '+') { sign = ""; digits = s.Substring(1); }
        else { sign = ""; digits = s; }
        if (digits.Length == 0 || !AllAsciiDigits(digits))
            throw KtavException.MessageEnvelope("$i payload not an integer literal: \"" + s + "\"");
        string trimmed = digits.TrimStart('0');
        if (trimmed.Length == 0) return "0";
        return sign + trimmed;
    }

    private static bool AllAsciiDigits(string s)
    {
        foreach (var ch in s)
            if (ch < '0' || ch > '9') return false;
        return true;
    }

    /// <summary>True when every Integer payload in the value fits the
    /// §5.2 source domain (i64). Bignums are rejected, not truncated.</summary>
    private static bool WithinSourceIntegerDomain(KtavValue v)
    {
        switch (v)
        {
            case KtavInteger i:
            {
                string text = i.Text;
                string signed = text.Length > 0 && (text[0] == '+' || text[0] == '-')
                    ? text.Substring(1)
                    : text;
                // Canonicalised payload: digits only, so >19 digits can
                // never fit i64; the boundary values are handled exactly
                // by the TryParse.
                if (signed.Length > 19) return false;
                return long.TryParse(text, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
            }
            case KtavArray a:
                foreach (var item in a.Items)
                    if (!WithinSourceIntegerDomain(item)) return false;
                return true;
            case KtavObject o:
                foreach (var e in o.Entries)
                    if (!WithinSourceIntegerDomain(e.Value)) return false;
                return true;
            default:
                return true;
        }
    }

    /// <summary>wire.rs canonical_float: must contain '.' or an exponent
    /// (the float lexical form); parses as f64; a NON-FINITE result (an
    /// extreme exponent like 1e400) passes through verbatim; finite values
    /// re-render as shortest round-trip decimal (ryu) — the same shape
    /// classify.rs stores for source-parsed floats.</summary>
    internal static string CanonicalWireFloat(string s)
    {
        bool looksLikeFloat = s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0;
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
            throw KtavException.MessageEnvelope("$f payload not a finite decimal: \"" + s + "\"");
        if (!looksLikeFloat)
            throw KtavException.MessageEnvelope("$f payload must contain '.' or exponent: \"" + s + "\"");
        if (double.IsNaN(val) || double.IsInfinity(val)) return s;
        return PureNum.RyuFormat(val);
    }

    /// <summary>ASCII digits only (signs are not indexes; leading zeros OK).</summary>
    internal static int ParseIndex(string seg)
    {
        if (seg.Length == 0) return -1;
        long v = 0;
        foreach (var ch in seg)
        {
            if (ch < '0' || ch > '9') return -1;
            v = v * 10 + (ch - '0');
            if (v > int.MaxValue) return -1;
        }
        return (int)v;
    }

    /// <summary>Value equality for edit semantics. Objects compare BY KEY
    /// (the reference's IndexMap PartialEq ignores insertion order — a
    /// replacement that names the same pairs in a different order is an
    /// equal value, so the no-op returns the source bytes unchanged);
    /// arrays compare element-wise in order. Numeric lexemes compare as
    /// exact text (conservative — a spelling-different numeric is "not
    /// equal", which only ever fails closed through verification).</summary>
    internal static bool EditValuesEqual(KtavValue a, KtavValue b)
    {
        if (ReferenceEquals(a, b)) return true;
        switch (a)
        {
            case KtavNull: return b is KtavNull;
            case KtavBool ba: return b is KtavBool bb && ba.Value == bb.Value;
            case KtavInteger ia: return b is KtavInteger ib && ia.Text == ib.Text;
            case KtavFloat fa: return b is KtavFloat fb && fa.Text == fb.Text;
            case KtavString sa: return b is KtavString sb && sa.Value == sb.Value;
            case KtavObject oa:
            {
                if (b is not KtavObject ob || oa.Entries.Count != ob.Entries.Count) return false;
                // Ordered fast path (the common case): identical key order
                // is decided in one positional pass, no allocation.
                bool sameOrder = true;
                for (int i = 0; i < oa.Entries.Count; i++)
                {
                    if (oa.Entries[i].Key != ob.Entries[i].Key) { sameOrder = false; break; }
                    if (!EditValuesEqual(oa.Entries[i].Value, ob.Entries[i].Value)) return false;
                }
                if (sameOrder) return true;
                // Reordered: key-based comparison with a single Dictionary
                // over `b` (Ordinal keys — duplicate keys are impossible
                // post-parse), O(n) instead of a quadratic scan.
                var index = new Dictionary<string, KtavValue>(ob.Entries.Count, StringComparer.Ordinal);
                foreach (var f in ob.Entries) index.Add(f.Key, f.Value);
                foreach (var e in oa.Entries)
                {
                    if (!index.TryGetValue(e.Key, out var matched)) return false;
                    if (!EditValuesEqual(e.Value, matched)) return false;
                }
                return true;
            }
            case KtavArray aa:
            {
                if (b is not KtavArray ab || aa.Items.Count != ab.Items.Count) return false;
                for (int i = 0; i < aa.Items.Count; i++)
                    if (!EditValuesEqual(aa.Items[i], ab.Items[i])) return false;
                return true;
            }
            default: return a.Equals(b);
        }
    }

    // ------------------------------------------------------------------
    // Tree descent (splice.rs descend)
    // ------------------------------------------------------------------

    private static (Node, Ctx) Descend(Node root, IReadOnlyList<string> segments, bool rootExplicit)
    {
        bool containerIsRoot = true;
        var cur = root;
        bool rootFirst = false;
        foreach (var seg in segments)
        {
            if (cur.IsObject)
            {
                rootFirst = false;
                var next = cur.Table!.Get(seg);
                if (next == null) throw KtavException.EditNotFound();
                cur = next;
            }
            else if (cur.IsArray)
            {
                int idx = ParseIndex(seg);
                if (idx < 0) throw KtavException.EditNotFound();
                rootFirst = containerIsRoot && !rootExplicit && idx == 0;
                if (idx >= cur.Items!.Count) throw KtavException.EditNotFound();
                cur = cur.Items[idx];
            }
            else throw KtavException.EditNotFound();
            containerIsRoot = false;
        }
        return (cur, new Ctx(cur.Inline, rootFirst));
    }

    // ------------------------------------------------------------------
    // Shared byte helpers
    // ------------------------------------------------------------------

    internal static string Txt(byte[] b, int lo, int hi) =>
        Encoding.UTF8.GetString(b, lo, hi - lo);

    internal static (int, int) LineBounds(byte[] b, int pos)
    {
        int start = pos;
        while (start > 0 && b[start - 1] != (byte)'\n' && b[start - 1] != (byte)'\r') start--;
        int end = pos;
        while (end < b.Length && b[end] != (byte)'\n' && b[end] != (byte)'\r') end++;
        return (start, end);
    }

    internal static string LineIndent(byte[] b, int pos)
    {
        int start = pos;
        while (start > 0 && b[start - 1] != (byte)'\n' && b[start - 1] != (byte)'\r') start--;
        int end = start;
        while (end < b.Length && (b[end] == (byte)' ' || b[end] == (byte)'\t')) end++;
        return Txt(b, start, end);
    }

    internal static string DetectEol(byte[] b, int pos)
    {
        int i = pos;
        while (i < b.Length)
        {
            if (b[i] == (byte)'\n') return "\n";
            if (b[i] == (byte)'\r') return i + 1 < b.Length && b[i + 1] == (byte)'\n' ? "\r\n" : "\r";
            i++;
        }
        // Fallback (no terminator at/after pos): the LAST \r or \n before
        // pos decides; a \n immediately preceded by \r is a CRLF pair.
        int last = -1;
        for (i = Math.Min(pos, b.Length) - 1; i >= 0; i--)
        {
            if (b[i] == (byte)'\r' || b[i] == (byte)'\n') { last = i; break; }
        }
        if (last < 0) return "\n";
        if (b[last] == (byte)'\r')
            return last + 1 < b.Length && b[last + 1] == (byte)'\n' ? "\r\n" : "\r";
        return last > 0 && b[last - 1] == (byte)'\r' ? "\r\n" : "\n";
    }

    /// <summary>Unicode scalar value count of [lo,hi) — the layout width unit
    /// (never bytes, never UTF-16 units).</summary>
    internal static int CountScalars(byte[] b, int lo, int hi)
    {
        int n = 0, i = lo;
        while (i < hi)
        {
            i += Utf8Len(b[i]);
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    // Node model (tree.rs Kind/Slot/Node + InsertTable semantics)
    // ------------------------------------------------------------------

    internal enum SlotKind { Pair, Item, Block }

    internal readonly struct Slot
    {
        public readonly SlotKind Kind;
        public readonly int A;    // Pair: colon offset; Item: start; Block: start
        public readonly int B0;   // body start
        public readonly int B1;   // body end (== End except Block)
        public readonly int End;  // Block: end of the closing line
        public readonly bool HasAnchor; // Block: an opening pair exists

        private Slot(SlotKind kind, int a, int b0, int b1, int end, bool hasAnchor)
        { Kind = kind; A = a; B0 = b0; B1 = b1; End = end; HasAnchor = hasAnchor; }

        public static Slot Pair(int colon, int b0, int b1) => new(SlotKind.Pair, colon, b0, b1, b1, false);
        public static Slot Item(int start, int b0, int b1) => new(SlotKind.Item, start, b0, b1, b1, false);
        public static Slot Block(int start, int end) => new(SlotKind.Block, start, start, end, end, false);
        public static Slot BlockAnchored(int anchor, int start, int end) => new(SlotKind.Block, anchor, start, end, end, true);
    }

    internal sealed class Node
    {
        public bool IsObject;
        public EditTable? Table;
        public List<Node>? Items; // array
        public bool Inline;
        public Slot? Slot;

        public bool IsArray => !IsObject && Items != null;
        public bool IsScalar => !IsObject && Items == null;
    }

    internal sealed class EditTable
    {
        public List<KeyValuePair<string, Node>> Entries = new();
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        public Node? Get(string key) =>
            _index.TryGetValue(key, out int i) ? Entries[i].Value : null;

        public bool InsertLeaf(string key, Node node)
        {
            if (_index.ContainsKey(key)) return false;
            _index[key] = Entries.Count;
            Entries.Add(new KeyValuePair<string, Node>(key, node));
            return true;
        }

        /// <summary>descend (§6.3 reopen): existing object → descend and
        /// invalidate its slot (the value becomes noncontiguous); missing
        /// key → fresh synthetic object. Null when a scalar/array blocks.</summary>
        public EditTable? Descend(string key)
        {
            if (_index.TryGetValue(key, out int i))
            {
                var node = Entries[i].Value;
                if (!node.IsObject) return null;
                node.Slot = null;
                return node.Table;
            }
            var fresh = new Node { IsObject = true, Table = new EditTable(), Inline = false, Slot = null };
            _index[key] = Entries.Count;
            Entries.Add(new KeyValuePair<string, Node>(key, fresh));
            return fresh.Table;
        }
    }

    internal readonly struct Ctx
    {
        public readonly bool Inline;
        public readonly bool RootFirst;
        public Ctx(bool inline, bool rootFirst) { Inline = inline; RootFirst = rootFirst; }
    }

    // ------------------------------------------------------------------
    // Positional tree builder (tree.rs Builder)
    // ------------------------------------------------------------------

    internal static (Node, bool) BuildPosition(byte[] src) => new Builder(src, insertionMode: true).Build();
    internal static (Node, bool) BuildPositionForEdit(byte[] src) => new Builder(src, insertionMode: false).Build();
    internal static (Node, bool) BuildPositionForRemove(byte[] src) => new Builder(src, insertionMode: false, rootRangeMode: true).Build();

    private sealed class Builder
    {
        private readonly byte[] _src;
        private readonly List<Frame> _stack = new();
        private int _collecting;          // 0 none, 1 stripped, 2 verbatim
        private int _collectingOpener;
        private bool _rootInitialized;
        private bool _rootConsumed;
        private Node? _rootInline;
        private bool _rootIsExplicit;
        private Slot? _rootSlot;
        private Node? _rootExplicitNode;
        private readonly bool _insertionMode;
        private readonly bool _rootRangeMode;

        internal Builder(byte[] src, bool insertionMode, bool rootRangeMode = false)
        { _src = src; _insertionMode = insertionMode; _rootRangeMode = rootRangeMode; }

        internal (Node, bool) Build()
        {
            int bom = PureUtf8.BomLen(_src);
            int n = _src.Length;
            int lineStart = bom;
            int lineNum = 0;
            while (lineStart <= n)
            {
                if (lineStart == n) break;
                int pos = lineStart;
                while (pos < n && _src[pos] != (byte)'\n' && _src[pos] != (byte)'\r') pos++;
                int nextStart;
                if (pos < n)
                {
                    if (_src[pos] == (byte)'\r')
                        nextStart = pos + 1 < n && _src[pos + 1] == (byte)'\n' ? pos + 2 : pos + 1;
                    else nextStart = pos + 1;
                }
                else nextStart = n + 1;
                lineNum++;
                HandleLine(lineStart, pos, lineNum);
                lineStart = nextStart;
            }
            return (Finish(n), _rootIsExplicit);
        }

        private void HandleLine(int ls, int le, int lineNum)
        {
            if (_collecting != 0)
            {
                var (tlo, thi) = PureUtf8.Trim(_src, ls, le);
                string trimmedStr = PureInline.Text(_src, tlo, thi);
                bool term = _collecting == 1 ? trimmedStr == ")" : trimmedStr == "))";
                if (term)
                {
                    int opener = _collectingOpener;
                    _collecting = 0;
                    _collectingOpener = 0;
                    AttachScalar(new Node { Slot = Slot.Block(opener, thi) }, lineNum);
                }
                return;
            }

            var (ts0, ts1) = PureUtf8.Trim(_src, ls, le);
            if (ts0 >= ts1 || Eq(ts0, ts1, "##") || StartsWith(ts0, ts1, "##")) return;

            if (_rootConsumed)
                throw PureError.OrphanLineAfterTopLevelInline(lineNum, ts0, ts1);

            if (!_rootInitialized)
            {
                _rootInitialized = true;
                if (!Eq(ts0, ts1, "}") && !Eq(ts0, ts1, "]"))
                {
                    if (ts1 - ts0 >= 2 && _src[ts0] == (byte)'{' && _src[ts1 - 1] == (byte)'}')
                    {
                        _rootConsumed = true;
                        _rootInline = PositionInlineTop(ts0, ts1, lineNum, isObject: true);
                        if (_insertionMode || _rootRangeMode) _rootInline.Slot = Slot.Pair(ts0, ts0, ts1);
                        return;
                    }
                    if (ts1 - ts0 >= 2 && _src[ts0] == (byte)'[' && _src[ts1 - 1] == (byte)']')
                    {
                        _rootConsumed = true;
                        _rootInline = PositionInlineTop(ts0, ts1, lineNum, isObject: false);
                        if (_insertionMode || _rootRangeMode) _rootInline.Slot = Slot.Item(ts0, ts0, ts1);
                        return;
                    }
                    if (Eq(ts0, ts1, "{"))
                    {
                        _rootIsExplicit = true;
                        _stack.Add(new Frame(isObject: true, ts0));
                        return;
                    }
                    if (Eq(ts0, ts1, "["))
                    {
                        _rootIsExplicit = true;
                        _stack.Add(new Frame(isObject: false, ts0));
                        return;
                    }
                    // Implicit root: pair-shaped → object, else array (§5.0.1).
                    bool pair = PureClassify.IsPairShape(_src, ts0, ts1);
                    _stack.Add(new Frame(pair, 0));
                }
            }

            if (Eq(ts0, ts1, "}")) { CloseFrame(lineNum, ts0, ts1); return; }
            if (Eq(ts0, ts1, "]")) { CloseFrame(lineNum, ts0, ts1); return; }

            if (_stack.Count > 0 && !_stack[_stack.Count - 1].IsObject)
                HandleArrayItem(ts0, ts1, lineNum);
            else
                HandleObjectPair(ts0, ts1, lineNum);
        }

        private bool Eq(int lo, int hi, string s) =>
            hi - lo == s.Length && PureInline.Eq(_src, lo, s);

        private bool StartsWith(int lo, int hi, string s) =>
            hi - lo >= s.Length && PureInline.Eq(_src, lo, s);

        // ---------------- inline positioning ----------------

        private Node PositionInlineTop(int lo, int hi, int lineNum, bool isObject)
        {
            var scan = PureInline.ScanInlineCloserWithBounds(_src, lo, hi,
                isObject ? '{' : '[', isObject ? '}' : ']', lineNum, lo, hi,
                out var boundsPairs, out PureError? bad);
            if (bad != null) throw bad;
            if (scan < 0)
                throw PureError.UnterminatedInlineCompound(lineNum, lo, hi);
            var bounds = new PureInline.InlineBounds(boundsPairs);
            return PositionInline(lo, hi, lineNum, isObject, 0, bounds);
        }

        /// <summary>Position an inline body whose first byte is at `lo` and
        /// whose last byte is the matching closer (guaranteed by the parse).</summary>
        private Node PositionInline(int lo, int hi, int lineNum, bool isObject, int depth,
            PureInline.InlineBounds bounds)
        {
            if (depth > PureInline.MaxInlineDepth)
                throw PureError.MalformedInlineCompound(lineNum,
                    "превышена максимальная глубина вложенности inline-compound", lo, hi);
            var segments = new List<(int s, int e)>();
            bool hasQuotes = PureKey.HasQuoteBytes(_src, lo, hi);
            PureInline.SplitTopLevel(_src, lo + 1, hi - 1, isObject, lineNum, lo, hi,
                bounds, hasQuotes, segments, out bool unterminated);
            if (unterminated)
                throw PureError.UnterminatedInlineCompound(lineNum, lo, hi);

            var node = new Node { Inline = true, Slot = null };
            if (isObject)
            {
                node.IsObject = true;
                node.Table = new EditTable();
                foreach (var (s, e) in segments)
                {
                    var (slo, shi) = PureUtf8.Trim(_src, s, e);
                    if (slo >= shi) continue; // trailing comma
                    if (!PureKey.FindUnescapedColon(_src, slo, shi, out int colon))
                        throw PureError.MalformedInlineCompound(lineNum, "inline-пара без разделителя", lo, hi);
                    // FindUnescapedColon reports the ABSOLUTE offset (see
                    // PureInline.ParseInlineObjectInner: keyHi = colon).
                    int colonAbs = colon;
                    int vLo = colon + 1;
                    Node child;
                    if (vLo < shi && _src[vLo] == (byte)':')
                    {
                        // raw marker `::`: literal string body
                        int rlo = PureUtf8.TrimStart(_src, vLo + 1, shi);
                        child = new Node { Inline = true, Slot = Slot.Pair(colonAbs, rlo, shi) };
                    }
                    else
                    {
                        int valueAbs = PureUtf8.TrimStart(_src, vLo, shi);
                        child = InlineChild(valueAbs, shi, lineNum, depth, bounds);
                        child.Slot = Slot.Pair(colonAbs, valueAbs, shi);
                    }
                    InsertNodePair(node.Table, slo, colon, child, lineNum, slo, shi);
                }
            }
            else
            {
                // `::` inside an inline array is literal item data: the body
                // spans the whole trimmed segment.
                node.Items = new List<Node>();
                foreach (var (s, e) in segments)
                {
                    var (slo, shi) = PureUtf8.Trim(_src, s, e);
                    if (slo >= shi) continue;
                    int valueAbs = PureUtf8.TrimStart(_src, slo, shi);
                    var child = InlineChild(valueAbs, shi, lineNum, depth, bounds);
                    child.Slot = Slot.Item(slo, valueAbs, shi);
                    node.Items.Add(child);
                }
            }
            return node;
        }

        private Node InlineChild(int vLo, int vHi, int lineNum, int depth,
            PureInline.InlineBounds bounds)
        {
            if (vLo < vHi && _src[vLo] == (byte)'{')
                return PositionInline(vLo, vHi, lineNum, true, depth + 1, bounds);
            if (vLo < vHi && _src[vLo] == (byte)'[')
                return PositionInline(vLo, vHi, lineNum, false, depth + 1, bounds);
            return new Node { Inline = true, Slot = null };
        }

        /// <summary>§6.3 insert over the Node table (single segment or dotted
        /// path); reopen invalidates intermediate slots.</summary>
        private void InsertNodePair(EditTable table, int kLo, int kHi, Node leaf,
            int lineNum, long spanS, long spanE)
        {
            var kb = Seg(kLo, kHi);
            string rawKey = Txt(kb, 0, kb.Length);
            if (PureKey.KeyIsSingleSegment(kb, 0, kb.Length))
            {
                var (tlo, thi) = PureUtf8.Trim(kb, 0, kb.Length);
                if (tlo >= thi) throw PureError.EmptyKey(lineNum, spanS, spanE);
                switch (PureKey.CheckKey(kb, tlo, thi))
                {
                    case PureKey.KeyValidity.Empty: throw PureError.EmptyKey(lineNum, spanS, spanE);
                    case PureKey.KeyValidity.Invalid:
                        throw PureError.InvalidKey(lineNum, rawKey,
                            PureKey.SplitKeyPath(rawKey), spanS, spanE);
                }
                string decoded = PureKey.DecodeKeySegmentChecked(kb, tlo, thi, lineNum, spanS, spanE);
                if (!table.InsertLeaf(decoded, leaf))
                    throw PureError.DuplicateKey(lineNum, rawKey,
                        PureError.DecodedPath(rawKey), spanS, spanE);
                return;
            }
            var rawSegs = new List<(int s, int e)>();
            CollectSegments(kb, 0, kb.Length, rawSegs);
            EditTable t = table;
            for (int idx = 0; idx < rawSegs.Count; idx++)
            {
                var (slo, shi) = PureUtf8.Trim(kb, rawSegs[idx].s, rawSegs[idx].e);
                if (slo >= shi) throw PureError.EmptyKey(lineNum, spanS, spanE);
                switch (PureKey.CheckKey(kb, slo, shi))
                {
                    case PureKey.KeyValidity.Empty: throw PureError.EmptyKey(lineNum, spanS, spanE);
                    case PureKey.KeyValidity.Invalid:
                        throw PureError.InvalidKey(lineNum, rawKey,
                            PureError.RawPath(rawKey), spanS, spanE);
                }
                string decoded = PureKey.DecodeKeySegmentChecked(kb, slo, shi, lineNum, spanS, spanE);
                if (idx + 1 < rawSegs.Count)
                {
                    var sub = t.Descend(decoded);
                    if (sub == null)
                        throw PureError.KeyPathConflictBlocked(lineNum, rawKey,
                            PureError.DecodedPath(rawKey), spanS, spanE);
                    t = sub;
                }
                else
                {
                    if (!t.InsertLeaf(decoded, leaf))
                        throw PureError.DuplicateKey(lineNum, rawKey,
                            PureError.DecodedPath(rawKey), spanS, spanE);
                    return;
                }
            }
        }

        private byte[] Seg(int lo, int hi)
        {
            var r = new byte[hi - lo];
            Array.Copy(_src, lo, r, 0, hi - lo);
            return r;
        }

        private static void CollectSegments(byte[] b, int lo, int hi, List<(int s, int e)> raw)
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
            raw.AddRange(bounds);
        }

        // ---------------- object pairs / array items ----------------

        private void HandleObjectPair(int lineLo, int hi, int lineNum)
        {
            int lo = lineLo;
            var colon = PureKey.ScanUnescapedColon(_src, lo, hi, out int c);
            switch (colon)
            {
                case PureKey.ColonScanResult.UnterminatedQuote:
                    throw PureError.UnterminatedQuotedKey(lineNum, lo, hi);
                case PureKey.ColonScanResult.Absent:
                    throw PureError.MissingSeparator(lineNum, lo, hi);
            }

            int keyLo = lo, kHi = c;
            while (kHi > keyLo)
            {
                int back = BackWsLen(keyLo, kHi);
                if (back == 0) break;
                kHi -= back;
            }
            if (kHi <= keyLo) throw PureError.EmptyKey(lineNum, lo, lo + 1);

            int after = c + 1;
            bool isRaw = after < hi && _src[after] == (byte)':';

            if (isRaw)
            {
                var vlo = PureUtf8.TrimStart(_src, after + 1, hi);
                var leaf = new Node { Slot = Slot.Pair(c, vlo, hi) };
                InsertNodePair(TopObject(), keyLo, kHi, leaf, lineNum, lo, hi);
                return;
            }

            int valueAbs = PureUtf8.TrimStart(_src, after, hi);
            switch (ClassifyShape(after, hi))
            {
                case Shape.Scalar:
                case Shape.EmptyString:
                    InsertNodePair(TopObject(), keyLo, kHi,
                        new Node { Slot = Slot.Pair(c, valueAbs, hi) }, lineNum, lo, hi);
                    break;
                case Shape.EmptyObject:
                    InsertNodePair(TopObject(), keyLo, kHi,
                        new Node { IsObject = true, Table = new EditTable(), Slot = Slot.Pair(c, valueAbs, hi) },
                        lineNum, lo, hi);
                    break;
                case Shape.EmptyArray:
                    InsertNodePair(TopObject(), keyLo, kHi,
                        new Node { Items = new List<Node>(), Slot = Slot.Pair(c, valueAbs, hi) },
                        lineNum, lo, hi);
                    break;
                case Shape.OpenObject:
                    SetPending(keyLo, kHi, c);
                    _stack.Add(new Frame(isObject: true, valueAbs));
                    break;
                case Shape.OpenArray:
                    SetPending(keyLo, kHi, c);
                    _stack.Add(new Frame(isObject: false, valueAbs));
                    break;
                case Shape.OpenMl1:
                    SetPending(keyLo, kHi, c);
                    _collecting = 1;
                    _collectingOpener = valueAbs;
                    break;
                case Shape.OpenMl2:
                    SetPending(keyLo, kHi, c);
                    _collecting = 2;
                    _collectingOpener = valueAbs;
                    break;
                case Shape.Inline:
                {
                    var node = PositionInlineTop(valueAbs, hi, lineNum, _src[valueAbs] == (byte)'{');
                    node.Inline = false;
                    node.Slot = Slot.Pair(c, valueAbs, hi);
                    InsertNodePair(TopObject(), keyLo, kHi, node, lineNum, lo, hi);
                    break;
                }
            }
        }

        private int BackWsLen(int klo, int khi)
        {
            for (int start = Math.Max(klo, khi - 3); start < khi; start++)
            {
                int len = PureUtf8.WsLenAt(_src, start, khi);
                if (len > 0 && start + len == khi) return len;
            }
            return 0;
        }

        private void HandleArrayItem(int lo, int hi, int lineNum)
        {
            if (hi - lo >= 2 && _src[lo] == (byte)':' && _src[lo + 1] == (byte)':')
            {
                var body = PureUtf8.TrimStart(_src, lo + 2, hi);
                PushItem(new Node { Slot = Slot.Item(lo, body, hi) });
                return;
            }
            int bodyAbs = PureUtf8.TrimStart(_src, lo, hi);
            switch (ClassifyShape(lo, hi))
            {
                case Shape.Scalar:
                case Shape.EmptyString:
                    PushItem(new Node { Slot = Slot.Item(lo, bodyAbs, hi) });
                    break;
                case Shape.EmptyObject:
                    PushItem(new Node { IsObject = true, Table = new EditTable(), Slot = Slot.Item(lo, bodyAbs, hi) });
                    break;
                case Shape.EmptyArray:
                    PushItem(new Node { Items = new List<Node>(), Slot = Slot.Item(lo, bodyAbs, hi) });
                    break;
                case Shape.OpenObject:
                    _stack.Add(new Frame(true, bodyAbs));
                    break;
                case Shape.OpenArray:
                    _stack.Add(new Frame(false, bodyAbs));
                    break;
                case Shape.OpenMl1:
                    _collecting = 1;
                    _collectingOpener = bodyAbs;
                    break;
                case Shape.OpenMl2:
                    _collecting = 2;
                    _collectingOpener = bodyAbs;
                    break;
                case Shape.Inline:
                {
                    var node = PositionInlineTop(bodyAbs, hi, lineNum, _src[bodyAbs] == (byte)'{');
                    node.Inline = false;
                    node.Slot = Slot.Item(lo, bodyAbs, hi);
                    PushItem(node);
                    break;
                }
            }
        }

        private enum Shape { Scalar, EmptyString, EmptyObject, EmptyArray, OpenObject, OpenArray, OpenMl1, OpenMl2, Inline }

        /// <summary>Shape-only classification over the trimmed line with the
        /// same decisions as ClassifyValueStart; malformed inline bodies throw
        /// the parser's own PureError (unreachable after a successful parse).</summary>
        private Shape ClassifyShape(int lo, int hi)
        {
            int tlo = PureUtf8.TrimStart(_src, lo, hi);
            if (tlo >= hi) return Shape.EmptyString;
            int first = _src[tlo];
            if (first == (byte)'{')
            {
                if (hi - tlo == 1) return Shape.OpenObject;
                var (blo, bhi) = PureUtf8.Trim(_src, tlo + 1, hi - 1);
                if (_src[hi - 1] == (byte)'}' && blo >= bhi) return Shape.EmptyObject;
                return Shape.Inline;
            }
            if (first == (byte)'[')
            {
                if (hi - tlo == 1) return Shape.OpenArray;
                var (blo, bhi) = PureUtf8.Trim(_src, tlo + 1, hi - 1);
                if (_src[hi - 1] == (byte)']' && blo >= bhi) return Shape.EmptyArray;
                return Shape.Inline;
            }
            if (hi - tlo == 1 && first == (byte)'(') return Shape.OpenMl1;
            if (hi - tlo == 2 && first == (byte)'(' && _src[tlo + 1] == (byte)'(') return Shape.OpenMl2;
            if ((hi - tlo == 2 && first == (byte)'(' && _src[tlo + 1] == (byte)')') ||
                (hi - tlo == 4 && first == (byte)'(' && _src[tlo + 1] == (byte)'(' &&
                 _src[tlo + 2] == (byte)')' && _src[tlo + 3] == (byte)')'))
                return Shape.EmptyString;
            return Shape.Scalar;
        }

        private void SetPending(int kLo, int kHi, int anchor)
        {
            var f = _stack[_stack.Count - 1];
            f.HasPending = true;
            f.PendingKeyBytes = Seg(kLo, kHi);
            f.PendingAnchor = anchor;
        }

        private EditTable TopObject() => _stack[_stack.Count - 1].Table!;

        private void PushItem(Node node)
        {
            var f = _stack[_stack.Count - 1];
            f.Items!.Add(node);
        }

        private void AttachScalar(Node node, int lineNum)
        {
            var f = _stack[_stack.Count - 1];
            if (f.IsObject)
            {
                if (!f.HasPending)
                    throw PureError.Other(lineNum, "internal: multiline closed without pending key", 0, 0);
                f.HasPending = false;
                var keyBytes = f.PendingKeyBytes!;
                // Re-anchor the block slot to the opening pair's colon.
                if (node.Slot.HasValue && node.Slot.Value.Kind == SlotKind.Block)
                    node.Slot = f.PendingAnchor.HasValue
                        ? Slot.BlockAnchored(f.PendingAnchor.Value, node.Slot.Value.B0, node.Slot.Value.End)
                        : Slot.Block(node.Slot.Value.B0, node.Slot.Value.End);
                f.PendingKeyBytes = null;
                InsertNodeKeyBytes(f.Table!, keyBytes, node, lineNum);
            }
            else
            {
                f.Items!.Add(node);
            }
        }

        private void InsertNodeKeyBytes(EditTable table, byte[] keyBytes, Node node, int lineNum)
        {
            // Split the dotted key into raw segments, then reuse the same
            // per-segment validation as InsertNodePair by delegating to it.
            var rawSegs = new List<(int s, int e)>();
            CollectSegments(keyBytes, 0, keyBytes.Length, rawSegs);
            EditTable t = table;
            for (int idx = 0; idx < rawSegs.Count; idx++)
            {
                var (slo, shi) = PureUtf8.Trim(keyBytes, rawSegs[idx].s, rawSegs[idx].e);
                if (slo >= shi) throw PureError.EmptyKey(lineNum, 0, 0);
                string decoded = PureKey.DecodeKeySegmentChecked(keyBytes, slo, shi, lineNum, 0, 0);
                bool isLeaf = idx + 1 == rawSegs.Count;
                if (!isLeaf)
                {
                    var sub = t.Descend(decoded);
                    if (sub == null)
                        throw PureError.KeyPathConflictBlocked(lineNum, Txt(keyBytes, 0, keyBytes.Length),
                            PureError.DecodedPath(Txt(keyBytes, 0, keyBytes.Length)), 0, 0);
                    t = sub;
                }
                else
                {
                    if (!t.InsertLeaf(decoded, node))
                        throw PureError.DuplicateKey(lineNum, Txt(keyBytes, 0, keyBytes.Length),
                            PureError.DecodedPath(Txt(keyBytes, 0, keyBytes.Length)), 0, 0);
                }
            }
        }

        private void CloseFrame(int lineNum, int ts0, int ts1)
        {
            if (_stack.Count == 1 && _rootIsExplicit)
            {
                _rootConsumed = true;
                _rootSlot = Slot.Block(_stack[0].RangeStart, ts1);
                if (_insertionMode)
                    _rootExplicitNode = new Node { IsObject = _stack[0].IsObject, Table = _stack[0].Table, Items = _stack[0].Items, Slot = _rootSlot, Inline = false };
                return;
            }
            if (_stack.Count <= 1)
                throw PureError.UnbalancedBracket(lineNum, ts0, ts1, '}', '{');
            var frame = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            // The anchor colon lives on the PARENT frame (the pair that
            // opened this block); an array-parent block has no anchor.
            var parent = _stack[_stack.Count - 1];
            int? anchor = parent.IsObject && parent.HasPending ? parent.PendingAnchor : null;
            var node = new Node
            {
                IsObject = frame.IsObject,
                Table = frame.Table,
                Items = frame.Items,
                Inline = false,
                Slot = anchor.HasValue
                    ? Slot.BlockAnchored(anchor.Value, frame.RangeStart, ts1)
                    : Slot.Block(frame.RangeStart, ts1),
            };
            if (parent.IsObject)
            {
                if (!parent.HasPending)
                    throw PureError.Other(lineNum, "internal: closed compound without pending key", ts0, ts1);
                parent.HasPending = false;
                var keyBytes = parent.PendingKeyBytes!;
                parent.PendingKeyBytes = null;
                InsertNodeKeyBytes(parent.Table!, keyBytes, node, lineNum);
            }
            else
            {
                parent.Items!.Add(node);
            }
        }

        private Node Finish(int eofOffset)
        {
            if (_collecting != 0)
                throw PureError.UnclosedCompound(_collecting == 1 ? "stripped" : "verbatim", 0, eofOffset);
            if (_stack.Count > 1)
                throw PureError.UnclosedCompound("object", 0, eofOffset);
            if (_rootIsExplicit && !_rootConsumed)
                throw PureError.UnclosedCompound("object", 0, eofOffset);
            if (_insertionMode && _rootExplicitNode != null) return _rootExplicitNode;
            if (_rootInline != null) return _rootInline;
            if (_insertionMode && _rootIsExplicit && _rootSlot.HasValue && _stack.Count == 0)
                return new Node { IsObject = true, Table = new EditTable(), Slot = _rootSlot, Inline = false };
            if (_stack.Count == 1)
            {
                var f = _stack[0];
                return new Node { IsObject = f.IsObject, Table = f.Table, Items = f.Items, Slot = null };
            }
            return new Node { IsObject = true, Table = new EditTable(), Slot = null };
        }

        private sealed class Frame
        {
            public readonly bool IsObject;
            public readonly EditTable? Table;
            public List<Node>? Items;
            public bool HasPending;
            public byte[]? PendingKeyBytes;
            public int? PendingAnchor;
            public readonly int RangeStart;

            public Frame(bool isObject, int rangeStart)
            {
                IsObject = isObject;
                RangeStart = rangeStart;
                if (isObject) Table = new EditTable();
                else Items = new List<Node>();
            }
        }
    }
}

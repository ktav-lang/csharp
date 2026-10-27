using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

/// <summary>Pure-backend lossless source removal.</summary>
internal static class PureRemove
{
    internal static string Apply(byte[] src, string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var original = PureParser.ParseLax(src);
        return ApplyCore(src, PureEdit.ParsePath(path), original);
    }

    internal static string ApplySegments(byte[] src, IReadOnlyList<string> segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        var original = PureParser.ParseLax(src);
        for (int i = 0; i < segments.Count; i++)
            if (segments[i] == null) throw new ArgumentException("null path segment", nameof(segments));
        return ApplyCore(src, segments, original);
    }

    private static string ApplyCore(byte[] src, IReadOnlyList<string> path, KtavValue original)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (path.Count == 0 || PureEdit.LookupValue(original, path) == null)
            throw KtavException.EditNotFound();
        var (root, _) = PureEdit.BuildPositionForRemove(src);
        var node = root;
        foreach (var seg in path)
        {
            if (node.IsObject) node = node.Table!.Get(seg) ?? throw KtavException.EditNotFound();
            else if (node.IsArray)
            {
                int index = PureEdit.ParseIndex(seg);
                if (index < 0 || index >= node.Items!.Count) throw KtavException.EditNotFound();
                node = node.Items[index];
            }
            else throw KtavException.EditNotFound();
        }
        if (!node.Slot.HasValue)
            throw KtavException.EditUnsupported("target is a noncontiguous dotted-key/reopen value; no lossless removal range exists");

        var slot = node.Slot.Value;
        int from, to;
        if (node.Inline)
            (from, to) = InlineRange(src, root, path, slot);
        else
        {
            int anchor = slot.A;
            int end = slot.Kind == PureEdit.SlotKind.Block ? slot.End : slot.B1;
            from = anchor;
            while (from > 0 && src[from - 1] != (byte)'\n' && src[from - 1] != (byte)'\r') from--;
            if (from == 0) from = PureUtf8.BomLen(src);
            to = end;
            while (to < src.Length && src[to] != (byte)'\n' && src[to] != (byte)'\r') to++;
            if (to < src.Length)
            {
                if (src[to] == (byte)'\r' && to + 1 < src.Length && src[to + 1] == (byte)'\n') to += 2;
                else to++;
            }
            if (to == src.Length && to > from && src[to - 1] != (byte)'\n' && src[to - 1] != (byte)'\r' && from > 0 && (src[from - 1] == (byte)'\n' || src[from - 1] == (byte)'\r'))
            {
                from--;
                if (from > 0 && src[from - 1] == (byte)'\r' && src[from] == (byte)'\n') from--;
            }
        }

        if (HasComment(src, from, to, node))
            throw KtavException.EditUnsupported("target range contains comments; removal cannot preserve them losslessly");

        var expected = RemoveSemantic(original, path, 0);
        var candidate = Splice(src, from, to);
        if (!Matches(candidate, expected))
        {
            var materialized = MaterializeDottedParent(src, from, to, path, root, expected);
            if (materialized == null || !Matches(materialized, expected))
                throw KtavException.EditUnsupported("semantic mismatch after removal splice; no lossless patch produced");
            candidate = materialized;
        }
        return PureUtf8.StrictUtf8.GetString(candidate);
    }

    private static (int, int) InlineRange(byte[] src, PureEdit.Node root, IReadOnlyList<string> path, PureEdit.Slot slot)
    {
        var (_, parents) = SplitLast(path);
        PureEdit.Node parent = root;
        foreach (var key in parents)
        {
            if (parent.IsObject) parent = parent.Table!.Get(key) ?? throw KtavException.EditNotFound();
            else if (parent.IsArray) parent = parent.Items![PureEdit.ParseIndex(key)];
            else throw KtavException.EditNotFound();
        }
        int p0, p1;
        if (parent.Slot.HasValue)
        {
            var ps = parent.Slot.Value;
            if (ps.Kind == PureEdit.SlotKind.Block) throw KtavException.EditUnsupported("internal: inline target in block parent");
            p0 = ps.B0; p1 = ps.B1;
        }
        else if (parents.Count == 0 && root.Inline)
        {
            // Root slot is the parsed value only; preserve surrounding trivia.
            var rootSlot = root.Slot;
            if (rootSlot.HasValue) { p0 = rootSlot.Value.B0; p1 = rootSlot.Value.B1; }
            else throw KtavException.EditUnsupported("inline root has no positional range");
        }
        else throw KtavException.EditUnsupported("inline target has no inline parent range");
        string body = PureEdit.Txt(src, p0, p1);
        int bodyByteStart = p0;
        int target = slot.A;
        var bytes = PureUtf8.StrictUtf8.GetBytes(body);
        bool obj = body.StartsWith("{", StringComparison.Ordinal);
        var parts = SplitInline(bytes, 1, bytes.Length - 1, obj);
        var spans = new List<(int s, int e)>();
        foreach (var part in parts)
        {
            var trimmed = PureUtf8.TrimInline(bytes, part.s, part.e);
            if (trimmed.Item1 < trimmed.Item2) spans.Add((bodyByteStart + trimmed.Item1, bodyByteStart + trimmed.Item2));
        }
        int pos = spans.FindIndex(x => x.s <= target && target < x.e);
        if (pos < 0) throw KtavException.EditUnsupported("inline target segment not found");
        if (spans.Count == 1) return (p0 + 1, p1 - 1);
        return pos + 1 < spans.Count ? (spans[pos].s, spans[pos + 1].s) : (spans[pos - 1].e, spans[pos].e);
    }

    private static List<(int s, int e)> SplitInline(byte[] bytes, int lo, int hi, bool obj)
    {
        var segments = new List<(int s, int e)>();
        PureInline.SplitTopLevel(bytes, lo, hi, obj, 1, lo, hi, null, ContainsQuote(bytes), segments, out bool unterminated);
        if (unterminated) throw KtavException.EditUnsupported("cannot split inline parent for removal");
        return segments;
    }

    private static bool ContainsQuote(byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++) if (bytes[i] == '"' || bytes[i] == '\'' || bytes[i] == '`') return true;
        return false;
    }

    private static (string, IReadOnlyList<string>) SplitLast(IReadOnlyList<string> path) { var p = new List<string>(); for (int i = 0; i < path.Count - 1; i++) p.Add(path[i]); return (path[path.Count - 1], p); }

    private static bool HasComment(byte[] src, int from, int to, PureEdit.Node node)
    {
        var ranges = new List<(int s, int e)>(); CollectMultiline(node, ranges);
        int i = from;
        while (i < to) { int end = i; while (end < to && src[end] != 10 && src[end] != 13) end++; int k = PureUtf8.TrimStart(src, i, end); bool data = ranges.Exists(r => r.s <= k && k < r.e); if (!data && k + 1 < end && src[k] == '#' && src[k + 1] == '#') return true; i = end; while (i < to && (src[i] == 10 || src[i] == 13)) i++; }
        return false;
    }
    private static void CollectMultiline(PureEdit.Node n, List<(int s, int e)> r) { if (n.IsScalar && n.Slot.HasValue && n.Slot.Value.Kind == PureEdit.SlotKind.Block) { r.Add((n.Slot.Value.B0, n.Slot.Value.End)); return; } if (n.IsObject) foreach (var e in n.Table!.Entries) CollectMultiline(e.Value, r); else if (n.IsArray) foreach (var x in n.Items!) CollectMultiline(x, r); }

    private static byte[]? MaterializeDottedParent(byte[] src, int from, int to, IReadOnlyList<string> path, PureEdit.Node root, KtavValue expected)
    {
        var direct = Splice(src, from, to);
        if (Matches(direct, expected)) return direct;

        var (_, parentPath) = SplitLast(path);
        if (parentPath.Count == 0) return null;
        var ancestors = new List<string>(parentPath);
        int emptiedFrom = -1;
        for (int start = 0; start < ancestors.Count; start++)
        {
            bool allEmpty = true;
            for (int k = start; k < ancestors.Count; k++)
            {
                var at = PureEdit.LookupValue(expected, ancestors.GetRange(0, k + 1));
                if (at is not KtavObject obj || obj.Entries.Count != 0) { allEmpty = false; break; }
            }
            if (allEmpty) { emptiedFrom = start; break; }
        }
        if (emptiedFrom < 0) return null;

        int containerDepth = 0;
        for (int i = 0; i < ancestors.Count; i++)
        {
            var n = root;
            bool found = true;
            for (int j = 0; j <= i; j++)
            {
                if (n.IsObject) n = n.Table!.Get(ancestors[j])!;
                else if (n.IsArray)
                {
                    int index = PureEdit.ParseIndex(ancestors[j]);
                    if (index < 0 || index >= n.Items!.Count) { found = false; break; }
                    n = n.Items[index];
                }
                else { found = false; break; }
                if (n == null) { found = false; break; }
            }
            if (found && n.Slot.HasValue) containerDepth = i + 1;
        }
        if (emptiedFrom < containerDepth) return null;

        int lineStart = from;
        while (lineStart < to && (src[lineStart] == (byte)'\r' || src[lineStart] == (byte)'\n')) lineStart++;
        int indentEnd = PureUtf8.TrimStart(src, lineStart, to);
        string indent = PureUtf8.StrictUtf8.GetString(src, lineStart, indentEnd - lineStart);
        var key = new StringBuilder();
        for (int i = containerDepth; i < ancestors.Count; i++)
        {
            if (i > containerDepth) key.Append('.');
            PureRender.PushEscapedKeySegment(ancestors[i], false, key);
        }
        var replacement = new StringBuilder();
        replacement.Append(PureUtf8.StrictUtf8.GetString(src, from, lineStart - from));
        replacement.Append(indent).Append(key).Append(": {}");
        int eolStart = to;
        if (eolStart > from && src[eolStart - 1] == (byte)'\n') { eolStart--; if (eolStart > from && src[eolStart - 1] == (byte)'\r') eolStart--; }
        else if (eolStart > from && src[eolStart - 1] == (byte)'\r') eolStart--;
        replacement.Append(PureUtf8.StrictUtf8.GetString(src, eolStart, to - eolStart));
        var rep = PureUtf8.StrictUtf8.GetBytes(replacement.ToString());
        var result = new byte[src.Length - (to - from) + rep.Length];
        Buffer.BlockCopy(src, 0, result, 0, from); Buffer.BlockCopy(rep, 0, result, from, rep.Length);
        Buffer.BlockCopy(src, to, result, from + rep.Length, src.Length - to);
        return result;
    }
    private static bool Matches(byte[] bytes, KtavValue expected) { try { return PureEdit.EditValuesEqual(PureParser.ParseLax(bytes), expected); } catch (KtavException) { return false; } }
    private static KtavValue RemoveSemantic(KtavValue value, IReadOnlyList<string> path, int depth)
    {
        string key = path[depth]; bool last = depth + 1 == path.Count;
        if (value is KtavObject obj) { var entries = new List<KeyValuePair<string,KtavValue>>(); bool found = false; foreach (var e in obj.Entries) { if (!found && e.Key == key) { found = true; if (!last) entries.Add(new(e.Key, RemoveSemantic(e.Value,path,depth+1))); } else entries.Add(e); } if (!found) throw KtavException.EditNotFound(); return new KtavObject(entries); }
        if (value is KtavArray array) { int i = PureEdit.ParseIndex(key); if (i < 0 || i >= array.Items.Count) throw KtavException.EditNotFound(); var items = new List<KtavValue>(array.Items); if (last) items.RemoveAt(i); else items[i] = RemoveSemantic(items[i],path,depth+1); return new KtavArray(items); }
        throw KtavException.EditNotFound();
    }
    private static byte[] Splice(byte[] src, int from, int to) { var r = new byte[src.Length - (to-from)]; Buffer.BlockCopy(src,0,r,0,from); Buffer.BlockCopy(src,to,r,from,src.Length-to); return r; }
}

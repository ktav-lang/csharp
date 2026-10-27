using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureRename
{
    internal static string Apply(byte[] src, string path, string newKey)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var original = PureParser.ParseLax(src);
        return ApplyCore(src, PureEdit.ParsePath(path), newKey, original);
    }

    internal static string ApplySegments(byte[] src, IReadOnlyList<string> segments, string newKey)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        var original = PureParser.ParseLax(src);
        for (int i = 0; i < segments.Count; i++) if (segments[i] == null) throw new ArgumentException("null path segment", nameof(segments));
        return ApplyCore(src, segments, newKey, original);
    }

    private static string ApplyCore(byte[] src, IReadOnlyList<string> path, string newKey, KtavValue original)
    {
        if (newKey == null) throw new ArgumentNullException(nameof(newKey));
        if (path.Count == 0) throw KtavException.EditNotFound();
        if (newKey.Length == 0) throw KtavException.EditUnsupported("empty key name is not representable");
        for (int i = 0; i < path.Count; i++) if (path[i] == null) throw new ArgumentException("null path segment", nameof(path));
        var parentPath = new List<string>();
        for (int i = 0; i + 1 < path.Count; i++) parentPath.Add(path[i]);
        string key = path[path.Count - 1];
        var parentValue = PureEdit.LookupValue(original, parentPath);
        if (parentValue is KtavArray) throw KtavException.EditUnsupported("rename of an Array element is not supported; only Object keys can be renamed");
        if (parentValue is not KtavObject obj) throw KtavException.EditNotFound();
        int targetIndex = -1;
        for (int i = 0; i < obj.Entries.Count; i++) if (obj.Entries[i].Key == key) { targetIndex = i; break; }
        if (targetIndex < 0) throw KtavException.EditNotFound();
        if (key == newKey) return PureUtf8.StrictUtf8.GetString(src);
        foreach (var entry in obj.Entries) if (entry.Key == newKey) throw KtavException.EditAlreadyExists();

        var (root, _) = PureEdit.BuildPositionForRemove(src);
        var parent = root;
        foreach (var seg in parentPath)
        {
            if (parent.IsObject) parent = parent.Table!.Get(seg) ?? throw KtavException.EditNotFound();
            else if (parent.IsArray) { int ix = PureEdit.ParseIndex(seg); if (ix < 0 || ix >= parent.Items!.Count) throw KtavException.EditNotFound(); parent = parent.Items[ix]; }
            else throw KtavException.EditNotFound();
        }
        if (!parent.IsObject || parent.Table == null) throw KtavException.EditNotFound();
        var target = parent.Table.Get(key) ?? throw KtavException.EditNotFound();
        if (target.Slot == null) throw KtavException.EditUnsupported("target is a noncontiguous value (dotted-key/reopen intermediate); no lossless rename range exists");
        var slot = target.Slot.Value;
        if (slot.Kind == PureEdit.SlotKind.Item || (slot.Kind == PureEdit.SlotKind.Block && !slot.HasAnchor))
            throw KtavException.EditUnsupported("rename of an Array element is not supported; only Object keys can be renamed");
        int colon = slot.A;
        // Pair.A is the colon byte. Restrict inline keys to their pair instead
        // of starting at the physical line's beginning (which includes the
        // containing object and earlier inline pairs).
        int lineStart = colon;
        while (lineStart > 0 && src[lineStart - 1] != (byte)'\n' && src[lineStart - 1] != (byte)'\r') lineStart--;
        if (lineStart == 0) lineStart = PureUtf8.BomLen(src);
        int keyStart = FindInlinePairStart(src, lineStart, colon, parent.Slot, parentPath.Count == 0);
        int keyEnd = colon;
        while (keyEnd > keyStart)
        {
            int ws = BackWhitespaceLen(src, keyStart, keyEnd);
            if (ws == 0) break;
            keyEnd -= ws;
        }
        int begin = FindLastSegment(src, keyStart, keyEnd);
        if (begin >= keyEnd) throw KtavException.EditUnsupported("internal: empty key range");
        // Ensure the selected byte range is exactly the target grammar segment;
        // do not splice a nearby token if source layout is unexpected.
        string selected;
        try { selected = PureKey.DecodeKeySegment(src, begin, keyEnd); }
        catch { throw KtavException.EditUnsupported("internal: invalid key range"); }
        if (selected != key) throw KtavException.EditUnsupported("internal: key range does not match target");
        // Preserve the prefix in dotted definitions: the target is the final
        // lexical segment, while the key expression may contain earlier ones.
        int end = keyEnd;
        bool rootFirst = parentPath.Count == 0 && parent.Table!.Entries.Count > 0 && parent.Table.Entries[0].Key == key;
        var encoded = new StringBuilder();
        PureRender.PushEscapedKeySegment(newKey, rootFirst, encoded);
        string text = PureUtf8.StrictUtf8.GetString(src);
        int endChar = Encoding.UTF8.GetCharCount(src, 0, end);
        int beginChar = Encoding.UTF8.GetCharCount(src, 0, begin);
        string result = text.Substring(0, beginChar) + encoded + text.Substring(endChar);

        var expectedEntries = new List<KeyValuePair<string, KtavValue>>(obj.Entries.Count);
        for (int i = 0; i < obj.Entries.Count; i++) expectedEntries.Add(new KeyValuePair<string, KtavValue>(i == targetIndex ? newKey : obj.Entries[i].Key, obj.Entries[i].Value));
        var expectedParent = new KtavObject(expectedEntries);
        var expected = ReplaceParent(original, parentPath, 0, expectedParent);
        KtavValue actual;
        try { actual = PureParser.ParseLax(PureUtf8.StrictUtf8.GetBytes(result)); }
        catch (KtavException e)
        { throw KtavException.EditUnsupported("patch produced an invalid document: " + e.Message); }
        if (!PureEdit.EditValuesEqual(actual, expected))
            throw KtavException.EditUnsupported("semantic mismatch after splice; no lossless edit produced");
        return result;
    }

    private static int FindInlinePairStart(byte[] src, int start, int colon, PureEdit.Slot? parentSlot, bool rootParent)
    {
        // Determine the containing inline-object range from the validated positional tree,
        // then use PureInline's grammar-aware scan and split decisions rather than
        // interpreting punctuation in scalar text as nested syntax.
        int lo, hi;
        if (parentSlot.HasValue && (parentSlot.Value.Kind == PureEdit.SlotKind.Pair || parentSlot.Value.Kind == PureEdit.SlotKind.Item))
        {
            lo = parentSlot.Value.B0; hi = parentSlot.Value.B1;
        }
        else return start;
        lo = PureUtf8.TrimStart(src, lo, hi);
        hi = PureUtf8.TrimEnd(src, lo, hi);
        if (lo >= hi || src[lo] != (byte)'{') return start;
        int found = PureInline.ScanInlineCloserWithBounds(src, lo, hi, '{', '}', 1, lo, hi,
            out var pairs, out _);
        if (found < 0) return start;
        var bounds = new PureInline.InlineBounds(pairs);
        var segments = new List<(int s, int e)>();
        PureInline.SplitTopLevel(src, lo + 1, found, true, 1, lo, found,
            bounds, PureKey.HasQuoteBytes(src, lo, found), segments, out _);
        foreach (var (s, e) in segments)
        {
            int c = s;
            if (PureKey.FindUnescapedColon(src, s, e, out c) && c == colon) return s;
        }
        return start;
    }

    private static int BackWhitespaceLen(byte[] b, int lo, int hi)
    {
        for (int start = Math.Max(lo, hi - 3); start < hi; start++)
        {
            int len = PureUtf8.WsLenAt(b, start, hi);
            if (len > 0 && start + len == hi) return len;
        }
        return 0;
    }

    private static bool IsWsAt(byte[] b, int i, int end) => PureUtf8.WsLenAt(b, i, end) > 0;

    private static int FindSegmentEnd(byte[] b, int start, int end)
    {
        int i = start;
        if (i < end && (b[i] == (byte)'\'' || b[i] == (byte)'"' || b[i] == (byte)'`'))
        {
            byte quote = b[i++];
            while (i < end)
            {
                if (b[i] == (byte)'\\') i += Math.Min(2, end - i);
                else if (b[i++] == quote) break;
            }
            return i;
        }
        while (i < end)
        {
            if (b[i] == (byte)'\\') i += Math.Min(2, end - i);
            else if (b[i] == (byte)'.') break;
            else i++;
        }
        while (i > start)
        {
            int ws = BackWhitespaceLen(b, start, i);
            if (ws == 0) break;
            i -= ws;
        }
        return i;
    }
    private static int FindLastSegment(byte[] b, int start, int end)
    {
        int i = start, boundary = start; bool segStart = true;
        while (i < end)
        {
            if (segStart)
            {
                while (i < end && IsWsAt(b, i, end)) i += PureUtf8.WsLenAt(b, i, end);
                if (i < end && (b[i] == (byte)'\'' || b[i] == (byte)'"' || b[i] == (byte)'`'))
                {
                    byte q = b[i++]; while (i < end) { if (b[i] == (byte)'\\') i += Math.Min(2, end - i); else if (b[i++] == q) break; }
                    segStart = false; continue;
                }
                segStart = false;
            }
            if (b[i] == (byte)'\\') { i += Math.Min(2, end - i); continue; }
            if (b[i] == (byte)'.') { boundary = ++i; segStart = true; continue; }
            i++;
        }
        while (boundary < end && IsWsAt(b, boundary, end)) boundary += PureUtf8.WsLenAt(b, boundary, end);
        return boundary;
    }
    private static KtavValue ReplaceParent(KtavValue value, IReadOnlyList<string> path, int depth, KtavValue replacement)
    {
        if (depth == path.Count) return replacement;
        string key = path[depth];
        if (value is KtavObject o)
        {
            var entries = new List<KeyValuePair<string, KtavValue>>(o.Entries.Count);
            foreach (var e in o.Entries) entries.Add(new KeyValuePair<string, KtavValue>(e.Key, e.Key == key ? ReplaceParent(e.Value, path, depth + 1, replacement) : e.Value));
            return new KtavObject(entries);
        }
        if (value is KtavArray a) { int ix = PureEdit.ParseIndex(key); var items = new List<KtavValue>(a.Items); items[ix] = ReplaceParent(items[ix], path, depth + 1, replacement); return new KtavArray(items); }
        throw KtavException.EditNotFound();
    }
}

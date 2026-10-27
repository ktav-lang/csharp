using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

/// <summary>Lossless insertion into an existing object. Array elements are
/// addressable intermediates; the final segment must name a new object key.</summary>
internal static class PureInsert
{
    internal static string Apply(byte[] src, string path, KtavValue value)
    {
        var original = PureParser.ParseLax(src);
        var segments = PureEdit.ParsePath(path);
        return ApplyCore(src, segments, value, original);
    }

    internal static string ApplySegments(byte[] src, IReadOnlyList<string> segments, KtavValue value)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        var original = PureParser.ParseLax(src);
        for (int i = 0; i < segments.Count; i++) if (segments[i] == null) throw new ArgumentException("null path segment", nameof(segments));
        return ApplyCore(src, segments, value, original);
    }

    private static string ApplyCore(byte[] src, IReadOnlyList<string> segments, KtavValue value, KtavValue original)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (segments.Count == 0) throw KtavException.EditNotFound();
        for (int i = 0; i < segments.Count; i++) if (segments[i] == null) throw new ArgumentException("null path segment", nameof(segments));
        value = PureEdit.NormalizeWireValue(value);
        if (!PureEditWithinSource(value)) throw KtavException.EditUnsupported("integer insertion is outside the i64 source domain");

        var parentPath = new List<string>();
        for (int i = 0; i + 1 < segments.Count; i++) parentPath.Add(segments[i]);
        var parentValue = PureEdit.LookupValue(original, parentPath);
        if (parentValue == null) throw KtavException.EditNotFound();
        if (parentValue is KtavArray) throw KtavException.EditUnsupported("insertion into an Array is not supported; only Object members can be created");
        if (parentValue is not KtavObject) throw KtavException.EditNotFound();
        foreach (var entry in ((KtavObject)parentValue).Entries)
            if (entry.Key == segments[segments.Count - 1]) throw KtavException.EditAlreadyExists();

        var (root, _) = PureEdit.BuildPosition(src);
        var parent = root;
        for (int i = 0; i < parentPath.Count; i++)
        {
            string segment = parentPath[i];
            if (parent.IsObject) parent = parent.Table!.Get(segment) ?? throw KtavException.EditNotFound();
            else if (parent.IsArray)
            {
                int ix = PureEdit.ParseIndex(segment);
                if (ix < 0 || ix >= parent.Items!.Count) throw KtavException.EditNotFound();
                parent = parent.Items[ix];
            }
            else throw KtavException.EditNotFound();
        }
        if (!parent.IsObject || parent.Table == null)
        {
            if (parent.IsArray) throw KtavException.EditUnsupported("insertion into an Array is not supported; only Object members can be created");
            throw KtavException.EditNotFound();
        }
        if (parent.Slot == null && parent != root)
            throw KtavException.EditUnsupported("target parent is a noncontiguous dotted-key/reopen object");
        if (parent == root && !root.IsObject)
            throw KtavException.EditUnsupported("cannot insert an object key into an array root");

        string key = segments[segments.Count - 1];
        if (key.Length == 0) throw KtavException.EditNotFound();
        bool inlineContainer = parent.Inline || (parent.Slot.HasValue && parent.Slot.Value.Kind != PureEdit.SlotKind.Block && parent.Slot.Value.B0 < src.Length && src[parent.Slot.Value.B0] == (byte)'{');
        string? insertionIndent = null;
        if (!inlineContainer && parent.Table.Entries.Count > 0)
        {
            var last = parent.Table.Entries[parent.Table.Entries.Count - 1].Value;
            if (last.Slot.HasValue)
            {
                var lastSlot = last.Slot.Value;
                insertionIndent = PureEdit.LineIndent(src, lastSlot.A);
            }
        }
        if (!inlineContainer && parent.Table.Entries.Count == 0 && parent.Slot.HasValue && parent.Slot.Value.Kind == PureEdit.SlotKind.Block)
            insertionIndent = PureEdit.LineIndent(src, parent.Slot.Value.A) + "    ";
        string eolForRender = PureEdit.DetectEol(src, parent.Slot?.End ?? src.Length);
        string entryText = PureInsertRender.RenderPair(key, value, new PureInsertRender.Context(inlineContainer, insertionIndent ?? "", eolForRender, parent == root));
        byte[] output;
        int spliceFrom = 0, spliceTo = 0, spliceInserted = 0;
        bool adjustInline = false;
        if (parent == root)
        {
            if (!root.IsObject) throw KtavException.EditUnsupported("cannot insert an object key into an array root");
            if (root.Inline && root.Slot.HasValue)
            {
                var slot = root.Slot.Value;
                int close = slot.B1 - 1;
                int insertion = close;
                insertion = PureUtf8.TrimInline(src, slot.B0, close).Item2;
                bool hasContent = root.Table!.Entries.Count > 0;
                int contentEnd = insertion;
                contentEnd = PureUtf8.TrimInline(src, slot.B0, insertion).Item2;
                int slashes = 0;
                for (int p = contentEnd - 2; p >= slot.B0 && src[p] == (byte)'\\'; p--) slashes++;
                bool trailingComma = hasContent && contentEnd > slot.B0 && src[contentEnd - 1] == (byte)',' && slashes % 2 == 0;
                insertion = trailingComma ? contentEnd : close;
                bool emptyInterior = !hasContent;
                if (emptyInterior) insertion = slot.B0 + 1;
                string addition = (hasContent ? (trailingComma ? " " : ", ") : "") + entryText + (trailingComma ? "," : "");
                int to = emptyInterior ? close : insertion;
                output = Splice(src, insertion, to, addition);
                spliceFrom = insertion; spliceTo = to; spliceInserted = PureUtf8.StrictUtf8.GetByteCount(addition); adjustInline = true;
            }
            else if (root.Slot.HasValue && root.Slot.Value.Kind == PureEdit.SlotKind.Block)
            {
                var slot = root.Slot.Value;
                int insertAt = slot.End;
                while (insertAt > 0 && src[insertAt - 1] != (byte)'\n' && src[insertAt - 1] != (byte)'\r') insertAt--;
            string indent = root.Table!.Entries.Count > 0
                ? ParentIndent(src, root)
                : PureEdit.LineIndent(src, slot.A) + "    ";
            string eol = PureEdit.DetectEol(src, insertAt);
                entryText = PureInsertRender.RenderPair(key, value, new PureInsertRender.Context(false, indent, eol, true));
                output = Splice(src, insertAt, insertAt, indent + entryText + eol);
            }
            else
            {
                int insertAt = RootInsertionPoint(src);
                string eol = PureEdit.DetectEol(src, insertAt);
                bool hasContent = insertAt > 0;
                bool hasFinalEol = src.Length > 0 && (src[src.Length - 1] == (byte)'\n' || src[src.Length - 1] == (byte)'\r');
                string addition = !hasContent ? entryText
                    : hasFinalEol ? entryText + eol
                    : eol + entryText;
                output = Splice(src, insertAt, insertAt, addition);
            }
        }
        else if (inlineContainer)
        {
            var slot = parent.Slot!.Value;
            int close = slot.Kind == PureEdit.SlotKind.Pair ? slot.B1 - 1 : slot.B1 - 1;
            int insertion = close;
            insertion = PureUtf8.TrimInline(src, slot.B0, close).Item2;
            bool hasContent = parent.Table.Entries.Count > 0;
            int contentEnd = insertion;
            int slashCount = 0;
            for (int p = contentEnd - 2; p >= slot.B0 && src[p] == (byte)'\\'; p--) slashCount++;
            bool trailingComma = hasContent && contentEnd > slot.B0 && src[contentEnd - 1] == (byte)',' && slashCount % 2 == 0;
            insertion = trailingComma ? contentEnd : close;
            bool emptyInterior = !hasContent;
            if (emptyInterior) insertion = slot.B0 + 1;
            string addition = (hasContent ? (trailingComma ? " " : ", ") : "") + entryText + (trailingComma ? "," : "");
            int to = emptyInterior ? close : insertion;
            output = Splice(src, insertion, to, addition);
            spliceFrom = insertion; spliceTo = to; spliceInserted = PureUtf8.StrictUtf8.GetByteCount(addition); adjustInline = true;
        }
        else
        {
            var slot = parent.Slot!.Value;
            if (slot.Kind == PureEdit.SlotKind.Block && parent.Table.Entries.Count == 0)
            {
                int closeLine = slot.End;
                int lineStart = closeLine;
                while (lineStart > 0 && src[lineStart - 1] != (byte)'\n' && src[lineStart - 1] != (byte)'\r') lineStart--;
                string eol = PureEdit.DetectEol(src, lineStart);
                output = Splice(src, lineStart, lineStart, (insertionIndent ?? "") + entryText + eol);
            }
            else
            {
                int insertAt = slot.Kind == PureEdit.SlotKind.Block ? slot.End : slot.B1;
                if (slot.Kind == PureEdit.SlotKind.Block)
                {
                    while (insertAt > 0 && src[insertAt - 1] != (byte)'\n' && src[insertAt - 1] != (byte)'\r') insertAt--;
                }
                string indent = insertionIndent ?? "";
                string eol = PureEdit.DetectEol(src, insertAt);
                string prefix = insertAt > 0 && src[insertAt - 1] != (byte)'\n' && src[insertAt - 1] != (byte)'\r' ? eol : "";
                output = Splice(src, insertAt, insertAt, prefix + indent + entryText + eol);
            }
        }

        if (adjustInline)
        {
            var candidate = output;
            output = PureEditLayout.AdjustInsertion(src, candidate, root, parentPath, value, spliceFrom, spliceTo, spliceInserted);
        }
        var result = PureUtf8.StrictUtf8.GetString(output);
        KtavValue parsed;
        try { parsed = PureParser.ParseLax(output); }
        catch (KtavException ke) { throw KtavException.EditUnsupported("patch produced an invalid document: " + ke.Message); }
        if (!PureEdit.EditValuesEqual(parsed, AddSemantic(original, segments, value)))
            throw KtavException.EditUnsupported("semantic mismatch after insertion; no lossless patch produced");
        return result;
    }

    private static KtavValue AddSemantic(KtavValue root, IReadOnlyList<string> path, KtavValue value)
    {
        return AddAt(root, path, 0, value);
    }

    private static KtavValue AddAt(KtavValue node, IReadOnlyList<string> path, int depth, KtavValue value)
    {
        if (depth == path.Count - 1)
        {
            if (node is not KtavObject obj) throw KtavException.EditNotFound();
            var entries = new List<KeyValuePair<string, KtavValue>>(obj.Entries) { new(path[depth], value) };
            return new KtavObject(entries);
        }
        if (node is KtavObject o)
        {
            var entries = new List<KeyValuePair<string, KtavValue>>(o.Entries);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Key == path[depth]) { entries[i] = new(entries[i].Key, AddAt(entries[i].Value, path, depth + 1, value)); return new KtavObject(entries); }
        }
        else if (node is KtavArray a)
        {
            int ix = PureEdit.ParseIndex(path[depth]);
            if (ix >= 0 && ix < a.Items.Count) { var items = new List<KtavValue>(a.Items); items[ix] = AddAt(items[ix], path, depth + 1, value); return new KtavArray(items); }
        }
        throw KtavException.EditNotFound();
    }

    private static bool PureEditWithinSource(KtavValue v) => v switch
    {
        KtavInteger i => long.TryParse(i.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
        KtavArray a => All(a.Items),
        KtavObject o => AllEntries(o.Entries),
        _ => true,
    };
    private static bool All(IReadOnlyList<KtavValue> xs) { foreach (var x in xs) if (!PureEditWithinSource(x)) return false; return true; }
    private static bool AllEntries(IReadOnlyList<KeyValuePair<string, KtavValue>> xs) { foreach (var x in xs) if (!PureEditWithinSource(x.Value)) return false; return true; }

    private static string ParentIndent(byte[] src, PureEdit.Node parent)
    {
        if (parent.Table == null || parent.Table.Entries.Count == 0) return "";
        var child = parent.Table.Entries[parent.Table.Entries.Count - 1].Value;
        return child.Slot.HasValue ? PureEdit.LineIndent(src, child.Slot.Value.A) : "";
    }

    private static int RootInsertionPoint(byte[] src)
    {
        // EOF insertion preserves the source's final-EOL convention; BOM-only is an empty root body.
        return src.Length;
    }
    private static byte[] Splice(byte[] src, int from, int to, string text)
    {
        byte[] inserted = PureUtf8.StrictUtf8.GetBytes(text), output = new byte[src.Length - (to - from) + inserted.Length];
        Buffer.BlockCopy(src, 0, output, 0, from); Buffer.BlockCopy(inserted, 0, output, from, inserted.Length);
        Buffer.BlockCopy(src, to, output, from + inserted.Length, src.Length - to); return output;
    }
}

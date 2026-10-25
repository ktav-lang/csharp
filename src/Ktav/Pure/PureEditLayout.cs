// Minimal inline-wrapper expansion when a splice would hurt readability
// (port of rust/src/document/parser/edit/splice/layout.rs): the containing
// inline wrapper and touched nested wrappers are promoted to block form
// while the criteria hold; untouched nested branches stay inline. New block
// levels use four spaces and the source EOL. If a lossless standalone form
// does not exist (CR, or a line equal to '))'), the valid inline candidate
// is kept instead of failing for formatting reasons.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureEditLayout
{
    internal static byte[] Adjust(byte[] source, byte[] candidate, PureEdit.Node root,
        IReadOnlyList<string> path, KtavValue replacement, PureEdit.Node targetNode, PureEdit.Ctx ctx)
    {
        // Recompute the splice range the same way PureEditSplice did: the
        // target body range, mapped into the candidate.
        if (!targetNode.Slot.HasValue) return candidate;
        var slot = targetNode.Slot.Value;
        int from = slot.B0;
        int to = slot.B1;

        var owner = InlineOwner(source, root, path, from);
        if (owner == null) return candidate;
        var (start, end) = owner.Value;
        if (from < start) return candidate;
        int inserted = candidate.Length - (source.Length - (to - from));
        int newEnd = end - (to - from) + inserted;
        if (newEnd < start || newEnd > candidate.Length) return candidate;
        if (!IsCompound(candidate, start, newEnd)) return candidate;

        var (lineLo, lineHi) = PureEdit.LineBounds(source, from);
        int oldWidth = PureEdit.CountScalars(source, lineLo, lineHi);
        int limit = Math.Max(oldWidth, 100);
        bool force = RequiresBlock(replacement, 0);
        var (candLo, candHi) = PureEdit.LineBounds(candidate, from);
        if (!force && PureEdit.CountScalars(candidate, candLo, candHi) <= limit)
            return candidate;

        string indent = PureEdit.LineIndent(source, start);
        string eol = PureEdit.DetectEol(source, end);
        var expanded = Expand(Slice(candidate, start, newEnd), indent, eol, limit,
            (from - start, from - start + inserted),
            force, 0);
        if (expanded == null) return candidate;

        var result = new byte[start + expanded.Length + (candidate.Length - newEnd)];
        Array.Copy(candidate, 0, result, 0, start);
        Array.Copy(Encoding.UTF8.GetBytes(expanded), 0, result, start, expanded.Length);
        Array.Copy(candidate, newEnd, result, start + expanded.Length, candidate.Length - newEnd);
        return result;
    }

    private static bool IsCompound(string body) =>
        (body.Length > 0 && body[0] == '{' && body[body.Length - 1] == '}') ||
        (body.Length > 0 && body[0] == '[' && body[body.Length - 1] == ']');

    private static bool IsCompound(byte[] b, int lo, int hi) =>
        hi - lo >= 2 && ((b[lo] == (byte)'{' && b[hi - 1] == (byte)'}') ||
                         (b[lo] == (byte)'[' && b[hi - 1] == (byte)']'));

    private static (int, int) SlotBody(PureEdit.Slot slot) =>
        (slot.B0, slot.Kind == PureEdit.SlotKind.Block ? slot.End : slot.B1);

    /// <summary>The smallest inline-compound ancestor containing the splice
    /// point, or the target itself when it lives inside a physically inline
    /// context (root inline / leaf inline flag).</summary>
    private static (int, int)? InlineOwner(byte[] source, PureEdit.Node root,
        IReadOnlyList<string> path, int pos)
    {
        var node = root;
        if (root.Inline)
        {
            var (lineLo, lineHi) = PureEdit.LineBounds(source, pos);
            var (tlo, thi) = PureUtf8.Trim(source, lineLo, lineHi);
            if (tlo < thi && source[tlo] == 0xEF && tlo + 3 <= thi) tlo += 3; // strip BOM
            return (tlo, thi);
        }
        foreach (var segment in path)
        {
            if (node.IsObject)
            {
                var next = node.Table!.Get(segment);
                if (next == null) throw KtavException.EditNotFound();
                node = next;
            }
            else if (node.IsArray)
            {
                int idx = PureEdit.ParseIndex(segment);
                if (idx < 0) throw KtavException.EditNotFound();
                if (idx >= node.Items!.Count) throw KtavException.EditNotFound();
                node = node.Items[idx];
            }
            else throw KtavException.EditNotFound();

            if (node.Slot.HasValue)
            {
                var (s, e) = SlotBody(node.Slot.Value);
                if ((node.IsObject || node.IsArray) && s >= 0 && e <= source.Length && s <= e &&
                    IsCompound(source, s, e) && !HasEol(source, s, e))
                    return (s, e);
            }
        }
        if (node.Inline)
        {
            var (lineLo, lineHi) = PureEdit.LineBounds(source, pos);
            var (tlo, thi) = PureUtf8.Trim(source, lineLo, lineHi);
            if (tlo < thi && source[tlo] == 0xEF && tlo + 3 <= thi) tlo += 3;
            if (IsCompound(source, tlo, thi)) return (tlo, thi);
            if (PureKey.FindUnescapedColon(source, tlo, thi, out int colon))
            {
                int vlo = PureUtf8.TrimStart(source, colon + 1, thi);
                if (IsCompound(source, vlo, thi)) return (vlo, thi);
            }
        }
        return null;
    }

    private static bool HasEol(byte[] b, int lo, int hi)
    {
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'\n' || b[i] == (byte)'\r') return true;
        return false;
    }

    /// <summary>Does the replacement mandate a block form anywhere?
    /// Multiline strings always; >= 4 escapes with >= 25% overhead per line.</summary>
    private static bool RequiresBlock(KtavValue value, int depth)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("replacement nesting exceeds inline depth limit (128)");
        switch (value)
        {
            case KtavString s:
            {
                string str = s.Value;
                if (str.IndexOf('\n') >= 0 || str.IndexOf('\r') >= 0) return true;
                var encoded = PureEditSplice.EncodeInline(str, forceString: false);
                var eb = Encoding.UTF8.GetBytes(encoded);
                int escapes = 0;
                for (int i = 0; i < eb.Length; i++)
                    if (eb[i] == (byte)'\\') { escapes++; i++; }
                int plain = ScalarCount(str);
                int overhead = ScalarCount(encoded) - plain;
                if (overhead < 0) overhead = 0;
                int quarter = plain / 4 + (plain % 4 != 0 ? 1 : 0);
                return escapes >= 4 && overhead >= quarter;
            }
            case KtavObject o:
                foreach (var e in o.Entries)
                    if (RequiresBlock(e.Value, depth + 1)) return true;
                return false;
            case KtavArray a:
                foreach (var item in a.Items)
                    if (RequiresBlock(item, depth + 1)) return true;
                return false;
            default:
                return false;
        }
    }

    private static int ScalarCount(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; )
        {
            if (char.IsSurrogatePair(s, i)) i += 2; else i += 1;
            n++;
        }
        return n;
    }

    /// <summary>Expand the inline compound body [lo,hi) into block form. The
    /// `changed` window (offsets relative to the body) tracks the splice so
    /// only touched branches recurse; returns null when no lossless
    /// standalone form exists for a moved value.</summary>
    private static string? Expand(byte[] body, string indent, string eol, int limit,
        (int, int) changed, bool force, int depth)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("layout nesting exceeds inline depth limit");
        bool isObject = body[0] == (byte)'{';
        var segments = new List<(int s, int e)>();
        bool hasQuotes = PureKey.HasQuoteBytes(body, 0, body.Length);
        var bounds = InlineBoundsForBody(body);
        PureInline.SplitTopLevel(body, 1, body.Length - 1, isObject, 1, 0, 0,
            bounds, hasQuotes, segments, out bool unterminated);
        if (unterminated)
            throw KtavException.EditUnsupported("cannot split inline layout");

        string childIndent = indent + "    ";
        var outBuf = new StringBuilder(body.Length + 64);
        outBuf.Append(isObject ? '{' : '[');
        foreach (var (s, e) in segments)
        {
            var (plo, phi) = PureUtf8.Trim(body, s, e);
            if (plo >= phi) continue; // trailing comma
            int offset = plo;
            bool touched = offset < changed.Item2 && offset + (phi - plo) > changed.Item1;

            string prefixStr;
            int vLo, vHi;
            bool raw = false;
            if (isObject)
            {
                if (!PureKey.FindUnescapedColon(body, plo, phi, out int colon))
                    throw KtavException.EditUnsupported("inline pair has no separator");
                raw = colon + 1 < phi && body[colon + 1] == (byte)':';
                int start0 = colon + 1 + (raw ? 1 : 0);
                vLo = PureUtf8.TrimStart(body, start0, phi);
                vHi = phi;
                prefixStr = PureEdit.Txt(body, plo, vLo);
            }
            else
            {
                prefixStr = "";
                vLo = plo;
                vHi = phi;
            }

            outBuf.Append(eol);
            outBuf.Append(childIndent);
            if (!raw && IsCompound(body, vLo, vHi) && touched &&
                (force ||
                 childIndent.Length + PureEdit.CountScalars(body, plo, phi) > limit))
            {
                int valueOffset = vLo;
                var local = (Math.Max(changed.Item1 - valueOffset, 0),
                             Math.Max(changed.Item2 - valueOffset, 0));
                outBuf.Append(prefixStr);
                var child = Expand(Slice(body, vLo, vHi), childIndent, eol, limit, local, force, depth + 1);
                if (child == null) return null;
                outBuf.Append(child);
            }
            else
            {
                var child = Standalone(Slice(body, plo, phi), prefixStr,
                    PureEdit.Txt(body, vLo, vHi), raw, isObject, childIndent, eol);
                if (child == null) return null;
                outBuf.Append(child);
            }
        }
        outBuf.Append(eol);
        outBuf.Append(indent);
        outBuf.Append(isObject ? '}' : ']');
        return outBuf.ToString();
    }

    private static byte[] Slice(byte[] b, int lo, int hi)
    {
        var r = new byte[hi - lo];
        Array.Copy(b, lo, r, 0, hi - lo);
        return r;
    }

    /// <summary>split.rs InlineBounds::for_input: an empty memo — nested
    /// closers are re-scanned where the split needs them.</summary>
    private static PureInline.InlineBounds InlineBoundsForBody(byte[] body) =>
        new PureInline.InlineBounds(new List<(int o, int c)>());

    /// <summary>Move one inline segment onto its own line: keep the source
    /// text when moving preserves semantics; otherwise decode the inline
    /// value and re-render it in standalone form. Null = keep inline.</summary>
    private static string? Standalone(byte[] part, string prefix, string bodyText,
        bool raw, bool isObject, string indent, string eol)
    {
        // Once moved, inline escapes stop being escapes: verify semantics.
        string partText = PureEdit.Txt(part, 0, part.Length);
        string inline = isObject ? "{" + partText + "}" : "[" + partText + "]";
        byte[] inlineBytes = Encoding.UTF8.GetBytes(inline);
        KtavValue expected;
        try
        {
            expected = PureParser.ParseLax(inlineBytes);
        }
        catch (KtavException)
        {
            throw KtavException.EditUnsupported("invalid inline layout");
        }
        string line = isObject
            ? partText
            : "[\n" + partText + "\n]";
        if (ParsesTo(line, expected)) return partText;

        KtavValue value;
        if (raw)
        {
            string decoded;
            try
            {
                decoded = PureKey.ProcessEscapes(part, VLo(part), part.Length, 1, 0, 0);
            }
            catch (PureError)
            {
                throw KtavException.EditUnsupported("invalid inline string");
            }
            value = new KtavString(decoded);
        }
        else
        {
            string wrapped = "[" + bodyText + "]";
            byte[] wb = Encoding.UTF8.GetBytes(wrapped);
            var bounds = InlineBoundsForBody(wb);
            List<object?> parsed;
            try
            {
                parsed = PureInline.ParseInlineArray(wb, 0, wb.Length, 1, 0, 0, false, bounds,
                    PureKey.HasQuoteBytes(wb, 0, wb.Length));
            }
            catch (PureError)
            {
                throw KtavException.EditUnsupported("cannot decode moved inline value");
            }
            if (parsed.Count != 1)
                throw KtavException.EditUnsupported("moved inline value is not a single value");
            value = Materialise(parsed[0]);
        }
        if (value is KtavString str)
        {
            if (str.Value.IndexOf('\r') >= 0) return null;
            int start = 0;
            while (start <= str.Value.Length)
            {
                int nl = str.Value.IndexOf('\n', start);
                string ln = nl < 0 ? str.Value.Substring(start) : str.Value.Substring(start, nl - start);
                if (TrimSpTab(ln) == "))") return null;
                if (nl < 0) break;
                start = nl + 1;
            }
        }
        var pc = new PureEditSplice.PlanCtx
        {
            Slot = isObject ? PureEdit.SlotKind.Pair : PureEdit.SlotKind.Item,
            RoleItemBlock = false,
            ExistingRaw = raw,
            RootFirst = false,
            Indent = indent,
            Eol = eol,
            InlineCompound = true,
            BlockCompound = false,
        };
        var (newBody, desiredRaw) = PlanValuePublic(value, pc);
        var outBuf = new StringBuilder();
        if (isObject)
        {
            int colon = FindColon(part);
            if (colon < 0)
                throw KtavException.EditUnsupported("moved pair has no separator");
            outBuf.Append(PureEdit.Txt(part, 0, colon + 1));
            if (desiredRaw) outBuf.Append(':');
            // ws between the marker and the body in the ORIGINAL segment
            string ws = raw
                ? prefix.Substring(colon + 2)
                : prefix.Substring(colon + 1);
            if (ws.Length == 0) outBuf.Append(' ');
            else outBuf.Append(ws);
        }
        else if (desiredRaw)
        {
            outBuf.Append(":: ");
        }
        outBuf.Append(newBody);
        return outBuf.ToString();
    }

    private static int VLo(byte[] part) => PureUtf8.TrimStart(part, 0, part.Length);

    private static int FindColon(byte[] part) =>
        PureKey.FindUnescapedColon(part, 0, part.Length, out int c) ? c : -1;

    private static (string, bool) PlanValuePublic(KtavValue value, PureEditSplice.PlanCtx pc)
    {
        // Reuse the splice planner through its internal surface.
        return PureEditSplice.PlanValueForLayout(value, pc);
    }

    private static bool ParsesTo(string line, KtavValue expected)
    {
        try
        {
            var got = PureParser.ParseLax(Encoding.UTF8.GetBytes(line));
            return PureEdit.EditValuesEqual(got, expected);
        }
        catch (KtavException)
        {
            return false;
        }
    }

    private static string TrimSpTab(string line)
    {
        int lo = 0, hi = line.Length;
        while (lo < hi && (line[lo] == ' ' || line[lo] == '\t')) lo++;
        while (hi > lo && (line[hi - 1] == ' ' || line[hi - 1] == '\t')) hi--;
        return line.Substring(lo, hi - lo);
    }

    private static KtavValue Materialise(object? v)
    {
        switch (v)
        {
            case PureObjBuilder b: return b.ToValue();
            case List<object?> list:
            {
                var items = new List<KtavValue>(list.Count);
                foreach (var item in list) items.Add(Materialise(item));
                return new KtavArray(items);
            }
            case KtavValue kv: return kv;
            default:
                throw KtavException.EditUnsupported("internal: unexpected inline builder node");
        }
    }
}

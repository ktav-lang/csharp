// Source-splice engine for the lossless edit (port of
// rust/src/document/parser/edit/splice.rs): only the target's bytes change;
// the `:` / `::` marker zone is kept or minimally converted (context read
// from the OLD slot, rendering decided by the NEW value). Structural
// replacement that would lose interior comments is a typed Unsupported
// BEFORE the splice; a byte-identical target is a no-op; semantic
// verification happens in PureEdit.Apply.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureEditSplice
{
    private sealed class Plan
    {
        public int From;
        public string Prefix0 = "";
        public string Prefix1 = "";
        public string Body = "";
    }

    /// <summary>Full entry: compute the splice plan and apply it. Returns the
    /// patched document; layout adjustment (inline expansion) is separate.</summary>
    internal static string Edit(byte[] text, PureEdit.Slot slot, PureEdit.Ctx ctx,
        PureEdit.Node node, KtavValue replacement)
    {
        var slotKind = slot.Kind;
        int b0 = slot.B0, b1 = slot.B1;
        int a = slot.A;
        bool blockItemRole = slotKind == PureEdit.SlotKind.Block && !slot.HasAnchor;

        // Marker zone: existing raw flag + source whitespace between the
        // marker and the body (data — preserved). A physically inline array
        // item has no marker (`::` is literal data) — not read.
        bool inlineItem = ctx.Inline && slotKind == PureEdit.SlotKind.Item;
        bool existingRaw = false;
        string wsRaw = "", wsPlain = "";
        if (slotKind != PureEdit.SlotKind.Block && !inlineItem)
        {
            existingRaw = a + 1 < text.Length && text[a + 1] == (byte)':';
            if (existingRaw)
            {
                if (a + 2 > b0) throw KtavException.EditUnsupported("internal: invalid raw marker range");
                wsRaw = PureEdit.Txt(text, a + 2, b0);
            }
            if (slotKind == PureEdit.SlotKind.Pair)
            {
                if (a + 1 > b0) throw KtavException.EditUnsupported("internal: invalid plain marker range");
                wsPlain = PureEdit.Txt(text, a + 1, b0);
            }
        }

        // Structural rewrite of a container: losing interior comments is a
        // typed refusal before any splice. Multiline scalar blocks are data.
        if (node.IsObject || node.IsArray)
        {
            var skip = new List<(int s, int e)>();
            CollectMultilineScalarRanges(node, skip);
            if (HasCommentLine(text, b0, b1, skip))
                throw KtavException.EditUnsupported(
                    "target container contains comment lines; structural replacement cannot preserve them losslessly");
        }

        string indent = PureEdit.LineIndent(text, a);
        string eol = PureEdit.DetectEol(text, b1);
        var pc = new PlanCtx
        {
            Slot = slotKind,
            RoleItemBlock = blockItemRole,
            ExistingRaw = existingRaw,
            RootFirst = ctx.RootFirst,
            Indent = indent,
            Eol = eol,
            InlineCompound = slotKind != PureEdit.SlotKind.Block && (node.IsObject || node.IsArray),
            BlockCompound = slotKind == PureEdit.SlotKind.Block && (node.IsObject || node.IsArray),
        };
        // Pair whitespace for a Block target with a pair role; an item-role
        // block has no separator anchor (anchor == b0) — the range would be
        // reversed.
        string wsPairBlock = slotKind == PureEdit.SlotKind.Block && !blockItemRole
            ? PureEdit.Txt(text, a + 1, b0)
            : "";

        Plan plan;
        if (ctx.Inline)
        {
            // Physically inline value.
            if (slotKind == PureEdit.SlotKind.Item &&
                replacement is KtavString s0 && s0.Value.Length == 0)
            {
                // Empty string → `()` (a bare empty string would be a blank line).
                plan = new Plan { From = b0, Body = "()" };
            }
            else if (slotKind == PureEdit.SlotKind.Item)
            {
                plan = new Plan { From = b0, Body = InlineBody(replacement, forceString: true) };
            }
            else if (existingRaw && replacement is not KtavString)
            {
                // Raw body is a literal — a container cannot be expressed;
                // remove only the SECOND `:`, keep the original whitespace.
                plan = new Plan { From = a + 1, Prefix0 = wsRaw, Body = InlineBody(replacement, forceString: false) };
            }
            else
            {
                plan = new Plan { From = b0, Body = InlineBody(replacement, forceString: !existingRaw) };
            }
        }
        else if (slotKind == PureEdit.SlotKind.Block)
        {
            plan = BlockReplacement(replacement, pc, a, b0, wsPairBlock);
        }
        else
        {
            // Linear slots: plan (body + desired marker), then a minimal
            // marker transition.
            var (body, desiredRaw) = PlanValue(replacement, pc);
            if (existingRaw == desiredRaw)
            {
                string prefix = b0 == b1 && wsPlain.Length == 0 && wsRaw.Length == 0 ? " " : "";
                plan = new Plan { From = b0, Prefix0 = prefix, Body = body };
            }
            else if (existingRaw && !desiredRaw && slotKind == PureEdit.SlotKind.Pair)
            {
                // raw → plain pair: drop only the second ':'; keep the WS.
                plan = new Plan { From = a + 1, Prefix0 = SepWs(b0, b1, wsRaw), Body = body };
            }
            else if (existingRaw && !desiredRaw)
            {
                // raw → plain item: remove `::` entirely, keeping the source
                // WS between the marker and the body.
                plan = new Plan { From = a, Prefix0 = wsRaw, Body = body };
            }
            else if (!existingRaw && desiredRaw && slotKind == PureEdit.SlotKind.Pair)
            {
                // plain → raw pair: add the second ':' after the first.
                plan = new Plan { From = a + 1, Prefix0 = ":", Prefix1 = SepWs(b0, b1, wsPlain), Body = body };
            }
            else
            {
                // plain → raw item: insert `:: ` before the body.
                plan = new Plan { From = a, Prefix0 = ":: ", Body = body };
            }
        }

        int from = plan.From;
        if (from < 0 || from > b1 || b1 > text.Length)
            throw KtavException.EditUnsupported("internal: splice range out of bounds");

        var result = new StringBuilder(text.Length + plan.Body.Length + 16);
        result.Append(PureEdit.Txt(text, 0, from));
        result.Append(plan.Prefix0);
        result.Append(plan.Prefix1);
        result.Append(plan.Body);
        result.Append(PureEdit.Txt(text, b1, text.Length));
        return result.ToString();
    }

    private static string SepWs(int b0, int b1, string ws) =>
        b0 == b1 && ws.Length == 0 ? " " : ws;

    internal sealed class PlanCtx
    {
        public PureEdit.SlotKind Slot;
        public bool RoleItemBlock;
        public bool ExistingRaw;
        public bool RootFirst;
        public string Indent = "";
        public string Eol = "\n";
        public bool InlineCompound;
        public bool BlockCompound;
    }

    // ------------------------------------------------------------------
    // Comment preservation gate
    // ------------------------------------------------------------------

    private static void CollectMultilineScalarRanges(PureEdit.Node node, List<(int s, int e)> outRanges)
    {
        if (node.IsScalar && node.Slot.HasValue && node.Slot.Value.Kind == PureEdit.SlotKind.Block)
        {
            outRanges.Add((node.Slot.Value.B0, node.Slot.Value.End));
            return;
        }
        if (node.IsObject)
        {
            foreach (var e in node.Table!.Entries) CollectMultilineScalarRanges(e.Value, outRanges);
        }
        else if (node.IsArray)
        {
            foreach (var item in node.Items!) CollectMultilineScalarRanges(item, outRanges);
        }
    }

    private static bool HasCommentLine(byte[] text, int from, int to, List<(int s, int e)> skip)
    {
        skip.Sort((x, y) => x.s.CompareTo(y.s));
        int skipI = 0;
        int i = from;
        while (i < to)
        {
            int lineStart = i;
            int j = lineStart;
            while (j < to && text[j] != (byte)'\n' && text[j] != (byte)'\r') j++;
            int k = PureUtf8.TrimStart(text, lineStart, j);
            while (skipI < skip.Count && skip[skipI].e <= k) skipI++;
            bool inData = skipI < skip.Count && skip[skipI].s <= k && k < skip[skipI].e;
            if (!inData && k + 1 < j && text[k] == (byte)'#' && text[k + 1] == (byte)'#')
                return true;
            i = j;
            while (i < to && (text[i] == (byte)'\n' || text[i] == (byte)'\r')) i++;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Body rendering
    // ------------------------------------------------------------------

    private static string ScalarBody(KtavValue v) => v switch
    {
        KtavNull => "null",
        KtavBool b => b.Value ? "true" : "false",
        KtavInteger i => i.Text,
        KtavFloat f => f.Text,
        _ => throw KtavException.EditUnsupported("internal: scalar_body called on a compound value"),
    };

    /// <summary>Physically inline value body. `forceString` applies under the
    /// plain marker: a number/keyword-like body must gain String provenance.</summary>
    private static string InlineBody(KtavValue replacement, bool forceString)
    {
        switch (replacement)
        {
            case KtavString s:
                return EncodeInline(s.Value, forceString && PureRender.NeedsRawMarker(s.Value));
            case KtavObject:
            case KtavArray:
                return InlineCompoundBody(replacement, 0);
            default:
                return ScalarBody(replacement);
        }
    }

    internal static void PushInlineString(string s, bool forceString, StringBuilder outBuf)
    {
        // Code-point view (the reference iterates Unicode scalars).
        var cps = new List<int>(s.Length);
        for (int i = 0; i < s.Length; )
        {
            if (char.IsSurrogatePair(s, i))
            {
                cps.Add(char.ConvertToUtf32(s, i));
                i += 2;
            }
            else
            {
                cps.Add(s[i]);
                i++;
            }
        }
        int last = cps.Count - 1;
        for (int i = 0; i < cps.Count; i++)
        {
            int cp = cps[i];
            if (forceString && i == 0)
            {
                outBuf.Append("\\u").Append(cp.ToString("X4"));
                continue;
            }
            EncodeInlineChar(outBuf, cp, i == 0, i == last);
        }
    }

    private static void EncodeInlineChar(StringBuilder outBuf, int cp, bool first, bool lastCp)
    {
        switch (cp)
        {
            case '\\': outBuf.Append("\\\\"); return;
            case ',': outBuf.Append("\\,"); return;
            case '}': outBuf.Append("\\}"); return;
            case ']': outBuf.Append("\\]"); return;
            case '{': outBuf.Append("\\{"); return;
            case '[': outBuf.Append("\\["); return;
            case '\n': outBuf.Append("\\n"); return;
            case '\r': outBuf.Append("\\r"); return;
            default:
                // Every §3.3 member is ≤ U+3000, so four hex digits suffice.
                if ((first || lastCp) && cp <= 0x3000 && PureRender.IsKtavWs((char)cp))
                {
                    outBuf.Append("\\u").Append(cp.ToString("X4"));
                    return;
                }
                if (cp > 0xFFFF)
                {
                    outBuf.Append(char.ConvertFromUtf32(cp));
                }
                else
                {
                    outBuf.Append((char)cp);
                }
                return;
        }
    }

    internal static string EncodeInline(string s, bool forceString)
    {
        var sb = new StringBuilder(s.Length + 8);
        PushInlineString(s, forceString, sb);
        return sb.ToString();
    }

    /// <summary>One-line inline render of a container: `{k: v, ...}` / `[v, v]`.</summary>
    internal static string InlineCompoundBody(KtavValue v, int depth)
    {
        var sb = new StringBuilder();
        PushInlineValue(v, depth, sb);
        return sb.ToString();
    }

    private static void PushInlineValue(KtavValue v, int depth, StringBuilder outBuf)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("replacement nesting exceeds inline depth limit (128)");
        switch (v)
        {
            case KtavObject o:
                outBuf.Append('{');
                for (int i = 0; i < o.Entries.Count; i++)
                {
                    if (i != 0) outBuf.Append(", ");
                    PureRender.PushEscapedKeySegment(o.Entries[i].Key, rootFirstKey: false, outBuf);
                    outBuf.Append(": ");
                    if (o.Entries[i].Value is KtavString s)
                        PushInlineString(s.Value, PureRender.NeedsRawMarker(s.Value), outBuf);
                    else
                        PushInlineValue(o.Entries[i].Value, depth + 1, outBuf);
                }
                outBuf.Append('}');
                break;
            case KtavArray a:
                outBuf.Append('[');
                for (int i = 0; i < a.Items.Count; i++)
                {
                    if (i != 0) outBuf.Append(", ");
                    if (a.Items[i] is KtavString es && es.Value.Length == 0)
                        outBuf.Append("()");
                    else if (a.Items[i] is KtavString s)
                        PushInlineString(s.Value, PureRender.NeedsRawMarker(s.Value), outBuf);
                    else
                        PushInlineValue(a.Items[i], depth + 1, outBuf);
                }
                outBuf.Append(']');
                break;
            case KtavString s:
                PushInlineString(s.Value, PureRender.NeedsRawMarker(s.Value), outBuf);
                break;
            default:
                outBuf.Append(ScalarBody(v));
                break;
        }
    }

    // ------------------------------------------------------------------
    // Replacement planning (plan_value / plan_string)
    // ------------------------------------------------------------------

    /// <summary>Layout-facing surface for the shared replacement planner.</summary>
    internal static (string Body, bool DesiredRaw) PlanValueForLayout(KtavValue replacement, PlanCtx pc)
        => PlanValue(replacement, pc);

    private static (string Body, bool DesiredRaw) PlanValue(KtavValue replacement, PlanCtx pc)
    {
        switch (replacement)
        {
            case KtavString s:
                return PlanString(s.Value, pc);
            case KtavNull:
            case KtavBool:
            case KtavInteger:
            case KtavFloat:
                return (ScalarBody(replacement), false);
            case KtavObject when pc.InlineCompound:
            case KtavArray when pc.InlineCompound:
                return (InlineCompoundBody(replacement, 0), false);
            case KtavObject o when o.Entries.Count == 0:
                return (pc.BlockCompound ? "{" + pc.Eol + pc.Indent + "}" : "{}", false);
            case KtavArray a when a.Items.Count == 0:
                return (pc.BlockCompound ? "[" + pc.Eol + pc.Indent + "]" : "[]", false);
            case KtavObject:
            case KtavArray:
                return (Reindent(RenderCompoundBody(replacement), pc.Indent, pc.Eol), false);
            default:
                throw KtavException.EditUnsupported("internal: unhandled replacement shape");
        }
    }

    private static (string Body, bool DesiredRaw) PlanString(string s, PlanCtx pc)
    {
        if (s.IndexOf('\r') >= 0)
            throw KtavException.EditUnsupported("replacement string contains CR (U+000D)");
        bool edgeWs = (s.Length > 0 && PureRender.IsKtavWs(s[0])) ||
                      (s.Length > 0 && PureRender.IsKtavWs(s[s.Length - 1]));
        if (s.IndexOf('\n') >= 0 || edgeWs)
        {
            // A plain marker is mandatory: raw does not open a multiline
            // block and trims the edges.
            return (VerbatimBlockBody(s, pc.Indent, pc.Eol), false);
        }
        bool desiredRaw = pc.Slot == PureEdit.SlotKind.Item
            ? PureRender.ItemNeedsRawMarker(s) || s.Length == 0
            : PureRender.NeedsRawMarker(s);
        // §5.0.1 root dispatch: a pair-like (or BOM-leading) body of the
        // first element of an implicit root array must be raw.
        if (pc.Slot == PureEdit.SlotKind.Item && pc.RootFirst)
        {
            desiredRaw = desiredRaw || BareItemIsPairCandidate(s) ||
                (s.Length > 0 && s[0] == '\uFEFF');
        }
        // Preserve an existing raw marker for a compatible one-line body.
        if (pc.ExistingRaw) desiredRaw = true;
        return (s, desiredRaw);
    }

    private static bool BareItemIsPairCandidate(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        return PureClassify.IsPairShape(b, 0, b.Length);
    }

    /// <summary>Verbatim block `(( ... ))`: content verbatim, '\n' → the
    /// document EOL; a trailing LF is an empty line before `))`.</summary>
    internal static string VerbatimBlockBody(string s, string indent, string eol)
    {
        int start = 0;
        while (start <= s.Length)
        {
            int nl = s.IndexOf('\n', start);
            string line = nl < 0 ? s.Substring(start) : s.Substring(start, nl - start);
            if (TrimSpTab(line) == "))")
                throw KtavException.EditUnsupported(
                    "multiline string contains a line equal to '))'; no lossless verbatim form exists");
            if (nl < 0) break;
            start = nl + 1;
        }
        var outBuf = new StringBuilder(s.Length + 2 * eol.Length + indent.Length + 4);
        outBuf.Append("((");
        outBuf.Append(eol);
        int pos = 0;
        bool firstLine = true;
        while (true)
        {
            int nl = s.IndexOf('\n', pos);
            if (!firstLine) outBuf.Append(eol);
            firstLine = false;
            if (nl < 0)
            {
                outBuf.Append(s, pos, s.Length - pos);
                break;
            }
            outBuf.Append(s, pos, nl - pos);
            pos = nl + 1;
        }
        outBuf.Append(eol);
        outBuf.Append(indent);
        outBuf.Append("))");
        return outBuf.ToString();
    }

    private static string TrimSpTab(string line)
    {
        int lo = 0, hi = line.Length;
        while (lo < hi && (line[lo] == ' ' || line[lo] == '\t')) lo++;
        while (hi > lo && (line[hi - 1] == ' ' || line[hi - 1] == '\t')) hi--;
        return line.Substring(lo, hi - lo);
    }

    /// <summary>Replacement of a multiline BLOCK target; the role (pair vs
    /// array item) shapes the result.</summary>
    private static Plan BlockReplacement(KtavValue replacement, PlanCtx pc, int a, int b0, string wsPair)
    {
        if (replacement is KtavString s)
        {
            string str = s.Value;
            if (str.IndexOf('\r') >= 0)
                throw KtavException.EditUnsupported("replacement string contains CR (U+000D)");
            bool multiline = str.IndexOf('\n') >= 0 ||
                (str.Length > 0 && PureRender.IsKtavWs(str[0])) ||
                (str.Length > 0 && PureRender.IsKtavWs(str[str.Length - 1]));
            if (multiline)
                return new Plan { From = b0, Body = VerbatimBlockBody(str, pc.Indent, pc.Eol) };
            if (pc.RoleItemBlock && str.Length == 0)
                return new Plan { From = b0, Body = "()" };
            bool itemRaw = pc.RoleItemBlock &&
                (PureRender.ItemNeedsRawMarker(str) ||
                 (pc.RootFirst && (BareItemIsPairCandidate(str) ||
                     (str.Length > 0 && str[0] == '\uFEFF'))));
            bool pairRaw = !pc.RoleItemBlock && PureRender.NeedsRawMarker(str);
            if (itemRaw)
                return new Plan { From = b0, Prefix0 = ":: ", Body = str };
            if (pairRaw)
                return new Plan { From = a + 1, Prefix0 = ":", Prefix1 = wsPair, Body = str };
            return new Plan { From = b0, Body = str };
        }
        var blockPc = new PlanCtx
        {
            Slot = PureEdit.SlotKind.Block,
            RoleItemBlock = pc.RoleItemBlock,
            ExistingRaw = pc.ExistingRaw,
            RootFirst = pc.RootFirst,
            Indent = pc.Indent,
            Eol = pc.Eol,
            InlineCompound = pc.InlineCompound,
            BlockCompound = pc.BlockCompound,
        };
        var (body, _) = PlanValue(replacement, blockPc);
        return new Plan { From = b0, Body = body };
    }

    /// <summary>Pretty render of a container without a root wrapper.</summary>
    private static string RenderCompoundBody(KtavValue v)
    {
        try
        {
            string rendered = PureWriter.DumpsCompoundItem(v, 1, rootFirstItem: false);
            return rendered.EndsWith("\n", StringComparison.Ordinal)
                ? rendered.Substring(0, rendered.Length - 1)
                : rendered;
        }
        catch (KtavException e)
        {
            throw KtavException.EditUnsupported("replacement is not representable: " + e.Message);
        }
    }


    /// <summary>Re-indent a pretty render under the target's line. Lines
    /// inside verbatim blocks of the render are not re-indented. Any
    /// heuristic miss is caught by the mandatory semantic verification.</summary>
    private static string Reindent(string doc, string indent, string eol)
    {
        var outBuf = new StringBuilder(doc.Length);
        bool inVerbatim = false;
        bool first = true;
        int start = 0;
        while (start <= doc.Length)
        {
            int nl = doc.IndexOf('\n', start);
            string line = nl < 0 ? doc.Substring(start) : doc.Substring(start, nl - start);
            if (!inVerbatim || TrimSpTab(line) == "))")
            {
                if (line.StartsWith("    ", StringComparison.Ordinal)) line = line.Substring(4);
            }
            bool verbatimOpen = line.EndsWith("((", StringComparison.Ordinal);
            bool verbatimClose = TrimSpTab(line) == "))";
            if (!first)
            {
                outBuf.Append(eol);
                bool skipPrefix = (inVerbatim && !verbatimClose) || line.Length == 0;
                if (!skipPrefix) outBuf.Append(indent);
            }
            first = false;
            outBuf.Append(line);
            if (verbatimOpen) inVerbatim = true;
            else if (verbatimClose) inVerbatim = false;
            if (nl < 0) break;
            start = nl + 1;
        }
        return outBuf.ToString();
    }
}

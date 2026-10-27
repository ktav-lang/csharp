using System;
using System.Text;

namespace Ktav.Pure;

/// <summary>Context-aware rendering for a newly inserted object member.</summary>
internal static class PureInsertRender
{
    internal readonly struct Context
    {
        internal Context(bool inline, string indent, string eol, bool rootFirst = false)
        {
            Inline = inline;
            Indent = indent ?? throw new ArgumentNullException(nameof(indent));
            Eol = eol ?? throw new ArgumentNullException(nameof(eol));
            RootFirst = rootFirst;
        }

        internal bool Inline { get; }
        internal string Indent { get; }
        internal string Eol { get; }
        internal bool RootFirst { get; }
    }

    /// <summary>Render a key/value pair for its owning inline or block context.
    /// Inline contexts always use escaped inline values; block contexts use
    /// the shared edit planner, including raw-marker selection and EOL policy.</summary>
    internal static string RenderPair(string key, KtavValue value, Context context)
    {
        if (string.IsNullOrEmpty(key))
            throw KtavException.EditUnsupported("empty key name is not representable");

        var output = new StringBuilder();
        PureRender.PushEscapedKeySegment(key, context.RootFirst, output);
        output.Append(':');

        if (context.Inline)
        {
            output.Append(' ');
            if (value is KtavString text)
                PureEditSplice.PushInlineString(text.Value,
                    PureRender.NeedsRawMarker(text.Value), output);
            else
                output.Append(PureEditSplice.InlineCompoundBody(value, 0));
            return output.ToString();
        }

        var plan = new PureEditSplice.PlanCtx
        {
            Slot = PureEdit.SlotKind.Pair,
            RoleItemBlock = false,
            ExistingRaw = false,
            RootFirst = context.RootFirst,
            Indent = context.Indent,
            Eol = context.Eol,
            InlineCompound = false,
            BlockCompound = true,
        };
        var (body, raw) = PureEditSplice.PlanValueForLayout(value, plan);
        if (raw) output.Append(':');
        output.Append(' ').Append(body);
        return output.ToString();
    }

    /// <summary>Whether block promotion is desirable for this value. If it is
    /// desirable but no verbatim block form exists, callers should keep their
    /// valid inline candidate instead.</summary>
    internal static bool RequiresBlock(KtavValue value) => RequiresBlock(value, 0);

    internal static bool CanRenderBlock(KtavValue value) => BlockEncodable(value, 0);

    private static bool RequiresBlock(KtavValue value, int depth)
    {
        CheckDepth(depth);
        switch (value)
        {
            case KtavString text:
            {
                var s = text.Value;
                if (s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0) return true;
                string encoded = PureEditSplice.EncodeInline(s, forceString: false);
                int escapes = 0;
                for (int i = 0; i < encoded.Length; i++)
                    if (encoded[i] == '\\') { escapes++; i++; }
                int plain = ScalarCount(s);
                int overhead = Math.Max(0, ScalarCount(encoded) - plain);
                int threshold = plain / 4 + (plain % 4 == 0 ? 0 : 1);
                return escapes >= 4 && overhead >= threshold;
            }
            case KtavObject obj:
                foreach (var entry in obj.Entries)
                    if (RequiresBlock(entry.Value, depth + 1)) return true;
                return false;
            case KtavArray array:
                foreach (var item in array.Items)
                    if (RequiresBlock(item, depth + 1)) return true;
                return false;
            default:
                return false;
        }
    }

    private static bool BlockEncodable(KtavValue value, int depth)
    {
        CheckDepth(depth);
        switch (value)
        {
            case KtavString text:
                if (text.Value.IndexOf('\r') >= 0) return false;
                int start = 0;
                while (start <= text.Value.Length)
                {
                    int nl = text.Value.IndexOf('\n', start);
                    string line = nl < 0 ? text.Value.Substring(start) : text.Value.Substring(start, nl - start);
                    int lo = 0, hi = line.Length;
                    while (lo < hi && (line[lo] == ' ' || line[lo] == '\t')) lo++;
                    while (hi > lo && (line[hi - 1] == ' ' || line[hi - 1] == '\t')) hi--;
                    if (line.Substring(lo, hi - lo) == "))") return false;
                    if (nl < 0) break;
                    start = nl + 1;
                }
                return true;
            case KtavObject obj:
                foreach (var entry in obj.Entries)
                    if (!BlockEncodable(entry.Value, depth + 1)) return false;
                return true;
            case KtavArray array:
                foreach (var item in array.Items)
                    if (!BlockEncodable(item, depth + 1)) return false;
                return true;
            default:
                return true;
        }
    }

    private static void CheckDepth(int depth)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("replacement nesting exceeds inline depth limit (128)");
    }

    private static int ScalarCount(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++, count++)
            if (char.IsSurrogatePair(text, i)) i++;
        return count;
    }
}

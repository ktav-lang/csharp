using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

/// <summary>Insertion-specific layout policy shared with PureEditLayout.</summary>
internal static class PureInsertLayout
{
    /// <summary>Whether any nested replacement value prefers a block layout.</summary>
    internal static bool RequiresBlock(KtavValue value) =>
        BlockEncodable(value, 0) && RequiresBlock(value, 0);

    private static bool BlockEncodable(KtavValue value, int depth)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("replacement nesting exceeds inline depth limit (128)");
        switch (value)
        {
            case KtavString s:
                if (s.Value.IndexOf('\r') >= 0) return false;
                int start = 0;
                while (start <= s.Value.Length)
                {
                    int nl = s.Value.IndexOf('\n', start);
                    string line = nl < 0 ? s.Value.Substring(start) : s.Value.Substring(start, nl - start);
                    if (TrimKtavWhitespace(line) == "))") return false;
                    if (nl < 0) break;
                    start = nl + 1;
                }
                return true;
            case KtavObject obj:
                foreach (var entry in obj.Entries) if (!BlockEncodable(entry.Value, depth + 1)) return false;
                return true;
            case KtavArray array:
                foreach (var item in array.Items) if (!BlockEncodable(item, depth + 1)) return false;
                return true;
            default: return true;
        }
    }

    private static string TrimKtavWhitespace(string value)
    {
        int lo = 0, hi = value.Length;
        while (lo < hi)
        {
            int start = lo;
            if (char.IsLowSurrogate(value[lo]) && lo > 0 && char.IsHighSurrogate(value[lo - 1])) break;
            if (!IsKtavWhitespace(value, lo)) break;
            lo += char.IsSurrogatePair(value, lo) ? 2 : 1;
        }
        while (hi > lo)
        {
            int cpStart = hi - 1;
            if (char.IsLowSurrogate(value[cpStart]) && cpStart > 0 && char.IsHighSurrogate(value[cpStart - 1])) cpStart--;
            if (!IsKtavWhitespace(value, cpStart)) break;
            hi = cpStart;
        }
        return value.Substring(lo, hi - lo);
    }

    private static bool RequiresBlock(KtavValue value, int depth)
    {
        if (depth > PureInline.MaxInlineDepth)
            throw KtavException.EditUnsupported("replacement nesting exceeds inline depth limit (128)");
        switch (value)
        {
            case KtavString s:
            {
                string text = s.Value;
                if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return true;
                string encoded = PureEditSplice.EncodeInline(text, false);
                byte[] bytes = Encoding.UTF8.GetBytes(encoded);
                int escapes = 0;
                for (int i = 0; i < bytes.Length; i++)
                    if (bytes[i] == (byte)'\\') { escapes++; i++; }
                int plain = ScalarCount(text);
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

    private static bool IsKtavWhitespace(string text, int index)
    {
        int cp = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
            ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];
        return cp == 0x20 || cp >= 0x09 && cp <= 0x0D || cp == 0x85 || cp == 0xA0 || cp == 0x1680 || cp >= 0x2000 && cp <= 0x200A || cp == 0x2028 || cp == 0x2029 || cp == 0x202F || cp == 0x205F || cp == 0x3000 || cp == 0xFEFF;
    }

    private static int ScalarCount(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length;)
        {
            i += char.IsSurrogatePair(value, i) ? 2 : 1;
            count++;
        }
        return count;
    }
}

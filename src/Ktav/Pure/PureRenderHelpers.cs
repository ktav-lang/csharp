// Rendering primitives shared by the pure writers: §5.9.10 key form selection
// + re-escape, §5.9.7 string predicates, §5.9.8-canonical float checked helper,
// §5.6 multi-line form chooser, indentation, and the §5.9.0 writer rejections.
// Mirrors rust/src/document/render/helpers.rs and whitespace.rs. All helpers
// operate on decoded C# strings (KtavValue stores strings); the §3.3 whitespace
// class is the same closed 25-code-point list the parser uses (all members are
// BMP, so char-level tests are exact).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ktav.Pure;

internal static class PureRender
{
    public const string Indent = "    ";
    private const string Spaces =
        "                                                                "; // 64

    /// <summary>Push level * 4 spaces (helpers.rs push_indent: chunked memcpy).</summary>
    public static void PushIndent(StringBuilder outBuf, int level)
    {
        int remaining = level * Indent.Length;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, Spaces.Length);
            outBuf.Append(Spaces, 0, chunk);
            remaining -= chunk;
        }
    }

    // ------------------------------------------------------------------
    // §3.3 whitespace views (whitespace.rs)
    // ------------------------------------------------------------------

    public static bool IsKtavWs(char c)
    {
        if (c <= 0x7F)
            return c == '\u0009' || c == '\u000A' || c == '\u000B' || c == '\u000C'
                || c == '\u000D' || c == '\u0020';
        return c == '\u0085' || c == '\u00A0' || c == '\u1680'
            || (c >= '\u2000' && c <= '\u200A')
            || c == '\u2028' || c == '\u2029' || c == '\u202F' || c == '\u205F' || c == '\u3000';
    }

    public static bool IsInlineWs(char c) => IsKtavWs(c) && c != '\u000A' && c != '\u000D';

    public static string TrimInline(string s)
    {
        int lo = 0, hi = s.Length;
        while (lo < hi && IsInlineWs(s[lo])) lo++;
        while (hi > lo && IsInlineWs(s[hi - 1])) hi--;
        return lo == 0 && hi == s.Length ? s : s.Substring(lo, hi - lo);
    }

    // ------------------------------------------------------------------
    // §5.9.0 writer rejections (KtavException.WriterError == the native
    // envelope's UnrepresentableAt rendering, byte for byte).
    // ------------------------------------------------------------------

    public static KtavException WriterError(string reason, List<string> path)
        => KtavException.WriterError(reason, path.ToArray());

    // ------------------------------------------------------------------
    // §5.9.10 key emission (helpers.rs push_escaped_key_segment and friends)
    // ------------------------------------------------------------------

    private const string Hex = "0123456789ABCDEF";

    /// <summary>\u + exactly four uppercase hex digits (§3.7.1).</summary>
    private static void PushUnicodeEscape(StringBuilder outBuf, char ch)
    {
        outBuf.Append("\\u");
        int cp = ch;
        outBuf.Append(Hex[(cp >> 12) & 0xF]);
        outBuf.Append(Hex[(cp >> 8) & 0xF]);
        outBuf.Append(Hex[(cp >> 4) & 0xF]);
        outBuf.Append(Hex[cp & 0xF]);
    }

    /// <summary>§5.9.10 bare/quoted form selection + re-escape, one pass.</summary>
    public static void PushEscapedKeySegment(string key, bool rootFirstKey, StringBuilder outBuf)
    {
        char first = key.Length > 0 ? key[0] : '\0';

        // Rules (b)/(c)/(d) read the leading code point(s) only.
        if (first == '"' || first == '\'' || first == '`'
            || (rootFirstKey && first == '\uFEFF')
            || (key.Length >= 2 && key[0] == '#' && key[1] == '#'))
        {
            PushQuotedKey(key, outBuf);
            return;
        }
        // (a) edge §3.3 whitespace (inline view — LF/CR buy nothing quoted).
        if (key.Length > 0 && (IsInlineWs(first) || IsInlineWs(key[key.Length - 1])))
        {
            PushQuotedKey(key, outBuf);
            return;
        }

        bool needsRewrite = false;
        foreach (var b in key)
        {
            switch (b)
            {
                case '.': case ':': case ',': case '{': case '}':
                case '[': case ']': case '(': case ')':
                    PushQuotedKey(key, outBuf);
                    return;
                case '\\': case '\n': case '\r': case '\u007F':
                    needsRewrite = true;
                    break;
                default:
                    if (b < 0x20 && b != '\t' && b != '\u000B' && b != '\u000C')
                        needsRewrite = true;
                    break;
            }
        }
        if (needsRewrite) PushBareKeyEscaped(key, outBuf);
        else outBuf.Append(key);
    }

    /// <summary>Bare form recipe: named escapes for structural bytes, \uXXXX for
    /// control bytes and edge whitespace.</summary>
    private static void PushBareKeyEscaped(string key, StringBuilder outBuf)
    {
        int lastIdx = key.Length - 1;
        for (int i = 0; i < key.Length; i++)
        {
            char ch = key[i];
            bool atEdge = i == 0 || i == lastIdx;
            switch (ch)
            {
                case '\\': outBuf.Append("\\\\"); break;
                case '\n': outBuf.Append("\\n"); break;
                case '\r': outBuf.Append("\\r"); break;
                default:
                    if (IsKtavWs(ch))
                    {
                        if (atEdge) PushUnicodeEscape(outBuf, ch);
                        else outBuf.Append(ch);
                    }
                    else switch (ch)
                    {
                        case '.': outBuf.Append("\\."); break;
                        case ':': outBuf.Append("\\:"); break;
                        case ',': outBuf.Append("\\,"); break;
                        case '{': outBuf.Append("\\{"); break;
                        case '}': outBuf.Append("\\}"); break;
                        case '[': outBuf.Append("\\["); break;
                        case ']': outBuf.Append("\\]"); break;
                        default:
                            if (ch < 0x20 || ch == 0x7F) PushUnicodeEscape(outBuf, ch);
                            else outBuf.Append(ch);
                            break;
                    }
                    break;
            }
        }
    }

    /// <summary>Quoted form (§5.9.10): only \" \\ \n \r, control &lt; 0x20
    /// (except TAB/VT/FF) and DEL are escaped.</summary>
    private static void PushQuotedKey(string key, StringBuilder outBuf)
    {
        outBuf.Append('"');
        foreach (var ch in key)
        {
            switch (ch)
            {
                case '"': outBuf.Append("\\\""); break;
                case '\\': outBuf.Append("\\\\"); break;
                case '\n': outBuf.Append("\\n"); break;
                case '\r': outBuf.Append("\\r"); break;
                default:
                    if ((ch < 0x20 && ch != '\t' && ch != '\u000B' && ch != '\u000C') || ch == 0x7F)
                        PushUnicodeEscape(outBuf, ch);
                    else outBuf.Append(ch);
                    break;
            }
        }
        outBuf.Append('"');
    }

    // ------------------------------------------------------------------
    // §5.9.7 / §5.9.5 / §5.9.6 body predicates
    // ------------------------------------------------------------------

    /// <summary>One-line body impossible → multi-line form required.</summary>
    public static bool StringNeedsMultiline(string s)
    {
        return (s.Length > 0 && (IsKtavWs(s[0]) || IsKtavWs(s[s.Length - 1])))
            || HasControlOtherThanTab(s);
    }

    private static bool HasControlOtherThanTab(string s)
    {
        foreach (var ch in s)
            if (ch < 0x20 && ch != '\t') return true;
        return false;
    }

    public static bool ContainsCr(string s)
    {
        foreach (var ch in s)
            if (ch == '\r') return true;
        return false;
    }

    /// <summary>Would the parser re-classify this one-line body as a non-String?</summary>
    public static bool NeedsRawMarker(string s)
    {
        if (s.Length == 0) return false;
        char first = s[0];
        if (first == ' ' || first == '\t')
        {
            string t = TrimStartInline(s);
            return t.Length > 0 && (t[0] == '{' || t[0] == '[' || NeedsRawMarkerContent(t));
        }
        if (first == '{' || first == '[') return true;
        return NeedsRawMarkerContent(s);
    }

    private static string TrimStartInline(string s)
    {
        int i = 0;
        while (i < s.Length && IsInlineWs(s[i])) i++;
        return i == 0 ? s : s.Substring(i);
    }

    private static bool NeedsRawMarkerContent(string s)
    {
        if (s == "null" || s == "true" || s == "false" || s == "(" || s == "((" || s == "()" || s == "(())")
            return true;
        if (MatchesIntegerGrammar(s) || MatchesFloatGrammar(s)) return true;
        return false;
    }

    /// <summary>Item-context raw-marker test (extra `##`, `::`, sole `]`/`}`).</summary>
    public static bool ItemNeedsRawMarker(string s)
    {
        return NeedsRawMarker(s) || s.StartsWith("##", StringComparison.Ordinal)
            || s.StartsWith("::", StringComparison.Ordinal) || s == "]" || s == "}";
    }

    // -- §3.6 grammars over a decoded string (byte view via UTF-8 round trip is
    // unnecessary: the grammars are ASCII-only, and every §3.6-relevant byte is
    // a single UTF-16 unit; non-ASCII bytes simply fail the digit/sign tests the
    // same way they fail the byte versions). --

    /// <summary>classify.rs matches_integer_grammar over a string.</summary>
    public static bool MatchesIntegerGrammar(string s)
    {
        int i = 0, n = s.Length;
        if (n == 0) return false;
        if (s[i] == '+' || s[i] == '-') i++;
        if (i >= n || s[i] < '0' || s[i] > '9') return false;
        if (s[i] == '0' && i + 1 < n && (s[i + 1] == 'x' || s[i + 1] == 'o' || s[i + 1] == 'b'))
            return ScanRadixBody(s, i + 2, RadixOf(s[i + 1]));
        return ScanDecimalBody(s, i, allowLeadingZero: true);
    }

    private static int RadixOf(char p) => p == 'x' ? 16 : p == 'o' ? 8 : 2;

    private static bool ScanRadixBody(string s, int i, int radix)
    {
        bool prevUs = false, any = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '_') { if (prevUs) return false; prevUs = true; continue; }
            int d = DigitVal(c);
            if (d < 0 || d >= radix) return false;
            prevUs = false; any = true;
        }
        return any && !prevUs;
    }

    private static int DigitVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }

    private static bool ScanDecimalBody(string s, int i, bool allowLeadingZero)
    {
        // Leading zero rule: `0` alone ok; `012` never a number.
        if (!allowLeadingZero && s[i] == '0' && i + 1 < s.Length)
        {
            char nx = s[i + 1];
            if ((nx >= '0' && nx <= '9') || nx == '_') return false;
        }
        bool prevUs = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '_') { if (prevUs) return false; prevUs = true; continue; }
            if (c < '0' || c > '9') return false;
            prevUs = false;
        }
        return !prevUs;
    }

    /// <summary>classify.rs matches_float_grammar over a string.</summary>
    public static bool MatchesFloatGrammar(string s)
    {
        int i = 0, n = s.Length;
        if (n == 0) return false;
        if (s[i] == '+' || s[i] == '-') i++;
        if (i >= n || s[i] < '0' || s[i] > '9') return false;
        bool prevUs = false;
        // digits
        while (i < n)
        {
            char c = s[i];
            if (c == '_') { if (prevUs) return false; prevUs = true; i++; continue; }
            if (c < '0' || c > '9') break;
            prevUs = false; i++;
        }
        if (prevUs) return false;
        if (i < n && s[i] == '.')
        {
            i++;
            if (i >= n || s[i] < '0' || s[i] > '9') return false;
            while (i < n)
            {
                char c = s[i];
                if (c == '_') { if (prevUs) return false; prevUs = true; i++; continue; }
                if (c < '0' || c > '9') break;
                prevUs = false; i++;
            }
            if (prevUs) return false;
        }
        if (i < n && (s[i] == 'e' || s[i] == 'E'))
        {
            i++;
            if (i < n && (s[i] == '+' || s[i] == '-')) i++;
            if (i >= n || s[i] < '0' || s[i] > '9') return false;
            while (i < n)
            {
                char c = s[i];
                if (c == '_') { if (prevUs) return false; prevUs = true; i++; continue; }
                if (c < '0' || c > '9') return false;
                prevUs = false; i++;
            }
            return !prevUs;
        }
        return i == n;
    }

    // ------------------------------------------------------------------
    // §5.6 multi-line form chooser (helpers.rs choose_multiline_form)
    // ------------------------------------------------------------------

    public const int MlStripped = 0, MlVerbatim = 1;

    /// <summary>
    /// Pick the multi-line form that reproduces `s` byte-for-byte on re-parse,
    /// or throw the §5.9.0 collision rejection. `preferStripped` mirrors the
    /// pretty writers (true) vs the canonical writer / formatter (false).
    /// </summary>
    public static int ChooseMultilineForm(string s, bool preferStripped, List<string> path)
    {
        bool soleSingle = false, soleDouble = false, wsOnlyLine = false,
             indentedLine = false, trailingWsLine = false;
        int start = 0;
        while (true)
        {
            int nl = s.IndexOf('\n', start);
            string line = nl < 0 ? s.Substring(start) : s.Substring(start, nl - start);
            string trimmed = TrimInline(line);
            if (trimmed == ")") soleSingle = true;
            else if (trimmed == "))") soleDouble = true;
            if (line.Length != 0)
            {
                if (trimmed.Length == 0) wsOnlyLine = true;
                else if (IsInlineWs(line[0])) indentedLine = true;
                if (TrimEndInline(line).Length != line.Length) trailingWsLine = true;
            }
            if (nl < 0) break;
            start = nl + 1;
        }

        bool strippedSafe = !soleSingle && !wsOnlyLine && !indentedLine && !trailingWsLine;
        bool verbatimOk = !soleDouble;

        if (preferStripped && strippedSafe) return MlStripped;
        if (verbatimOk) return MlVerbatim;

        // §5.6 common-leading-whitespace code-point prefix over non-blank lines.
        bool hasCommonIndent = CommonLeadingWsPrefixLen(s) != 0;
        bool strippedLossless = !soleSingle && !wsOnlyLine && !hasCommonIndent && !trailingWsLine;
        if (strippedLossless) return MlStripped;

        string code = soleSingle ? "BothFormsRequired"
            : trailingWsLine ? "TrailingWhitespaceCollision"
            : "LeadingWhitespaceCollision";
        throw WriterError(code, path);
    }

    private static string TrimEndInline(string s)
    {
        int hi = s.Length;
        while (hi > 0 && IsInlineWs(s[hi - 1])) hi--;
        return hi == s.Length ? s : s.Substring(0, hi);
    }

    /// <summary>whitespace.rs common_leading_whitespace_prefix_len: length (in
    /// code points — every §3.3 member is BMP, so chars) of the leading-whitespace
    /// prefix shared code-point-for-code-point by every non-blank line.</summary>
    public static int CommonLeadingWsPrefixLen(string s)
    {
        string? firstRun = null;
        int len = -1;
        int start = 0;
        while (start <= s.Length)
        {
            int nl = s.IndexOf('\n', start);
            int lineEnd = nl < 0 ? s.Length : nl;
            if (lineEnd > start)
            {
                int run = LeadingWsRun(s, start, lineEnd);
                bool blank = run >= lineEnd - start && LeadingWsRunFull(s, start, lineEnd) >= lineEnd - start;
                if (!blank)
                {
                    if (len < 0)
                    {
                        len = run;
                        firstRun = s.Substring(start, run);
                    }
                    else
                    {
                        // Compare the two runs code-point-for-code-point, capped
                        // at the running common length (§3.3 members are single
                        // UTF-16 units, so char comparison is code-point exact).
                        int lim = Math.Min(len, run);
                        int i = 0;
                        while (i < lim && s[start + i] == firstRun[i]) i++;
                        len = i;
                        if (len == 0) return 0;
                    }
                }
            }
            if (nl < 0) break;
            start = nl + 1;
        }
        return Math.Max(len, 0);
    }

    private static int LeadingWsRun(string s, int lo, int hi)
    {
        int i = lo;
        while (i < hi && IsInlineWs(s[i])) i++;
        return i - lo;
    }

    private static int LeadingWsRunFull(string s, int lo, int hi)
    {
        int i = lo;
        while (i < hi && IsKtavWs(s[i])) i++;
        return i - lo;
    }

    // ------------------------------------------------------------------
    // §5.9.8 canonical float, checked (canonical/num.rs canonical_float_checked)
    // ------------------------------------------------------------------

    /// <summary>Canonical §5.9.8 text of a stored float spelling; NonFiniteFloat
    /// rejection for NaN/±Inf payloads; non-lexical payloads pass through.</summary>
    public static string CanonicalFloatChecked(string text, List<string> path)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return text; // outside the Float domain — not a §5.9.0 reason code
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw WriterError("NonFiniteFloat", path);
        return PureNum.CanonicalFloat(text);
    }

    /// <summary>The pretty writers' NonFiniteFloat check (helpers.rs reject_non_finite).</summary>
    public static void RejectNonFinite(string text, List<string> path)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return;
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw WriterError("NonFiniteFloat", path);
    }
}

// Key-context scanning: pair separator (§4/§5.3), quoted segments (§5.3.3),
// dotted-path splitting, §3.7 escape processing, §4 key validation.
// Mirrors rust/src/document/parser/inline/{keys,escapes}.rs and build/validate.rs.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureKey
{
    // ---------------- §3.7 escapes ----------------

    private static readonly string[] SimpleEscapes =
    {
        "\\", ",", "}", "]", "{", "[", "n", "r", ".", ":", "\"", "'", "`",
    };

    private static int SimpleEscapeChar(int next)
    {
        for (int k = 0; k < SimpleEscapes.Length; k++)
            if (SimpleEscapes[k][0] == next)
                return k == 6 ? '\n' : k == 7 ? '\r' : SimpleEscapes[k][0];
        return -1;
    }

    /// <summary>
    /// Scan one escape at bytes[i] (== '\\'). On error returns the exact
    /// BadEscapeSequence payload text (escapes.rs scan_escape).
    /// </summary>
    private static string? ScanEscape(byte[] b, int lo, int hi, int i, out char ch, out int high, out int low, out int len)
    {
        ch = '\0';
        high = 0;
        low = 0;
        len = 0;
        if (i + 1 >= hi)
            return "\\<end-of-line>";
        int next = b[i + 1];
        if (next == 'u')
        {
            if (i + 6 > hi || !AllHex(b, i + 2, i + 6))
                return RenderMalformedUnicode(b, lo, hi, i);
            int value = HexAt(b, i + 2) << 12 | HexAt(b, i + 3) << 8 | HexAt(b, i + 4) << 4 | HexAt(b, i + 5);
            if (value >= 0xD800 && value <= 0xDBFF)
            {
                if (i + 12 <= hi && b[i + 6] == (byte)'\\' && b[i + 7] == (byte)'u' &&
                    AllHex(b, i + 8, i + 12))
                {
                    int lowSur = HexAt(b, i + 8) << 12 | HexAt(b, i + 9) << 8 | HexAt(b, i + 10) << 4 | HexAt(b, i + 11);
                    if (lowSur >= 0xDC00 && lowSur <= 0xDFFF)
                    {
                        high = value;
                        low = lowSur;
                        len = 12;
                        return null;
                    }
                }
                return RenderMalformedUnicode(b, lo, hi, i);
            }
            if (value >= 0xDC00 && value <= 0xDFFF)
                return RenderMalformedUnicode(b, lo, hi, i);
            ch = (char)value;
            len = 6;
            return null;
        }
        int simple = SimpleEscapeChar(next);
        if (simple >= 0)
        {
            ch = (char)simple;
            len = 2;
            return null;
        }
        return next < 0x80
            ? "\\" + (char)next
            : "\\<0x" + next.ToString("X2") + ">";
    }

    private static bool AllHex(byte[] b, int lo, int hi)
    {
        for (int i = lo; i < hi; i++)
        {
            int c = b[i];
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return true;
    }

    private static int HexAt(byte[] b, int i)
    {
        int c = b[i];
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return c - 'A' + 10;
    }

    private static string RenderMalformedUnicode(byte[] b, int lo, int hi, int i)
    {
        int end = Math.Min(i + 6, hi);
        var sb = new StringBuilder("\\u");
        for (int j = i + 2; j < end; j++)
        {
            int by = b[j];
            if (by < 0x80) sb.Append((char)by);
            else sb.Append("<0x").Append(by.ToString("X2")).Append('>');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Process escapes in a byte window (escapes.rs process_escapes). Throws
    /// PureError BadEscapeSequence. Returns the decoded string (the input
    /// window has no '\\' → decoded form equals the raw bytes).
    /// </summary>
    public static string ProcessEscapes(byte[] b, int lo, int hi, int line, long spanS, long spanE)
    {
        bool hasBs = false;
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'\\') { hasBs = true; break; }
        if (!hasBs) return Encoding.UTF8.GetString(b, lo, hi - lo);

        var sb = new StringBuilder(hi - lo);
        int i2 = lo;
        while (i2 < hi)
        {
            if (b[i2] == (byte)'\\')
            {
                string? err = ScanEscape(b, lo, hi, i2, out char ch, out int high, out int low, out int len);
                if (err != null)
                    throw PureError.BadEscapeSequence(line, err, spanS, spanE);
                if (len == 12)
                {
                    sb.Append((char)high).Append((char)low);
                }
                else
                {
                    sb.Append(ch);
                }
                i2 += len;
            }
            else
            {
                // Copy one whole UTF-8 code point (input is validated).
                int cp = CodePointLen(b, i2, hi);
                sb.Append(Encoding.UTF8.GetString(b, i2, cp));
                i2 += cp;
            }
        }
        return sb.ToString();
    }

    private static int CodePointLen(byte[] b, int i, int hi)
    {
        int x = b[i];
        if (x < 0x80) return 1;
        if (x < 0xE0) return 2;
        if (x < 0xF0) return 3;
        return 4;
    }

    // ---------------- separator / quoted-segment scanning ----------------

    public static bool IsQuoteByte(int b) => b == '"' || b == '\'' || b == '`';

    public static bool HasQuoteBytes(byte[] b, int lo, int hi)
    {
        for (int i = lo; i < hi; i++)
            if (IsQuoteByte(b[i])) return true;
        return false;
    }

    /// <summary>Skip inline whitespace; returns the first non-ws offset (keys.rs skip_segment_ws).</summary>
    public static int SkipSegmentWs(byte[] b, int i, int hi)
    {
        while (i < hi)
        {
            int len = PureUtf8.InlineWsLenAt(b, i, hi);
            if (len == 0) break;
            i += len;
        }
        return i;
    }

    /// <summary>
    /// Matching unescaped closer for a quoted segment opened at openIdx
    /// (keys.rs quoted_span_end); -1 when unterminated.
    /// </summary>
    public static int QuotedSpanEnd(byte[] b, int hi, int openIdx)
    {
        int quote = b[openIdx];
        int j = openIdx + 1;
        while (j < hi)
        {
            if (b[j] == (byte)'\\') { j += 2; continue; }
            if (b[j] == quote) return j;
            j++;
        }
        return -1;
    }

    public enum ColonScanResult { Found, Absent, UnterminatedQuote }

    /// <summary>keys.rs scan_unescaped_colon. Writes the offset on Found.</summary>
    public static ColonScanResult ScanUnescapedColon(byte[] b, int lo, int hi, out int colon)
    {
        colon = -1;
        int from = lo;
        while (FindUnescapedColonFast(b, from, hi, out int cand))
        {
            switch (KeyPrefixQuoteState(b, lo, hi, from, cand, out int resume))
            {
                case 0: // opaque
                    colon = cand;
                    return ColonScanResult.Found;
                case 1: // open segment
                    from = resume;
                    break;
                default:
                    return ColonScanResult.UnterminatedQuote;
            }
        }
        if (HasQuoteBytes(b, lo, hi))
        {
            int c;
            var r = ScanUnescapedColonSlow(b, lo, hi, out c);
            if (r == ColonScanResult.Found) colon = c;
            return r;
        }
        return ColonScanResult.Absent;
    }

    /// <summary>Escape-aware candidate scan (keys.rs find_unescaped_colon_fast).</summary>
    private static bool FindUnescapedColonFast(byte[] b, int lo, int hi, out int colon)
    {
        int i = lo;
        while (i < hi)
        {
            int by = b[i];
            if (by == (byte)':') { colon = i; return true; }
            if (by == (byte)'\\') { i += 2; continue; }
            i++;
        }
        colon = -1;
        return false;
    }

    /// <summary>0 = opaque, 1 = open segment (resume past closer), 2 = unterminated.</summary>
    private static int KeyPrefixQuoteState(byte[] b, int lo, int hi, int from, int cand, out int resume)
    {
        resume = -1;
        bool hasQ = HasQuoteBytes(b, from, cand);
        if (!hasQ) return 0;
        int i = from;
        bool segStart = from == lo;
        while (i < cand)
        {
            if (segStart)
            {
                i = SkipSegmentWs(b, i, hi);
                if (i < cand && IsQuoteByte(b[i]))
                {
                    int end = QuotedSpanEnd(b, hi, i);
                    if (end < 0) return 2;
                    if (end > cand)
                    {
                        resume = end + 1;
                        return 1;
                    }
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= cand) break;
            int by = b[i];
            if (by == (byte)'\\') i += 2;
            else if (by == (byte)'.') { segStart = true; i++; }
            else i++;
        }
        return 0;
    }

    private static ColonScanResult ScanUnescapedColonSlow(byte[] b, int lo, int hi, out int colon)
    {
        colon = -1;
        int i = lo;
        bool segStart = true;
        while (i < hi)
        {
            if (segStart)
            {
                i = SkipSegmentWs(b, i, hi);
                if (i < hi && IsQuoteByte(b[i]))
                {
                    int end = QuotedSpanEnd(b, hi, i);
                    if (end < 0) return ColonScanResult.UnterminatedQuote;
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= hi) break;
            int by = b[i];
            if (by == (byte)'\\') i += 2;
            else if (by == (byte)'.') { segStart = true; i++; }
            else if (by == (byte)':') { colon = i; return ColonScanResult.Found; }
            else i++;
        }
        return ColonScanResult.Absent;
    }

    /// <summary>keys.rs find_unescaped_colon (None also for unterminated quote).</summary>
    public static bool FindUnescapedColon(byte[] b, int lo, int hi, out int colon)
    {
        var r = ScanUnescapedColon(b, lo, hi, out colon);
        return r == ColonScanResult.Found;
    }

    // ---------------- dotted-path splitting ----------------

    /// <summary>keys.rs split_key_path: split at unescaped `.` bytes.</summary>
    public static List<string> SplitKeyPath(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        var raw = new List<string>();
        SplitKeyPathBytes(b, 0, b.Length, raw, decode: false);
        return raw;
    }

    /// <summary>Byte-window variant appending decoded-or-raw segments.</summary>
    public static void SplitKeyPathBytes(byte[] b, int lo, int hi, List<string> raw, bool decode)
    {
        int start = lo;
        int i = lo;
        bool segStart = true;
        bool hasQ = HasQuoteBytes(b, lo, hi);
        while (i < hi)
        {
            if (hasQ && segStart)
            {
                i = SkipSegmentWs(b, i, hi);
                if (i < hi && IsQuoteByte(b[i]))
                {
                    int end = QuotedSpanEnd(b, hi, i);
                    if (end < 0)
                    {
                        // Defensive: keep remainder as one segment (keys.rs).
                        break;
                    }
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= hi) break;
            int by = b[i];
            if (by == (byte)'\\')
            {
                i += i + 1 < hi ? 2 : 1;
            }
            else if (by == (byte)'.')
            {
                AddSegment(b, start, i, raw, decode);
                start = i + 1;
                i++;
                segStart = true;
            }
            else i++;
        }
        AddSegment(b, start, hi, raw, decode);
    }

    private static void AddSegment(byte[] b, int lo, int hi, List<string> raw, bool decode)
    {
        if (decode)
        {
            try { raw.Add(DecodeKeySegment(b, lo, hi)); return; }
            catch (PureError) { }
        }
        raw.Add(Encoding.UTF8.GetString(b, lo, hi - lo));
    }

    /// <summary>keys.rs key_is_single_segment.</summary>
    public static bool KeyIsSingleSegment(byte[] b, int lo, int hi)
    {
        bool hasQ = HasQuoteBytes(b, lo, hi);
        int i = lo;
        bool segStart = true;
        while (i < hi)
        {
            if (hasQ && segStart)
            {
                i = SkipSegmentWs(b, i, hi);
                if (i < hi && IsQuoteByte(b[i]))
                {
                    int end = QuotedSpanEnd(b, hi, i);
                    if (end < 0) return true; // defensive (keys.rs)
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= hi) break;
            int by = b[i];
            if (by == (byte)'\\') { i += i + 1 < hi ? 2 : 1; continue; }
            if (by == (byte)'.') return false;
            i++;
        }
        return true;
    }

    // ---------------- segment decoding (§3.7 / §5.3.3) ----------------

    /// <summary>
    /// Decode one validated segment (keys.rs decode_key_segment): quoted
    /// segments strip the outer delimiters and decode the interior only;
    /// bare segments decode as-is. Throws BadEscapeSequence PureError with a
    /// zero span (callers rethrow with their own span context when needed —
    /// the Rust call sites pass their span; use the overload with ctx).
    /// </summary>
    public static string DecodeKeySegment(string input)
    {
        var b = Encoding.UTF8.GetBytes(input);
        return DecodeKeySegment(b, 0, b.Length);
    }

    public static string DecodeKeySegment(byte[] b, int lo, int hi)
    {
        if (lo < hi && IsQuoteByte(b[lo]))
            return ProcessEscapesNoLine(b, lo + 1, hi - 1);
        return ProcessEscapesNoLine(b, lo, hi);
    }

    /// <summary>decode_key_segment with the error raised with the given span/line.</summary>
    public static string DecodeKeySegmentChecked(byte[] b, int lo, int hi, int line, long s, long e)
    {
        try
        {
            return DecodeKeySegment(b, lo, hi);
        }
        catch (PureError pe)
        {
            throw PureError.BadEscapeSequence(line, pe.Body ?? "?", s, e);
        }
    }

    private static string ProcessEscapesNoLine(byte[] b, int lo, int hi)
    {
        bool hasBs = false;
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'\\') { hasBs = true; break; }
        if (!hasBs) return Encoding.UTF8.GetString(b, lo, hi - lo);
        // Same machine as ProcessEscapes; the caller (insert_value) rebuilds
        // the BadEscapeSequence error with its own line/span context.
        return ProcessEscapes(b, lo, hi, 0, 0, 0);
    }

    // ---------------- §4 key validation (validate.rs) ----------------

    private static bool ForbiddenControl(int b) =>
        (b < 0x20 && b != 0x09 && b != 0x0B && b != 0x0C) || b == 0x7F;

    private static bool ForbiddenRawKeyByte(int b) =>
        b == ',' || b == '{' || b == '}' || b == '[' || b == ']' || b == '(' || b == ')' ||
        ForbiddenControl(b);

    /// <summary>validate.rs escaped_len (surrogate-pair aware length skip).</summary>
    private static int EscapedLen(byte[] b, int hi, int i)
    {
        if (i + 1 >= hi) return 1;
        if (b[i + 1] != (byte)'u' || i + 6 > hi) return 2;
        if (!AllHex(b, i + 2, i + 6)) return 2;
        int value = HexAt(b, i + 2) << 12 | HexAt(b, i + 3) << 8 | HexAt(b, i + 4) << 4 | HexAt(b, i + 5);
        if (!(value >= 0xD800 && value <= 0xDBFF)) return 6;
        if (i + 12 > hi || b[i + 6] != (byte)'\\' || b[i + 7] != (byte)'u') return 6;
        if (!AllHex(b, i + 8, i + 12)) return 6;
        int low = HexAt(b, i + 8) << 12 | HexAt(b, i + 9) << 8 | HexAt(b, i + 10) << 4 | HexAt(b, i + 11);
        return low >= 0xDC00 && low <= 0xDFFF ? 12 : 6;
    }

    private static bool IsValidKey(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return false;
        int i = lo;
        while (i < hi)
        {
            if (b[i] == (byte)'\\') { i += EscapedLen(b, hi, i); continue; }
            if (ForbiddenRawKeyByte(b[i])) return false;
            i++;
        }
        return true;
    }

    public enum KeyValidity { Valid, Empty, Invalid }

    private static KeyValidity CheckQuotedKey(byte[] b, int lo, int hi)
    {
        int closer = QuotedSpanEnd(b, hi, lo);
        if (closer < 0) return KeyValidity.Invalid;
        if (closer != hi - 1) return KeyValidity.Invalid;
        int i = lo + 1;
        while (i < closer)
        {
            if (b[i] == (byte)'\\') { i += EscapedLen(b, hi, i); continue; }
            if (ForbiddenControl(b[i])) return KeyValidity.Invalid;
            i++;
        }
        if (closer == lo + 1) return KeyValidity.Empty; // "" / '' / ``
        return KeyValidity.Valid;
    }

    /// <summary>validate.rs check_key over one raw, outer-trimmed segment.</summary>
    public static KeyValidity CheckKey(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return KeyValidity.Empty;
        if (IsQuoteByte(b[lo])) return CheckQuotedKey(b, lo, hi);
        return IsValidKey(b, lo, hi) ? KeyValidity.Valid : KeyValidity.Invalid;
    }
}

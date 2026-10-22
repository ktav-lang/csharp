// UTF-8 validation and line/whitespace primitives over raw bytes (spec §3.1–§3.3, §6.15).
// Mirrors rust/src/cabi/envelope.rs (not_utf8_envelope, line_of_offset) and
// rust/src/document/whitespace.rs (the fixed 25-code-point §3.3 class).

using System;
using System.Text;

namespace Ktav.Pure;

internal static class PureUtf8
{
    public static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Length of a leading U+FEFF BOM, else 0 (spec §3.1).</summary>
    public static int BomLen(byte[] b)
    {
        return b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
    }

    /// <summary>
    /// Outcome of std-equivalent UTF-8 validation. <see cref="ErrorLen"/> is
    /// 0 for a truncated sequence at EOF (std's <c>error_len() == None</c>),
    /// otherwise the byte length of the invalid sequence.
    /// </summary>
    public readonly struct Utf8Failure
    {
        public readonly int ValidUpTo;
        public readonly int ErrorLen; // 0 == truncated at EOF

        public Utf8Failure(int validUpTo, int errorLen)
        {
            ValidUpTo = validUpTo;
            ErrorLen = errorLen;
        }
    }

    /// <summary>
    /// Validate the whole buffer; null when valid. Sequence rules mirror the
    /// std DFA: an illegal lead byte fails after 1 byte; a bad continuation
    /// fails after the bytes consumed of that sequence (2..4); a sequence
    /// cut off by EOF is "truncated" (ErrorLen 0).
    /// </summary>
    public static Utf8Failure? Validate(byte[] b)
    {
        int n = b.Length, i = 0;
        while (i < n)
        {
            int x = b[i];
            if (x < 0x80) { i++; continue; }
            if (x < 0xC2) return new Utf8Failure(i, 1); // 0x80..0xC1: lone cont / overlong lead
            if (x < 0xE0)
            {
                // 2-byte: C2..DF + cont
                if (i + 1 >= n) return new Utf8Failure(i, 0);
                if (!IsCont(b[i + 1])) return new Utf8Failure(i, 2);
                i += 2;
                continue;
            }
            if (x < 0xF0)
            {
                // 3-byte; second byte range depends on the lead (overlong / surrogates)
                int lo2, hi2;
                if (x == 0xE0) { lo2 = 0xA0; hi2 = 0xBF; }
                else if (x == 0xED) { lo2 = 0x80; hi2 = 0x9F; }
                else { lo2 = 0x80; hi2 = 0xBF; }
                if (i + 1 >= n) return new Utf8Failure(i, 0);
                if (b[i + 1] < lo2 || b[i + 1] > hi2) return new Utf8Failure(i, 2);
                if (i + 2 >= n) return new Utf8Failure(i, 0);
                if (!IsCont(b[i + 2])) return new Utf8Failure(i, 3);
                i += 3;
                continue;
            }
            if (x < 0xF5)
            {
                // 4-byte; F0 needs >= 0x90, F4 needs <= 0x8F
                int lo2, hi2;
                if (x == 0xF0) { lo2 = 0x90; hi2 = 0xBF; }
                else if (x == 0xF4) { lo2 = 0x80; hi2 = 0x8F; }
                else { lo2 = 0x80; hi2 = 0xBF; }
                if (i + 1 >= n) return new Utf8Failure(i, 0);
                if (b[i + 1] < lo2 || b[i + 1] > hi2) return new Utf8Failure(i, 2);
                if (i + 2 >= n) return new Utf8Failure(i, 0);
                if (!IsCont(b[i + 2])) return new Utf8Failure(i, 3);
                if (i + 3 >= n) return new Utf8Failure(i, 0);
                if (!IsCont(b[i + 3])) return new Utf8Failure(i, 4);
                i += 4;
                continue;
            }
            return new Utf8Failure(i, 1); // F5..FF
        }
        return null;
    }

    private static bool IsCont(int b) => b >= 0x80 && b <= 0xBF;

    /// <summary>std <c>Utf8Error</c> Display text, mirrored verbatim for the body field.</summary>
    public static string Utf8ErrorText(int validUpTo, int errorLen)
    {
        return errorLen == 0
            ? "incomplete utf-8 byte sequence from index " + validUpTo
            : "invalid utf-8 sequence of " + errorLen + " bytes from index " + validUpTo;
    }

    /// <summary>
    /// Span end for a raw InvalidUtf8 envelope: validUpTo + errorLen, with the
    /// cabi fix-up that a one-byte illegal lead swallows following continuation
    /// bytes; a truncated sequence runs to end-of-input (envelope.rs).
    /// </summary>
    public static long InvalidSpanEnd(byte[] src, int validUpTo, int errorLen)
    {
        long end;
        if (errorLen == 0)
            end = src.Length;
        else
        {
            end = (long)validUpTo + errorLen;
            if (errorLen == 1)
                while (end < src.Length && src[end] >= 0x80 && src[end] <= 0xBF) end++;
        }
        return Math.Min(end, src.Length);
    }

    /// <summary>
    /// 1-based line of a raw byte offset; each LF, lone CR and CR LF pair counts
    /// as one terminator that belongs to the offsets after it (envelope.rs).
    /// </summary>
    public static int LineOfOffset(byte[] b, long pos)
    {
        int line = 1;
        long bound = Math.Min(pos, b.Length);
        int i = 0;
        while (i < bound)
        {
            if (b[i] == (byte)'\n') { line++; i++; }
            else if (b[i] == (byte)'\r')
            {
                if (i + 1 < b.Length && b[i + 1] == (byte)'\n')
                {
                    if (i + 2 <= pos) line++;
                    i += 2;
                }
                else { line++; i++; }
            }
            else i++;
        }
        return line;
    }

    /// <summary>
    /// The source line containing byte offset <paramref name="pos"/> (pos == len
    /// yields the last line); one trailing CR is stripped. Returns null for an
    /// empty source (envelope.rs line_text_at).
    /// </summary>
    public static string? LineTextAt(byte[] src, long pos)
    {
        if (src.Length == 0) return null;
        int p = (int)Math.Min(pos, src.Length);
        int start = 0;
        for (int i = p - 1; i >= 0; i--)
            if (src[i] == (byte)'\n') { start = i + 1; break; }
        int end = src.Length;
        for (int i = p; i < src.Length; i++)
            if (src[i] == (byte)'\n') { end = i; break; }
        if (end > start && src[end - 1] == (byte)'\r') end--;
        return Encoding.UTF8.GetString(src, start, end - start);
    }

    // ------------------------------------------------------------------
    // §3.3 fixed 25-code-point whitespace class. ASCII members take the
    // byte fast path; the multi-byte members are matched by their exact
    // UTF-8 encodings. Inline view = full class minus LF/CR (23 cps).
    // ------------------------------------------------------------------

    /// <summary>Byte length of the §3.3 whitespace code point at b[lo], else 0.</summary>
    public static int WsLenAt(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return 0;
        int x = b[lo];
        if (x == 0x20 || x == 0x09 || x == 0x0A || x == 0x0B || x == 0x0C || x == 0x0D) return 1;
        if (x < 0x80 || lo + 1 >= hi) return 0;
        int y = b[lo + 1];
        if (x == 0xC2) return y == 0x85 || y == 0xA0 ? 2 : 0;
        if (x == 0xE1 && y == 0x9A && lo + 2 < hi && b[lo + 2] == 0x80) return 3; // U+1680
        if (x == 0xE2 && y == 0x80 && lo + 2 < hi)
        {
            int z = b[lo + 2];
            // U+2000..U+200A, U+2028, U+2029, U+202F
            if ((z >= 0x80 && z <= 0x8A) || z == 0xA8 || z == 0xA9 || z == 0xAF) return 3;
            return 0;
        }
        if (x == 0xE2 && y == 0x81 && lo + 2 < hi && b[lo + 2] == 0x9F) return 3; // U+205F
        if (x == 0xE3 && y == 0x80 && lo + 2 < hi && b[lo + 2] == 0x80) return 3; // U+3000
        return 0;
    }

    /// <summary>Inline view (no LF/CR) whitespace length at b[lo], else 0.</summary>
    public static int InlineWsLenAt(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return 0;
        int x = b[lo];
        if (x == 0x20 || x == 0x09 || x == 0x0B || x == 0x0C) return 1;
        if (x <= 0x0D) return 0;
        int len = WsLenAt(b, lo, hi);
        return len;
    }

    /// <summary>True iff the only bytes in [lo,hi) are full-class whitespace.</summary>
    public static bool AllWhitespace(byte[] b, int lo, int hi)
    {
        int i = lo;
        while (i < hi)
        {
            int len = WsLenAt(b, i, hi);
            if (len == 0) return false;
            i += len;
        }
        return true;
    }

    /// <summary>Trim full-class whitespace from both ends; returns the inner window.</summary>
    public static (int, int) Trim(byte[] b, int lo, int hi)
    {
        while (lo < hi)
        {
            int len = WsLenAt(b, lo, hi);
            if (len == 0) break;
            lo += len;
        }
        while (hi > lo)
        {
            // Trim one code point at a time from the end (all class members
            // are at most 3 bytes; decode backwards conservatively).
            int back = BackWsLen(b, lo, hi);
            if (back == 0) break;
            hi -= back;
        }
        return (lo, hi);
    }

    private static int BackWsLen(byte[] b, int lo, int hi)
    {
        for (int start = Math.Max(lo, hi - 3); start < hi; start++)
        {
            int len = WsLenAt(b, start, hi);
            if (len > 0 && start + len == hi) return len;
        }
        return 0;
    }

    /// <summary>Trim inline-class whitespace from both ends of [lo,hi).</summary>
    public static (int, int) TrimInline(byte[] b, int lo, int hi)
    {
        while (lo < hi)
        {
            int len = InlineWsLenAt(b, lo, hi);
            if (len == 0) break;
            lo += len;
        }
        while (hi > lo)
        {
            int back = BackInlineWsLen(b, lo, hi);
            if (back == 0) break;
            hi -= back;
        }
        return (lo, hi);
    }

    private static int BackInlineWsLen(byte[] b, int lo, int hi)
    {
        for (int start = Math.Max(lo, hi - 3); start < hi; start++)
        {
            int len = InlineWsLenAt(b, start, hi);
            if (len > 0 && start + len == hi) return len;
        }
        return 0;
    }

    /// <summary>Trim inline whitespace from the start only.</summary>
    public static int TrimInlineStart(byte[] b, int lo, int hi)
    {
        while (lo < hi)
        {
            int len = InlineWsLenAt(b, lo, hi);
            if (len == 0) break;
            lo += len;
        }
        return lo;
    }

    /// <summary>Trim full-class whitespace from the start only.</summary>
    public static int TrimStart(byte[] b, int lo, int hi)
    {
        while (lo < hi)
        {
            int len = WsLenAt(b, lo, hi);
            if (len == 0) break;
            lo += len;
        }
        return lo;
    }

    /// <summary>
    /// Byte length of the code-point leading-whitespace run at the start of
    /// [lo,hi) (§5.6; inline view — LF/CR cannot occur in a split line).
    /// </summary>
    public static int LeadingWsRun(byte[] b, int lo, int hi)
    {
        int i = lo;
        while (i < hi)
        {
            int len = InlineWsLenAt(b, i, hi);
            if (len == 0) break;
            i += len;
        }
        return i - lo;
    }

}

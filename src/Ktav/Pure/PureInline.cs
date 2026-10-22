// Inline compounds (§5.8): the shared byte scanner, top-level splitting,
// closer scanning, and inline object/array/scalar parsing.
// Mirrors rust/src/document/parser/inline/{scan/scanner.rs,scan/split.rs,
// compound.rs,classify inline arms}.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureInline
{
    /// <summary>compound.rs MAX_INLINE_DEPTH (Q-3 decision).</summary>
    public const int MaxInlineDepth = 128;

    // ------------------------------------------------------------------
    // Shared scanner (scanner.rs). The Rust compile-time config knobs are
    // runtime flags here; behaviour is byte-identical per mode.
    // ------------------------------------------------------------------

    private enum Mode { Find, Scan, Split }

    private sealed class Scanner
    {
        private readonly byte[] _b;
        private readonly int _lo, _hi;
        private readonly int _open, _close;
        private readonly bool _bodyObject;
        private readonly int _line;
        private readonly long _spanS, _spanE;
        private readonly Mode _mode;
        private readonly bool _trackQuotes;   // hasQuotes && (scan/find/split-object)
        private readonly bool _validateEscapes;
        private readonly bool _useStack;
        private readonly bool _openerJump;
        private readonly bool _commaSplits;
        private readonly bool _commaCtxScope;
        private readonly bool _commaClearsRaw;
        private readonly bool _colonElseClearVs;
        private readonly bool _closerLiteral;
        private readonly bool _restoreSegOnMatch;
        private readonly bool _restoreRawOnMatch;
        private readonly bool _mismatchSegFalse;
        private readonly bool _recordBounds;

        internal int I;
        internal int Depth;
        internal bool InKey, SegStart, ValueStart;
        private bool _raw;
        private int _prev;
        private int _segAt;
        private readonly List<int> _stackKind = new();      // opener byte per scope
        private readonly List<byte> _stackSaved = new();    // packed saved flags
        internal readonly List<string> Segments = new();
        private List<(int o, int c)> _pairs = new();
        private readonly List<(int open, bool pure, int entry)> _openAt = new();

        internal Scanner(byte[] b, int lo, int hi, int open, int close, bool bodyObject,
            int line, long spanS, long spanE, Mode mode, bool trackQuotes, bool validateEscapes,
            bool useStack, bool openerJump, bool commaSplits, bool commaCtxScope,
            bool commaClearsRaw, bool colonElseClearVs, bool closerLiteral,
            bool restoreSegOnMatch, bool restoreRawOnMatch, bool mismatchSegFalse,
            bool recordBounds)
        {
            _b = b; _lo = lo; _hi = hi; _open = open; _close = close;
            _bodyObject = bodyObject; _line = line; _spanS = spanS; _spanE = spanE;
            _mode = mode;
            _trackQuotes = trackQuotes; _validateEscapes = validateEscapes;
            _useStack = useStack; _openerJump = openerJump; _commaSplits = commaSplits;
            _commaCtxScope = commaCtxScope; _commaClearsRaw = commaClearsRaw;
            _colonElseClearVs = colonElseClearVs; _closerLiteral = closerLiteral;
            _restoreSegOnMatch = restoreSegOnMatch; _restoreRawOnMatch = restoreRawOnMatch;
            _mismatchSegFalse = mismatchSegFalse; _recordBounds = recordBounds;
            I = lo; Depth = 0; InKey = false; SegStart = false; ValueStart = false;
            _raw = false; _prev = open; _segAt = lo;
        }

        internal List<(int o, int c)> TakePairs() { var p = _pairs; _pairs = new(); return p; }
        internal int SegAt => _segAt;

        internal const int StopCloser = 0, StopEofAfterWsSkip = 1, StopUnterminatedQuote = 2,
            StopBadEscape = 3, StopExhausted = 4;
        internal int StopByte = 0; // byte at StopCloser
        internal PureError? BadEscape;

        internal int Run()
        {
            int i = I, depth = Depth, prev = _prev, segAt = _segAt;
            bool inKey = InKey, segStart = SegStart, valueStart = ValueStart, raw = _raw;
            var b = _b; int lo = _lo, hi = _hi;
            int stop = StopExhausted;

            while (true)
            {
                if (i >= hi) { stop = StopExhausted; break; }

                // (1) quoted key segment start
                if (_trackQuotes && inKey && segStart)
                {
                    i = PureKey.SkipSegmentWs(b, i, hi);
                    if (i >= hi) { stop = StopEofAfterWsSkip; break; }
                    if (PureKey.IsQuoteByte(b[i]))
                    {
                        int end = PureKey.QuotedSpanEnd(b, hi, i);
                        if (end < 0) { stop = StopUnterminatedQuote; break; }
                        prev = b[end];
                        i = end + 1;
                        segStart = false;
                        continue;
                    }
                    segStart = false;
                }

                int by = b[i];
                if (by == (byte)'\\')
                {
                    if (_validateEscapes)
                    {
                        string? err = ScanEscapeForScanner(b, lo, hi, i, out int escLen);
                        if (err != null)
                        {
                            BadEscape = PureError.BadEscapeSequence(_line, err, _spanS, _spanE);
                            stop = StopBadEscape;
                            break;
                        }
                        valueStart = false;
                        prev = (byte)'\\';
                        i += escLen;
                        continue;
                    }
                    valueStart = false;
                    i += 2;
                    continue;
                }
                if (by == (byte)':')
                {
                    if (inKey)
                    {
                        inKey = false;
                        valueStart = true;
                    }
                    else if (_mode != Mode.Split && prev == (byte)':' && valueStart)
                    {
                        raw = true; // `::` raw marker (§5.4; TRACK_RAW configs only)
                    }
                    else
                    {
                        if (_colonElseClearVs)
                        {
                            valueStart = false; // split's non-key colon
                        }
                        else if (valueStart && !(i + 1 < hi && b[i + 1] == (byte)':'))
                        {
                            valueStart = false; // lone `:` consumes the value position
                        }
                    }
                }
                else if (by == (byte)',')
                {
                    if (_commaSplits)
                    {
                        Segments.Add(Encoding.UTF8.GetString(b, segAt, i - segAt));
                        segAt = i + 1;
                    }
                    bool scopeObject = _commaCtxScope
                        ? (_stackKind.Count > 0 ? _stackKind[_stackKind.Count - 1] == '{' : _bodyObject)
                        : _bodyObject;
                    inKey = scopeObject;
                    if (_trackQuotes) segStart = scopeObject;
                    if (_commaClearsRaw) raw = false;
                    valueStart = !scopeObject;
                }
                else if (by == (byte)'.' && _trackQuotes && inKey)
                {
                    segStart = true;
                }
                else if (by == (byte)'{' || by == (byte)'[')
                {
                    bool gatedOpen = valueStart && !raw;
                    if (!gatedOpen)
                    {
                        valueStart = false; // mid-scalar / raw-mode opener: literal
                    }
                    else if (_openerJump)
                    {
                        int o = by == '{' ? '{' : '[';
                        int c = by == '{' ? '}' : ']';
                        if (_knownCloserAbs(i) is int endAbs && endAbs < hi)
                        {
                            i = endAbs + 1;
                            valueStart = false;
                            if (_trackQuotes) segStart = false;
                            continue;
                        }
                        int pos;
                        var scan = ScanInlineCloser(b, i, hi, o, c, _line, _spanS, _spanE, out pos);
                        if (scan)
                        {
                            i += pos + 1;
                            valueStart = false;
                            if (_trackQuotes) segStart = false;
                            continue;
                        }
                        // No closer — treat as literal byte (mid-value brace).
                        valueStart = false;
                        i += 1;
                        continue;
                    }
                    else
                    {
                        depth++;
                        _stackKind.Add(by);
                        _stackSaved.Add((byte)((inKey ? 2 : 0) | (segStart ? 4 : 0) | (raw ? 8 : 0)));
                        if (_recordBounds) _openAt.Add((i, true, depth));
                    }
                    valueStart = by == '[';
                    inKey = by == '{';
                    if (_trackQuotes) segStart = by == '{';
                }
                else if ((by == (byte)'}' || by == (byte)']') && !_closerLiteral)
                {
                    raw = false;
                    depth--;
                    if (depth == 0)
                    {
                        stop = StopCloser;
                        StopByte = by;
                        break;
                    }
                    int want = by == ']' ? '[' : '{';
                    if (_recordBounds && _openAt.Count > 0)
                    {
                        var top = _openAt[_openAt.Count - 1];
                        if (depth == top.entry - 1)
                        {
                            if (!(_stackKind.Count > 0 && _stackKind[_stackKind.Count - 1] == want))
                                _openAt[_openAt.Count - 1] = (top.open, false, top.entry);
                        }
                        else if (depth < top.entry - 1)
                        {
                            _openAt[_openAt.Count - 1] = (top.open, false, top.entry);
                        }
                    }
                    if (_stackKind.Count > 0 && _stackKind[_stackKind.Count - 1] == want)
                    {
                        byte saved = _stackSaved[_stackSaved.Count - 1];
                        _stackKind.RemoveAt(_stackKind.Count - 1);
                        _stackSaved.RemoveAt(_stackSaved.Count - 1);
                        if (_recordBounds && _openAt.Count > 0)
                        {
                            var (open, pure, entry) = _openAt[_openAt.Count - 1];
                            _openAt.RemoveAt(_openAt.Count - 1);
                            if (pure && depth == entry - 1) _pairs.Add((open, i));
                        }
                        inKey = (saved & 2) != 0;
                        if (_trackQuotes)
                        {
                            segStart = _restoreSegOnMatch ? (saved & 4) != 0 : false;
                            if (_restoreRawOnMatch) raw = (saved & 8) != 0;
                        }
                    }
                    else if (_mismatchSegFalse && _trackQuotes)
                    {
                        segStart = false;
                    }
                    valueStart = false; // closed compound consumed the value position
                }
                else
                {
                    int wsLen = PureUtf8.InlineWsLenAt(b, i, hi);
                    if (wsLen > 0)
                    {
                        prev = b[i + wsLen - 1];
                        i += wsLen;
                        continue;
                    }
                    valueStart = false;
                }
                prev = by;
                i++;
            }

            I = i; Depth = depth; InKey = inKey; SegStart = segStart;
            ValueStart = valueStart; _raw = raw; _prev = prev; _segAt = segAt;
            return stop;
        }

        /// <summary>Recorded closer of a span opening at absolute `i`, or null.</summary>
        private int? _knownCloserAbs(int i)
        {
            // split's memo consult (bounds recorded by the gate scan)
            if (_memo == null) return null;
            return _memo.CloseOf(i);
        }

        internal InlineBounds? _memo;

        internal void SetMemo(InlineBounds memo) => _memo = memo;
    }

    /// <summary>scanner.rs escape scan wrapper returning the full sequence length.</summary>
    private static string? ScanEscapeForScanner(byte[] b, int lo, int hi, int i, out int len)
    {
        string? err = null;
        len = 0;
        if (i + 1 >= hi) { err = "\\<end-of-line>"; return err; }
        int next = b[i + 1];
        if (next == 'u')
        {
            // mirror escapes.rs: validate the full form; full length advance
            if (i + 6 > hi || !AllHexStatic(b, i + 2, i + 6))
            {
                return MalformedUnicodeText(b, lo, hi, i);
            }
            int v = Hex4(b, i + 2);
            if (v >= 0xD800 && v <= 0xDBFF)
            {
                if (i + 12 <= hi && b[i + 6] == (byte)'\\' && b[i + 7] == (byte)'u' &&
                    AllHexStatic(b, i + 8, i + 12))
                {
                    int low = Hex4(b, i + 8);
                    if (low >= 0xDC00 && low <= 0xDFFF) { len = 12; return null; }
                }
                return MalformedUnicodeText(b, lo, hi, i);
            }
            if (v >= 0xDC00 && v <= 0xDFFF)
                return MalformedUnicodeText(b, lo, hi, i);
            len = 6;
            return null;
        }
        int simple = SimpleEscapeIndex(next);
        if (simple >= 0) { len = 2; return null; }
        return next < 0x80 ? "\\" + (char)next : "\\<0x" + next.ToString("X2") + ">";
    }

    private static int SimpleEscapeIndex(int next)
    {
        switch (next)
        {
            case (byte)'\\': case (byte)',': case (byte)'}': case (byte)']':
            case (byte)'{': case (byte)'[': case (byte)'n': case (byte)'r':
            case (byte)'.': case (byte)':': case (byte)'"': case (byte)'\'':
            case (byte)'`':
                return 0;
        }
        return -1;
    }

    private static bool AllHexStatic(byte[] b, int lo, int hi)
    {
        for (int i = lo; i < hi; i++)
        {
            int c = b[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }

    private static int Hex4(byte[] b, int i) =>
        HexDigit(b[i]) << 12 | HexDigit(b[i + 1]) << 8 | HexDigit(b[i + 2]) << 4 | HexDigit(b[i + 3]);

    private static int HexDigit(int c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return c - 'A' + 10;
    }

    private static string MalformedUnicodeText(byte[] b, int lo, int hi, int i)
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

    // ------------------------------------------------------------------
    // Gate-scan bounds memo (split.rs InlineBounds)
    // ------------------------------------------------------------------

    internal sealed class InlineBounds
    {
        private readonly List<(int o, int c)> _pairs;
        public InlineBounds(List<(int o, int c)> pairs) { _pairs = pairs; _pairs.Sort((x, y) => x.o.CompareTo(y.o)); }

        public int? CloseOf(int opener)
        {
            int lo = 0, hi = _pairs.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2; // lo+hi cannot overflow int here
                if (_pairs[mid].o == opener) return _pairs[mid].c;
                if (_pairs[mid].o < opener) lo = mid + 1; else hi = mid - 1;
            }
            return null;
        }

        public int? KnownCloser(int opener, int spanEnd)
        {
            var c = CloseOf(opener);
            if (c.HasValue && c.Value < spanEnd) return c.Value;
            return null;
        }
    }

    /// <summary>split.rs scan_inline_closer: true + pos on Found; false = NotFound/BadEscape.</summary>
    internal static bool ScanInlineCloser(byte[] b, int lo, int hi, int open, int close,
        int line, long spanS, long spanE, out int pos)
    {
        pos = -1;
        if (lo >= hi || b[lo] != open) return false;
        bool hasQ = PureKey.HasQuoteBytes(b, lo, hi);
        var sc = NewScanner(b, lo, hi, open, close, open == '{', line, spanS, spanE,
            Mode.Scan, hasQ, validate: true, record: false);
        sc.I = lo + 1;
        sc.Depth = 1;
        sc.InKey = open == '{';
        sc.SegStart = open == '{';
        sc.ValueStart = open != '{';
        int stop = sc.Run();
        if (stop == Scanner.StopCloser && sc.StopByte == close)
        {
            pos = sc.I;
            return true;
        }
        return false; // includes BadEscape → the jump treats the byte as literal
    }

    /// <summary>scan_inline_closer_with_bounds: Found/NotFound/BadEscape + recorded pairs.</summary>
    internal static int ScanInlineCloserWithBounds(byte[] b, int lo, int hi, int open, int close,
        int line, long spanS, long spanE, out List<(int o, int c)> bounds, out PureError? badEscape)
    {
        badEscape = null;
        bounds = new List<(int o, int c)>();
        if (lo >= hi || b[lo] != open) return -1; // NotFound
        bool hasQ = PureKey.HasQuoteBytes(b, lo, hi);
        var sc = NewScanner(b, lo, hi, open, close, open == '{', line, spanS, spanE,
            Mode.Scan, hasQ, validate: true, record: true);
        sc.I = lo + 1;
        sc.Depth = 1;
        sc.InKey = open == '{';
        sc.SegStart = open == '{';
        sc.ValueStart = open != '{';
        int stop = sc.Run();
        var pairs = sc.TakePairs();
        if (stop == Scanner.StopBadEscape)
        {
            badEscape = sc.BadEscape;
            return -2; // BadEscape
        }
        if (stop == Scanner.StopCloser && sc.StopByte == close)
        {
            pairs.Sort((x, y) => x.o.CompareTo(y.o));
            bounds = pairs;
            return sc.I; // Found
        }
        return -1; // NotFound
    }

    /// <summary>split.rs find_matching_close (Option&lt;usize&gt;).</summary>
    internal static int FindMatchingClose(byte[] b, int lo, int hi, int open, int close)
    {
        if (lo >= hi || b[lo] != open) return -1;
        bool hasQ = PureKey.HasQuoteBytes(b, lo, hi);
        var sc = NewScanner(b, lo, hi, open, close, open == '{', 0, 0, 0,
            Mode.Find, hasQ, validate: false, record: false);
        sc.I = lo + 1;
        sc.Depth = 1;
        sc.InKey = open == '{';
        sc.SegStart = open == '{';
        sc.ValueStart = open != '{';
        int stop = sc.Run();
        if (stop == Scanner.StopCloser && sc.StopByte == close) return sc.I;
        return -1;
    }

    private static Scanner NewScanner(byte[] b, int lo, int hi, int open, int close,
        bool bodyObject, int line, long spanS, long spanE, Mode mode, bool trackQuotes,
        bool validate, bool record)
    {
        return new Scanner(
            b, lo, hi, open, close, bodyObject, line, spanS, spanE, mode,
            trackQuotes: trackQuotes,
            validateEscapes: validate,
            useStack: mode != Mode.Split,
            openerJump: mode == Mode.Split,
            commaSplits: mode == Mode.Split,
            commaCtxScope: mode != Mode.Split,
            commaClearsRaw: mode != Mode.Split,
            colonElseClearVs: mode == Mode.Split,
            closerLiteral: mode == Mode.Split,
            restoreSegOnMatch: trackQuotes,
            restoreRawOnMatch: trackQuotes,
            mismatchSegFalse: trackQuotes,
            recordBounds: record);
    }

    /// <summary>split.rs split_top_level; segments as string windows are appended raw.</summary>
    internal static void SplitTopLevel(byte[] b, int lo, int hi, bool bodyObject,
        int line, long spanS, long spanE, InlineBounds? bounds, bool hasQuotes,
        List<(int s, int e)> segments, out bool unterminated)
    {
        unterminated = false;
        bool trackQuotes = bodyObject && hasQuotes;
        var sc = NewScanner(b, lo, hi, bodyObject ? '{' : '[', bodyObject ? '}' : ']',
            bodyObject, line, spanS, spanE, Mode.Split, trackQuotes,
            validate: false, record: false);
        sc._memo = bounds;
        sc.I = lo;
        sc.Depth = 0;
        sc.InKey = bodyObject;
        sc.SegStart = bodyObject;
        sc.ValueStart = !bodyObject;
        int stop = sc.Run();
        if (stop == Scanner.StopUnterminatedQuote)
        {
            unterminated = true;
            return;
        }
        if (stop == Scanner.StopEofAfterWsSkip)
        {
            var (tlo, thi) = PureUtf8.TrimInline(b, sc.SegAt, hi);
            if (tlo >= thi)
                return; // trailing comma: no final segment
            segments.Add((sc.SegAt, hi));
            return;
        }
        // Exhausted (split's closers are literal, so no other stop can occur)
        segments.Add((sc.SegAt, hi));
    }

    // ------------------------------------------------------------------
    // Inline compound parsing (compound.rs)
    // ------------------------------------------------------------------

    internal static PureObjBuilder ParseInlineObject(byte[] b, int lo, int hi,
        int line, long spanS, long spanE, bool strict, InlineBounds bounds, bool hasQuotes)
    {
        return ParseInlineObjectInner(b, lo, hi, line, spanS, spanE, 0, strict, bounds, hasQuotes);
    }

    internal static List<object?> ParseInlineArray(byte[] b, int lo, int hi,
        int line, long spanS, long spanE, bool strict, InlineBounds bounds, bool hasQuotes)
    {
        return ParseInlineArrayInner(b, lo, hi, line, spanS, spanE, 0, strict, bounds, hasQuotes);
    }

    private static PureObjBuilder ParseInlineObjectInner(byte[] b, int lo, int hi,
        int line, long spanS, long spanE, int depth, bool strict, InlineBounds bounds,
        bool hasQuotes)
    {
        if (depth >= MaxInlineDepth)
            throw PureError.MalformedInlineCompound(line, "nesting depth exceeds limit (128)", spanS, spanE);

        int innerLo = lo + 1, innerHi = hi - 1;
        var (tlo, thi) = PureUtf8.TrimInline(b, innerLo, innerHi);
        var map = new PureObjBuilder();
        if (tlo >= thi) return map;

        var segments = new List<(int s, int e)>();
        SplitTopLevel(b, innerLo, innerHi, bodyObject: true, line, spanS, spanE,
            bounds, hasQuotes, segments, out bool unterminated);
        if (unterminated)
            throw PureError.UnterminatedInlineCompound(line, spanS, spanE);

        int n = segments.Count;
        for (int i = 0; i < n; i++)
        {
            var (slo, shi) = PureUtf8.TrimInline(b, segments[i].s, segments[i].e);
            if (slo >= shi)
            {
                if (i == n - 1) break; // trailing comma OK
                throw PureError.MalformedInlineCompound(line,
                    "empty pair segment (leading comma, double comma, or missing pair)", spanS, spanE);
            }

            if (!PureKey.FindUnescapedColon(b, slo, shi, out int colon))
                throw PureError.MalformedInlineCompound(line,
                    "inline object pair missing ':' separator in '" +
                    Encoding.UTF8.GetString(b, slo, shi - slo) + "'", spanS, spanE);

            int keyLo = slo, keyHi = colon;
            (keyLo, keyHi) = PureUtf8.TrimInline(b, keyLo, keyHi);
            int vLo = colon + 1, vHi = shi;
            bool isRaw = vLo < vHi && b[vLo] == (byte)':';
            if (isRaw) vLo++;

            if (keyLo >= keyHi)
                throw PureError.EmptyKey(line, spanS, spanE);

            object? value;
            if (isRaw)
            {
                (vLo, vHi) = PureUtf8.TrimInline(b, vLo, vHi);
                value = new KtavString(PureKey.ProcessEscapes(b, vLo, vHi, line, spanS, spanE));
            }
            else
            {
                value = ParseInlineValue(b, vLo, vHi, line, spanS, spanE, depth, strict, bounds, hasQuotes);
            }

            InsertValue(map, b, keyLo, keyHi, value, line, spanS, spanE);
        }

        return map;
    }

    private static List<object?> ParseInlineArrayInner(byte[] b, int lo, int hi,
        int line, long spanS, long spanE, int depth, bool strict, InlineBounds bounds,
        bool hasQuotes)
    {
        if (depth >= MaxInlineDepth)
            throw PureError.MalformedInlineCompound(line, "nesting depth exceeds limit (128)", spanS, spanE);

        int innerLo = lo + 1, innerHi = hi - 1;
        var (tlo, thi) = PureUtf8.TrimInline(b, innerLo, innerHi);
        var items = new List<object?>();
        if (tlo >= thi) return items;

        var segments = new List<(int s, int e)>();
        SplitTopLevel(b, innerLo, innerHi, bodyObject: false, line, spanS, spanE,
            bounds, hasQuotes, segments, out bool unterminated);
        if (unterminated)
            throw PureError.UnterminatedInlineCompound(line, spanS, spanE);

        int n = segments.Count;
        for (int i = 0; i < n; i++)
        {
            var (slo, shi) = PureUtf8.TrimInline(b, segments[i].s, segments[i].e);
            if (slo >= shi)
            {
                if (i == n - 1) break; // trailing comma OK
                throw PureError.MalformedInlineCompound(line,
                    "empty inline-array item (leading comma, double comma, or empty position)", spanS, spanE);
            }
            items.Add(ParseInlineValueRaw(b, slo, shi, line, spanS, spanE, depth, strict, bounds, hasQuotes));
        }
        return items;
    }

    /// <summary>compound.rs parse_inline_value (value body not yet trimmed).</summary>
    private static object? ParseInlineValue(byte[] b, int lo, int hi, int line,
        long spanS, long spanE, int depth, bool strict, InlineBounds bounds, bool hasQuotes)
    {
        var (tlo, thi) = PureUtf8.TrimInline(b, lo, hi);
        if (tlo >= thi) return new KtavString("");
        return ParseInlineValueRaw(b, tlo, thi, line, spanS, spanE, depth, strict, bounds, hasQuotes);
    }

    /// <summary>compound.rs parse_inline_value_raw (§5.8.5 mid-value brace rule).</summary>
    private static object? ParseInlineValueRaw(byte[] b, int lo, int hi, int line,
        long spanS, long spanE, int depth, bool strict, InlineBounds bounds, bool hasQuotes)
    {
        int first = b[lo];

        if (first == '{' || first == '[')
        {
            int close = first == '{' ? '}' : ']';
            int open = first;
            int closeIdx = -1;
            int? memo = bounds.KnownCloser(lo, hi);
            if (memo.HasValue)
            {
                closeIdx = memo.Value;
            }
            else
            {
                int found = FindMatchingClose(b, lo, hi, open, close);
                if (found >= 0)
                {
                    closeIdx = found;
                }
                else
                {
                    var r = ScanInlineCloserWithBounds(b, lo, hi, open, close, line, spanS, spanE,
                        out _, out PureError? bad);
                    if (bad != null) throw bad;
                    if (r >= 0) closeIdx = r;
                }
            }
            if (closeIdx >= 0)
            {
                if (closeIdx == hi - 1)
                {
                    var (ilo, ihi) = PureUtf8.TrimInline(b, lo + 1, hi - 1);
                    if (ilo >= ihi)
                        return first == '{' ? (object?)new PureObjBuilder()
                            : (object?)new List<object?>();
                    return first == '{'
                        ? (object?)ParseInlineObjectInner(b, lo, hi, line, spanS, spanE,
                            depth + 1, strict, bounds, hasQuotes)
                        : (object?)ParseInlineArrayInner(b, lo, hi, line, spanS, spanE,
                            depth + 1, strict, bounds, hasQuotes);
                }
                throw PureError.MalformedInlineCompound(line,
                    "matching closer is not the last byte of the body; non-whitespace content follows the closed inline compound",
                    spanS, spanE);
            }
            throw PureError.UnterminatedInlineCompound(line, spanS, spanE);
        }

        // §5.2 rule 5: `()` / `(())` → empty String
        if (hi - lo == 2 && b[lo] == '(' && b[lo + 1] == ')')
            return new KtavString("");
        if (hi - lo == 4 && b[lo] == '(' && b[lo + 1] == '(' && b[lo + 2] == ')' && b[lo + 3] == ')')
            return new KtavString("");

        // Plain inline scalar — escapes force String (§3.7/§5.2 rule 15).
        bool hasBs = false;
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'\\') { hasBs = true; break; }
        if (hasBs)
            return new KtavString(PureKey.ProcessEscapes(b, lo, hi, line, spanS, spanE));

        return ClassifyInlineScalar(b, lo, hi, line, spanS, spanE, strict);
    }

    /// <summary>compound.rs classify_inline_scalar (§5.2 rules 10–15).</summary>
    internal static object? ClassifyInlineScalar(byte[] b, int lo, int hi, int line,
        long spanS, long spanE, bool strict)
    {
        if (lo >= hi) return new KtavString("");
        if (hi - lo == 4 && Eq(b, lo, "null")) return KtavNull.Instance;
        if (hi - lo == 4 && Eq(b, lo, "true")) return KtavBool.True;
        if (hi - lo == 5 && Eq(b, lo, "false")) return KtavBool.False;

        if (PureNum.HasRedundantLeadingZero(b, lo, hi))
            return new KtavString(Text(b, lo, hi));

        if (PureNum.FastPlainDecimalI64(b, lo, hi))
            return new KtavInteger(Text(b, lo, hi));
        if (PureNum.TryParseInteger(b, lo, hi, out long val))
        {
            string canonical = PureNum.I64Text(val);
            if (strict && canonical != Text(b, lo, hi))
                throw PureError.LossyScalar(line, Text(b, lo, hi), canonical, spanS, spanE);
            return new KtavInteger(canonical);
        }

        if (PureNum.IsFloatLiteral(b, lo, hi) &&
            PureNum.ParseFloatValue(b, lo, hi, out double fv))
        {
            string ryu = PureNum.RyuFormat(fv);
            if (strict)
            {
                string rendered = PureNum.CanonicalFloat(ryu);
                string body = Text(b, lo, hi);
                if (rendered != body)
                    throw PureError.LossyScalar(line, body, rendered, spanS, spanE);
                return new KtavFloat(ryu);
            }
            return new KtavFloat(ryu);
        }

        return new KtavString(Text(b, lo, hi));
    }

    internal static bool Eq(byte[] b, int lo, string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (b[lo + i] != (byte)s[i]) return false;
        return true;
    }

    internal static string Text(byte[] b, int lo, int hi) =>
        Encoding.UTF8.GetString(b, lo, hi - lo);

    // ------------------------------------------------------------------
    // §6.3 insert tables (build/insert.rs) — shared with the line parser.
    // ------------------------------------------------------------------

    internal static void InsertValue(PureObjBuilder table, byte[] b, int kLo, int kHi,
        object? value, int line, long spanS, long spanE)
    {
        if (PureKey.KeyIsSingleSegment(b, kLo, kHi))
        {
            var (tlo, thi) = PureUtf8.Trim(b, kLo, kHi); // inline view in practice; full class trims identically (no LF/CR inside a line)
            if (tlo >= thi)
                throw PureError.EmptyKey(line, spanS, spanE);
            switch (PureKey.CheckKey(b, tlo, thi))
            {
                case PureKey.KeyValidity.Empty:
                    throw PureError.EmptyKey(line, spanS, spanE);
                case PureKey.KeyValidity.Invalid:
                    throw PureError.InvalidKey(line, Text(b, kLo, kHi),
                        PureKey.SplitKeyPath(Text(b, kLo, kHi)), spanS, spanE);
            }
            string decoded = PureKey.DecodeKeySegmentChecked(b, tlo, thi, line, spanS, spanE);
            var slot = PureObjBuilder.SlotOf(value);
            if (!table.InsertLeaf(decoded, slot))
            {
                var existing = ExistingSlot(table, decoded);
                if (existing.IsObject != slot.IsObject)
                {
                    throw PureError.KeyPathConflictOverwrite(line, Text(b, kLo, kHi),
                        existing.Label, slot.Label, PureError.DecodedPath(Text(b, kLo, kHi)), spanS, spanE);
                }
                throw PureError.DuplicateKey(line, Text(b, kLo, kHi),
                    PureError.DecodedPath(Text(b, kLo, kHi)), spanS, spanE);
            }
            return;
        }

        // Dotted path: raw byte windows, validated per segment.
        var rawSegs = new List<(int s, int e)>();
        CollectSegments(b, kLo, kHi, rawSegs);
        int nSegs = rawSegs.Count;
        PureObjBuilder t = table;
        for (int idx = 0; idx < nSegs; idx++)
        {
            var (slo, shi) = rawSegs[idx];
            var (tlo, thi) = PureUtf8.Trim(b, slo, shi);
            if (tlo >= thi)
                throw PureError.EmptyKey(line, spanS, spanE);
            switch (PureKey.CheckKey(b, tlo, thi))
            {
                case PureKey.KeyValidity.Empty:
                    throw PureError.EmptyKey(line, spanS, spanE);
                case PureKey.KeyValidity.Invalid:
                    throw PureError.InvalidKey(line, Text(b, kLo, kHi),
                        PureError.RawPath(Text(b, kLo, kHi)), spanS, spanE);
            }
            string decoded = PureKey.DecodeKeySegmentChecked(b, tlo, thi, line, spanS, spanE);
            bool isLeaf = idx + 1 == nSegs;
            if (!isLeaf)
            {
                var sub = t.Descend(decoded);
                if (sub == null)
                {
                    throw PureError.KeyPathConflictBlocked(line, Text(b, kLo, kHi),
                        PureError.DecodedPath(Text(b, kLo, kHi)), spanS, spanE);
                }
                t = sub;
            }
            else
            {
                var slot = PureObjBuilder.SlotOf(value);
                if (!t.InsertLeaf(decoded, slot))
                    throw PureError.DuplicateKey(line, Text(b, kLo, kHi),
                        PureError.DecodedPath(Text(b, kLo, kHi)), spanS, spanE);
                return;
            }
        }
    }

    /// <summary>Existing slot shape for the single-segment conflict path.</summary>
    private static PureObjBuilder.Slot ExistingSlot(PureObjBuilder table, string key)
    {
        // InsertLeaf already proved the key exists.
        foreach (var e in table.Entries)
            if (e.Key == key) return PureObjBuilder.SlotOf(e.Value);
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>Raw byte windows of the dotted segments (same split machine).</summary>
    private static void CollectSegments(byte[] b, int lo, int hi, List<(int s, int e)> raw)
    {
        var bounds = new List<(int s, int e)>();
        int start = lo, i = lo;
        bool segStart = true;
        bool hasQ = PureKey.HasQuoteBytes(b, lo, hi);
        while (i < hi)
        {
            if (hasQ && segStart)
            {
                i = PureKey.SkipSegmentWs(b, i, hi);
                if (i < hi && PureKey.IsQuoteByte(b[i]))
                {
                    int end = PureKey.QuotedSpanEnd(b, hi, i);
                    if (end < 0) break;
                    i = end + 1;
                    segStart = false;
                    continue;
                }
                segStart = false;
            }
            if (i >= hi) break;
            int by = b[i];
            if (by == (byte)'\\') { i += i + 1 < hi ? 2 : 1; }
            else if (by == (byte)'.')
            {
                bounds.Add((start, i));
                start = i + 1;
                i++;
                segStart = true;
            }
            else i++;
        }
        bounds.Add((start, hi));
        foreach (var t in bounds) raw.Add(t);
    }
}

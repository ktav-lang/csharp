// The line-oriented state machine (spec §5.0–§5.6, §6.1–§6.3, §6.10, §6.14, §6.16).
// Mirrors rust/src/document/parser/parser/{mod,parse_str,root,sep}.rs and
// build/{insert,frame}.rs + syntax/collecting.rs (multiline dedent).

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureParser
{
    /// <summary>Parse lax (spec parse) — validates UTF-8 first (§6.15).</summary>
    public static KtavValue ParseLax(byte[] utf8)
    {
        return ParseBytes(utf8, strict: false);
    }

    /// <summary>Parse strict (spec parse_strict) — LossyScalar on lossy spellings.</summary>
    public static KtavValue ParseStrict(byte[] utf8)
    {
        return ParseBytes(utf8, strict: true);
    }

    private static KtavValue ParseBytes(byte[] src, bool strict)
    {
        var fail = PureUtf8.Validate(src);
        if (fail != null)
            throw PureError.InvalidUtf8(src, fail.Value.ValidUpTo, fail.Value.ErrorLen);
        return ParseValidated(src, strict);
    }

    internal static KtavValue ParseValidated(byte[] src, bool strict)
    {
        // Canonical public error boundary: every internal PureError raised by
        // the line machine becomes its KtavException exactly once, here.
        // InvalidUtf8 is already a KtavException (built in ParseBytes before
        // this call) and is never re-wrapped. ToExceptionWithDerivedLine
        // derives the Line for EOF UnclosedCompound and otherwise behaves as
        // ToException — one conversion path for the whole parser.
        try
        {
            return ParseValidatedCore(src, strict);
        }
        catch (PureError pe)
        {
            throw pe.ToExceptionWithDerivedLine(src);
        }
    }

    private static KtavValue ParseValidatedCore(byte[] src, bool strict)
    {
        var p = new Machine(strict);
        int bom = PureUtf8.BomLen(src);
        int n = src.Length;
        int lineStart = bom;
        int lineNum = 0;
        while (lineStart <= n)
        {
            if (lineStart == n) break;
            int pos = lineStart;
            while (pos < n && src[pos] != (byte)'\n' && src[pos] != (byte)'\r') pos++;
            int nextStart;
            if (pos < n)
            {
                if (src[pos] == (byte)'\r')
                    nextStart = pos + 1 < n && src[pos + 1] == (byte)'\n' ? pos + 2 : pos + 1;
                else
                    nextStart = pos + 1;
            }
            else
            {
                nextStart = n + 1; // EOF without terminator
            }
            lineNum++;
            p.HandleLine(src, lineStart, pos, lineNum);
            lineStart = nextStart;
        }
        return p.Finish(src, n);
    }

    private sealed class Frame
    {
        public PureObjBuilder? Obj;
        public List<object?>? Items; // array under construction
        public string? PendingKey;
        public bool HasPending;
        public long KeySpanS, KeySpanE;
        public bool HasKeySpan;
    }

    private sealed class Machine
    {
        private readonly bool _strict;
        private readonly List<Frame> _stack = new();
        private readonly List<long> _openerOffsets = new();
        private long _multilineOpener = -1;
        private int _multilineMode; // 0 none, 1 stripped, 2 verbatim
        private readonly List<(int s, int e)> _collecting = new();
        private bool _rootInitialized;
        private bool _rootConsumed;
        private object? _rootInlineValue;
        private bool _rootIsExplicitCompound;

        internal Machine(bool strict) { _strict = strict; }

        internal void HandleLine(byte[] src, int ls, int le, int lineNum)
        {
            // Inside a multi-line string: raw content unless the terminator.
            if (_multilineMode != 0)
            {
                var (tlo, thi) = PureUtf8.Trim(src, ls, le);
                string trimmedStr = PureInline.Text(src, tlo, thi);
                bool term = _multilineMode == 1 ? trimmedStr == ")" : trimmedStr == "))";
                if (term)
                {
                    int mode = _multilineMode;
                    long opener = _multilineOpener;
                    _multilineMode = 0;
                    _multilineOpener = -1;
                    string finished = FinishMultiline(src, mode, _collecting);
                    _collecting.Clear();
                    AttachScalarValue(new KtavString(finished), lineNum, opener);
                    return;
                }
                _collecting.Add((ls, le));
                return;
            }

            var (lo, hi) = PureUtf8.Trim(src, ls, le);
            long tStart = lo, tEnd = hi; // trimmed span in original-byte coordinates
            if (lo >= hi) return;
            if (hi - lo >= 2 && src[lo] == (byte)'#' && src[lo + 1] == (byte)'#') return;

            // §5.0.1: orphan content after a consumed root.
            if (_rootConsumed)
                throw PureError.OrphanLineAfterTopLevelInline(lineNum, tStart, tEnd);

            // §5.0.1: first content line establishes the root kind.
            if (!_rootInitialized)
            {
                _rootInitialized = true;
                if (!(hi - lo == 1 && (src[lo] == (byte)'}' || src[lo] == (byte)']')))
                {
                    switch (ClassifyRoot(src, lo, hi, lineNum, tStart, tEnd))
                    {
                        case 0: // inline object
                        case 1: // inline array
                        {
                            _rootConsumed = true;
                            int open = src[lo];
                            int close = open == '{' ? '}' : ']';
                            var scanR = PureInline.ScanInlineCloserWithBounds(src, lo, hi, open, close,
                                lineNum, tStart, tEnd, out var bounds0, out PureError? bad0);
                            if (bad0 != null) throw bad0;
                            if (scanR < 0)
                                throw PureError.UnterminatedInlineCompound(lineNum, tStart, tEnd);
                            if (scanR != hi - 1)
                                throw PureError.MalformedInlineCompound(lineNum,
                                    "matching closer is not the last byte of the body; non-whitespace content follows the closed inline compound",
                                    tStart, tEnd);
                            var memo = new PureInline.InlineBounds(bounds0);
                            bool hq = PureKey.HasQuoteBytes(src, lo, hi);
                            _rootInlineValue = open == '{'
                                ? PureInline.ParseInlineObject(src, lo, hi, lineNum, tStart, tEnd, _strict, memo, hq)
                                : (object?)PureInline.ParseInlineArray(src, lo, hi, lineNum, tStart, tEnd, _strict, memo, hq);
                            return;
                        }
                        case 2: // explicit object
                            _rootIsExplicitCompound = true;
                            PushObj(tStart);
                            return;
                        case 3: // explicit array
                            _rootIsExplicitCompound = true;
                            PushArr(tStart);
                            return;
                        case 4: // implicit object root — the line ITSELF is the
                                // first pair (§5.0.1 rule 7): push the root and
                                // FALL THROUGH to the normal pair dispatch below
                                // (a `return` here would swallow the first line).
                            PushObj(0);
                            break;
                        case 5: // implicit array root — same fall-through (§5 rule 8).
                            PushArr(0);
                            break;
                    }
                }
            }

            if (hi - lo == 1 && src[lo] == (byte)'}')
            {
                CloseFrame(true, lineNum, tStart, tEnd);
                return;
            }
            if (hi - lo == 1 && src[lo] == (byte)']')
            {
                CloseFrame(false, lineNum, tStart, tEnd);
                return;
            }

            var top = _stack[_stack.Count - 1];
            if (top.Items != null)
                HandleArrayItem(src, lo, hi, lineNum, tStart, tEnd);
            else
                HandleObjectPair(src, lo, hi, lineNum, ls, tStart, tEnd);
        }

        private void PushObj(long opener)
        {
            _stack.Add(new Frame { Obj = new PureObjBuilder() });
            _openerOffsets.Add(opener);
        }

        private void PushArr(long opener)
        {
            _stack.Add(new Frame { Items = new List<object?>() });
            _openerOffsets.Add(opener);
        }

        internal KtavValue Finish(byte[] src, int eof)
        {
            if (_multilineMode != 0)
            {
                string kind = _multilineMode == 1 ? "stripped" : "verbatim";
                long start = _multilineOpener >= 0 ? _multilineOpener : eof;
                throw PureError.UnclosedCompound(kind, start, eof).ToExceptionWithDerivedLine(src);
            }
            if (_stack.Count > 1)
            {
                var last = _stack[_stack.Count - 1];
                string kind = last.Obj != null ? "object" : "array";
                long start = _openerOffsets[_openerOffsets.Count - 1];
                throw PureError.UnclosedCompound(kind, start, eof).ToExceptionWithDerivedLine(src);
            }
            if (_rootIsExplicitCompound && !_rootConsumed)
            {
                var last = _stack[_stack.Count - 1];
                string kind = last.Obj != null ? "object" : "array";
                long start = _openerOffsets[_openerOffsets.Count - 1];
                throw PureError.UnclosedCompound(kind, start, eof).ToExceptionWithDerivedLine(src);
            }
            if (_rootInlineValue != null)
                return PureObjBuilder.Materialise(_rootInlineValue);
            if (_stack.Count == 0)
                return new PureObjBuilder().ToValue(); // empty/comments-only doc
            var f = _stack[_stack.Count - 1];
            return f.Obj != null ? f.Obj.ToValue() : PureObjBuilder.Materialise(f.Items);
        }

        // ---------------- root classification (root.rs) ----------------

        /// <summary>0 inline object, 1 inline array, 2 explicit object, 3 explicit array, 4 object, 5 array.</summary>
        private int ClassifyRoot(byte[] src, int lo, int hi, int lineNum, long tS, long tE)
        {
            if (hi - lo == 1 && src[lo] == (byte)'{') return 2;
            if (hi - lo == 1 && src[lo] == (byte)'[') return 3;
            if (src[lo] == (byte)'{') { DiagnoseRootInline(src, lo, hi, '{', '}', lineNum, tS, tE); return 0; }
            if (src[lo] == (byte)'[') { DiagnoseRootInline(src, lo, hi, '[', ']', lineNum, tS, tE); return 1; }
            if (IsPairShape(src, lo, hi)) return 4;
            return 5;
        }

        /// <summary>
        /// §5.0.1 rules 2/3 + addendum: BadEscape → §6.13 (thrown by the scan),
        /// no closer → §6.11, closer followed by content → §6.12.
        /// </summary>
        private void DiagnoseRootInline(byte[] src, int lo, int hi, int open, int close,
            int lineNum, long tS, long tE)
        {
            var scan = PureInline.ScanInlineCloserWithBounds(src, lo, hi, open, close,
                lineNum, tS, tE, out _, out PureError? bad);
            if (bad != null) throw bad;
            if (scan < 0)
                throw PureError.UnterminatedInlineCompound(lineNum, tS, tE);
            if (scan != hi - 1)
                throw PureError.MalformedInlineCompound(lineNum,
                    "matching closer is not the last byte of the body; non-whitespace content follows the closed inline compound",
                    tS, tE);
        }

        /// <summary>classify.rs is_pair_shape — shared implementation in PureClassify.</summary>
        private static bool IsPairShape(byte[] src, int lo, int hi)
            => PureClassify.IsPairShape(src, lo, hi);

        // ---------------- separator (sep.rs) ----------------

        private static void RequireSepEnd(byte[] src, int restLo, int restHi, int lineNum,
            long markerCol, long bodyOff, long lineEnd)
        {
            if (restLo >= restHi || PureUtf8.WsLenAt(src, restLo, restHi) > 0) return;
            throw PureError.MissingSeparatorSpace(lineNum, bodyOff, lineEnd, (int)markerCol);
        }

        // ---------------- array items ----------------

        private void HandleArrayItem(byte[] src, int lo, int hi, int lineNum, long tS, long tE)
        {
            // §5.4: the only array-item prefix marker is `::`.
            if (hi - lo >= 2 && src[lo] == (byte)':' && src[lo + 1] == (byte)':')
            {
                RequireSepEnd(src, lo + 2, hi, lineNum, 1, tS + 2, tE);
                int vLo = PureUtf8.TrimStart(src, lo + 2, hi);
                PushItem(new KtavString(PureInline.Text(src, vLo, hi)));
                return;
            }

            switch (ClassifyValueStart(src, lo, hi, lineNum, tS, tE))
            {
                case VScalar: PushItem(new KtavString(VStr)); break;
                case VNull: PushItem(KtavNull.Instance); break;
                case VBool: PushItem(KtavBool.Of(VB == 1)); break;
                case VInt: PushItem(new KtavInteger(VStr)); break;
                case VFloat: PushItem(new KtavFloat(VStr)); break;
                case VEmptyObj: PushItem(new PureObjBuilder()); break;
                case VEmptyArr: PushItem(new List<object?>()); break;
                case VOpenObj: PushObj(tE - 1); break;
                case VOpenArr: PushArr(tE - 1); break;
                case VOpenMl1: BeginMultiline(1, tE - 1); break;
                case VOpenMl2: BeginMultiline(2, tE - 2); break;
                case VInline: PushItem(VInlineValue); break;
            }
        }

        private void PushItem(object? v) => _stack[_stack.Count - 1].Items!.Add(v);

        // ---------------- object pairs ----------------

        private void HandleObjectPair(byte[] src, int lo, int hi, int lineNum,
            int rawLineStart, long tS, long tE)
        {
            var colon = PureKey.ScanUnescapedColon(src, lo, hi, out int c);
            switch (colon)
            {
                case PureKey.ColonScanResult.UnterminatedQuote:
                    throw PureError.UnterminatedQuotedKey(lineNum, tS, tE);
                case PureKey.ColonScanResult.Absent:
                    throw PureError.MissingSeparator(lineNum, tS, tE);
            }

            // Key: [lo, colon) with trailing whitespace trimmed.
            int kLo = lo, kHi = c;
            while (kHi > kLo)
            {
                int back = BackWsLenByte(src, kLo, kHi);
                if (back == 0) break;
                kHi -= back;
            }
            long keyStart = tS;
            long keyEnd = keyStart + (kHi - kLo);
            if (kLo >= kHi)
                throw PureError.EmptyKey(lineNum, keyStart, keyStart + 1);

            int after = c + 1;
            long afterOff = tS + (after - lo);
            int markerCol = c - lo;

            bool isRaw = after < hi && src[after] == (byte)':';
            if (isRaw)
            {
                RequireSepEnd(src, after + 1, hi, lineNum, markerCol, afterOff + 1, tE);
                var (vlo, vhi) = PureUtf8.Trim(src, after + 1, hi);
                InsertObjectPair(src, kLo, kHi, new KtavString(PureInline.Text(src, vlo, vhi)),
                    lineNum, keyStart, keyEnd);
                return;
            }

            RequireSepEnd(src, after, hi, lineNum, markerCol, afterOff, tE);
            long keySpanS = keyStart, keySpanE = keyEnd;
            switch (ClassifyValueStart(src, after, hi, lineNum, tS, tE))
            {
                case VScalar: InsertObjectPair(src, kLo, kHi, new KtavString(VStr), lineNum, keySpanS, keySpanE); break;
                case VNull: InsertObjectPair(src, kLo, kHi, KtavNull.Instance, lineNum, keySpanS, keySpanE); break;
                case VBool: InsertObjectPair(src, kLo, kHi, KtavBool.Of(VB == 1), lineNum, keySpanS, keySpanE); break;
                case VInt: InsertObjectPair(src, kLo, kHi, new KtavInteger(VStr), lineNum, keySpanS, keySpanE); break;
                case VFloat: InsertObjectPair(src, kLo, kHi, new KtavFloat(VStr), lineNum, keySpanS, keySpanE); break;
                case VEmptyObj: InsertObjectPair(src, kLo, kHi, new PureObjBuilder(), lineNum, keySpanS, keySpanE); break;
                case VEmptyArr: InsertObjectPair(src, kLo, kHi, new List<object?>(), lineNum, keySpanS, keySpanE); break;
                case VOpenObj:
                    SetPendingKey(src, kLo, kHi, lineNum, keySpanS, keySpanE);
                    PushObj(tE - 1);
                    break;
                case VOpenArr:
                    SetPendingKey(src, kLo, kHi, lineNum, keySpanS, keySpanE);
                    PushArr(tE - 1);
                    break;
                case VOpenMl1:
                    SetPendingKey(src, kLo, kHi, lineNum, keySpanS, keySpanE);
                    BeginMultiline(1, tE - 1);
                    break;
                case VOpenMl2:
                    SetPendingKey(src, kLo, kHi, lineNum, keySpanS, keySpanE);
                    BeginMultiline(2, tE - 2);
                    break;
                case VInline:
                    InsertObjectPair(src, kLo, kHi, VInlineValue, lineNum, keySpanS, keySpanE);
                    break;
            }
        }

        private void SetPendingKey(byte[] src, int kLo, int kHi, int lineNum, long s, long e)
        {
            var f = _stack[_stack.Count - 1];
            f.PendingKey = PureInline.Text(src, kLo, kHi);
            f.HasPending = true;
            f.KeySpanS = s;
            f.KeySpanE = e;
            f.HasKeySpan = true;
        }

        private void InsertObjectPair(byte[] src, int kLo, int kHi, object? value,
            int lineNum, long keySpanS, long keySpanE)
        {
            var top = _stack[_stack.Count - 1];
            PureInline.InsertValue(top.Obj!, src, kLo, kHi, value, lineNum, keySpanS, keySpanE);
        }

        // ---------------- close frames (mod.rs close_frame) ----------------

        private void CloseFrame(bool objectBracket, int lineNum, long tS, long tE)
        {
            if (_stack.Count <= 1)
            {
                if (_stack.Count == 1 && _rootIsExplicitCompound)
                {
                    var top = _stack[_stack.Count - 1];
                    bool frameIsObj = top.Obj != null;
                    if (frameIsObj != objectBracket)
                    {
                        char found = objectBracket ? '}' : ']';
                        char opener = frameIsObj ? '{' : '[';
                        throw PureError.UnbalancedBracket(lineNum, tS, tE, found, opener);
                    }
                    _rootConsumed = true;
                    return;
                }
                char f2 = objectBracket ? '}' : ']';
                char o2 = objectBracket ? '{' : '[';
                throw PureError.UnbalancedBracket(lineNum, tS, tE, f2, o2);
            }
            var frame = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            _openerOffsets.RemoveAt(_openerOffsets.Count - 1);
            bool isObj = frame.Obj != null;
            if (isObj != objectBracket)
            {
                char found = objectBracket ? '}' : ']';
                char opener = isObj ? '{' : '[';
                throw PureError.UnbalancedBracket(lineNum, tS, tE, found, opener);
            }
            object? value = frame.Obj != null
                ? (object?)frame.Obj.ToValue()
                : (object?)PureObjBuilder.Materialise(frame.Items);
            AttachChildValue(value, lineNum, tS, tE);
        }

        private void AttachChildValue(object? value, int lineNum, long tS, long tE)
        {
            var top = _stack[_stack.Count - 1];
            if (top.Obj != null)
            {
                if (!top.HasPending)
                    throw PureError.Other(lineNum,
                        "Line " + lineNum + ": internal error \u2014 closed compound without pending key",
                        tS, tE);
                string key = top.PendingKey!;
                long ks = top.HasKeySpan ? top.KeySpanS : tS;
                long ke = top.HasKeySpan ? top.KeySpanE : tE;
                top.HasPending = false;
                top.PendingKey = null;
                top.HasKeySpan = false;
                InsertPendingKey(top.Obj!, key, value, lineNum, ks, ke);
            }
            else
            {
                top.Items!.Add(value);
            }
        }

        private void AttachScalarValue(KtavValue value, int lineNum, long lineStart)
        {
            var top = _stack[_stack.Count - 1];
            if (top.Obj != null)
            {
                if (!top.HasPending)
                    throw PureError.Other(lineNum,
                        "Line " + lineNum + ": internal error \u2014 multi-line string closed without pending key",
                        lineStart, lineStart);
                string key = top.PendingKey!;
                long ks = top.HasKeySpan ? top.KeySpanS : lineStart;
                long ke = top.HasKeySpan ? top.KeySpanE : lineStart;
                top.HasPending = false;
                top.PendingKey = null;
                top.HasKeySpan = false;
                InsertPendingKey(top.Obj!, key, value, lineNum, ks, ke);
            }
            else
            {
                top.Items!.Add(value);
            }
        }

        /// <summary>
        /// Pending keys are stored RAW (Rust keeps the raw &amp;str and runs
        /// insert_value at attach time), so re-encode and run the same §4
        /// validation / §3.7 decode / §6.3 tables.
        /// </summary>
        private static void InsertPendingKey(PureObjBuilder table, string rawKey,
            object? value, int lineNum, long spanS, long spanE)
        {
            var kb = Encoding.UTF8.GetBytes(rawKey);
            PureInline.InsertValue(table, kb, 0, kb.Length, value, lineNum, spanS, spanE);
        }

        // ---------------- multiline strings (collecting.rs) ----------------

        private void BeginMultiline(int mode, long opener)
        {
            _multilineMode = mode;
            _multilineOpener = opener;
            _collecting.Clear();
        }

        /// <summary>collecting.rs Collecting::finish (verbatim join / stripped dedent).</summary>
        private static string FinishMultiline(byte[] src, int mode, List<(int s, int e)> lines)
        {
            if (mode == 2)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(Encoding.UTF8.GetString(src, lines[i].s, lines[i].e - lines[i].s));
                }
                return sb.ToString();
            }
            // Stripped: single line → trim both edges.
            if (lines.Count == 1)
            {
                var (lo, hi) = PureUtf8.Trim(src, lines[0].s, lines[0].e);
                return lo >= hi ? "" : Encoding.UTF8.GetString(src, lo, hi - lo);
            }
            // Dedent: longest common leading-whitespace prefix over non-blank
            // lines, compared CODE POINT by code point (each member must be the
            // same code point across lines). A minimum byte-RUN length would
            // wrongly strip equal-length runs of different whitespace
            // characters (e.g. three ASCII spaces vs one U+3000), so the runs
            // are walked in parallel and cut at the first code-point mismatch.
            int common = -1; // common prefix length in code points
            int refLo = -1, refEnd = 0; // first non-blank line's leading-ws run
            foreach (var (ls, le) in lines)
            {
                var (tlo, thi) = PureUtf8.Trim(src, ls, le);
                if (tlo >= thi) continue; // blank lines don't constrain
                int runEnd = ls + PureUtf8.LeadingWsRun(src, ls, le);
                if (common < 0)
                {
                    refLo = ls; refEnd = runEnd;
                    common = CountCps(src, ls, runEnd);
                    continue;
                }
                int matched = 0, pRef = refLo, pCur = ls;
                while (matched < common && pRef < refEnd && pCur < runEnd
                    && CpAt(src, pRef) == CpAt(src, pCur))
                {
                    matched++;
                    pRef += PureUtf8.InlineWsLenAt(src, pRef, refEnd);
                    pCur += PureUtf8.InlineWsLenAt(src, pCur, runEnd);
                }
                common = matched;
                if (common == 0) break;
            }
            if (common < 0) common = 0; // all lines blank
            var sb2 = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb2.Append('\n');
                var (ls, le) = lines[i];
                var (tlo, thi) = PureUtf8.Trim(src, ls, le);
                if (tlo >= thi) continue; // blank line → empty
                // Advance `common` whitespace CODE POINTS (each <= 3 bytes).
                int cLo = ls;
                for (int k = 0; k < common; k++)
                    cLo += PureUtf8.InlineWsLenAt(src, cLo, le);
                int cHi = le;
                while (cHi > cLo)
                {
                    int back = PureParser.BackWsLenByte(src, cLo, cHi);
                    if (back == 0) break;
                    cHi -= back;
                }
                sb2.Append(Encoding.UTF8.GetString(src, cLo, cHi - cLo));
            }
            return sb2.ToString();
        }

        // ---------------- value classification (classify.rs) ----------------

        private const int VScalar = PureValueStart.Scalar, VNull = PureValueStart.Null,
            VBool = PureValueStart.Bool, VInt = PureValueStart.Int, VFloat = PureValueStart.Float,
            VEmptyObj = PureValueStart.EmptyObj, VEmptyArr = PureValueStart.EmptyArr,
            VOpenObj = PureValueStart.OpenObj, VOpenArr = PureValueStart.OpenArr,
            VOpenMl1 = PureValueStart.OpenMl1, VOpenMl2 = PureValueStart.OpenMl2,
            VInline = PureValueStart.Inline;

        private int VB; // 0=false, 1=true
        private string VStr = "";
        private object? VInlineValue;

        /// <summary>classify.rs classify_value_start over [lo,hi) — shared implementation in PureClassify.</summary>
        private int ClassifyValueStart(byte[] src, int lo, int hi, int lineNum, long tS, long tE)
        {
            var r = PureClassify.ClassifyValueStart(src, lo, hi, _strict, lineNum, tS, tE);
            VB = r.BoolFlag;
            VStr = r.Str;
            VInlineValue = r.InlineValue;
            return r.Kind;
        }
    }

    /// <summary>Code point count of [lo,hi) — used for whitespace-only
    /// ranges in the multiline dedent. Internal so PureFmtTree shares it
    /// (same sharing pattern as CpAt).</summary>
    internal static int CountCps(byte[] b, int lo, int hi)
    {
        int n = 0;
        while (lo < hi) { lo += PureUtf8.InlineWsLenAt(b, lo, hi); n++; }
        return n;
    }

    /// <summary>Code point starting at b[lo] (input is UTF-8 validated).
    /// Internal so PureFmtTree shares it in the common-prefix multiline
    /// dedent (identical algorithm in both FinishMultiline copies).</summary>
    internal static int CpAt(byte[] b, int lo)
    {
        int x = b[lo];
        if (x < 0x80) return x;
        if (x < 0xE0) return ((x & 0x1F) << 6) | (b[lo + 1] & 0x3F);
        if (x < 0xF0) return ((x & 0x0F) << 12) | ((b[lo + 1] & 0x3F) << 6) | (b[lo + 2] & 0x3F);
        return ((x & 0x07) << 18) | ((b[lo + 1] & 0x3F) << 12)
            | ((b[lo + 2] & 0x3F) << 6) | (b[lo + 3] & 0x3F);
    }

    /// <summary>Trailing full-class whitespace length ending at kHi.</summary>
    private static int BackWsLenByte(byte[] b, int kLo, int kHi)
    {
        for (int start = Math.Max(kLo, kHi - 3); start < kHi; start++)
        {
            int len = PureUtf8.WsLenAt(b, start, kHi);
            if (len > 0 && start + len == kHi) return len;
        }
        return 0;
    }
}

// Comment-preserving formatter (Format). Mirrors the Rust fmt_parser fork
// (document/parser/fmt_parser/{mod,doc,frame,positioned}.rs) plus the trivia
// writer (document/render/formatted.rs): a line-dispatch parser identical to
// PureParser's machine — same root detection, same pair/item grammar, same
// dotted-key insert tables — that additionally captures every comment line and
// blank line as trivia attached to the construct that follows it, then writes
// the §5.9 canonical structure back out with trivia interleaved.
//
// Trivia rules (all mirrored from the Rust fork):
//  - a comment owns a whole physical line (§3.4); its trimmed text is kept
//    verbatim and re-indented to the attachment depth on output;
//  - a run of 2+ blank lines collapses to one;
//  - blank padding immediately inside a bracket (and at EOF) is dropped;
//  - comments are never dropped;
//  - trivia preceding a dotted-key line attaches to the leaf segment (the same
//    documented simplification as the Rust formatter);
//  - the transform is a fixed point: Format(Format(x)) == Format(x);
//  - a comment-free, blank-free document formats to EmitCanonical(Loads(x));
//  - line endings are normalised to LF on output (the writers push '\n' only),
//    exactly like the Rust format_str; the BOM's meaning is preserved because
//    §5.9.10/§5.9.12 guards keep any byte-0 collision from being emitted.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureFormatter
{
    // ------------------------------------------------------------------
    // Entry point (fmt_parser/mod.rs parse_with_trivia + formatted.rs)
    // ------------------------------------------------------------------

    /// <summary>Format a validated UTF-8 source (byte offsets preserved for errors).</summary>
    public static string Format(byte[] src)
    {
        // Canonical public error boundary (same single conversion path as
        // PureParser.ParseValidated): internal PureError → KtavException with
        // the full envelope; InvalidUtf8 is already public upstream and is
        // never re-wrapped.
        try
        {
            return FormatCore(src);
        }
        catch (PureError pe)
        {
            throw pe.ToExceptionWithDerivedLine(src);
        }
    }

    private static string FormatCore(byte[] src)
    {
        var p = new PositionedParser();
        int bom = PureUtf8.BomLen(src);
        int n = src.Length;
        int lineStart = bom;
        int lineNum = 0;
        // Line splitting mirrors parse_with_trivia / PureParser.ParseValidated
        // (LF-only fast path unnecessary — the CR-aware loop is the same scan).
        while (lineStart <= n)
        {
            if (lineStart == n) break;
            int pos = lineStart;
            while (pos < n && src[pos] != (byte)'\n' && src[pos] != (byte)'\r') pos++;
            int nextStart;
            if (pos < n)
                nextStart = src[pos] == (byte)'\r'
                    ? (pos + 1 < n && src[pos + 1] == (byte)'\n' ? pos + 2 : pos + 1)
                    : pos + 1;
            else
                nextStart = n + 1;
            lineNum++;
            p.HandleLine(src, lineStart, pos, lineNum);
            lineStart = nextStart;
        }
        var doc = p.Finish(src, n);
        return EmitFormatted(doc);
    }

    // ------------------------------------------------------------------
    // The trivia parser (fmt_parser/positioned.rs)
    // ------------------------------------------------------------------

    private sealed class Frame
    {
        public PObject? Obj;
        public PArray? Arr;
        public string? PendingKey;
        public bool HasPending;
        public long KeySpanS, KeySpanE;
        public bool HasKeySpan;
        public List<PTrivia> PendingKeyTrivia = new();
        public List<PTrivia> PendingItemTrivia = new();
        public bool AtStart = true;

        public List<PTrivia> Trailing => Obj != null ? Obj.Trailing : Arr!.Trailing;
    }

    private sealed class PositionedParser
    {
        private readonly List<Frame> _stack = new();
        private readonly List<long> _openerOffsets = new();
        private long _multilineOpener = -1;
        private int _multilineMode; // 0 none, 1 stripped, 2 verbatim
        private readonly List<(int s, int e)> _collecting = new();
        private bool _rootInitialized;
        private bool _rootConsumed;
        private object? _rootInlineValue; // PObject or PArray (stamped inline root)
        private bool _rootIsExplicitCompound;
        private List<PTrivia> _pendingTrivia = new();
        private List<PTrivia> _docLeading = new();

        private bool CurrentAtStart =>
            _stack.Count > 0 ? _stack[_stack.Count - 1].AtStart : !_rootInitialized;

        private void PushBlankTrivia()
        {
            if (_pendingTrivia.Count == 0 && CurrentAtStart) return;
            if (_pendingTrivia.Count > 0 && _pendingTrivia[_pendingTrivia.Count - 1].Blank) return;
            _pendingTrivia.Add(PTrivia.BlankLine);
        }

        private void PushCommentTrivia(string trimmed)
            => _pendingTrivia.Add(new PTrivia(false, trimmed));

        internal FmtDoc Finish(byte[] src, int eof)
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

            var eofTrivia = _pendingTrivia;
            _pendingTrivia = new List<PTrivia>();
            PureFmtTree.TrimTrailingBlanks(eofTrivia);

            if (_rootInlineValue != null)
            {
                if (_rootInlineValue is PObject po) po.Trailing.AddRange(eofTrivia);
                else ((PArray)_rootInlineValue).Trailing.AddRange(eofTrivia);
                return new FmtDoc(_docLeading, _rootInlineValue);
            }
            if (_stack.Count == 0)
            {
                // Empty / comments-only document: empty Object root, trivia leading.
                _docLeading.AddRange(eofTrivia);
                return new FmtDoc(_docLeading, new PObject());
            }
            var f = _stack[_stack.Count - 1];
            if (f.Obj != null) f.Obj.Trailing.AddRange(eofTrivia);
            else f.Arr!.Trailing.AddRange(eofTrivia);
            object root = f.Obj != null ? (object)f.Obj : f.Arr!;
            return new FmtDoc(_docLeading, root);
        }

        internal void HandleLine(byte[] src, int ls, int le, int lineNum)
        {
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
                    AttachScalarValue(new KtavString(finished));
                    return;
                }
                _collecting.Add((ls, le));
                return;
            }

            var (lo, hi) = PureUtf8.Trim(src, ls, le);
            long tStart = lo, tEnd = hi;
            if (lo >= hi) { PushBlankTrivia(); return; }
            if (hi - lo >= 2 && src[lo] == (byte)'#' && src[lo + 1] == (byte)'#')
            {
                PushCommentTrivia(PureInline.Text(src, lo, hi));
                return;
            }

            if (_rootConsumed)
                throw PureError.OrphanLineAfterTopLevelInline(lineNum, tStart, tEnd);

            if (!_rootInitialized)
            {
                _rootInitialized = true;
                if (!(hi - lo == 1 && (src[lo] == (byte)'}' || src[lo] == (byte)']')))
                {
                    switch (ClassifyRoot(src, lo, hi))
                    {
                        case 0: // inline object root
                        case 1: // inline array root
                        {
                            _rootConsumed = true;
                            _docLeading = _pendingTrivia;
                            _pendingTrivia = new List<PTrivia>();
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
                            object parsed = open == '{'
                                ? PureInline.ParseInlineObject(src, lo, hi, lineNum, tStart, tEnd, false, memo, hq)
                                : (object?)PureInline.ParseInlineArray(src, lo, hi, lineNum, tStart, tEnd, false, memo, hq);
                            _rootInlineValue = Stamp(parsed);
                            return;
                        }
                        case 2: // explicit object
                            TakeDocLeading();
                            _rootIsExplicitCompound = true;
                            PushObj(tStart);
                            return;
                        case 3: // explicit array
                            TakeDocLeading();
                            _rootIsExplicitCompound = true;
                            PushArr(tStart);
                            return;
                        case 4: // implicit object root — the line ITSELF is the
                                // first pair (§5.0.1 rule 7): push the root and
                                // FALL THROUGH to the dispatch below, where the
                                // pending trivia is captured exactly once and
                                // attached to that first pair (a `return` here
                                // would swallow the first line and its trivia).
                            PushObj(0);
                            break;
                        case 5: // implicit array root — same fall-through (§5 rule 8);
                                // trivia attaches to the first item.
                            PushArr(0);
                            break;
                    }
                }
            }

            if (hi - lo == 1 && src[lo] == (byte)'}') { CloseFrame(true, lineNum, tStart, tEnd); return; }
            if (hi - lo == 1 && src[lo] == (byte)']') { CloseFrame(false, lineNum, tStart, tEnd); return; }

            var trivia = _pendingTrivia;
            _pendingTrivia = new List<PTrivia>();
            var top = _stack[_stack.Count - 1];
            top.AtStart = false;
            if (top.Arr != null)
                HandleArrayItem(src, lo, hi, lineNum, tStart, tEnd, trivia);
            else
                HandleObjectPair(src, lo, hi, lineNum, tStart, tEnd, trivia);
        }

        private void TakeDocLeading()
        {
            _docLeading = _pendingTrivia;
            _pendingTrivia = new List<PTrivia>();
        }

        /// <summary>0 inline object, 1 inline array, 2 explicit object, 3 explicit
        /// array, 4 object, 5 array (parser/mod.rs classify_root_kind_050).</summary>
        private static int ClassifyRoot(byte[] src, int lo, int hi)
        {
            if (hi - lo == 1 && src[lo] == (byte)'{') return 2;
            if (hi - lo == 1 && src[lo] == (byte)'[') return 3;
            if (src[lo] == (byte)'{') return 0;
            if (src[lo] == (byte)'[') return 1;
            if (PureClassify.IsPairShape(src, lo, hi)) return 4;
            return 5;
        }

        private void PushObj(long opener)
        {
            _stack.Add(new Frame { Obj = new PObject() });
            _openerOffsets.Add(opener);
        }

        private void PushArr(long opener)
        {
            _stack.Add(new Frame { Arr = new PArray() });
            _openerOffsets.Add(opener);
        }

        private void AttachScalarValue(KtavValue value)
        {
            var top = _stack[_stack.Count - 1];
            if (top.Obj != null)
            {
                if (!top.HasPending)
                    throw PureError.Other(0,
                        "internal error \u2014 multi-line string closed without pending key", 0, 0);
                string key = top.PendingKey!;
                long ks = top.HasKeySpan ? top.KeySpanS : 0;
                long ke = top.HasKeySpan ? top.KeySpanE : 0;
                var trivia = top.PendingKeyTrivia;
                top.PendingKeyTrivia = new List<PTrivia>();
                top.HasPending = false;
                top.PendingKey = null;
                top.HasKeySpan = false;
                InsertObjectPair(top.Obj, key, new PSlot(trivia, value), ks, ke);
            }
            else
            {
                var trivia = top.PendingItemTrivia;
                top.PendingItemTrivia = new List<PTrivia>();
                top.Arr!.Items.Add(new PSlot(trivia, value));
            }
        }

        private void HandleObjectPair(byte[] src, int lo, int hi, int lineNum,
            long tStart, long tEnd, List<PTrivia> trivia)
        {
            var colon = PureKey.ScanUnescapedColon(src, lo, hi, out int c);
            switch (colon)
            {
                case PureKey.ColonScanResult.UnterminatedQuote:
                    throw PureError.UnterminatedQuotedKey(lineNum, tStart, tEnd);
                case PureKey.ColonScanResult.Absent:
                    throw PureError.MissingSeparator(lineNum, tStart, tEnd);
            }

            int kLo = lo, kHi = c;
            while (kHi > kLo)
            {
                int back = BackWsLenByte(src, kLo, kHi);
                if (back == 0) break;
                kHi -= back;
            }
            long keyStart = tStart;
            long keyEnd = keyStart + (kHi - kLo);
            if (kLo >= kHi)
                throw PureError.EmptyKey(lineNum, keyStart, keyStart + 1);
            string key = PureInline.Text(src, kLo, kHi);

            int after = c + 1;
            long afterOff = tStart + (after - lo);
            int markerCol = c - lo;

            bool isRaw = after < hi && src[after] == (byte)':';
            if (isRaw)
            {
                RequireSepEnd(src, after + 1, hi, lineNum, markerCol, afterOff + 1, tEnd);
                var (vlo, vhi) = PureUtf8.Trim(src, after + 1, hi);
                InsertObjectPair(TopObj(), key,
                    new PSlot(trivia, new KtavString(PureInline.Text(src, vlo, vhi))),
                    keyStart, keyEnd);
                return;
            }

            RequireSepEnd(src, after, hi, lineNum, markerCol, afterOff, tEnd);
            var r = PureClassify.ClassifyValueStart(src, after, hi, strict: false,
                lineNum, tStart, tEnd);
            switch (r.Kind)
            {
                case PureValueStart.Scalar:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, new KtavString(r.Str)),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.Null:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, KtavNull.Instance),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.Bool:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, KtavBool.Of(r.BoolFlag == 1)),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.Int:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, new KtavInteger(r.Str)),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.Float:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, new KtavFloat(r.Str)),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.EmptyObj:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, new PObject()),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.EmptyArr:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, new PArray()),
                        keyStart, keyEnd);
                    break;
                case PureValueStart.OpenObj:
                case PureValueStart.OpenArr:
                    SetPendingKey(key, keyStart, keyEnd, trivia);
                    if (r.Kind == PureValueStart.OpenObj) PushObj(tEnd - 1);
                    else PushArr(tEnd - 1);
                    break;
                case PureValueStart.OpenMl1:
                case PureValueStart.OpenMl2:
                    SetPendingKey(key, keyStart, keyEnd, trivia);
                    BeginMultiline(r.Kind == PureValueStart.OpenMl1 ? 1 : 2,
                        r.Kind == PureValueStart.OpenMl1 ? tEnd - 1 : tEnd - 2);
                    break;
                case PureValueStart.Inline:
                    InsertObjectPair(TopObj(), key, new PSlot(trivia, Stamp(r.InlineValue)),
                        keyStart, keyEnd);
                    break;
            }
        }

        private void HandleArrayItem(byte[] src, int lo, int hi, int lineNum,
            long tStart, long tEnd, List<PTrivia> trivia)
        {
            if (hi - lo >= 2 && src[lo] == (byte)':' && src[lo + 1] == (byte)':')
            {
                RequireSepEnd(src, lo + 2, hi, lineNum, 1, tStart + 2, tEnd);
                int vLo = PureUtf8.TrimStart(src, lo + 2, hi);
                TopArr().Items.Add(new PSlot(trivia,
                    new KtavString(PureInline.Text(src, vLo, hi))));
                return;
            }

            var r = PureClassify.ClassifyValueStart(src, lo, hi, strict: false,
                lineNum, tStart, tEnd);
            switch (r.Kind)
            {
                case PureValueStart.Scalar:
                    TopArr().Items.Add(new PSlot(trivia, new KtavString(r.Str))); break;
                case PureValueStart.Null:
                    TopArr().Items.Add(new PSlot(trivia, KtavNull.Instance)); break;
                case PureValueStart.Bool:
                    TopArr().Items.Add(new PSlot(trivia, KtavBool.Of(r.BoolFlag == 1))); break;
                case PureValueStart.Int:
                    TopArr().Items.Add(new PSlot(trivia, new KtavInteger(r.Str))); break;
                case PureValueStart.Float:
                    TopArr().Items.Add(new PSlot(trivia, new KtavFloat(r.Str))); break;
                case PureValueStart.EmptyObj:
                    TopArr().Items.Add(new PSlot(trivia, new PObject())); break;
                case PureValueStart.EmptyArr:
                    TopArr().Items.Add(new PSlot(trivia, new PArray())); break;
                case PureValueStart.OpenObj:
                case PureValueStart.OpenArr:
                    SetPendingItem(trivia);
                    if (r.Kind == PureValueStart.OpenObj) PushObj(tEnd - 1);
                    else PushArr(tEnd - 1);
                    break;
                case PureValueStart.OpenMl1:
                case PureValueStart.OpenMl2:
                    SetPendingItem(trivia);
                    BeginMultiline(r.Kind == PureValueStart.OpenMl1 ? 1 : 2,
                        r.Kind == PureValueStart.OpenMl1 ? tEnd - 1 : tEnd - 2);
                    break;
                case PureValueStart.Inline:
                    TopArr().Items.Add(new PSlot(trivia, Stamp(r.InlineValue))); break;
            }
        }

        private PObject TopObj() => _stack[_stack.Count - 1].Obj
            ?? throw new InvalidOperationException("dispatched as object");
        private PArray TopArr() => _stack[_stack.Count - 1].Arr
            ?? throw new InvalidOperationException("dispatched as array");

        private void SetPendingKey(string key, long s, long e, List<PTrivia> trivia)
        {
            var f = _stack[_stack.Count - 1];
            f.PendingKey = key;
            f.HasPending = true;
            f.KeySpanS = s;
            f.KeySpanE = e;
            f.HasKeySpan = true;
            f.PendingKeyTrivia = trivia;
        }

        private void SetPendingItem(List<PTrivia> trivia)
        {
            var f = _stack[_stack.Count - 1];
            f.PendingItemTrivia = trivia;
        }

        private void InsertObjectPair(PObject table, string key, PSlot slot, long spanS, long spanE)
            => InsertValue(table, key, slot, spanS, spanE);

        /// <summary>insert.rs insert_value over the P tree — shared implementation
        /// in PureFmtTree (§4 validation, §3.7 decode, §6.3 tables).</summary>
        private static void InsertValue(PObject table, string rawKey, PSlot slot, long spanS, long spanE)
            => PureFmtTree.InsertValue(table, rawKey, slot, spanS, spanE);

        private void BeginMultiline(int mode, long opener)
        {
            _multilineMode = mode;
            _multilineOpener = opener;
            _collecting.Clear();
        }

        /// <summary>collecting.rs Collecting::finish — shared implementation in
        /// PureFmtTree.FinishMultiline.</summary>
        private static string FinishMultiline(byte[] src, int mode, List<(int s, int e)> lines)
            => PureFmtTree.FinishMultiline(src, mode, lines);

        private void CloseFrame(bool objectBracket, int lineNum, long tS, long tE)
        {
            var trailing = _pendingTrivia;
            _pendingTrivia = new List<PTrivia>();
            PureFmtTree.TrimTrailingBlanks(trailing);

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
                    top.Trailing.AddRange(trailing);
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
            frame.Trailing.AddRange(trailing);
            object value = frame.Obj != null ? (object)frame.Obj : frame.Arr!;
            AttachChildValue(value, lineNum, tS, tE);
        }

        private void AttachChildValue(object value, int lineNum, long tS, long tE)
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
                var trivia = top.PendingKeyTrivia;
                top.PendingKeyTrivia = new List<PTrivia>();
                top.HasPending = false;
                top.PendingKey = null;
                top.HasKeySpan = false;
                InsertObjectPair(top.Obj, key, new PSlot(trivia, value), ks, ke);
            }
            else
            {
                var trivia = top.PendingItemTrivia;
                top.PendingItemTrivia = new List<PTrivia>();
                top.Arr!.Items.Add(new PSlot(trivia, value));
            }
        }

        private static int BackWsLenByte(byte[] b, int kLo, int kHi)
        {
            for (int start = Math.Max(kLo, kHi - 3); start < kHi; start++)
            {
                int len = PureUtf8.WsLenAt(b, start, kHi);
                if (len > 0 && start + len == kHi) return len;
            }
            return 0;
        }

        private static void RequireSepEnd(byte[] src, int restLo, int restHi, int lineNum,
            long markerCol, long bodyOff, long lineEnd)
        {
            if (restLo >= restHi || PureUtf8.WsLenAt(src, restLo, restHi) > 0) return;
            throw PureError.MissingSeparatorSpace(lineNum, bodyOff, lineEnd, (int)markerCol);
        }

        /// <summary>doc.rs stamp — shared implementation in PureFmtTree.Stamp:
        /// wrap a plain inline value (builder/list/leaf) with empty trivia
        /// throughout. Sound because a comment can never occur inside an inline
        /// compound (§3.4).</summary>
        private static object Stamp(object? v) => PureFmtTree.Stamp(v);
    }

    internal sealed class FmtDoc
    {
        public readonly List<PTrivia> Leading;
        public readonly object Root; // PObject or PArray
        public FmtDoc(List<PTrivia> leading, object root) { Leading = leading; Root = root; }
    }
    // PTrivia / PSlot / PObject / PArray / FmtDoc construction live in
    // PureFmtTree (doc.rs part of the fork); FmtDoc is re-exported here for
    // readability of the entry point above.

    // ------------------------------------------------------------------
    // The formatted writer (render/formatted.rs) — explicit task stack
    // ------------------------------------------------------------------

    private static string EmitFormatted(FmtDoc doc)
    {
        var w = new FWalker();
        foreach (var t in doc.Leading) EmitTrivia(t, 0, w.Out);
        switch (doc.Root)
        {
            case PObject o:
                w.PushPairs(o, 0, isRoot: true);
                break;
            case PArray a when a.Items.Count == 0 && a.Trailing.Count == 0:
                w.Out.Append("[]\n");
                return w.Out.ToString();
            case PArray a:
                w.ArrayRoot(a);
                break;
            default:
                throw PureRender.WriterError("ScalarRoot", new List<string>());
        }
        w.Run();
        return w.Out.ToString();
    }

    private static void EmitTrivia(PTrivia t, int indent, StringBuilder outBuf)
    {
        if (t.Blank) outBuf.Append('\n');
        else
        {
            PureRender.PushIndent(outBuf, indent);
            outBuf.Append(t.Text).Append('\n');
        }
    }

    private sealed class FWalker
    {
        public readonly StringBuilder Out = new();
        private readonly List<Task> _tasks = new();
        private readonly List<string> _path = new();

        public void PushPairs(PObject obj, int indent, bool isRoot)
            => _tasks.Add(new Task(Task.KPairs, obj, null, 0, indent, isRoot, false));

        public void ArrayRoot(PArray arr)
        {
            bool firstIsCompound = arr.Items.Count > 0 && arr.Items[0].Value is PObject or PArray;
            bool needsWrap = firstIsCompound || (arr.Items.Count == 0 && arr.Trailing.Count > 0);
            if (needsWrap)
            {
                Out.Append("[\n");
                _tasks.Add(new Task(Task.KClose, 1, ']'));
                PushArrayItems(arr, 1);
            }
            else
            {
                // §5.9.6/§5.9.12: only index 0 of the unwrapped root array is
                // exposed to root-kind detection.
                _tasks.Add(new Task(Task.KItems, null, arr, 0, 0, false, true));
            }
        }

        private void PushArrayItems(PArray arr, int indent)
        {
            _tasks.Add(new Task(Task.KItems, null, arr, 0, indent, false, false));
        }

        public void Run()
        {
            while (_tasks.Count > 0)
            {
                var t = _tasks[_tasks.Count - 1];
                _tasks.RemoveAt(_tasks.Count - 1);
                switch (t.Code)
                {
                    case Task.KPathPush: _path.Add(t.Key!); break;
                    case Task.KPathPop: _path.RemoveAt(_path.Count - 1); break;
                    case Task.KClose:
                        PureRender.PushIndent(Out, t.Indent);
                        Out.Append(t.Closer).Append('\n');
                        break;
                    case Task.KPairs:
                    {
                        var obj = t.Obj!;
                        if (t.Index >= obj.Entries.Count)
                        {
                            foreach (var tr in obj.Trailing) EmitTrivia(tr, t.Indent, Out);
                            break;
                        }
                        var e = obj.Entries[t.Index];
                        bool rootFirstKey = t.IsRoot && t.Index == 0;
                        _tasks.Add(new Task(Task.KPairs, obj, null, t.Index + 1, t.Indent, t.IsRoot, false));
                        foreach (var tr in e.Value.Trivia) EmitTrivia(tr, t.Indent, Out);
                        EmitPair(e.Key, e.Value.Value, t.Indent, rootFirstKey);
                        break;
                    }
                    case Task.KItems:
                    {
                        var arr = t.Arr!;
                        if (t.Index >= arr.Items.Count)
                        {
                            foreach (var tr in arr.Trailing) EmitTrivia(tr, t.Indent, Out);
                            break;
                        }
                        var slot = arr.Items[t.Index];
                        bool rootFirstItem = t.RootFirst && t.Index == 0;
                        _tasks.Add(new Task(Task.KItems, null, arr, t.Index + 1, t.Indent, false, t.RootFirst));
                        foreach (var tr in slot.Trivia) EmitTrivia(tr, t.Indent, Out);
                        EmitArrayItem(slot.Value, t.Indent, rootFirstItem);
                        break;
                    }
                }
            }
        }

        private void EmitPair(string key, object value, int indent, bool rootFirstKey)
        {
            if (key.Length == 0)
                throw PureRender.WriterError("EmptyKeyName", _path);
            PureRender.PushIndent(Out, indent);
            PureRender.PushEscapedKeySegment(key, rootFirstKey, Out);
            switch (value)
            {
                case KtavNull: Out.Append(": null\n"); break;
                case KtavBool b: Out.Append(": ").Append(b.Value ? "true" : "false").Append('\n'); break;
                case KtavInteger i: Out.Append(": ").Append(i.Text).Append('\n'); break;
                case KtavFloat f:
                    Out.Append(": ").Append(PureRender.CanonicalFloatChecked(f.Text, _path)).Append('\n');
                    break;
                case KtavString s: EmitStringInPair(s.Value, indent); break;
                case PArray a:
                    if (a.Items.Count == 0 && a.Trailing.Count == 0) { Out.Append(": []\n"); break; }
                    Out.Append(": [\n");
                    _tasks.Add(new Task(Task.KPathPop, ""));
                    _tasks.Add(new Task(Task.KClose, indent, ']'));
                    _tasks.Add(new Task(Task.KItems, null, a, 0, indent + 1, false, false));
                    _tasks.Add(new Task(Task.KPathPush, key));
                    break;
                case PObject o:
                    if (o.Entries.Count == 0 && o.Trailing.Count == 0) { Out.Append(": {}\n"); break; }
                    Out.Append(": {\n");
                    _tasks.Add(new Task(Task.KPathPop, ""));
                    _tasks.Add(new Task(Task.KClose, indent, '}'));
                    _tasks.Add(new Task(Task.KPairs, o, null, 0, indent + 1, false, false));
                    _tasks.Add(new Task(Task.KPathPush, key));
                    break;
            }
        }

        private void EmitArrayItem(object value, int indent, bool rootFirstItem)
        {
            PureRender.PushIndent(Out, indent);
            switch (value)
            {
                case KtavNull: Out.Append("null\n"); break;
                case KtavBool b: Out.Append(b.Value ? "true" : "false").Append('\n'); break;
                case KtavInteger i: Out.Append(i.Text).Append('\n'); break;
                case KtavFloat f:
                    Out.Append(PureRender.CanonicalFloatChecked(f.Text, _path)).Append('\n');
                    break;
                case KtavString s: EmitStringAsItem(s.Value, indent, rootFirstItem); break;
                case PArray a:
                    if (a.Items.Count == 0 && a.Trailing.Count == 0) { Out.Append("[]\n"); break; }
                    Out.Append("[\n");
                    _tasks.Add(new Task(Task.KClose, indent, ']'));
                    _tasks.Add(new Task(Task.KItems, null, a, 0, indent + 1, false, false));
                    break;
                case PObject o:
                    if (o.Entries.Count == 0 && o.Trailing.Count == 0) { Out.Append("{}\n"); break; }
                    Out.Append("{\n");
                    _tasks.Add(new Task(Task.KClose, indent, '}'));
                    _tasks.Add(new Task(Task.KPairs, o, null, 0, indent + 1, false, false));
                    break;
            }
        }

        // canonical/strings.rs emitters, shared verbatim with the canonical writer.
        private void EmitStringInPair(string s, int indent)
        {
            if (s.Length == 0) { Out.Append(":\n"); return; }
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: false, _path);
                Out.Append(form == PureRender.MlVerbatim ? ": ((\n" : ": (\n");
                Out.Append(s).Append('\n');
                PureRender.PushIndent(Out, indent);
                Out.Append(form == PureRender.MlVerbatim ? "))\n" : ")\n");
                return;
            }
            Out.Append(PureRender.NeedsRawMarker(s) ? ":: " : ": ");
            Out.Append(s).Append('\n');
        }

        private void EmitStringAsItem(string s, int indent, bool isRootArrayFirst)
        {
            if (s.Length == 0) { Out.Append("::\n"); return; }
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: false, _path);
                Out.Append(form == PureRender.MlVerbatim ? "((\n" : "(\n");
                Out.Append(s).Append('\n');
                PureRender.PushIndent(Out, indent);
                Out.Append(form == PureRender.MlVerbatim ? "))\n" : ")\n");
                return;
            }
            if (PureRender.ItemNeedsRawMarker(s))
            {
                Out.Append(":: ");
            }
            else if (isRootArrayFirst)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                if (PureClassify.IsPairShape(bytes, 0, bytes.Length)
                    || (s.Length > 0 && s[0] == '\uFEFF'))
                {
                    Out.Append(":: ");
                }
            }
            Out.Append(s).Append('\n');
        }
    }

    private readonly struct Task
    {
        public const int KPathPush = 0, KPathPop = 1, KClose = 2, KPairs = 3, KItems = 4;

        public readonly int Code;
        public readonly int Index, Indent;
        public readonly bool IsRoot, RootFirst;
        public readonly char Closer;
        public readonly string? Key;
        public readonly PObject? Obj;
        public readonly PArray? Arr;

        public Task(int code, string key)
        { Code = code; Key = key; Index = 0; Indent = 0; IsRoot = false; RootFirst = false; Closer = '\0'; }

        public Task(int code, int indent, char closer)
        { Code = code; Indent = indent; Closer = closer; Index = 0; IsRoot = false; RootFirst = false; Key = null; }

        public Task(int code, PObject? obj, PArray? arr, int index, int indent, bool isRoot, bool rootFirst)
        { Code = code; Obj = obj; Arr = arr; Index = index; Indent = indent; IsRoot = isRoot; RootFirst = rootFirst; Closer = '\0'; Key = null; }
    }
}

// Shared value-start classification (classify.rs classify_value_start) and
// root pair-shape probe (classify.rs is_pair_shape) — extracted from
// PureParser.Machine so the trivia-preserving formatter (PureFormatter) can
// share the exact same classification as the value-tree parser, mirroring how
// the Rust fmt_parser reuses crate::parser::classify.

using System;
using System.Collections.Generic;

namespace Ktav.Pure;

/// <summary>Result slots of ClassifyValueStart (Machine's VStr/VB/VInlineValue fields).</summary>
internal sealed class PureValueStart
{
    public const int Scalar = 0, Null = 1, Bool = 2, Int = 3, Float = 4, EmptyObj = 5,
        EmptyArr = 6, OpenObj = 7, OpenArr = 8, OpenMl1 = 9, OpenMl2 = 10, Inline = 11;

    public int Kind;
    public string Str = "";
    public int BoolFlag;   // 0 = false, 1 = true (NOT the kind const Bool)
    public object? InlineValue; // PureObjBuilder or List<object?> (inline compound)
}

internal static class PureClassify
{
    /// <summary>classify.rs classify_value_start over [lo,hi).</summary>
    public static PureValueStart ClassifyValueStart(byte[] src, int lo, int hi, bool strict,
        int lineNum, long tS, long tE)
    {
        var r = new PureValueStart();
        int tlo = PureUtf8.TrimStart(src, lo, hi); // leading ws only
        int thi = hi;

        if (thi - tlo == 1 && src[tlo] == (byte)'{') { r.Kind = PureValueStart.OpenObj; return r; }
        if (thi - tlo == 1 && src[tlo] == (byte)'[') { r.Kind = PureValueStart.OpenArr; return r; }

        if (tlo < thi && src[tlo] == (byte)'{')
        {
            var (blo, bhi) = PureUtf8.Trim(src, tlo + 1, thi - 1);
            if (thi - tlo >= 2 && src[thi - 1] == (byte)'}' && blo >= bhi)
            { r.Kind = PureValueStart.EmptyObj; return r; }
            return DispatchInlineCompound(src, tlo, thi, '{', '}', lineNum, tS, tE, r, strict);
        }
        if (tlo < thi && src[tlo] == (byte)'[')
        {
            var (blo, bhi) = PureUtf8.Trim(src, tlo + 1, thi - 1);
            if (thi - tlo >= 2 && src[thi - 1] == (byte)']' && blo >= bhi)
            { r.Kind = PureValueStart.EmptyArr; return r; }
            return DispatchInlineCompound(src, tlo, thi, '[', ']', lineNum, tS, tE, r, strict);
        }

        // Multi-line openers — exact tokens only.
        if (thi - tlo == 1 && src[tlo] == (byte)'(') { r.Kind = PureValueStart.OpenMl1; return r; }
        if (thi - tlo == 2 && src[tlo] == (byte)'(' && src[tlo + 1] == (byte)'(')
        { r.Kind = PureValueStart.OpenMl2; return r; }
        if ((thi - tlo == 2 && src[tlo] == (byte)'(' && src[tlo + 1] == (byte)')') ||
            (thi - tlo == 4 && src[tlo] == (byte)'(' && src[tlo + 1] == (byte)'(' &&
             src[tlo + 2] == (byte)')' && src[tlo + 3] == (byte)')'))
        {
            r.Str = "";
            r.Kind = PureValueStart.Scalar;
            return r;
        }

        // Keywords.
        if (thi - tlo == 4 && PureInline.Eq(src, tlo, "null")) { r.Kind = PureValueStart.Null; return r; }
        if (thi - tlo == 4 && PureInline.Eq(src, tlo, "true")) { r.BoolFlag = 1; r.Kind = PureValueStart.Bool; return r; }
        if (thi - tlo == 5 && PureInline.Eq(src, tlo, "false")) { r.BoolFlag = 0; r.Kind = PureValueStart.Bool; return r; }

        // Redundant leading zero is never a number (§5.2 rules 13–14 exception).
        if (PureNum.HasRedundantLeadingZero(src, tlo, thi))
        {
            r.Str = PureInline.Text(src, tlo, thi);
            r.Kind = PureValueStart.Scalar;
            return r;
        }

        if (PureNum.FastPlainDecimalI64(src, tlo, thi))
        {
            r.Str = PureInline.Text(src, tlo, thi);
            r.Kind = PureValueStart.Int;
            return r;
        }
        if (PureNum.TryParseInteger(src, tlo, thi, out long val))
        {
            string canonical = PureNum.I64Text(val);
            if (strict && canonical != PureInline.Text(src, tlo, thi))
                throw PureError.LossyScalar(lineNum, PureInline.Text(src, tlo, thi), canonical, tS, tE);
            r.Str = canonical;
            r.Kind = PureValueStart.Int;
            return r;
        }

        if (PureNum.IsFloatLiteral(src, tlo, thi) &&
            PureNum.ParseFloatValue(src, tlo, thi, out double fv))
        {
            string ryu = PureNum.RyuFormat(fv);
            if (strict)
            {
                string rendered = PureNum.CanonicalFloat(ryu);
                string body = PureInline.Text(src, tlo, thi);
                if (rendered != body)
                    throw PureError.LossyScalar(lineNum, body, rendered, tS, tE);
            }
            r.Str = ryu;
            r.Kind = PureValueStart.Float;
            return r;
        }

        r.Str = PureInline.Text(src, tlo, thi);
        r.Kind = PureValueStart.Scalar;
        return r;
    }

    /// <summary>classify.rs dispatch_inline_compound.</summary>
    private static PureValueStart DispatchInlineCompound(byte[] src, int lo, int hi, int open, int close,
        int lineNum, long tS, long tE, PureValueStart r, bool strict)
    {
        var scan = PureInline.ScanInlineCloserWithBounds(src, lo, hi, open, close,
            lineNum, tS, tE, out var bounds, out PureError? bad);
        if (bad != null) throw bad;
        if (scan < 0)
            throw PureError.UnterminatedInlineCompound(lineNum, tS, tE);
        if (scan != hi - 1)
            throw PureError.MalformedInlineCompound(lineNum,
                "matching closer is not the last byte of the body; non-whitespace content follows the closed inline compound",
                tS, tE);
        var memo = new PureInline.InlineBounds(bounds);
        bool hasQuotes = PureKey.HasQuoteBytes(src, lo, hi);
        r.InlineValue = open == '{'
            ? PureInline.ParseInlineObject(src, lo, hi, lineNum, tS, tE, strict, memo, hasQuotes)
            : (object?)PureInline.ParseInlineArray(src, lo, hi, lineNum, tS, tE, strict, memo, hasQuotes);
        r.Kind = PureValueStart.Inline;
        return r;
    }

    /// <summary>classify.rs is_pair_shape (§5.0.1 rule 6 phase-1 probe).</summary>
    public static bool IsPairShape(byte[] src, int lo, int hi)
    {
        if (!PureKey.FindUnescapedColon(src, lo, hi, out int colon)) return false;
        var (klo, khi) = PureUtf8.Trim(src, lo, colon); // trailing-ws trim of the key part
        if (klo >= khi) return false;
        int after = colon + 1;
        if (after < hi && src[after] == (byte)':') return true; // `key::`
        return after >= hi || PureUtf8.WsLenAt(src, after, hi) > 0;
    }
}

// §3.6 numeric grammars + shortest-round-trip float text (ryu-equivalent) and
// the §5.9.8 canonical float region rule.
// Mirrors rust/src/document/parser/syntax/classify.rs and render/canonical/num.rs.

using System;
using System.Globalization;

namespace Ktav.Pure;

internal static class PureNum
{
    private const ulong I64MinMag = 1UL << 63; // |i64::MIN|

    /// <summary>Plain ASCII decimal fitting i64 (already canonical); mirrors fast_plain_decimal_i64.</summary>
    public static bool FastPlainDecimalI64(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return false;
        int first = b[lo];
        if (first == '0') return hi - lo == 1;
        if (first < '1' || first > '9') return false;
        unchecked
        {
            long acc = first - '0';
            for (int i = lo + 1; i < hi; i++)
            {
                int d = b[i] - '0';
                if ((uint)d > 9) return false;
                if (acc > (long.MaxValue - d) / 10) return false;
                acc = acc * 10 + d;
            }
        }
        return true;
    }

    /// <summary>
    /// §3.6 integer literal → i64; false when the grammar doesn't match or the
    /// value overflows i64 (classify.rs try_parse_integer).
    /// </summary>
    public static bool TryParseInteger(byte[] b, int lo, int hi, out long val)
    {
        val = 0;
        if (lo >= hi) return false;
        int i = lo;
        bool negative = false;
        if (b[i] == (byte)'-') { negative = true; i++; }
        else if (b[i] == (byte)'+') i++;
        if (i >= hi) return false;
        if (b[i] == (byte)'0' && i + 1 < hi)
        {
            switch (b[i + 1])
            {
                case (byte)'x': return ParsePrefixed(b, i + 2, hi, 16, negative, out val);
                case (byte)'o': return ParsePrefixed(b, i + 2, hi, 8, negative, out val);
                case (byte)'b': return ParsePrefixed(b, i + 2, hi, 2, negative, out val);
            }
        }
        return ParseDecimal(b, i, hi, negative, out val);
    }

    private static int DigitValue(int c, int radix)
    {
        int v;
        if (c >= '0' && c <= '9') v = c - '0';
        else if (c >= 'a' && c <= 'f') v = c - 'a' + 10;
        else if (c >= 'A' && c <= 'F') v = c - 'A' + 10;
        else return -1;
        return v < radix ? v : -1;
    }

    private static bool ParsePrefixed(byte[] b, int lo, int hi, int radix, bool negative, out long val)
    {
        val = 0;
        if (lo >= hi) return false;
        if (DigitValue(b[lo], radix) < 0) return false;
        // Guarded multiply-add: once the accumulator could exceed the i64
        // magnitude bound the parse returns false, mirroring i64::from_str_radix
        // Err → String classification (an arbitrarily long valid numeral is a
        // String, never an exception; a malformed suffix is also just a
        // non-number → String, exactly like the Rust classify.rs path).
        ulong acc = 0;
        bool prevUs = false;
        for (int i = lo; i < hi; i++)
        {
            int c = b[i];
            if (c == '_')
            {
                if (prevUs) return false;
                prevUs = true;
                continue;
            }
            prevUs = false;
            int d = DigitValue(c, radix);
            if (d < 0) return false;
            if (acc > (I64MinMag - (ulong)d) / (ulong)radix) return false;
            acc = acc * (ulong)radix + (ulong)d;
        }
        if (prevUs) return false;
        return FinishSigned(acc, negative, out val);
    }

    private static bool ParseDecimal(byte[] b, int lo, int hi, bool negative, out long val)
    {
        val = 0;
        if (lo >= hi) return false;
        if (b[lo] < '0' || b[lo] > '9') return false;
        ulong acc = 0;
        bool prevUs = false;
        int count = 0;
        for (int i = lo; i < hi; i++)
        {
            int c = b[i];
            if (c == '_')
            {
                if (prevUs || count == 0) return false;
                prevUs = true;
                continue;
            }
            if (c < '0' || c > '9') return false;
            prevUs = false;
            // Guarded multiply-add (see ParsePrefixed): overflow → String.
            if (acc > (I64MinMag - (ulong)(c - '0')) / 10) return false;
            acc = acc * 10 + (ulong)(c - '0');
            count++;
        }
        if (prevUs || count == 0) return false;
        return FinishSigned(acc, negative, out val);
    }

    private static bool FinishSigned(ulong acc, bool negative, out long val)
    {
        val = 0;
        if (negative)
        {
            if (acc > I64MinMag) return false;
            if (acc == 0) return true; // -0 → 0, val stays 0
            val = acc == I64MinMag ? long.MinValue : unchecked(-(long)acc);
            return true;
        }
        if (acc > (ulong)long.MaxValue) return false;
        val = (long)acc;
        return true;
    }

    /// <summary>Canonical decimal (itoa) text of an i64.</summary>
    public static string I64Text(long v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>§5.2: redundant leading zero (`01234`, `-045`, `00`, `0_7`) — never a number.</summary>
    public static bool HasRedundantLeadingZero(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return false;
        int start = lo;
        if (b[start] == (byte)'+' || b[start] == (byte)'-') start++;
        if (start >= hi || b[start] != (byte)'0') return false;
        if (start + 1 >= hi) return false;
        int nx = b[start + 1];
        return (nx >= '0' && nx <= '9') || nx == (byte)'_';
    }

    /// <summary>§3.6 float literal grammar (classify.rs is_float_literal).</summary>
    public static bool IsFloatLiteral(byte[] b, int lo, int hi)
    {
        if (lo >= hi) return false;
        int first = b[lo];
        if (!((uint)(first - '0') <= 9) && first != '+' && first != '-') return false;
        bool hasSep = false;
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'.' || b[i] == (byte)'e' || b[i] == (byte)'E') { hasSep = true; break; }
        if (!hasSep) return false;
        int i2 = lo;
        if (b[i2] == (byte)'+' || b[i2] == (byte)'-') i2++;
        if (!ScanDecPart(b, hi, ref i2)) return false;
        if (i2 < hi && b[i2] == (byte)'.')
        {
            i2++;
            if (!ScanDecPart(b, hi, ref i2)) return false;
            if (i2 < hi && (b[i2] == (byte)'e' || b[i2] == (byte)'E'))
                return ScanExponent(b, hi, ref i2) && i2 == hi;
            return i2 == hi;
        }
        if (i2 < hi && (b[i2] == (byte)'e' || b[i2] == (byte)'E'))
            return ScanExponent(b, hi, ref i2) && i2 == hi;
        return false;
    }

    private static bool ScanDecPart(byte[] b, int hi, ref int i)
    {
        if (i >= hi || b[i] < '0' || b[i] > '9') return false;
        i++;
        bool prevUs = false;
        while (i < hi)
        {
            if (b[i] == (byte)'_')
            {
                if (prevUs) return false;
                prevUs = true;
                i++;
                continue;
            }
            if (b[i] >= '0' && b[i] <= '9') { prevUs = false; i++; continue; }
            break;
        }
        return !prevUs;
    }

    private static bool ScanExponent(byte[] b, int hi, ref int i)
    {
        if (i >= hi || (b[i] != (byte)'e' && b[i] != (byte)'E')) return false;
        i++;
        if (i < hi && (b[i] == (byte)'+' || b[i] == (byte)'-')) i++;
        return ScanDecPart(b, hi, ref i);
    }

    /// <summary>Parse a validated float literal (underscores allowed); false on overflow/NaN.</summary>
    public static bool ParseFloatValue(byte[] b, int lo, int hi, out double val)
    {
        val = 0;
        bool hasUs = false;
        for (int i = lo; i < hi; i++)
            if (b[i] == (byte)'_') { hasUs = true; break; }
        string s;
        if (hasUs)
        {
            var buf = new char[hi - lo];
            int w = 0;
            for (int i = lo; i < hi; i++)
                if (b[i] != (byte)'_') buf[w++] = (char)b[i];
            s = new string(buf, 0, w);
        }
        else
        {
            s = System.Text.Encoding.UTF8.GetString(b, lo, hi - lo);
        }
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            return false;
        return !double.IsNaN(val) && !double.IsInfinity(val);
    }

    // ------------------------------------------------------------------
    // Shortest round-trip float text formatted with the ryu-pretty branch
    // rules (ryu/src/pretty/mod.rs format64): the shortest digit string comes
    // from .NET's correctly-rounded shortest search; ryu's rules decide
    // decimal vs scientific shape.
    // ------------------------------------------------------------------

    /// <summary>ryu-equivalent text of a finite double ("0.0", "1.1", "1e300", "-0.0", ...).</summary>
    public static string RyuFormat(double v)
    {
        long bits = BitConverter.DoubleToInt64Bits(v);
        bool neg = bits < 0;
        if ((bits & 0x7FFFFFFFFFFFFFFFL) == 0)
            return neg ? "-0.0" : "0.0";
        ShortestDigits(v, out string digits, out int kk); // 10^(kk-1) <= |v| < 10^kk
        var sb = new System.Text.StringBuilder(digits.Length + 8);
        if (neg) sb.Append('-');
        int len = digits.Length;
        // Branch structure mirrors ryu/src/pretty/mod.rs format64:52-115
        // (vendored ryu 1.0.23) exactly, with ryu's k == kk - len here:
        //   1. `0 <= k && kk <= 16` -> digits + '0'*(kk-len) + ".0" (always a
        //      ".0" suffix in this branch, even when kk == len; 1.0 -> "1.0",
        //      1e15 -> "1000000000000000.0", 2^53 -> "9007199254740992.0")
        //   2. `0 < kk && kk <= 16` -> point inside digits (kk < len here)
        //   3. `-5 < kk && kk <= 0` -> "0." + '0'*(-kk) + digits
        //   4. else, len == 1       -> d + 'e' + (kk-1)
        //   5. else                 -> d.ddd + 'e' + (kk-1)
        if (kk >= len && kk <= 16)
        {
            // 1234e7 -> 12340000000.0 ; 1.0 -> "1.0"
            sb.Append(digits);
            sb.Append('0', kk - len);
            sb.Append(".0");
        }
        else if (kk > 0 && kk <= 16)
        {
            // 1234e-2 -> 12.34
            sb.Append(digits, 0, kk).Append('.').Append(digits, kk, len - kk);
        }
        else if (kk > -5 && kk <= 0)
        {
            // 1234e-6 -> 0.001234
            sb.Append("0.");
            sb.Append('0', -kk);
            sb.Append(digits);
        }
        else if (len == 1)
        {
            // 1e30
            sb.Append(digits).Append('e').Append(kk - 1);
        }
        else
        {
            // 1234e30 -> 1.234e33
            sb.Append(digits, 0, 1).Append('.').Append(digits, 1, len - 1);
            sb.Append('e').Append(kk - 1);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Shortest decimal digits of |v| plus the decimal-point position kk
    /// (value == 0.digits * 10^kk). Uses the minimal-precision G-format search,
    /// which yields the same correctly-rounded shortest digit string as ryu.
    /// </summary>
    private static void ShortestDigits(double v, out string digits, out int kk)
    {
        double a = Math.Abs(v);
        for (int p = 1; p <= 17; p++)
        {
            string s = a.ToString("G" + p.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double back)
                && BitConverter.DoubleToInt64Bits(back) == BitConverter.DoubleToInt64Bits(a))
            {
                int e = s.IndexOfAny(ExpChars);
                string mant;
                int exp10 = 0;
                if (e >= 0)
                {
                    mant = s.Substring(0, e);
                    int sign = 1, j = e + 1;
                    if (j < s.Length && (s[j] == '+' || s[j] == '-'))
                    {
                        if (s[j] == '-') sign = -1;
                        j++;
                    }
                    while (j < s.Length) exp10 = exp10 * 10 + (s[j++] - '0');
                    exp10 *= sign;
                }
                else
                {
                    mant = s;
                }
                int dot = mant.IndexOf('.');
                string ds;
                int frac;
                if (dot >= 0)
                {
                    ds = mant.Substring(0, dot) + mant.Substring(dot + 1);
                    frac = mant.Length - dot - 1;
                }
                else
                {
                    ds = mant;
                    frac = 0;
                }
                // ryu mantissas carry no leading/trailing zeros; normalise.
                int nz = 0;
                while (nz < ds.Length - 1 && ds[nz] == '0') nz++;
                ds = ds.Substring(nz);
                int tz = ds.Length - 1;
                while (tz > 0 && ds[tz] == '0') tz--;
                ds = ds.Substring(0, tz + 1);
                digits = ds;
                kk = exp10 - frac + ds.Length;
                return;
            }
        }
        digits = "0";
        kk = 1;
    }

    private static readonly char[] ExpChars = { 'E', 'e' };

    /// <summary>
    /// §5.9.8 canonical float form from a stored ryu text (num.rs canonical_float):
    /// scientific with shortest mantissa when |v| >= 1e7 or 0 < |v| < 1e-2,
    /// otherwise the ryu form unchanged; zero stays as-is.
    /// </summary>
    public static string CanonicalFloat(string ryuText)
    {
        if (!double.TryParse(ryuText, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return ryuText;
        if (double.IsNaN(v) || double.IsInfinity(v)) return ryuText;
        if (v == 0.0) return ryuText;
        double abs = Math.Abs(v);
        if (abs >= 1e7 || abs < 1e-2)
        {
            ShortestDigits(v, out string digits, out int kk);
            // format!("{:e}") normalised: mantissa point after the first digit,
            // exponent kk-1; shortest digits never have trailing zeros.
            var sb = new System.Text.StringBuilder(digits.Length + 8);
            bool neg = bits(v) < 0;
            if (neg) sb.Append('-');
            sb.Append(digits, 0, 1);
            if (digits.Length > 1) sb.Append('.').Append(digits, 1, digits.Length - 1);
            sb.Append('e').Append(kk - 1);
            return sb.ToString();
        }
        return ryuText;
    }

    private static long bits(double d) => BitConverter.DoubleToInt64Bits(d);

    // ------------------------------------------------------------------
    // Golden vectors for the ryu-pretty formatter (consumed by the test
    // round; internal so tests can enumerate without reflection). Each
    // pair is (input double bits/name, expected RyuFormat text). Derived
    // by hand from ryu 1.0.23 src/pretty/mod.rs format64 branch rules.
    // ------------------------------------------------------------------
    internal static readonly (string Name, double Value, string Expected)[] RyuGoldenVectors =
    {
        ("zero",        0.0,                        "0.0"),
        ("neg-zero",    BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL)), "-0.0"),
        ("one",         1.0,                        "1.0"),
        ("half",        0.5,                        "0.5"),
        ("tenth",       0.1,                        "0.1"),
        ("e15",         1e15,                       "1000000000000000.0"), // k=15,kk=16 → branch 1
        ("two-pow-53",  9007199254740992.0,         "9007199254740992.0"), // k=0,kk=16,len=16 → branch 1
        ("e16",         1e16,                       "1e16"),               // kk=17 → branches 1–3 reject → branch 4
        ("e17",         100000000000000000.0,       "1e17"),
        ("e-5",         1e-5,                       "0.00001"),            // kk=-4 → branch 3
        ("e-4",         1e-4,                       "0.0001"),             // kk=-3 → branch 3
        ("e-6",         1e-6,                       "1e-6"),               // kk=-5 → branch 4 (exact lower bound of branch 3)
        ("1.2e-7",      1.2e-7,                     "1.2e-7"),             // kk=-6 → branch 5 (just below branch 3)
        ("1234.5678",   1234.5678,                  "1234.5678"),          // kk=4 < len=8 → branch 2
        ("e22",         1e22,                       "1e22"),               // kk=23,len=1 → branch 4
        ("1.5e22",      1.5e22,                     "1.5e22"),             // kk=23,len=2 → branch 5
        ("two-pow-53+1 rounds to 2^53", 9007199254740993.0, "9007199254740992.0"),
        ("big-exp",     1.234e300,                  "1.234e300"),
        ("epsilon",     double.Epsilon,             "5e-324"),             // subnormal shortest digits
        ("neg",         -2.5,                       "-2.5"),
    };
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ktav;

/// <summary>
/// Dynamic representation of a Ktav document. Mirrors the Rust crate's
/// <c>ktav::value::Value</c> enum — seven variants, one per primitive,
/// no lossy coercions.
/// </summary>
/// <remarks>
/// Pattern-match with C# 9+ <c>switch</c> expressions:
/// <code>
/// string label = value switch {
///     KtavNull          => "null",
///     KtavBool b        => b.Value ? "true" : "false",
///     KtavInteger i     => i.Text,
///     KtavFloat f       => f.Text,
///     KtavString s      => s.Value,
///     KtavArray a       => $"[{a.Items.Count}]",
///     KtavObject o      => $"{{{o.Entries.Count}}}",
///     _                 => throw new InvalidOperationException(),
/// };
/// </code>
/// </remarks>
public abstract record KtavValue
{
    private protected KtavValue() { }
}

/// <summary>The <c>null</c> keyword. Singleton; use <see cref="Instance"/>.</summary>
public sealed record KtavNull : KtavValue
{
    public static KtavNull Instance { get; } = new();
    private KtavNull() { }
    public override string ToString() => "null";
}

/// <summary>The <c>true</c> / <c>false</c> keywords.</summary>
public sealed record KtavBool(bool Value) : KtavValue
{
    public static KtavBool True { get; } = new(true);
    public static KtavBool False { get; } = new(false);
    public static KtavBool Of(bool v) => v ? True : False;
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>
/// Typed integer scalar (the <c>:i</c> form). Held as text so arbitrary
/// precision (digits beyond <see cref="long"/>) round-trips byte for byte.
/// </summary>
public sealed record KtavInteger(string Text) : KtavValue
{
    public BigInteger ToBigInteger() => BigInteger.Parse(Text, System.Globalization.CultureInfo.InvariantCulture);
    public long ToInt64() => long.Parse(Text, System.Globalization.CultureInfo.InvariantCulture);

    public static KtavInteger Of(long v) => new(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static KtavInteger Of(BigInteger v) => new(v.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public override string ToString() => Text;
}

/// <summary>
/// Typed float scalar (the <c>:f</c> form). Held as text (mantissa with a
/// decimal point, optional scientific exponent) so precision round-trips
/// exactly.
/// </summary>
public sealed record KtavFloat(string Text) : KtavValue
{
    public double ToDouble() => double.Parse(Text, System.Globalization.CultureInfo.InvariantCulture);

    public static KtavFloat Of(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new ArgumentException("Ktav floats must be finite: " + v, nameof(v));
        var s = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0)
            s += ".0";
        return new KtavFloat(s);
    }

    public override string ToString() => Text;
}

/// <summary>Untyped scalar / string leaf.</summary>
public sealed record KtavString(string Value) : KtavValue
{
    public override string ToString() => Value;
}

/// <summary>Multi-line <c>[ ... ]</c> array.</summary>
public sealed record KtavArray(IReadOnlyList<KtavValue> Items) : KtavValue
{
    /// <summary>The array contents — defensively copied at construction.</summary>
    public IReadOnlyList<KtavValue> Items { get; init; } =
        (Items ?? throw new ArgumentNullException(nameof(Items))).ToArray();

    public override string ToString() => $"[{Items.Count}]";
}

/// <summary>
/// Multi-line <c>{ ... }</c> object — also the top-level document shape.
/// Key insertion order is preserved across parse and render.
/// </summary>
public sealed record KtavObject(IReadOnlyList<KeyValuePair<string, KtavValue>> Entries) : KtavValue
{
    /// <summary>The entries — defensively copied at construction.</summary>
    public IReadOnlyList<KeyValuePair<string, KtavValue>> Entries { get; init; } =
        (Entries ?? throw new ArgumentNullException(nameof(Entries))).ToArray();

    /// <summary>
    /// Linear lookup over <see cref="Entries"/>. Suitable for typical
    /// configuration shapes; if you need O(1) access, materialise a
    /// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>
    /// from <see cref="Entries"/> yourself.
    /// </summary>
    public KtavValue? TryGet(string key)
    {
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].Key == key) return Entries[i].Value;
        return null;
    }

    public override string ToString() => $"{{{Entries.Count}}}";
}

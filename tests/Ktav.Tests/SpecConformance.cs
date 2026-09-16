using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Walks the Ktav spec conformance suite and replays every fixture
/// against the loaded native parser across four fixture categories:
/// <list type="bullet">
/// <item><c>valid</c> — parses, compared against a plain-JSON oracle.</item>
/// <item><c>invalid</c> — must fail to parse.</item>
/// <item><c>unrepresentable</c> — JSON-only Values a writer (Dumps and
/// EmitCanonical) must refuse; there is deliberately no .ktav source.</item>
/// <item><c>parseable-unrepresentable</c> — parses fine, but canonical
/// emit must refuse.</item>
/// </list>
/// A guard test hard-fails when the suite is missing or when an unknown
/// fixture category directory appears — an empty submodule or a newly
/// added category must not silently keep CI green.
/// </summary>
[TestFixture]
public class SpecConformance
{
    private static readonly string s_validDir = Path.Combine(TestPaths.Spec, "valid");
    private static readonly string s_invalidDir = Path.Combine(TestPaths.Spec, "invalid");
    private static readonly string s_unrepresentableDir = Path.Combine(TestPaths.Spec, "unrepresentable");
    private static readonly string s_parseableUnrepresentableDir = Path.Combine(TestPaths.Spec, "parseable-unrepresentable");

    private static List<string> ValidPaths()
    {
        if (!Directory.Exists(s_validDir))
            return new List<string>();
        return Directory.EnumerateFiles(s_validDir, "*.ktav",
                    SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".canonical.ktav", StringComparison.Ordinal))
                .OrderBy(p => p)
                .ToList();
    }

    private static List<string> InvalidPaths()
    {
        if (!Directory.Exists(s_invalidDir))
            return new List<string>();
        return Directory.EnumerateFiles(s_invalidDir, "*.ktav",
                    SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();
    }

    private static List<string> UnrepresentablePaths()
    {
        if (!Directory.Exists(s_unrepresentableDir))
            return new List<string>();
        return Directory.EnumerateFiles(s_unrepresentableDir, "*.json",
                    SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();
    }

    private static List<string> ParseableUnrepresentablePaths()
    {
        if (!Directory.Exists(s_parseableUnrepresentableDir))
            return new List<string>();
        return Directory.EnumerateFiles(s_parseableUnrepresentableDir, "*.ktav",
                    SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();
    }

    public static IEnumerable<TestCaseData> ValidCases()
    {
        foreach (var ktavPath in ValidPaths())
        {
            var name = Path.GetRelativePath(s_validDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"valid:{name}");
        }
    }

    public static IEnumerable<TestCaseData> InvalidCases()
    {
        foreach (var ktavPath in InvalidPaths())
        {
            var name = Path.GetRelativePath(s_invalidDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"invalid:{name}");
        }
    }

    public static IEnumerable<TestCaseData> UnrepresentableCases()
    {
        foreach (var jsonPath in UnrepresentablePaths())
        {
            var name = Path.GetRelativePath(s_unrepresentableDir, jsonPath).Replace('\\', '/');
            yield return new TestCaseData(jsonPath).SetName($"unrepresentable:{name}");
        }
    }

    public static IEnumerable<TestCaseData> ParseableUnrepresentableCases()
    {
        foreach (var ktavPath in ParseableUnrepresentablePaths())
        {
            var name = Path.GetRelativePath(s_parseableUnrepresentableDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"parseable-unrepresentable:{name}");
        }
    }

    [TestCaseSource(nameof(ValidCases))]
    public void Valid(string ktavPath)
    {
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        Assert.That(File.Exists(oraclePath), $"oracle JSON missing: {oraclePath}");

        var src = File.ReadAllText(ktavPath);
        var oracleBytes = File.ReadAllBytes(oraclePath);

        var got = Ktav.Loads(src);

        // Decode the oracle through the same wire reader so bare numbers
        // become KtavInteger / KtavFloat — matching what cabi emits via
        // its $i / $f wrappers.
        var want = WireJson.Decode(oracleBytes);

        Assert.That(ValueEquals(want, got),
            $"mismatch for {ktavPath}\nsrc:\n{src}\nwant: {want}\ngot:  {got}");
    }

    [TestCaseSource(nameof(InvalidCases))]
    public void Invalid(string ktavPath)
    {
        var raw = File.ReadAllBytes(ktavPath);

        // § 6.15 fixtures are deliberately invalid UTF-8 (e.g.
        // invalid_utf8/lone_continuation_byte — the spec's own note warns
        // runners the file IS the fixture). A C# string cannot carry
        // invalid UTF-8: lossy decoding would hide the defect before the
        // parser ever sees it, so such fixtures go through the binding's
        // byte-level native entry and must be rejected there.
        if (!TryDecodeStrictUtf8(raw, out var src))
        {
            Assert.That(NativeLoadsRejects(raw),
                $"expected invalid-UTF-8 rejection for {ktavPath}");
            return;
        }

        Assert.Throws<KtavException>(() => Ktav.Loads(src),
            $"expected parse error for {ktavPath}");
    }

    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (System.Text.DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Drives <c>ktav_loads</c> over raw bytes via the binding's own
    /// native pipeline (same resolver plumbing as <see cref="Ktav.Loads"/>.
    /// Returns true when the native call fails.
    /// </summary>
    private static bool NativeLoadsRejects(byte[] input)
    {
        NativeLoader.EnsureRegistered();
        IntPtr inputPtr = IntPtr.Zero;
        try
        {
            if (input.Length > 0)
            {
                inputPtr = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputPtr, input.Length);
            }
            int rc = NativeMethods.ktav_loads(inputPtr, (nuint)input.Length,
                out IntPtr outBuf, out nuint outLen,
                out IntPtr outErr, out nuint outErrLen);
            FreeNativeBuffer(outBuf, outLen);
            FreeNativeBuffer(outErr, outErrLen);
            return rc != 0;
        }
        finally
        {
            if (inputPtr != IntPtr.Zero) Marshal.FreeHGlobal(inputPtr);
        }
    }

    private static void FreeNativeBuffer(IntPtr ptr, nuint len)
    {
        if (ptr != IntPtr.Zero) NativeMethods.ktav_free(ptr, len);
    }

    [TestCaseSource(nameof(UnrepresentableCases))]
    public void Unrepresentable(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var value = FixtureValueToKtav(root.GetProperty("value"));
        var reason = root.GetProperty("unrepresentable_reason").GetString();

        Assert.Throws<KtavException>(() => Ktav.Dumps(value),
            $"Dumps should refuse unrepresentable value ({reason}): {jsonPath}");
        Assert.Throws<KtavException>(() => Ktav.EmitCanonical(value),
            $"EmitCanonical should refuse unrepresentable value ({reason}): {jsonPath}");
    }

    [TestCaseSource(nameof(ParseableUnrepresentableCases))]
    public void ParseableUnrepresentable(string ktavPath)
    {
        var oraclePath = Path.ChangeExtension(ktavPath, ".json");
        Assert.That(File.Exists(oraclePath), $"oracle JSON missing: {oraclePath}");

        var src = File.ReadAllText(ktavPath);
        var got = Ktav.Loads(src);

        using var doc = JsonDocument.Parse(File.ReadAllText(oraclePath));
        var root = doc.RootElement;
        var want = FixtureValueToKtav(root.GetProperty("value"));
        var reason = root.GetProperty("unrepresentable_reason").GetString();

        Assert.That(ValueEquals(want, got),
            $"mismatch for {ktavPath}\nsrc:\n{src}\nwant: {want}\ngot:  {got}");

        Assert.Throws<KtavException>(() => Ktav.EmitCanonical(got),
            $"EmitCanonical should refuse unrepresentable value ({reason}): {ktavPath}");
    }

    /// <summary>
    /// No fixture category may be silently skipped: the spec submodule
    /// must be checked out, every directory under the suite root must be
    /// a known category, all four categories must be present, and each
    /// must contain at least one fixture.
    /// </summary>
    [Test]
    public void SuiteCoversEveryFixtureCategory()
    {
        Assert.That(TestPaths.SpecPresent(), Is.True,
            $"spec submodule checkout missing/empty: {TestPaths.Spec} — the suite must not silently pass");

        var whitelist = new[] { "valid", "invalid", "unrepresentable", "parseable-unrepresentable" };

        var found = Directory.EnumerateDirectories(TestPaths.Spec)
            .Select(Path.GetFileName)!
            .ToList();
        foreach (var name in found)
            Assert.That(whitelist, Does.Contain(name),
                $"unknown fixture category directory: {name}");

        foreach (var name in whitelist)
            Assert.That(Directory.Exists(Path.Combine(TestPaths.Spec, name)), Is.True,
                $"missing fixture category directory: {name}");

        Assert.That(ValidPaths(), Is.Not.Empty, "no valid fixtures found");
        Assert.That(InvalidPaths(), Is.Not.Empty, "no invalid fixtures found");
        Assert.That(UnrepresentablePaths(), Is.Not.Empty, "no unrepresentable fixtures found");
        Assert.That(ParseableUnrepresentablePaths(), Is.Not.Empty,
            "no parseable-unrepresentable fixtures found");
    }

    /// <summary>
    /// Converts a plain-JSON fixture oracle value to a KtavValue.
    /// A JSON object whose only property is <c>$float</c> is the spec's
    /// encoding for a non-finite float (payload is a string like "NaN")
    /// — no JSON number can be non-finite, so this is unambiguous.
    /// Numbers keep their raw text, split into Integer/Float the same
    /// way the wire reader does.
    /// </summary>
    private static KtavValue FixtureValueToKtav(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (el.EnumerateObject().Count() == 1)
                {
                    var prop = el.EnumerateObject().First();
                    if (prop.Name == "$float")
                        return new KtavFloat(prop.Value.GetString()!);
                }
                return new KtavObject(el.EnumerateObject()
                    .Select(p => new KeyValuePair<string, KtavValue>(p.Name, FixtureValueToKtav(p.Value)))
                    .ToList());
            }
            case JsonValueKind.Array:
                return new KtavArray(el.EnumerateArray().Select(FixtureValueToKtav).ToList());
            case JsonValueKind.String:
                return new KtavString(el.GetString()!);
            case JsonValueKind.Number:
                // Use the raw JSON text; '.'/'e'/'E' implies a float.
                var text = el.GetRawText();
                if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
                    return new KtavFloat(text);
                return new KtavInteger(text);
            case JsonValueKind.True:
                return KtavBool.True;
            case JsonValueKind.False:
                return KtavBool.False;
            case JsonValueKind.Null:
                return KtavNull.Instance;
            default:
                throw new KtavException($"unsupported fixture JSON kind: {el.ValueKind}");
        }
    }

    /// <summary>
    /// Structural equality with one subtlety: floats compare by numeric
    /// value (the spec oracle JSON may use a canonical text form like
    /// <c>2.5e+8</c> where the source Ktav had <c>2.5E+8</c>). Integers
    /// compare by <see cref="BigInteger"/>.
    /// </summary>
    private static bool ValueEquals(KtavValue a, KtavValue b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is KtavFloat fa && b is KtavFloat fb)
        {
            var da = double.Parse(fa.Text, System.Globalization.CultureInfo.InvariantCulture);
            var db = double.Parse(fb.Text, System.Globalization.CultureInfo.InvariantCulture);
            return da.Equals(db);
        }
        if (a is KtavInteger ia && b is KtavInteger ib)
            return BigInteger.Parse(ia.Text) == BigInteger.Parse(ib.Text);
        if (a is KtavArray aa && b is KtavArray ab)
        {
            if (aa.Items.Count != ab.Items.Count) return false;
            for (int i = 0; i < aa.Items.Count; i++)
                if (!ValueEquals(aa.Items[i], ab.Items[i])) return false;
            return true;
        }
        if (a is KtavObject oa && b is KtavObject ob)
        {
            if (oa.Entries.Count != ob.Entries.Count) return false;
            for (int i = 0; i < oa.Entries.Count; i++)
            {
                if (oa.Entries[i].Key != ob.Entries[i].Key) return false;
                if (!ValueEquals(oa.Entries[i].Value, ob.Entries[i].Value)) return false;
            }
            return true;
        }
        return a.Equals(b);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Walks the Ktav spec conformance suite and replays every fixture
/// against the loaded native parser. Hard-fails when the suite is
/// missing — an empty submodule must not silently keep CI green.
/// </summary>
[TestFixture]
public class SpecConformance
{
    private static readonly string s_validDir = Path.Combine(TestPaths.Spec, "valid");
    private static readonly string s_invalidDir = Path.Combine(TestPaths.Spec, "invalid");

    public static IEnumerable<TestCaseData> ValidCases()
    {
        if (!Directory.Exists(s_validDir))
            yield break;
        foreach (var ktavPath in Directory.EnumerateFiles(s_validDir, "*.ktav",
                     SearchOption.AllDirectories).OrderBy(p => p))
        {
            var name = Path.GetRelativePath(s_validDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"valid:{name}");
        }
    }

    public static IEnumerable<TestCaseData> InvalidCases()
    {
        if (!Directory.Exists(s_invalidDir))
            yield break;
        foreach (var ktavPath in Directory.EnumerateFiles(s_invalidDir, "*.ktav",
                     SearchOption.AllDirectories).OrderBy(p => p))
        {
            var name = Path.GetRelativePath(s_invalidDir, ktavPath).Replace('\\', '/');
            yield return new TestCaseData(ktavPath).SetName($"invalid:{name}");
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
        var src = File.ReadAllText(ktavPath);
        Assert.Throws<KtavException>(() => Ktav.Loads(src),
            $"expected parse error for {ktavPath}");
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

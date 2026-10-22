using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Native &lt;-&gt; pure cross-backend round trips, BOTH directions, plus
/// independent canonical-oracle assertions against the fixture
/// .canonical.ktav companions. The oracle checks are what stop two
/// equally-wrong backends from agreeing their way to green: each backend
/// is compared to the checked-in spec bytes on its own.
/// Every test in this fixture exercises BOTH backends, so the whole
/// fixture carries the "NativeOnly" category and is excluded (never
/// silently failed) from the pure-backend CI job.
/// </summary>
[TestFixture]
[Category("NativeOnly")]
public class CrossBackendTests
{
    private static IEnumerable<string> ValidFixtures()
    {
        TestPaths.Corpus.ToString(); // fail closed if the corpus is missing/changed
        foreach (var f in TestPaths.Corpus.CategoriesByName["valid"])
            yield return Path.Combine(TestPaths.Spec, "valid", f.Path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static IEnumerable<TestCaseData> ValidCases()
    {
        foreach (var p in ValidFixtures())
        {
            var name = Path.GetRelativePath(TestPaths.Spec, p).Replace('\\', '/');
            yield return new TestCaseData(p).SetName($"xbackend:{name}");
        }
    }

    /// <summary>Exact-spelling tree equality: Integer/Float Text must be
    /// byte-identical across backends, not merely numerically equal
    /// (unlike <see cref="SpecConformance.ValueEquals"/>). Object key
    /// order and array order must match too.</summary>
    internal static bool ExactEquals(KtavValue a, KtavValue b)
    {
        switch (a, b)
        {
            case (KtavNull, KtavNull): return true;
            case (KtavBool x, KtavBool y): return x == y;
            case (KtavInteger x, KtavInteger y): return x.Text == y.Text;
            case (KtavFloat x, KtavFloat y): return x.Text == y.Text;
            case (KtavString x, KtavString y): return x.Value == y.Value;
            case (KtavArray x, KtavArray y):
                if (x.Items.Count != y.Items.Count) return false;
                for (int i = 0; i < x.Items.Count; i++)
                    if (!ExactEquals(x.Items[i], y.Items[i])) return false;
                return true;
            case (KtavObject x, KtavObject y):
                if (x.Entries.Count != y.Entries.Count) return false;
                for (int i = 0; i < x.Entries.Count; i++)
                    if (x.Entries[i].Key != y.Entries[i].Key ||
                        !ExactEquals(x.Entries[i].Value, y.Entries[i].Value)) return false;
                return true;
            default: return false;
        }
    }

    // -------- parse: native -> pure and pure -> native --------

    [TestCaseSource(nameof(ValidCases))]
    public void BothBackendsParseToIdenticalTrees(string ktavPath)
    {
        var src = File.ReadAllText(ktavPath);
        var native = Ktav.Loads(src, KtavBackend.Native);
        var pure = Ktav.Loads(src, KtavBackend.Pure);
        Assert.That(ExactEquals(native, pure),
            $"backends disagree on {ktavPath}\nnative: {native}\npure:   {pure}");
    }

    [TestCaseSource(nameof(ValidCases))]
    public void NativeValueRendersAndParsesThroughPure(string ktavPath)
    {
        var src = File.ReadAllText(ktavPath);
        var native = Ktav.Loads(src, KtavBackend.Native);

        // native value -> pure writer -> pure parser
        var viaPure = Ktav.Loads(Ktav.Dumps(native, KtavBackend.Pure), KtavBackend.Pure);
        Assert.That(ExactEquals(native, viaPure), $"native->pure dump/load diverged: {ktavPath}");

        // and back: pure value -> native writer -> native parser
        var viaNative = Ktav.Loads(Ktav.Dumps(viaPure, KtavBackend.Native), KtavBackend.Native);
        Assert.That(ExactEquals(viaPure, viaNative), $"pure->native dump/load diverged: {ktavPath}");
    }

    // -------- write: byte-equal output for the same value --------

    [TestCaseSource(nameof(ValidCases))]
    public void WritersProduceIdenticalBytesAcrossBackends(string ktavPath)
    {
        var src = File.ReadAllText(ktavPath);
        var value = Ktav.Loads(src, KtavBackend.Native);

        Assert.That(Ktav.Dumps(value, KtavBackend.Pure),
            Is.EqualTo(Ktav.Dumps(value, KtavBackend.Native)), $"Dumps: {ktavPath}");
        Assert.That(Ktav.DumpsForceStrings(value, KtavBackend.Pure),
            Is.EqualTo(Ktav.DumpsForceStrings(value, KtavBackend.Native)), $"DumpsForceStrings: {ktavPath}");
        Assert.That(Encoding.UTF8.GetBytes(Ktav.EmitCanonical(value, KtavBackend.Pure)),
            Is.EqualTo(Encoding.UTF8.GetBytes(Ktav.EmitCanonical(value, KtavBackend.Native))),
            $"EmitCanonical: {ktavPath}");
    }

    [TestCaseSource(nameof(ValidCases))]
    public void FormatProducesIdenticalBytesAcrossBackends(string ktavPath)
    {
        var src = File.ReadAllText(ktavPath);
        Assert.That(Ktav.Format(src, KtavBackend.Pure),
            Is.EqualTo(Ktav.Format(src, KtavBackend.Native)), $"Format: {ktavPath}");
    }

    // -------- independent canonical oracles (per backend) --------

    /// <summary>
    /// Pure-mode canonical emission must byte-match the checked-in
    /// .canonical.ktav companion WITHOUT any native involvement — this is
    /// the pure backend's own oracle check, independent of the native
    /// backend's identical check in SpecConformance.ValidCanonical.
    /// </summary>
    [TestCaseSource(nameof(ValidCases))]
    public void PureEmitCanonicalMatchesIndependentOracle(string ktavPath)
    {
        var canonicalPath = Path.ChangeExtension(ktavPath, ".canonical.ktav");
        Assert.That(File.Exists(canonicalPath), $"canonical companion missing: {canonicalPath}");

        var value = Ktav.Loads(File.ReadAllText(ktavPath), KtavBackend.Pure);
        var actual = Encoding.UTF8.GetBytes(Ktav.EmitCanonical(value, KtavBackend.Pure));
        var expected = File.ReadAllBytes(canonicalPath);
        Assert.That(actual, Is.EqualTo(expected),
            $"pure canonical mismatch for {ktavPath}\n" +
            $"want: {Encoding.UTF8.GetString(expected)}\ngot:  {Encoding.UTF8.GetString(actual)}");
    }

    // -------- synthetic value shapes not covered by the corpus --------

    [Test]
    public void SyntheticValuesWriteIdenticallyAcrossBackends()
    {
        foreach (var value in new KtavValue[]
        {
            new KtavObject(new[]
            {
                new KeyValuePair<string, KtavValue>("empty", new KtavObject(new List<KeyValuePair<string, KtavValue>>())),
                new KeyValuePair<string, KtavValue>("arr", new KtavArray(new KtavValue[]
                {
                    KtavNull.Instance, KtavBool.False, new KtavInteger("-9223372036854775808"),
                    new KtavFloat("-0.0"), new KtavFloat("1.5e-300"), new KtavString(""),
                })),
                new KeyValuePair<string, KtavValue>("a.b.c", new KtavString("dotted")),
                new KeyValuePair<string, KtavValue>("multi\nline", new KtavString(":: not raw")),
            }),
            new KtavArray(new KtavValue[] { new KtavObject(new[]
            {
                new KeyValuePair<string, KtavValue>("k", new KtavString("v")),
            }) }),
        })
        {
            Assert.That(Ktav.Dumps(value, KtavBackend.Pure), Is.EqualTo(Ktav.Dumps(value, KtavBackend.Native)));
            Assert.That(Ktav.DumpsForceStrings(value, KtavBackend.Pure),
                Is.EqualTo(Ktav.DumpsForceStrings(value, KtavBackend.Native)));
            Assert.That(Ktav.EmitCanonical(value, KtavBackend.Pure),
                Is.EqualTo(Ktav.EmitCanonical(value, KtavBackend.Native)));
        }
    }

    [Test]
    public void SyntheticSourcesFormatIdenticallyAcrossBackends()
    {
        foreach (var src in new[]
        {
            "## why\na:   {x: 1}\n",
            "\uFEFFkey: value\n",
            "a: [\n  ## c\n  1,\n  2,\n]\n",
            "top:\n    deep.nested.path: 1\n    deep.other: 2\n",
        })
        {
            Assert.That(Ktav.Format(src, KtavBackend.Pure),
                Is.EqualTo(Ktav.Format(src, KtavBackend.Native)), $"Format diverged for {src}");
        }
    }
}

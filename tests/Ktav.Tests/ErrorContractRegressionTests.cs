using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Ktav.Tests;

[TestFixture]
public class ErrorContractRegressionTests
{
    [TestCaseSource(nameof(ScalarWriters))]
    public void ScalarRootsHaveStructuredErrors(System.Action<KtavValue> writer)
    {
        var ex = Assert.Throws<KtavException>(() => writer(KtavInteger.Of(1)))!;
        Assert.That(ex.Error, Is.EqualTo("UnrepresentableAt"));
        Assert.That(ex.Reason, Is.EqualTo("ScalarRoot"));
        Assert.That(ex.SpecSection, Is.EqualTo("§5.9.0"));
        Assert.That(ex.Path, Is.Not.Null);
        Assert.That(ex.Path, Is.Empty);
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void NonFiniteFloatsHaveStructuredUnicodePath(string spelling)
    {
        KtavValue value = new KtavObject(new[]
        {
            Pair("outer", new KtavArray(new KtavValue[]
            {
                new KtavObject(new[] { Pair("café-😀", new KtavFloat(spelling)) }),
            })),
        });

        foreach (var writer in new System.Func<KtavValue, string>[]
        {
            Ktav.Dumps,
            Ktav.EmitCanonical,
        })
        {
            var ex = Assert.Throws<KtavException>(() => writer(value))!;
            Assert.That(ex.Error, Is.EqualTo("UnrepresentableAt"));
            Assert.That(ex.Reason, Is.EqualTo("NonFiniteFloat"));
            Assert.That(ex.SpecSection, Is.EqualTo("§5.9.0"));
            Assert.That(ex.Path, Is.EqualTo(new[] { "outer", "café-😀" }));
        }
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void ForceStringsStillCoercesNonFiniteFloat(string spelling)
    {
        var text = Ktav.DumpsForceStrings(new KtavObject(new[]
        {
            Pair("value", new KtavFloat(spelling)),
        }));
        var parsed = (KtavObject)Ktav.Loads(text);
        Assert.That(parsed.TryGet("value"), Is.EqualTo(new KtavString(spelling)));
    }

    [Test]
    public void EmptyKeyErrorPrecedesNonFiniteFloat()
    {
        var value = new KtavObject(new[]
        {
            Pair(string.Empty, new KtavFloat("NaN")),
        });
        var ex = Assert.Throws<KtavException>(() => Ktav.EmitCanonical(value))!;
        Assert.That(ex.Error, Is.EqualTo("UnrepresentableAt"));
        Assert.That(ex.Reason, Is.EqualTo("EmptyKeyName"));
        Assert.That(ex.SpecSection, Is.EqualTo("§5.9.0"));
        Assert.That(ex.Path, Is.EqualTo(new[] { string.Empty }));
    }

    [Test]
    public void SiblingWriterErrorsFollowInsertionOrder()
    {
        var emptyThenFloat = new KtavObject(new[]
        {
            Pair(string.Empty, new KtavString("value")),
            Pair("float", new KtavFloat("NaN")),
        });
        var floatThenEmpty = new KtavObject(new[]
        {
            Pair("float", new KtavFloat("NaN")),
            Pair(string.Empty, new KtavString("value")),
        });

        foreach (var writer in new System.Func<KtavValue, string>[]
        {
            Ktav.Dumps,
            Ktav.EmitCanonical,
        })
        {
            var emptyError = Assert.Throws<KtavException>(() => writer(emptyThenFloat))!;
            Assert.That(emptyError.Reason, Is.EqualTo("EmptyKeyName"));
            Assert.That(emptyError.Path, Is.EqualTo(new[] { string.Empty }));

            var floatError = Assert.Throws<KtavException>(() => writer(floatThenEmpty))!;
            Assert.That(floatError.Reason, Is.EqualTo("NonFiniteFloat"));
            Assert.That(floatError.Path, Is.EqualTo(new[] { "float" }));
        }
    }

    [TestCase(new byte[] { 0x61, 0x80 }, 1, 2)]
    [TestCase(new byte[] { 0xF0, 0x9F }, 0, 2)]
    public void InvalidUtf8HasStructuredByteSpan(byte[] source, long start, long end)
    {
        var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(source))!;
        Assert.That(ex.Error, Is.EqualTo("InvalidUtf8"));
        Assert.That(ex.Reason, Is.Null);
        Assert.That(ex.SpecSection, Is.EqualTo("§6.15"));
        Assert.That(ex.Span, Is.EqualTo(new KtavErrorSpan(start, end)));
    }

    [Test]
    public void LoadsUtf8PassesValidBytesToNativePipeline()
    {
        var value = (KtavObject)Ktav.LoadsUtf8(Encoding.UTF8.GetBytes("key: value\n"));
        Assert.That(value.TryGet("key"), Is.EqualTo(new KtavString("value")));
    }

    private static IEnumerable<System.Action<KtavValue>> ScalarWriters()
    {
        yield return value => Ktav.Dumps(value);
        yield return value => Ktav.DumpsForceStrings(value);
        yield return value => Ktav.EmitCanonical(value);
    }

    private static KeyValuePair<string, KtavValue> Pair(string key, KtavValue value) =>
        new(key, value);
}

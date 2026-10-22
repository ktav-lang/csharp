using System.Collections.Generic;
using NUnit.Framework;
using KtavApi = global::Ktav.Ktav;

namespace Ktav.Tests;

[TestFixture]
[Category("PureOnly")]
public class PureRuntimeRepairTests
{
    [Test]
    public void ClosedExplicitObjectRemainsExtendableUntilDocumentFinish()
    {
        var root = (KtavObject)KtavApi.Loads("a: {\nx: 1\n}\na.y: 2\n", KtavBackend.Pure);
        var obj = root.TryGet("a") as KtavObject;
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.Entries, Has.Count.EqualTo(2));
        Assert.That(obj.Entries[0].Key, Is.EqualTo("x"));
        Assert.That(obj.Entries[1].Key, Is.EqualTo("y"));
        Assert.That(((KtavInteger)obj.TryGet("x")!).Text, Is.EqualTo("1"));
        Assert.That(((KtavInteger)obj.TryGet("y")!).Text, Is.EqualTo("2"));
    }

    [Test]
    public void ClosedArrayPreservesNestedBuilderValuesUntilDocumentFinish()
    {
        const string source = "a: {\nb: [\n{\nx: 1\n}\n]\n}\na.y: 2\n";
        var root = (KtavObject)KtavApi.Loads(source, KtavBackend.Pure);
        var obj = (KtavObject)root.TryGet("a")!;
        var items = (KtavArray)obj.TryGet("b")!;
        Assert.That(items.Items, Has.Count.EqualTo(1));
        var nested = (KtavObject)items.Items[0];
        Assert.That(((KtavInteger)nested.TryGet("x")!).Text, Is.EqualTo("1"));
        Assert.That(((KtavInteger)obj.TryGet("y")!).Text, Is.EqualTo("2"));
    }

    [TestCase("[{}, 2]\n", "[\n    {}\n    2\n]\n")]
    [TestCase("[[1], [2]]\n", "[\n    [\n        1\n    ]\n    [\n        2\n    ]\n]\n")]
    public void CanonicalAndFormattedRootArrayClosersUseRootIndentation(string source, string expected)
    {
        Assert.That(KtavApi.CanonicalFromSource(source, KtavBackend.Pure), Is.EqualTo(expected));
        Assert.That(KtavApi.Format(source, KtavBackend.Pure), Is.EqualTo(expected));
    }

    [Test]
    public void NumericSyntaxStringsUseRawMarkersWithoutChangingPrefixCaseRules()
    {
        const string source = "zip: 01234\nver: 01.5\nupper: 0X1A\n";
        const string expected = "zip:: 01234\nver:: 01.5\nupper: 0X1A\n";
        Assert.That(KtavApi.CanonicalFromSource(source, KtavBackend.Pure), Is.EqualTo(expected));
    }

    [Test]
    public void CarriageReturnWriterRefusalUsesStringRepresentabilitySection()
    {
        var value = new KtavObject(new[]
        {
            new KeyValuePair<string, KtavValue>("x", new KtavString("a\rb")),
        });
        var error = Assert.Throws<KtavException>(() => KtavApi.EmitCanonical(value, KtavBackend.Pure))!;
        Assert.That(error.Error, Is.EqualTo("UnrepresentableAt"));
        Assert.That(error.Reason, Is.EqualTo("CRByte"));
        Assert.That(error.SpecSection, Is.EqualTo("§5.9.7"));
    }
}

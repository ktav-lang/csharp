using System.Collections.Generic;

using NUnit.Framework;

namespace Ktav.Tests;

[TestFixture]
public class BasicTests
{
    [Test]
    public void LoadsBasicDocument()
    {
        // In spec 0.5.0 bare numbers are typed automatically; no :i / :f markers.
        var src = """
                  service: web
                  port: 8080
                  ratio: 0.75
                  tls: true
                  tags: [
                      prod
                      eu-west-1
                  ]
                  db.host: primary
                  db.timeout: 30
                  """;

        var v = Ktav.Loads(src);
        var top = (KtavObject)v;

        Assert.That(top.TryGet("service"), Is.EqualTo(new KtavString("web")));
        Assert.That(top.TryGet("port"), Is.EqualTo(new KtavInteger("8080")));
        Assert.That(top.TryGet("tls"), Is.EqualTo(KtavBool.True));

        var ratio = top.TryGet("ratio");
        Assert.That(ratio, Is.InstanceOf<KtavFloat>());

        var tags = (KtavArray)top.TryGet("tags")!;
        Assert.That(tags.Items, Has.Count.EqualTo(2));
        Assert.That(tags.Items[0], Is.EqualTo(new KtavString("prod")));
        Assert.That(tags.Items[1], Is.EqualTo(new KtavString("eu-west-1")));

        var db = (KtavObject)top.TryGet("db")!;
        Assert.That(db.TryGet("host"), Is.EqualTo(new KtavString("primary")));
        Assert.That(db.TryGet("timeout"), Is.EqualTo(new KtavInteger("30")));
    }

    [Test]
    public void RoundTripSimpleDocument()
    {
        var entries = new List<KeyValuePair<string, KtavValue>>
        {
            new("name",    new KtavString("demo")),
            new("count",   KtavInteger.Of(42)),
            new("ratio",   KtavFloat.Of(0.5)),
            new("flag",    KtavBool.True),
            new("nothing", KtavNull.Instance),
            new("nested",  new KtavObject(new[]
            {
                new KeyValuePair<string, KtavValue>("inner", KtavInteger.Of(1)),
            })),
        };

        var text = Ktav.Dumps(new KtavObject(entries));
        Assert.That(text, Is.Not.Empty);

        var back = (KtavObject)Ktav.Loads(text);
        Assert.That(back.TryGet("name"),    Is.EqualTo(new KtavString("demo")));
        Assert.That(back.TryGet("count"),   Is.EqualTo(new KtavInteger("42")));
        Assert.That(back.TryGet("flag"),    Is.EqualTo(KtavBool.True));
        Assert.That(back.TryGet("nothing"), Is.EqualTo(KtavNull.Instance));
    }

    [Test]
    public void ArbitraryPrecisionIntegerRoundTrip()
    {
        const string huge = "99999999999999999999999999999";
        // In spec 0.5.0 integers that overflow the native integer range are
        // returned as KtavString (the native library treats them as opaque
        // string scalars rather than bigint).
        var v = Ktav.Loads("value: " + huge);
        var top = (KtavObject)v;
        var s = (KtavString)top.TryGet("value")!;

        Assert.That(s.Value, Is.EqualTo(huge));

        // Round-trip via Dumps: encode as KtavInteger and verify the text
        // is preserved literally in the rendered output.
        var entries = new[]
        {
            new KeyValuePair<string, KtavValue>("v", new KtavInteger(huge)),
        };
        var text = Ktav.Dumps(new KtavObject(entries));
        Assert.That(text, Does.Contain(huge),
            "render must carry the big integer literally");
    }

    [Test]
    public void ParseErrorThrows()
    {
        Assert.Throws<KtavException>(() => Ktav.Loads("a: ["));
    }

    [Test]
    public void DumpsAcceptsTopLevelArray()
    {
        // spec 0.5.0 § 5.0.1: a top-level Array renders as bare
        // item-per-line with no surrounding brackets.
        var arr = new KtavArray(new KtavValue[]
        {
            new KtavString("alpha"),
            new KtavString("beta"),
            KtavInteger.Of(42),
        });
        var text = Ktav.Dumps(arr);
        Assert.That(text, Is.Not.Empty);

        var back = Ktav.Loads(text);
        Assert.That(back, Is.InstanceOf<KtavArray>());
        var items = ((KtavArray)back).Items;
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items[0], Is.EqualTo(new KtavString("alpha")));
        Assert.That(items[1], Is.EqualTo(new KtavString("beta")));
        Assert.That(items[2], Is.EqualTo(new KtavInteger("42")));
    }

    [Test]
    public void DumpsRejectsScalarTopLevel()
    {
        // The top level must be a compound (Object or Array per
        // spec 0.5.0 § 5.0.1) — a bare scalar is rejected.
        Assert.Throws<KtavException>(() => Ktav.Dumps(new KtavString("x")));
        Assert.Throws<KtavException>(() => Ktav.Dumps(KtavBool.True));
        Assert.Throws<KtavException>(() => Ktav.Dumps(KtavInteger.Of(1)));
    }

    [Test]
    public void LoadsTopLevelArrayBareScalars()
    {
        var src = "alpha\nbeta\ngamma\n";
        var v = Ktav.Loads(src);
        Assert.That(v, Is.InstanceOf<KtavArray>());
        var items = ((KtavArray)v).Items;
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items[0], Is.EqualTo(new KtavString("alpha")));
        Assert.That(items[1], Is.EqualTo(new KtavString("beta")));
        Assert.That(items[2], Is.EqualTo(new KtavString("gamma")));
    }

    [Test]
    public void LoadsTopLevelArrayTypedItems()
    {
        // In spec 0.5.0 bare numbers are typed automatically; :: is the
        // raw-string marker (forces String regardless of content).
        var src = "1\n2\n3.14\n:: bare-text\n";
        var v = Ktav.Loads(src);
        Assert.That(v, Is.InstanceOf<KtavArray>());
        var items = ((KtavArray)v).Items;
        Assert.That(items, Has.Count.EqualTo(4));
        Assert.That(items[0], Is.EqualTo(new KtavInteger("1")));
        Assert.That(items[1], Is.EqualTo(new KtavInteger("2")));
        Assert.That(items[2], Is.InstanceOf<KtavFloat>());
        Assert.That(items[3], Is.EqualTo(new KtavString("bare-text")));
    }

    [Test]
    public void LoadsEmptyDocumentDefaultsToObject()
    {
        // Spec § 5.0.1: empty / comments-only docs default to Object.
        var v = Ktav.Loads("");
        Assert.That(v, Is.InstanceOf<KtavObject>());
        Assert.That(((KtavObject)v).Entries, Is.Empty);

        var v2 = Ktav.Loads("## just a comment\n## another\n");
        Assert.That(v2, Is.InstanceOf<KtavObject>());
        Assert.That(((KtavObject)v2).Entries, Is.Empty);
    }

    [Test]
    public void DumpsForceStringsCoercesAllScalars()
    {
        // Every scalar should round-trip back as KtavString.
        var entries = new List<KeyValuePair<string, KtavValue>>
        {
            new("name",    new KtavString("demo")),
            new("count",   KtavInteger.Of(42)),
            new("ratio",   KtavFloat.Of(0.5)),
            new("flag",    KtavBool.True),
            new("nothing", KtavNull.Instance),
            new("nested",  new KtavObject(new[]
            {
                new KeyValuePair<string, KtavValue>("inner", KtavInteger.Of(7)),
            })),
            new("list",    new KtavArray(new KtavValue[]
            {
                KtavBool.False,
                KtavFloat.Of(1.5),
            })),
        };

        var text = Ktav.DumpsForceStrings(new KtavObject(entries));
        Assert.That(text, Is.Not.Empty);

        var back = (KtavObject)Ktav.Loads(text);
        Assert.That(back.TryGet("name"),    Is.EqualTo(new KtavString("demo")));
        Assert.That(back.TryGet("count"),   Is.EqualTo(new KtavString("42")));
        Assert.That(back.TryGet("ratio"),   Is.InstanceOf<KtavString>());
        Assert.That(back.TryGet("flag"),    Is.EqualTo(new KtavString("true")));
        Assert.That(back.TryGet("nothing"), Is.EqualTo(new KtavString("null")));

        var nested = (KtavObject)back.TryGet("nested")!;
        Assert.That(nested.TryGet("inner"), Is.EqualTo(new KtavString("7")));

        var list = (KtavArray)back.TryGet("list")!;
        Assert.That(list.Items, Has.Count.EqualTo(2));
        Assert.That(list.Items[0], Is.EqualTo(new KtavString("false")));
        Assert.That(list.Items[1], Is.InstanceOf<KtavString>());
    }

    [Test]
    public void DumpsForceStringsAcceptsTopLevelArray()
    {
        var arr = new KtavArray(new KtavValue[]
        {
            KtavInteger.Of(1),
            KtavBool.True,
            KtavNull.Instance,
            new KtavString("plain"),
        });
        var text = Ktav.DumpsForceStrings(arr);
        var back = (KtavArray)Ktav.Loads(text);
        Assert.That(back.Items, Has.Count.EqualTo(4));
        foreach (var item in back.Items)
            Assert.That(item, Is.InstanceOf<KtavString>(),
                "every item must round-trip as KtavString");
        Assert.That(back.Items[0], Is.EqualTo(new KtavString("1")));
        Assert.That(back.Items[1], Is.EqualTo(new KtavString("true")));
        Assert.That(back.Items[2], Is.EqualTo(new KtavString("null")));
        Assert.That(back.Items[3], Is.EqualTo(new KtavString("plain")));
    }

    [Test]
    public void DumpsForceStringsRejectsScalarTopLevel()
    {
        Assert.Throws<KtavException>(() => Ktav.DumpsForceStrings(new KtavString("x")));
        Assert.Throws<KtavException>(() => Ktav.DumpsForceStrings(KtavBool.True));
    }

    [Test]
    public void DumpsForceStringsNullArgumentThrowsAne()
    {
        Assert.Throws<System.ArgumentNullException>(() => Ktav.DumpsForceStrings(null!));
    }

    [Test]
    public void NativeVersionReportsSomething()
    {
        var v = Ktav.NativeVersion();
        Assert.That(v, Is.Not.Empty);
    }

    [Test]
    public void LoadsNullArgumentThrowsAne()
    {
        Assert.Throws<System.ArgumentNullException>(() => Ktav.Loads(null!));
    }

    [Test]
    public void DumpsNullArgumentThrowsAne()
    {
        Assert.Throws<System.ArgumentNullException>(() => Ktav.Dumps(null!));
    }
}

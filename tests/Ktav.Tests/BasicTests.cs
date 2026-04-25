using System.Collections.Generic;
using System.Numerics;

using NUnit.Framework;

namespace Ktav.Tests;

[TestFixture]
public class BasicTests
{
    [Test]
    public void LoadsBasicDocument()
    {
        var src = """
                  service: web
                  port:i 8080
                  ratio:f 0.75
                  tls: true
                  tags: [
                      prod
                      eu-west-1
                  ]
                  db.host: primary
                  db.timeout:i 30
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
        var v = Ktav.Loads("value:i " + huge);
        var top = (KtavObject)v;
        var i = (KtavInteger)top.TryGet("value")!;

        Assert.That(i.Text, Is.EqualTo(huge));
        Assert.That(i.ToBigInteger(), Is.EqualTo(BigInteger.Parse(huge)));

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
    public void DumpsRejectsNonObjectTopLevel()
    {
        var arr = new KtavArray(new KtavValue[] { new KtavString("x") });
        Assert.Throws<KtavException>(() => Ktav.Dumps(arr));
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

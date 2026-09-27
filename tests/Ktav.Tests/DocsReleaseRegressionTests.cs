using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using NUnit.Framework;

namespace Ktav.Tests;

public class DocsReleaseRegressionTests
{
    [TestCase("en")]
    [TestCase("ru")]
    [TestCase("zh")]
    public void KeyEscapingDocumentationExamplesParseToTheirDocumentedKeys(string language)
    {
        var path = Path.Combine(RepoRoot(), "root-docs", "README", "key-escaping", "body-1.md");
        var document = File.ReadAllText(path);
        var marker = ">>>>> lang=" + language + "\n";
        var start = document.IndexOf(marker);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"missing {language} section");
        start += marker.Length;

        var end = document.IndexOf("\n>>>>> lang=", start);
        var section = document.Substring(start, end < 0 ? document.Length - start : end - start);
        var example = Regex.Match(section, "```text\\r?\\n([\\s\\S]*?)\\r?\\n```");
        Assert.That(example.Success, Is.True, $"missing Ktav example in {language}");

        var root = (KtavObject)global::Ktav.Ktav.Loads(example.Groups[1].Value);
        Assert.That(root.TryGet("a.b"), Is.EqualTo(new KtavString("v")));
        Assert.That(root.TryGet("a:b"), Is.EqualTo(new KtavString("v")));

        var nested = (KtavObject)root.TryGet("x")!;
        Assert.That(nested.TryGet("y.z"), Is.EqualTo(new KtavString("v")));
    }

    [Test]
    public void NumericDocumentationMatchesCoreParsingAndNormalization()
    {
        var root = (KtavObject)global::Ktav.Ktav.Loads(
            "fraction: 1.10\noutsideInt64: 9223372036854775808\n");

        Assert.That(root.TryGet("fraction"), Is.EqualTo(new KtavFloat("1.1")));
        Assert.That(root.TryGet("outsideInt64"),
            Is.EqualTo(new KtavString("9223372036854775808")));
    }

    [TestCase("en")]
    [TestCase("ru")]
    [TestCase("zh")]
    public void QuickStartMatchesCompiledConsumerCode(string language)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "root-docs", "README",
            "quick-start-parse", "body-1.md"));
        var marker = ">>>>> lang=" + language + "\n";
        var start = source.IndexOf(marker);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        start += marker.Length;
        var end = source.IndexOf("\n>>>>> lang=", start);
        var section = source.Substring(start, end < 0 ? source.Length - start : end - start);
        var snippet = Regex.Match(section, "```csharp\\r?\\n([\\s\\S]*?)\\r?\\n```");
        Assert.That(snippet.Success, Is.True);

        var fixture = File.ReadAllText(Path.Combine(RepoRoot(), "tests", "Ktav.Tests",
            "ConsumerQuickStartFixture.cs"));
        var imports = ExtractFixturePart(fixture, "imports").TrimEnd();
        var body = Regex.Replace(ExtractFixturePart(fixture, "body"), "(?m)^ {8}", "").TrimEnd();
        Assert.That(snippet.Groups[1].Value.TrimEnd(), Is.EqualTo(imports + "\n\n" + body));

        var result = global::ConsumerQuickStartFixture.Run();
        Assert.That(result.Service, Is.EqualTo("web"));
        Assert.That(result.Port, Is.EqualTo(8080));
        Assert.That(result.Ratio, Is.EqualTo(0.75));
        Assert.That(result.Tls, Is.True);
        Assert.That(result.Host, Is.EqualTo("primary.internal"));
        Assert.That(result.Timeout, Is.EqualTo(30));
    }

    [Test]
    public void BasicExampleInputHasDocumentedNumericTypes()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "examples", "Basic", "Program.cs"));
        var literal = Regex.Match(source,
            "const string Src = \"\"\"\\r?\\n([\\s\\S]*?)\\r?\\n([ \\t]*)\"\"\";");
        Assert.That(literal.Success, Is.True);
        var input = Regex.Replace(literal.Groups[1].Value,
            "(?m)^" + Regex.Escape(literal.Groups[2].Value), "");
        var value = (KtavObject)global::Ktav.Ktav.Loads(input);
        Assert.That(value.TryGet("port"), Is.EqualTo(KtavInteger.Of(8080)));
        Assert.That(value.TryGet("ratio"), Is.EqualTo(KtavFloat.Of(0.75)));
        var db = (KtavObject)value.TryGet("db")!;
        Assert.That(db.TryGet("timeout"), Is.EqualTo(KtavInteger.Of(30)));
    }

    private static string ExtractFixturePart(string fixture, string part)
    {
        var match = Regex.Match(fixture,
            "(?m)^[ \\t]*// README " + part + " begin\\r?\\n([\\s\\S]*?)^[ \\t]*// README " + part + " end");
        Assert.That(match.Success, Is.True);
        return match.Groups[1].Value;
    }

    private static string RepoRoot([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
}

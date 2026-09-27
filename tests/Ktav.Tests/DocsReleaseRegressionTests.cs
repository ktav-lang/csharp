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

    private static string RepoRoot([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
}

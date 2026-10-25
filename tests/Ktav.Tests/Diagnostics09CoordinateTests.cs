using System;
using System.IO;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Hard-coded diagnostics-0.9 coordinate regressions, hand-verified against
/// the REAL spec/versions/0.9/tests fixtures in this worktree (not derived
/// from the implementation):
/// <list type="bullet">
/// <item><c>eof_object_lf_plain</c>: source "## שלום\n{\n  key: value\n" —
/// the `{` opener is byte 12 (11 bytes of the Hebrew comment line + LF), the
/// document ends at byte 27; UnclosedCompound spans opener..EOF, line 2.</item>
/// <item><c>eof_nested_crlf_bom</c>: BOM(3) + "## שלום"(11) + CRLF(2) +
/// "outer: {"(8) + CRLF(2) + "inner: ["(8) + CRLF(2) + "  x"(3) + CRLF(2):
/// the innermost unclosed opener `[` is byte 33, EOF is 41 — innermost
/// opener..EOF, line 3, CRLF counted as ONE terminator.</item>
/// <item><c>eof_verbatim_cr_plain</c>: lone-CR terminators; the multiline
/// opener `(` of "s: ((" is byte 15, EOF 26 — lone CR is one EOL.</item>
/// <item><c>eof_stripped_lf_bom</c>: BOM(3) + comment(11) + LF + "s: ("(4):
/// opener `(` at 18 (tE−1 for stripped), EOF 28 with trailing LF.</item>
/// <item><c>utf8_continuation_lf_plain</c> / <c>utf8_priority_crlf_bom</c>:
/// InvalidUtf8 at line 3 starting at the first invalid byte 31; the span is
/// nonempty (end >= minimum_end 32) and line_text stays null. The priority
/// fixture additionally proves the UTF-8 check precedes grammar errors even
/// with BOM + CRLF byte geometry.</item>
/// </list>
/// These run against the pure backend unconditionally (no skips in the
/// pure-backend CI job) and against the native backend whenever a cabi
/// binary is present.
/// </summary>
[TestFixture]
public class Diagnostics09CoordinateTests
{
    private static string Fixture(string name) =>
        Path.Combine(TestPaths.Spec09, "invalid", "diagnostics09", name);

    [Test]
    public void EofObject_LfPlain_OpenerThroughEof()
    {
        var bytes = File.ReadAllBytes(Fixture("eof_object_lf_plain.ktav"));
        Assert.That(bytes.Length, Is.EqualTo(27), "fixture byte geometry (oracle end == EOF)");
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("UnclosedCompound"));
            Assert.That(ex.Line, Is.EqualTo(2));
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(12), $"span.start ({backend})");
            Assert.That(ex.Span.Value.End, Is.EqualTo(27), $"span.end == EOF ({backend})");
        }
    }

    [Test]
    public void EofNested_CrlfBom_InnermostOpenerThroughEof()
    {
        var bytes = File.ReadAllBytes(Fixture("eof_nested_crlf_bom.ktav"));
        Assert.That(bytes.Length, Is.EqualTo(41), "fixture byte geometry (oracle end == EOF)");
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("UnclosedCompound"));
            Assert.That(ex.Line, Is.EqualTo(3));
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(33), $"innermost opener `[` ({backend})");
            Assert.That(ex.Span.Value.End, Is.EqualTo(41), $"EOF with trailing CRLF ({backend})");
        }
    }

    [Test]
    public void EofVerbatim_CrPlain_LoneCrIsOneEol()
    {
        var bytes = File.ReadAllBytes(Fixture("eof_verbatim_cr_plain.ktav"));
        Assert.That(bytes.Length, Is.EqualTo(26), "fixture byte geometry (oracle end == EOF)");
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("UnclosedCompound"));
            Assert.That(ex.Line, Is.EqualTo(2));
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(15), $"multiline opener `(` ({backend})");
            Assert.That(ex.Span.Value.End, Is.EqualTo(26), $"EOF with lone CR ({backend})");
        }
    }

    [Test]
    public void EofStripped_LfBom_StrippedOpenerAtTrailingParen()
    {
        var bytes = File.ReadAllBytes(Fixture("eof_stripped_lf_bom.ktav"));
        Assert.That(bytes.Length, Is.EqualTo(28), "fixture byte geometry (oracle end == EOF)");
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("UnclosedCompound"));
            Assert.That(ex.Line, Is.EqualTo(2));
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(18), $"stripped opener `(` ({backend})");
            Assert.That(ex.Span.Value.End, Is.EqualTo(28), $"EOF with trailing LF ({backend})");
        }
    }

    [Test]
    public void InvalidUtf8_LfPlain_ByteOffsetsAndNullLineText()
    {
        var bytes = File.ReadAllBytes(Fixture("utf8_continuation_lf_plain.ktav"));
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("InvalidUtf8"));
            Assert.That(ex.Line, Is.EqualTo(3), $"line of the first invalid byte ({backend})");
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(31), $"span.start ({backend})");
            Assert.That(ex.Span.Value.End, Is.GreaterThanOrEqualTo(32), $"minimum_end ({backend})");
            Assert.That(ex.LineText, Is.Null, $"line_text must stay null for invalid UTF-8 ({backend})");
        }
    }

    [Test]
    public void InvalidUtf8_PriorityCrlfBom_Utf8BeforeGrammar()
    {
        var bytes = File.ReadAllBytes(Fixture("utf8_priority_crlf_bom.ktav"));
        foreach (var backend in Backends())
        {
            var ex = Assert.Throws<KtavException>(() => Ktav.LoadsUtf8(bytes, backend))!;
            Assert.That(ex.Error, Is.EqualTo("InvalidUtf8"),
                $"UTF-8 validation must precede grammar errors ({backend})");
            Assert.That(ex.Line, Is.EqualTo(3));
            Assert.That(ex.Span!.Value.Start, Is.EqualTo(31));
            Assert.That(ex.Span.Value.End, Is.GreaterThanOrEqualTo(32));
            Assert.That(ex.LineText, Is.Null);
        }
    }

    private static KtavBackend[] Backends() =>
        TestPaths.CabiBuilt()
            ? new[] { KtavBackend.Native, KtavBackend.Pure }
            : new[] { KtavBackend.Pure };
}

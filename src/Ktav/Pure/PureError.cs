// Structured parse errors: exact Rust Display texts (error/error/display.rs),
// spec sections + envelope fields (error/envelope.rs), and conversion to
// KtavException. Byte spans are positions into the ORIGINAL UTF-8 source.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

/// <summary>One structured error, mirroring ktav::ErrorKind + the cabi envelope.</summary>
internal sealed class PureError : Exception
{
    public readonly string Class;        // envelope `error`
    public readonly string? Reason;      // writer-time only (unused at parse time)
    public readonly int? Line;           // 1-based
    public readonly bool HasSpan;
    public readonly long SpanStart, SpanEnd;
    public IReadOnlyList<string>? Path;  // decoded segments (or raw for InvalidKey)
    public readonly string? Body;
    public readonly string? Canonical;
    public readonly string? SpecSection;
    public readonly string MessageText;  // Rust Display, byte-exact

    private PureError(string cls, int? line, long s, long e, bool hasSpan,
        IReadOnlyList<string>? path, string? body, string? canonical,
        string? specSection, string message)
    {
        Class = cls; Line = line; SpanStart = s; SpanEnd = e; HasSpan = hasSpan;
        Path = path; Body = body; Canonical = canonical; SpecSection = specSection;
        MessageText = message;
    }

    /// <summary>Build the KtavException, computing line_text against the source.</summary>
    public KtavException ToException(byte[] source)
    {
        string? lineText = HasSpan && source.Length > 0
            ? PureUtf8.LineTextAt(source, SpanStart)
            : null;
        return KtavException.FromPure(MessageText, Class, Reason, Line, lineText,
            HasSpan ? new KtavErrorSpan(SpanStart, SpanEnd) : (KtavErrorSpan?)null,
            Path, Body, Canonical, SpecSection);
    }

    // ----- factories (one per Rust ErrorKind; Display strings verbatim) -----

    private const string P = "Syntax error: ";

    public static PureError MissingSeparatorSpace(int line, long bodyOff, long lineEnd, int col)
    {
        return new PureError("MissingSeparatorSpace", line, bodyOff, lineEnd, true,
            null, null, null, "\u00a76.10",
            P + "Line " + line + ": MissingSeparatorSpace: separator must be followed by whitespace or end of line");
    }

    public static PureError LossyScalar(int line, string body, string canonical, long s, long e)
    {
        return new PureError("LossyScalar", line, s, e, true,
            null, body, canonical, "\u00a73.6/\u00a75.2",
            P + "Line " + line + ": LossyScalar: '" + body + "' would be inferred as a number and silently canonicalised to '" + canonical + "'; append '::' to keep it a String or write the canonical form");
    }

    public static PureError DuplicateKey(int line, string rawKey, IReadOnlyList<string> path, long s, long e)
    {
        return new PureError("DuplicateKey", line, s, e, true,
            path, null, null, "\u00a76.2",
            P + "Line " + line + ": duplicate key '" + rawKey + "'");
    }

    public static PureError KeyPathConflictOverwrite(int line, string rawPath,
        string existing, string newKind, IReadOnlyList<string> path, long s, long e)
    {
        return new PureError("KeyPathConflict", line, s, e, true,
            path, null, null, "\u00a76.3",
            P + "Line " + line + ": conflict at '" + rawPath + "' \u2014 cannot overwrite " + existing + " with " + newKind);
    }

    public static PureError KeyPathConflictBlocked(int line, string rawPath,
        IReadOnlyList<string> path, long s, long e)
    {
        return new PureError("KeyPathConflict", line, s, e, true,
            path, null, null, "\u00a76.3",
            P + "Line " + line + ": conflict at '" + rawPath + "' \u2014 an existing value blocks the path");
    }

    public static PureError EmptyKey(int line, long s, long e)
    {
        return new PureError("EmptyKey", line, s, e, true,
            null, null, null, "\u00a76.5",
            "Empty key at line " + line);
    }

    public static PureError InvalidKey(int line, string rawKey, IReadOnlyList<string> rawSegments, long s, long e)
    {
        return new PureError("InvalidKey", line, s, e, true,
            rawSegments, null, null, "\u00a76.4",
            "Invalid key at line " + line + ": '" + rawKey + "'");
    }

    public static PureError UnclosedCompound(string kind, long s, long e)
    {
        // `line` is None from the parser; the cabi layer fills it from the
        // span start — ToException only carries line_text, and Line is filled
        // here exactly as envelope.rs does (LineOfOffset over the source).
        return new PureError("UnclosedCompound", null, s, e, true,
            null, null, null, "\u00a76.1",
            kind == "object" ? "Unclosed object at end of input"
            : kind == "array" ? "Unclosed array at end of input"
            : "Unclosed multi-line string at end of input");
    }

    public static PureError UnbalancedBracket(int line, long s, long e, char found, char opener)
    {
        return new PureError("UnbalancedBracket", line, s, e, true,
            null, null, null, "\u00a76.1",
            P + "Line " + line + ": UnbalancedBracket: '" + found + "' without matching '" + opener + "'");
    }

    public static PureError MissingSeparator(int line, long s, long e)
    {
        return new PureError("MissingSeparator", line, s, e, true,
            null, null, null, "\u00a76.6",
            P + "Line " + line + ": MissingSeparator: object entries must be 'key: value' pairs");
    }

    public static PureError UnterminatedInlineCompound(int line, long s, long e)
    {
        return new PureError("UnterminatedInlineCompound", line, s, e, true,
            null, null, null, "\u00a76.11",
            P + "Line " + line + ": UnterminatedInlineCompound: inline compound not closed on the same line");
    }

    public static PureError UnterminatedQuotedKey(int line, long s, long e)
    {
        return new PureError("UnterminatedQuotedKey", line, s, e, true,
            null, null, null, "\u00a76.16",
            P + "Line " + line + ": UnterminatedQuotedKey: quoted key segment not closed on the same line");
    }

    public static PureError MalformedInlineCompound(int line, string detail, long s, long e)
    {
        return new PureError("MalformedInlineCompound", line, s, e, true,
            null, detail, null, "\u00a76.12",
            P + "Line " + line + ": MalformedInlineCompound: " + detail);
    }

    public static PureError BadEscapeSequence(int line, string sequence, long s, long e)
    {
        return new PureError("BadEscapeSequence", line, s, e, true,
            null, sequence, null, "\u00a76.13",
            P + "Line " + line + ": BadEscapeSequence: invalid escape sequence '" + sequence + "'");
    }

    public static PureError OrphanLineAfterTopLevelInline(int line, long s, long e)
    {
        return new PureError("OrphanLineAfterTopLevelInline", line, s, e, true,
            null, null, null, "\u00a76.14",
            P + "Line " + line + ": OrphanLineAfterTopLevelInline: content after root-level inline compound");
    }

    public static PureError Other(int line, string message, long s, long e)
    {
        return new PureError("Other", line, s, e, true,
            null, null, null, null, message);
    }

    /// <summary>Raw-bytes InvalidUtf8 envelope (cabi not_utf8_envelope).</summary>
    public static KtavException InvalidUtf8(byte[] src, int validUpTo, int errorLen)
    {
        long start = validUpTo;
        long end = PureUtf8.InvalidSpanEnd(src, validUpTo, errorLen);
        int line = PureUtf8.LineOfOffset(src, validUpTo);
        string body = "input is not valid UTF-8: " + PureUtf8.Utf8ErrorText(validUpTo, errorLen);
        string message = "InvalidUtf8: input is not valid UTF-8; first invalid byte sequence at byte offset " + validUpTo;
        return KtavException.FromPure(message, "InvalidUtf8", null, line, null,
            new KtavErrorSpan(start, end), null, body, null, "\u00a76.15");
    }

    /// <summary>
    /// Fill the Line of an EOF UnclosedCompound the way the cabi layer does
    /// (from the span start over the raw source) before throwing.
    /// </summary>
    public KtavException ToExceptionWithDerivedLine(byte[] source)
    {
        if (Line == null && HasSpan && source.Length > 0)
        {
            var derived = new PureError(Class,
                PureUtf8.LineOfOffset(source, SpanStart), SpanStart, SpanEnd, true,
                Path, Body, Canonical, SpecSection, MessageText);
            return derived.ToException(source);
        }
        return ToException(source);
    }

    /// <summary>
    /// envelope.rs kind_path: DuplicateKey/KeyPathConflict paths are the raw
    /// key split on unescaped dots and each segment escape-decoded (falling
    /// back to the raw segment on a decode failure).
    /// </summary>
    public static IReadOnlyList<string> DecodedPath(string raw)
    {
        var segs = PureKey.SplitKeyPath(raw);
        var outSegs = new List<string>(segs.Count);
        foreach (var seg in segs)
        {
            string? decoded = null;
            try { decoded = PureKey.DecodeKeySegment(seg); }
            catch (PureError) { }
            outSegs.Add(decoded ?? seg);
        }
        return outSegs;
    }

    /// <summary>envelope.rs kind_path for InvalidKey: raw split segments, undecoded.</summary>
    public static IReadOnlyList<string> RawPath(string raw) => PureKey.SplitKeyPath(raw);
}

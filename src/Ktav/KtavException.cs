using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav;

/// <summary>Byte-offset span reported by the native error envelope.</summary>
public readonly struct KtavErrorSpan
{
    public long Start { get; }
    public long End { get; }

    public KtavErrorSpan(long Start, long End)
    {
        this.Start = Start;
        this.End = End;
    }
}

/// <summary>
/// Thrown when the native library rejects an input — parse failure for
/// <see cref="Ktav.Loads"/>, render failure for <see cref="Ktav.Dumps"/>.
/// The native side returns a structured JSON error envelope; since ktav
/// 0.7.2 the human-readable <see cref="Exception.Message"/> is the
/// envelope's own <c>message</c> field, taken verbatim — never a JSON
/// blob, and never reassembled from the other fields (a reassembled
/// sentence didn't match what every other Ktav binding prints for the
/// same error). Against a native library built before 0.7.2, which
/// never wrote <c>message</c>, this falls back to a locally-built
/// sentence. The nine other envelope fields are available as
/// first-class properties.
/// </summary>
[Serializable]
public sealed class KtavException : Exception
{
    public KtavException() { }
    public KtavException(string message) : base(message) { }
    public KtavException(string message, Exception inner) : base(message, inner) { }

    /// <summary>Error class name (JSON <c>error</c>; e.g.
    /// "UnclosedCompound", "Message", "UnrepresentableAt").</summary>
    public string Error { get; } = null!;

    /// <summary>Writer-time §5.9.0 reason code (JSON <c>reason</c>;
    /// e.g. "NonFiniteFloat"); null for parse-time errors.</summary>
    public string? Reason { get; }

    /// <summary>1-based source line (JSON <c>line</c>); parse-time only,
    /// may be null even when a span exists.</summary>
    public int? Line { get; }

    /// <summary>Source line containing the span start (JSON
    /// <c>line_text</c>).</summary>
    public string? LineText { get; }

    /// <summary>Byte-offset span into the UTF-8 source (JSON
    /// <c>span</c>).</summary>
    public KtavErrorSpan? Span { get; }

    /// <summary>Exact decoded key segments (JSON <c>path</c>) — never a
    /// joined string: a key literally named "a.b" is one segment.</summary>
    public IReadOnlyList<string>? Path { get; }

    /// <summary>Class-specific payload (JSON <c>body</c>; e.g.
    /// LossyScalar's source form).</summary>
    public string? Body { get; }

    /// <summary>Canonical form (JSON <c>canonical</c>; e.g. LossyScalar's
    /// canonical rendering).</summary>
    public string? Canonical { get; }

    /// <summary>Relevant spec section (JSON <c>spec_section</c>; e.g.
    /// "§6.1").</summary>
    public string? SpecSection { get; }

    private KtavException(string message, string error, string? reason,
        int? line, string? lineText, KtavErrorSpan? span,
        IReadOnlyList<string>? path, string? body, string? canonical,
        string? specSection)
        : base(message)
    {
        Error = error;
        Reason = reason;
        Line = line;
        LineText = lineText;
        Span = span;
        Path = path;
        Body = body;
        Canonical = canonical;
        SpecSection = specSection;
    }

    /// <summary>
    /// Decode the native error envelope into a typed exception. A
    /// pre-envelope native binary may still return plain text; in that
    /// case we fall back to a plain KtavException with all envelope
    /// properties null (compatibility shim).
    /// </summary>
    internal static KtavException FromEnvelope(byte[] payload)
    {
        System.Text.Json.JsonDocument doc;
        try
        {
            doc = System.Text.Json.JsonDocument.Parse(new ReadOnlyMemory<byte>(payload));
        }
        catch (System.Text.Json.JsonException)
        {
            // Compatibility shim for a pre-envelope native binary.
            return new KtavException(Encoding.UTF8.GetString(payload));
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                // Compatibility shim for a pre-envelope native binary.
                return new KtavException(Encoding.UTF8.GetString(payload));
            }

            var root = doc.RootElement;

            string? error = null;
            if (root.TryGetProperty("error", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                error = v.GetString();
            if (string.IsNullOrEmpty(error))
            {
                // Compatibility shim for a pre-envelope native binary.
                return new KtavException(Encoding.UTF8.GetString(payload));
            }

            string? reason = GetStr(root, "reason");
            int? line = null;
            if (root.TryGetProperty("line", out v) && v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt32(out var l))
                line = l;
            string? lineText = GetStr(root, "line_text");

            KtavErrorSpan? span = null;
            if (root.TryGetProperty("span", out v) && v.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (v.TryGetProperty("start", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    v.TryGetProperty("end", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Number)
                    span = new KtavErrorSpan(s.GetInt64(), e.GetInt64());
            }

            IReadOnlyList<string>? path = null;
            if (root.TryGetProperty("path", out v) && v.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var segments = new List<string>();
                var ok = true;
                foreach (var item in v.EnumerateArray())
                {
                    if (item.ValueKind != System.Text.Json.JsonValueKind.String) { ok = false; break; }
                    segments.Add(item.GetString()!);
                }
                if (ok) path = segments;
            }

            string? body = GetStr(root, "body");
            string? canonical = GetStr(root, "canonical");
            string? specSection = GetStr(root, "spec_section");
            string? coreMessage = GetStr(root, "message");

            string msg;
            if (coreMessage != null)
            {
                // Since ktav 0.7.2: the core's own Display rendering,
                // taken verbatim. This is what task #303 replaces the
                // local reconstruction below with — a reassembled
                // sentence differed from what every other binding prints
                // for the identical error.
                msg = coreMessage;
            }
            else
            {
                // Fallback against a pre-0.7.2 native library, which
                // never wrote `message`. A user reading a stack trace
                // must still not be shown a JSON blob (issue rust#12
                // decision) — this is the pre-#303 local reconstruction.
                msg = error!;
                if (body != null) msg += ": " + body;
                if (path != null) msg += " at [" + string.Join(" -> ", path) + "]";
                if (lineText != null)
                    msg += line.HasValue
                        ? " (line " + line.Value + ": \"" + lineText + "\")"
                        : " (\"" + lineText + "\")";
                if (reason != null) msg += " [" + reason + "]";
                if (specSection != null) msg += " (spec " + specSection + ")";
            }

            return new KtavException(msg, error!, reason, line, lineText,
                span, path, body, canonical, specSection);
        }
    }

    private static string? GetStr(System.Text.Json.JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var v) &&
               v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString()
            : null;
    }

#if !NET8_0_OR_GREATER
    private KtavException(System.Runtime.Serialization.SerializationInfo info,
                          System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
#endif
}

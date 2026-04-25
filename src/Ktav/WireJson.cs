using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ktav;

/// <summary>
/// JSON ⇄ <see cref="KtavValue"/> bridge. Uses
/// <see cref="System.Text.Json.Utf8JsonReader"/> /
/// <see cref="System.Text.Json.Utf8JsonWriter"/> — no allocations for
/// the reader, single growable buffer for the writer.
/// <para>
/// Wire schema (shared with the native cabi side):
/// </para>
/// <list type="bullet">
///   <item><c>null</c> ⇄ <see cref="KtavNull.Instance"/></item>
///   <item><c>true</c>/<c>false</c> ⇄ <see cref="KtavBool"/></item>
///   <item><c>{"$i":"&lt;digits&gt;"}</c> ⇄ <see cref="KtavInteger"/></item>
///   <item><c>{"$f":"&lt;text&gt;"}</c> ⇄ <see cref="KtavFloat"/></item>
///   <item>string ⇄ <see cref="KtavString"/></item>
///   <item>array ⇄ <see cref="KtavArray"/></item>
///   <item>object ⇄ <see cref="KtavObject"/> (key order preserved)</item>
/// </list>
/// </summary>
internal static class WireJson
{
    public static KtavValue Decode(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);
        if (!reader.Read())
            throw new KtavException("empty JSON from native");
        return ReadValue(ref reader);
    }

    public static byte[] Encode(KtavValue value)
    {
        using var stream = new MemoryStream(256);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            // We control all the input — no need to escape '<', '>', '&', etc.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            SkipValidation = true,
        }))
        {
            WriteValue(writer, value);
        }
        return stream.ToArray();
    }

    private static KtavValue ReadValue(ref Utf8JsonReader r)
    {
        switch (r.TokenType)
        {
            case JsonTokenType.Null: return KtavNull.Instance;
            case JsonTokenType.True: return KtavBool.True;
            case JsonTokenType.False: return KtavBool.False;
            case JsonTokenType.String: return new KtavString(r.GetString()!);
            case JsonTokenType.Number:
                {
                    // Cabi never emits bare numbers (always wrapped in $i/$f),
                    // but accept them defensively for round-trip resilience.
                    var text = System.Text.Encoding.UTF8.GetString(r.HasValueSequence
                        ? r.ValueSequence.ToArray()
                        : r.ValueSpan.ToArray());
                    if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0 && text.IndexOf('E') < 0)
                        return new KtavInteger(text);
                    return new KtavFloat(text);
                }
            case JsonTokenType.StartArray: return ReadArray(ref r);
            case JsonTokenType.StartObject: return ReadObject(ref r);
            default:
                throw new KtavException("unexpected JSON token from native: " + r.TokenType);
        }
    }

    private static KtavValue ReadArray(ref Utf8JsonReader r)
    {
        var items = new List<KtavValue>();
        while (true)
        {
            if (!r.Read())
                throw new KtavException("truncated JSON array");
            if (r.TokenType == JsonTokenType.EndArray)
                return new KtavArray(items);
            items.Add(ReadValue(ref r));
        }
    }

    private static KtavValue ReadObject(ref Utf8JsonReader r)
    {
        if (!r.Read())
            throw new KtavException("truncated JSON object");
        if (r.TokenType == JsonTokenType.EndObject)
            return new KtavObject(Array.Empty<KeyValuePair<string, KtavValue>>());
        if (r.TokenType != JsonTokenType.PropertyName)
            throw new KtavException("malformed JSON object");

        var k1 = r.GetString()!;
        if (!r.Read()) throw new KtavException("truncated JSON object");
        var v1 = ReadValue(ref r);

        if (!r.Read()) throw new KtavException("truncated JSON object");

        if (r.TokenType == JsonTokenType.EndObject && (k1 == "$i" || k1 == "$f"))
        {
            var payload = (v1 as KtavString)?.Value
                ?? throw new KtavException(k1 + " payload must be a string");
            return k1 == "$i" ? new KtavInteger(payload) : new KtavFloat(payload);
        }

        var entries = new List<KeyValuePair<string, KtavValue>> { new(k1, v1) };
        while (r.TokenType != JsonTokenType.EndObject)
        {
            if (r.TokenType != JsonTokenType.PropertyName)
                throw new KtavException("malformed JSON object");
            var k = r.GetString()!;
            if (!r.Read()) throw new KtavException("truncated JSON object");
            entries.Add(new KeyValuePair<string, KtavValue>(k, ReadValue(ref r)));
            if (!r.Read()) throw new KtavException("truncated JSON object");
        }
        return new KtavObject(entries);
    }

    private static void WriteValue(Utf8JsonWriter w, KtavValue v)
    {
        switch (v)
        {
            case KtavNull:
                w.WriteNullValue();
                break;
            case KtavBool b:
                w.WriteBooleanValue(b.Value);
                break;
            case KtavInteger i:
                w.WriteStartObject();
                w.WriteString("$i", i.Text);
                w.WriteEndObject();
                break;
            case KtavFloat f:
                w.WriteStartObject();
                w.WriteString("$f", f.Text);
                w.WriteEndObject();
                break;
            case KtavString s:
                w.WriteStringValue(s.Value);
                break;
            case KtavArray a:
                w.WriteStartArray();
                foreach (var item in a.Items) WriteValue(w, item);
                w.WriteEndArray();
                break;
            case KtavObject o:
                w.WriteStartObject();
                foreach (var entry in o.Entries)
                {
                    w.WritePropertyName(entry.Key);
                    WriteValue(w, entry.Value);
                }
                w.WriteEndObject();
                break;
            default:
                throw new KtavException("unexpected KtavValue subtype: " + v.GetType());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Empty-ABI marshaling regression (native backend only).
///
/// The Rust cabi builds its input via slice::from_raw_parts(src, src_len)
/// (rust/src/cabi/declare.rs:76 loads, :110 loads_strict), and Rust
/// requires a NON-NULL, aligned pointer even when the length is 0 —
/// debug builds assert it, release builds hide the UB. A caller that
/// passes IntPtr.Zero for empty input therefore aborts a debug ktav_cabi.
/// The managed caller now allocates a live 1-byte buffer on every path
/// and passes the ORIGINAL logical length (0 for empty source).
///
/// Derivations from the Rust source (0.8.1, crates.io checksum
/// 7140dfe2…): empty source parses to an empty Object root
/// (rust/src/document/parser/parser/mod.rs:113-114 "fall back to an
/// empty Object (spec § 5.0.1 rule 1)"); canonical of an empty object
/// emits zero bytes (rust/src/document/render/canonical/walk.rs:22
/// emit_object_pairs writes nothing for an empty Object); format of an
/// empty document likewise emits zero bytes
/// (rust/src/document/render/formatted.rs:27-31,73-80 — no leading
/// trivia, no pairs, no trailing trivia, so `out` stays empty).
/// </summary>
[TestFixture]
[Category("NativeOnly")]
public class NativeEmptyAbiTests
{
    private static readonly KtavObject EmptyObject =
        new(Array.Empty<KeyValuePair<string, KtavValue>>());

    [Test]
    public void Loads_EmptySource_IsEmptyObject()
    {
        // parser/mod.rs:113-114 — empty document falls back to an empty
        // Object root (spec § 5.0.1 rule 1); cabi/ops.rs:5-11 wraps it
        // as the wire JSON of that object.
        var v = Ktav.Loads("", KtavBackend.Native);
        Assert.That(v, Is.InstanceOf<KtavObject>());
        Assert.That(((KtavObject)v).Entries.Count, Is.EqualTo(0));
    }

    [Test]
    public void LoadsUtf8_EmptyBytes_IsEmptyObject()
    {
        // Same path as Loads but with a zero-length byte array — the
        // exact IntPtr.Zero-at-len-0 case this regression pins down
        // (internal + InternalsVisibleTo).
        var v = Ktav.LoadsUtf8(Array.Empty<byte>(), KtavBackend.Native);
        Assert.That(v, Is.InstanceOf<KtavObject>());
        Assert.That(((KtavObject)v).Entries.Count, Is.EqualTo(0));
    }

    [Test]
    public void LoadsStrict_EmptySource_IsEmptyObject()
    {
        // ops.rs:15-21 — loads_strict differs only in scalar inference;
        // an empty document has no scalars, so the empty Object result
        // is identical to lax loads.
        var v = Ktav.LoadsStrict("", KtavBackend.Native);
        Assert.That(v, Is.InstanceOf<KtavObject>());
        Assert.That(((KtavObject)v).Entries.Count, Is.EqualTo(0));
    }

    [Test]
    public void EmitCanonical_EmptyObject_IsZeroBytes()
    {
        // canonical/walk.rs:19-22 — an empty Object root writes no pair
        // bytes, so canonical output is "" (UTF-8 length 0).
        var canonical = Ktav.EmitCanonical(EmptyObject, KtavBackend.Native);
        Assert.That(Encoding.UTF8.GetBytes(canonical).Length, Is.EqualTo(0));
        Assert.That(canonical, Is.Empty);
    }

    [Test]
    public void CanonicalFromSource_EmptySource_IsZeroBytes()
    {
        // ops.rs:70-75 — text-to-text canonical of an empty document:
        // parse yields the empty Object (parser/mod.rs:113-114), then
        // canonical/walk.rs:22 emits nothing → "" (0 bytes).
        var canonical = Ktav.CanonicalFromSource("", KtavBackend.Native);
        Assert.That(Encoding.UTF8.GetBytes(canonical).Length, Is.EqualTo(0));
        Assert.That(canonical, Is.Empty);
    }

    [Test]
    public void Format_EmptySource_IsZeroBytes()
    {
        // lib.rs:314-317 format_str → formatted.rs:27-31 emit_formatted:
        // no leading trivia, an empty Object root with no pairs and no
        // trailing trivia (formatted.rs:73-80) — `out` stays empty, so
        // format_str("") is "" (0 bytes), not e.g. "{}\n".
        var formatted = Ktav.Format("", KtavBackend.Native);
        Assert.That(Encoding.UTF8.GetBytes(formatted).Length, Is.EqualTo(0));
        Assert.That(formatted, Is.Empty);
    }
}

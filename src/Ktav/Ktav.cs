using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Ktav;

/// <summary>
/// Public facade for the Ktav configuration format. Thin wrapper around
/// the native <c>ktav_cabi</c> library — see <see cref="KtavValue"/>
/// for the data model.
/// </summary>
/// <example>
/// <code>
/// var doc  = Ktav.Loads("port: 8080\nname: app\n");
/// var text = Ktav.Dumps(doc);
/// </code>
/// </example>
/// <remarks>
/// The native library is loaded lazily on first call. On <c>net8.0</c>
/// and later, override the lookup path with <c>$KTAV_LIB_PATH</c>
/// (useful for local dev or air-gapped environments). Otherwise the
/// matching binary is loaded from the bundled NuGet
/// <c>runtimes/&lt;rid&gt;/native/</c> layout, or downloaded once from
/// the companion GitHub Release into the user cache.
/// <para>
/// On <c>netstandard2.0</c> targets the custom resolver API is not
/// available — <c>$KTAV_LIB_PATH</c> is ignored and the runtime falls
/// back to the default loader (NuGet <c>runtimes/</c> layout, system
/// search paths, or <c>LD_LIBRARY_PATH</c>).
/// </para>
/// </remarks>
public static class Ktav
{
    // Process-wide default backend. 0 = Native, 1 = Pure. Swapped with
    // Interlocked so a default flip is atomic and race-free; per-call
    // parameters remain the authoritative choice.
    // Initial value: Native unless the process (e.g. the pure-backend CI
    // job) exports KTAV_BACKEND=pure. Read once at type initialization;
    // use UsePureBackend()/UseNativeBackend() to change it afterwards.
    private static int _backend =
        Environment.GetEnvironmentVariable("KTAV_BACKEND") == "pure"
            ? (int)KtavBackend.Pure
            : (int)KtavBackend.Native;

    /// <summary>The process-wide default backend (initially
    /// <see cref="KtavBackend.Native"/>). Individual calls that pass an explicit
    /// backend ignore this value.</summary>
    public static KtavBackend DefaultBackend => (KtavBackend)Volatile.Read(ref _backend);

    /// <summary>Make the managed pure implementation the process-wide default.
    /// Pure mode never loads or downloads a native binary.</summary>
    public static void UsePureBackend() => Interlocked.Exchange(ref _backend, (int)KtavBackend.Pure);

    /// <summary>Restore the Rust-backed native library as the process-wide
    /// default (the initial state).</summary>
    public static void UseNativeBackend() => Interlocked.Exchange(ref _backend, (int)KtavBackend.Native);

    private static bool IsPure(KtavBackend backend) => backend == KtavBackend.Pure;

    /// <summary>Parse a Ktav document into a <see cref="KtavValue"/>.</summary>
    /// <exception cref="KtavException">on any parse error.</exception>
    public static KtavValue Loads(string src) => Loads(src, DefaultBackend);

    /// <summary>Parse a Ktav document into a <see cref="KtavValue"/>, choosing
    /// the implementation explicitly (overrides the process-wide default).</summary>
    /// <exception cref="KtavException">on any parse error.</exception>
    public static KtavValue Loads(string src, KtavBackend backend)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var bytes = Encoding.UTF8.GetBytes(src);
        if (IsPure(backend)) return Pure.PureParser.ParseLax(bytes);
        NativeLoader.EnsureRegistered();
        var output = CallNative(NativeOp.Loads, bytes);
        return WireJson.Decode(output);
    }

    internal static KtavValue LoadsUtf8(byte[] source) => LoadsUtf8(source, DefaultBackend);

    internal static KtavValue LoadsUtf8(byte[] source, KtavBackend backend)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (IsPure(backend)) return Pure.PureParser.ParseLax(source);
        NativeLoader.EnsureRegistered();
        var output = CallNative(NativeOp.Loads, source);
        return WireJson.Decode(output);
    }

    /// <summary>Parse a Ktav document with strict numeric spelling checks.</summary>
    /// <exception cref="KtavException">when strict parsing rejects the source.</exception>
    public static KtavValue LoadsStrict(string src) => LoadsStrict(src, DefaultBackend);

    /// <summary>Parse a Ktav document with strict numeric spelling checks,
    /// choosing the implementation explicitly.</summary>
    /// <exception cref="KtavException">when strict parsing rejects the source.</exception>
    public static KtavValue LoadsStrict(string src, KtavBackend backend)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var bytes = Encoding.UTF8.GetBytes(src);
        if (IsPure(backend)) return Pure.PureParser.ParseStrict(bytes);
        NativeLoader.EnsureRegistered();
        var output = CallNative(NativeOp.LoadsStrict, bytes);
        return WireJson.Decode(output);
    }

    private static void ValidateUtf8OrThrow(byte[] source)
    {
        try
        {
            new UTF8Encoding(false, true).GetCharCount(source);
        }
        catch (DecoderFallbackException ex)
        {
            var start = Math.Max(0, Math.Min(ex.Index, source.Length));
            var count = ex.BytesUnknown?.Length ?? 0;
            if (count == 0) count = 1;
            var end = (int)Math.Min(source.Length, (long)start + count);
            throw KtavException.InvalidUtf8(start, end);
        }
    }

    /// <summary>
    /// Render a <see cref="KtavValue"/> back to Ktav text. The top-level
    /// value must be a <see cref="KtavObject"/> or <see cref="KtavArray"/>
    /// (top-level Array support added in spec 0.1.1) — other shapes are
    /// rejected.
    /// </summary>
    /// <exception cref="KtavException">on any render error.</exception>
    public static string Dumps(KtavValue value) => Dumps(value, DefaultBackend);

    /// <inheritdoc cref="Dumps(KtavValue)"/>
    /// <param name="backend">Implementation choice (overrides the default).</param>
    public static string Dumps(KtavValue value, KtavBackend backend)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value is not (KtavObject or KtavArray))
            throw KtavException.WriterError("ScalarRoot", Array.Empty<string>());
        ValidateWriterValue(value);
        if (IsPure(backend)) return Pure.PureWriter.Dumps(value);
        NativeLoader.EnsureRegistered();
        var input = WireJson.Encode(value);
        var output = CallNative(NativeOp.Dumps, input);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>
    /// Render a <see cref="KtavValue"/> back to Ktav text with **every
    /// scalar coerced to a String** — booleans, integers, floats, and
    /// null are flattened to their textual form using the raw-string
    /// <c>::</c>. Compounds preserve their structure; only leaf scalars
    /// are coerced. The output round-trips back through
    /// <see cref="Loads"/> as the same set of <see cref="KtavString"/>
    /// scalars.
    /// </summary>
    /// <remarks>
    /// Useful for "everything is a string" dumps — e.g. for downstream
    /// consumers that don't understand typed values. Top-level value
    /// must be a <see cref="KtavObject"/> or <see cref="KtavArray"/>.
    /// </remarks>
    /// <exception cref="KtavException">on any render error.</exception>
    public static string DumpsForceStrings(KtavValue value) => DumpsForceStrings(value, DefaultBackend);

    /// <inheritdoc cref="DumpsForceStrings(KtavValue)"/>
    /// <param name="backend">Implementation choice (overrides the default).</param>
    public static string DumpsForceStrings(KtavValue value, KtavBackend backend)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value is not (KtavObject or KtavArray))
            throw KtavException.WriterError("ScalarRoot", Array.Empty<string>());
        if (IsPure(backend)) return Pure.PureWriter.DumpsForceStrings(value);
        NativeLoader.EnsureRegistered();
        var input = WireJson.Encode(value, forceStrings: true);
        var output = CallNative(NativeOp.DumpsForceStrings, input);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>
    /// Render a <see cref="KtavValue"/> to its **canonical (normalised)
    /// Ktav form** as defined by spec § 5.9. The output is idempotent:
    /// parsing it and calling <see cref="EmitCanonical"/> again yields
    /// byte-identical output. Useful for normalising config files,
    /// diffing, and storing a canonical source of truth.
    /// </summary>
    /// <remarks>
    /// Top-level value must be a <see cref="KtavObject"/> or
    /// <see cref="KtavArray"/>.
    /// </remarks>
    /// <exception cref="KtavException">on any render error.</exception>
    public static string EmitCanonical(KtavValue value) => EmitCanonical(value, DefaultBackend);

    /// <inheritdoc cref="EmitCanonical(KtavValue)"/>
    /// <param name="backend">Implementation choice (overrides the default).</param>
    public static string EmitCanonical(KtavValue value, KtavBackend backend)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value is not (KtavObject or KtavArray))
            throw KtavException.WriterError("ScalarRoot", Array.Empty<string>());
        ValidateWriterValue(value);
        if (IsPure(backend)) return Pure.PureCanonical.Emit(value);
        NativeLoader.EnsureRegistered();
        var input = WireJson.Encode(value);
        var output = CallNative(NativeOp.EmitCanonical, input);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>
    /// Reformat <paramref name="src"/> — the Ktav source text itself,
    /// not a parsed value (contrast with <see cref="EmitCanonical"/>,
    /// which takes a <see cref="KtavValue"/>) — into the document's
    /// canonical layout while preserving every comment verbatim (spec
    /// § 3.4: a comment owns a whole line, so attachment is unambiguous).
    /// Blank lines survive as a grouping hint, but runs of two or more
    /// collapse to exactly one, and blank padding immediately inside a
    /// bracket is dropped — the transform is a fixed point:
    /// <c>Format(Format(x)) == Format(x)</c>. Key order is never changed
    /// (spec § 5.9 has no sorting rule). For a document with no comments
    /// and no blank lines the output equals
    /// <c>EmitCanonical(Loads(src))</c>.
    /// </summary>
    /// <param name="src">Ktav source text to reformat.</param>
    /// <returns>The canonically formatted Ktav source text.</returns>
    /// <exception cref="KtavException">on any format error.</exception>
    public static string Format(string src) => Format(src, DefaultBackend);

    /// <inheritdoc cref="Format(string)"/>
    /// <param name="backend">Implementation choice (overrides the default).</param>
    public static string Format(string src, KtavBackend backend)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var bytes = Encoding.UTF8.GetBytes(src);
        if (IsPure(backend))
        {
            ValidateUtf8OrThrow(bytes);
            return Pure.PureFormatter.Format(bytes);
        }
        NativeLoader.EnsureRegistered();
        var output = CallNative(NativeOp.Format, bytes);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>
    /// Parse Ktav source text and immediately re-emit it in canonical
    /// form (spec § 5.9), preserving the source's insertion order of
    /// object keys. Equivalent to <c>EmitCanonical(Loads(src))</c>, but
    /// with no <see cref="KtavValue"/> in between: one native call
    /// instead of two, and no round-trip through the JSON wire format.
    /// Comments and blank lines do NOT survive — canonical form carries
    /// no trivia; use <see cref="Format"/> for that.
    /// </summary>
    /// <param name="src">Ktav source text.</param>
    /// <returns>Canonical Ktav source text.</returns>
    /// <exception cref="KtavException">on any parse or render error.</exception>
    public static string CanonicalFromSource(string src) => CanonicalFromSource(src, DefaultBackend);

    /// <inheritdoc cref="CanonicalFromSource(string)"/>
    /// <param name="backend">Implementation choice (overrides the default).</param>
    public static string CanonicalFromSource(string src, KtavBackend backend)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        var bytes = Encoding.UTF8.GetBytes(src);
        if (IsPure(backend))
            return Pure.PureCanonical.Emit(Pure.PureParser.ParseLax(bytes));
        NativeLoader.EnsureRegistered();
        var output = CallNative(NativeOp.CanonicalFromSource, bytes);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>
    /// Version of the loaded <c>ktav_cabi</c>. Useful for sanity checks
    /// against <see cref="ExpectedNativeVersion"/>.
    /// </summary>
    public static string NativeVersion()
    {
        NativeLoader.EnsureRegistered();
        var ptr = NativeMethods.ktav_version();
        return ptr == IntPtr.Zero ? string.Empty : ReadCString(ptr);
    }

    /// <summary>
    /// Native library version this build was compiled against. If
    /// <see cref="NativeVersion"/> differs at runtime, the loaded
    /// binary was not the one we expected.
    /// </summary>
    public static string ExpectedNativeVersion => NativeLoader.LibVersion;

    private enum NativeOp { Loads, LoadsStrict, Dumps, DumpsForceStrings, EmitCanonical, Format, CanonicalFromSource }

    /// <summary>
    /// Pre-flight §5.9.0 validation of an untrusted value tree. Iterative
    /// (explicit frame stack): runs before the emitters on every
    /// Dumps/EmitCanonical, and deep untrusted trees must not overflow the
    /// host stack. Same traversal order and error semantics as the previous
    /// recursive walk: depth-first, entries in insertion order, arrays leave
    /// the key path untouched; EmptyKeyName is thrown with the path including
    /// the offending key, NonFiniteFloat with the path at the float.
    /// </summary>
    private static void ValidateWriterValue(KtavValue root)
    {
        var path = new List<string>();
        var frames = new List<VFrame>();
        PushValidate(frames, path, root);
        while (frames.Count > 0)
        {
            var f = frames[frames.Count - 1];
            if (f.Obj != null)
            {
                if (f.PrevKey != null) { path.RemoveAt(path.Count - 1); f.PrevKey = null; }
                if (f.Next >= f.Obj.Entries.Count) { frames.RemoveAt(frames.Count - 1); continue; }
                var e = f.Obj.Entries[f.Next++];
                path.Add(e.Key);
                f.PrevKey = e.Key;
                if (e.Key.Length == 0)
                    throw KtavException.WriterError("EmptyKeyName", path.ToArray());
                PushValidate(frames, path, e.Value);
            }
            else
            {
                if (f.Next >= f.Arr!.Items.Count) { frames.RemoveAt(frames.Count - 1); continue; }
                PushValidate(frames, path, f.Arr.Items[f.Next++]);
            }
        }
    }

    /// <summary>Check one value; queue a frame when compound. Float spellings
    /// are checked before descending (document order).</summary>
    private static void PushValidate(List<VFrame> frames, List<string> path, KtavValue value)
    {
        if (value is KtavFloat fl && IsNativeNonFiniteSpelling(fl.Text))
            throw KtavException.WriterError("NonFiniteFloat", path.ToArray());
        switch (value)
        {
            case KtavObject o when o.Entries.Count > 0:
                frames.Add(new VFrame(o));
                break;
            case KtavArray a when a.Items.Count > 0:
                frames.Add(new VFrame(a));
                break;
        }
    }

    private sealed class VFrame
    {
        public readonly KtavObject? Obj;
        public readonly KtavArray? Arr;
        public int Next;
        public string? PrevKey;
        public VFrame(KtavObject obj) { Obj = obj; }
        public VFrame(KtavArray arr) { Arr = arr; }
    }

    private static bool IsNativeNonFiniteSpelling(string text) =>
        text == "NaN" || text == "Infinity" || text == "-Infinity";

    private static byte[] CallNative(NativeOp op, byte[] input)
    {
        IntPtr inputPtr = IntPtr.Zero;
        try
        {
            // Rust slices need a non-null aligned pointer even at len 0
            // (declare.rs builds slice::from_raw_parts(src, src_len));
            // we hand a live 1-byte buffer and pass the original length 0.
            inputPtr = Marshal.AllocHGlobal(Math.Max(input.Length, 1));
            if (input.Length > 0)
                Marshal.Copy(input, 0, inputPtr, input.Length);

#if NET8_0_OR_GREATER
            int rc;
            IntPtr outBuf;
            nuint outLen;
            IntPtr outErr;
            nuint outErrLen;
            switch (op)
            {
                case NativeOp.Loads:
                    rc = NativeMethods.ktav_loads(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.LoadsStrict:
                    rc = NativeMethods.ktav_loads_strict(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.Dumps:
                    rc = NativeMethods.ktav_dumps(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.DumpsForceStrings:
                    rc = NativeMethods.ktav_dumps_force_strings(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.EmitCanonical:
                    rc = NativeMethods.ktav_emit_canonical(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.Format:
                    rc = NativeMethods.ktav_format(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.CanonicalFromSource:
                    rc = NativeMethods.ktav_canonical_from_source(inputPtr, (nuint)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                default:
                    throw new InvalidOperationException("unknown native op: " + op);
            }
#else
            int rc;
            IntPtr outBuf;
            UIntPtr outLen;
            IntPtr outErr;
            UIntPtr outErrLen;
            switch (op)
            {
                case NativeOp.Loads:
                    rc = NativeMethods.ktav_loads(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.LoadsStrict:
                    rc = NativeMethods.ktav_loads_strict(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.Dumps:
                    rc = NativeMethods.ktav_dumps(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.DumpsForceStrings:
                    rc = NativeMethods.ktav_dumps_force_strings(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.EmitCanonical:
                    rc = NativeMethods.ktav_emit_canonical(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.Format:
                    rc = NativeMethods.ktav_format(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                case NativeOp.CanonicalFromSource:
                    rc = NativeMethods.ktav_canonical_from_source(inputPtr, (UIntPtr)input.Length,
                        out outBuf, out outLen, out outErr, out outErrLen);
                    break;
                default:
                    throw new InvalidOperationException("unknown native op: " + op);
            }
#endif

            if (rc != 0)
            {
                var payload = CopyBytesAndFree(outErr, outErrLen);
                // Drain success buffer too, just in case the native side
                // populated both — real cabi never does, defence in depth.
                FreeIfPresent(outBuf, outLen);
                if (payload.Length == 0)
                    throw new KtavException("native call failed with code " + rc);
                throw KtavException.FromEnvelope(payload);
            }

            // Success path: native side may still have written an error
            // pointer (it doesn't, but defence in depth — free it).
            FreeIfPresent(outErr, outErrLen);

            return CopyBytesAndFree(outBuf, outLen);
        }
        finally
        {
            if (inputPtr != IntPtr.Zero) Marshal.FreeHGlobal(inputPtr);
        }
    }

#if NET8_0_OR_GREATER
    private static byte[] CopyBytesAndFree(IntPtr ptr, nuint len)
    {
        if (ptr == IntPtr.Zero || len == 0) return Array.Empty<byte>();
        try
        {
            if (len > int.MaxValue)
                throw new KtavException("native buffer too large: " + len);
            var arr = new byte[(int)len];
            Marshal.Copy(ptr, arr, 0, arr.Length);
            return arr;
        }
        finally
        {
            NativeMethods.ktav_free(ptr, len);
        }
    }

    private static void FreeIfPresent(IntPtr ptr, nuint len)
    {
        if (ptr != IntPtr.Zero) NativeMethods.ktav_free(ptr, len);
    }
#else
    private static byte[] CopyBytesAndFree(IntPtr ptr, UIntPtr len)
    {
        if (ptr == IntPtr.Zero || (ulong)len == 0) return Array.Empty<byte>();
        try
        {
            if ((ulong)len > int.MaxValue)
                throw new KtavException("native buffer too large: " + len);
            var arr = new byte[(int)len];
            Marshal.Copy(ptr, arr, 0, arr.Length);
            return arr;
        }
        finally
        {
            NativeMethods.ktav_free(ptr, len);
        }
    }

    private static void FreeIfPresent(IntPtr ptr, UIntPtr len)
    {
        if (ptr != IntPtr.Zero) NativeMethods.ktav_free(ptr, len);
    }
#endif

    private static string ReadCString(IntPtr ptr)
    {
#if NET8_0_OR_GREATER
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
#else
        // PtrToStringUTF8 isn't on netstandard2.0. ktav_version returns
        // a static NUL-terminated ASCII version literal — bounded length.
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0 && len < 64) len++;
        if (len == 0) return string.Empty;
        var bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return Encoding.UTF8.GetString(bytes);
#endif
    }
}

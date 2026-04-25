using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Ktav;

/// <summary>
/// Public facade for the Ktav configuration format. Thin wrapper around
/// the native <c>ktav_cabi</c> library — see <see cref="KtavValue"/>
/// for the data model.
/// </summary>
/// <example>
/// <code>
/// var doc  = Ktav.Loads("port :i= 8080\nname = app\n");
/// var text = Ktav.Dumps(doc);
/// </code>
/// </example>
/// <remarks>
/// The native library is loaded lazily on first call. Override the
/// lookup path with <c>$KTAV_LIB_PATH</c> (useful for local dev or air-
/// gapped environments). Otherwise the matching binary is loaded from
/// the bundled NuGet <c>runtimes/&lt;rid&gt;/native/</c> layout, or
/// downloaded once from the companion GitHub Release into the user
/// cache.
/// </remarks>
public static class Ktav
{
    /// <summary>Parse a Ktav document into a <see cref="KtavValue"/>.</summary>
    /// <exception cref="KtavException">on any parse error.</exception>
    public static KtavValue Loads(string src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        NativeLoader.EnsureRegistered();
        var bytes = Encoding.UTF8.GetBytes(src);
        var output = CallNative(loads: true, bytes);
        return WireJson.Decode(output);
    }

    /// <summary>
    /// Render a <see cref="KtavValue"/> back to Ktav text. The top-level
    /// value must be a <see cref="KtavObject"/> — other shapes are
    /// rejected.
    /// </summary>
    /// <exception cref="KtavException">on any render error.</exception>
    public static string Dumps(KtavValue value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value is not KtavObject)
            throw new KtavException("top-level Ktav document must be an object");
        NativeLoader.EnsureRegistered();
        var input = WireJson.Encode(value);
        var output = CallNative(loads: false, input);
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

    private static byte[] CallNative(bool loads, byte[] input)
    {
        IntPtr inputPtr = IntPtr.Zero;
        try
        {
            if (input.Length > 0)
            {
                inputPtr = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputPtr, input.Length);
            }

#if NET8_0_OR_GREATER
            int rc = loads
                ? NativeMethods.ktav_loads(inputPtr, (nuint)input.Length,
                    out var outBuf, out var outLen, out var outErr, out var outErrLen)
                : NativeMethods.ktav_dumps(inputPtr, (nuint)input.Length,
                    out outBuf, out outLen, out outErr, out outErrLen);
#else
            int rc = loads
                ? NativeMethods.ktav_loads(inputPtr, (UIntPtr)input.Length,
                    out var outBuf, out var outLen, out var outErr, out var outErrLen)
                : NativeMethods.ktav_dumps(inputPtr, (UIntPtr)input.Length,
                    out outBuf, out outLen, out outErr, out outErrLen);
#endif

            if (rc != 0)
            {
                var msg = CopyAndFree(outErr, outErrLen);
                if (msg.Length == 0) msg = "native call failed with code " + rc;
                // Drain success buffer too, just in case the native side
                // populated both — real cabi never does, defence in depth.
                FreeIfPresent(outBuf, outLen);
                throw new KtavException(msg);
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
    private static string CopyAndFree(IntPtr ptr, nuint len)
    {
        var bytes = CopyBytesAndFree(ptr, len);
        return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
    }

    private static byte[] CopyBytesAndFree(IntPtr ptr, nuint len)
    {
        if (ptr == IntPtr.Zero || len == 0) return Array.Empty<byte>();
        if (len > int.MaxValue) throw new KtavException("native buffer too large: " + len);
        var arr = new byte[(int)len];
        Marshal.Copy(ptr, arr, 0, arr.Length);
        NativeMethods.ktav_free(ptr, len);
        return arr;
    }

    private static void FreeIfPresent(IntPtr ptr, nuint len)
    {
        if (ptr != IntPtr.Zero) NativeMethods.ktav_free(ptr, len);
    }
#else
    private static string CopyAndFree(IntPtr ptr, UIntPtr len)
    {
        var bytes = CopyBytesAndFree(ptr, len);
        return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
    }

    private static byte[] CopyBytesAndFree(IntPtr ptr, UIntPtr len)
    {
        if (ptr == IntPtr.Zero || (ulong)len == 0) return Array.Empty<byte>();
        if ((ulong)len > int.MaxValue) throw new KtavException("native buffer too large: " + len);
        var arr = new byte[(int)len];
        Marshal.Copy(ptr, arr, 0, arr.Length);
        NativeMethods.ktav_free(ptr, len);
        return arr;
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

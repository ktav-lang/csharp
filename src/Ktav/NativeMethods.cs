using System;
using System.Runtime.InteropServices;

namespace Ktav;

/// <summary>
/// P/Invoke surface to the <c>ktav_cabi</c> shared library. Four
/// functions, all using the "caller-owned input pointer, callee-owned
/// output buffer" pattern. The output buffer (success or error) MUST
/// be freed via <see cref="ktav_free"/>.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Logical name resolved by <see cref="NativeLoader"/> through
    /// <c>NativeLibrary.SetDllImportResolver</c> on <c>net8.0+</c>; on
    /// <c>netstandard2.0</c> the runtime's default loader handles it.
    /// </summary>
    public const string LibName = "ktav_cabi";

#if NET8_0_OR_GREATER
    [LibraryImport(LibName)]
    public static partial int ktav_loads(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_dumps(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial void ktav_free(IntPtr ptr, nuint len);

    [LibraryImport(LibName)]
    public static partial IntPtr ktav_version();
#else
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_loads(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_dumps(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktav_free(IntPtr ptr, UIntPtr len);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr ktav_version();
#endif
}

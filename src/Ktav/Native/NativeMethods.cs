using System;
using System.Runtime.InteropServices;

namespace Ktav;

/// <summary>
/// P/Invoke surface to the <c>ktav_cabi</c> shared library. Twelve
/// functions (including <c>ktav_abi_version</c> and the two spec-0.9
/// edit exports, ABI version 2), all using the "caller-owned input
/// pointer, callee-owned output buffer" pattern. The output buffer
/// (success or error) MUST be freed via <see cref="ktav_free"/>.
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
    public static partial int ktav_loads_strict(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_dumps(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_dumps_force_strings(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_emit_canonical(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_format(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_canonical_from_source(
        IntPtr src, nuint srcLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial void ktav_free(IntPtr ptr, nuint len);

    [LibraryImport(LibName)]
    public static partial IntPtr ktav_version();

    // spec 0.9 lossless edit exports (ABI_VERSION 2: additive symbols +
    // version bump).
    [LibraryImport(LibName)]
    public static partial int ktav_abi_version();

    [LibraryImport(LibName)]
    public static partial int ktav_edit_value(
        IntPtr src, nuint srcLen,
        IntPtr path, nuint pathLen,
        IntPtr replacement, nuint replacementLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_edit_value_segments(
        IntPtr src, nuint srcLen,
        IntPtr path, nuint pathLen,
        IntPtr replacement, nuint replacementLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_insert_value(
        IntPtr src, nuint srcLen,
        IntPtr path, nuint pathLen,
        IntPtr replacement, nuint replacementLen,
        out IntPtr outBuf, out nuint outLen,
        out IntPtr outErr, out nuint outErrLen);

    [LibraryImport(LibName)]
    public static partial int ktav_insert_value_segments(IntPtr src, nuint srcLen, IntPtr path, nuint pathLen, IntPtr replacement, nuint replacementLen, out IntPtr outBuf, out nuint outLen, out IntPtr outErr, out nuint outErrLen);
    [LibraryImport(LibName)]
    public static partial int ktav_remove_value(IntPtr src, nuint srcLen, IntPtr path, nuint pathLen, out IntPtr outBuf, out nuint outLen, out IntPtr outErr, out nuint outErrLen);
    [LibraryImport(LibName)]
    public static partial int ktav_remove_value_segments(IntPtr src, nuint srcLen, IntPtr path, nuint pathLen, out IntPtr outBuf, out nuint outLen, out IntPtr outErr, out nuint outErrLen);
    [LibraryImport(LibName)]
    public static partial int ktav_rename_key(IntPtr src, nuint srcLen, IntPtr path, nuint pathLen, IntPtr newKey, nuint newKeyLen, out IntPtr outBuf, out nuint outLen, out IntPtr outErr, out nuint outErrLen);
    [LibraryImport(LibName)]
    public static partial int ktav_rename_key_segments(IntPtr src, nuint srcLen, IntPtr path, nuint pathLen, IntPtr newKey, nuint newKeyLen, out IntPtr outBuf, out nuint outLen, out IntPtr outErr, out nuint outErrLen);
#else
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_loads(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_loads_strict(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_dumps(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_dumps_force_strings(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_emit_canonical(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_format(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_canonical_from_source(
        IntPtr src, UIntPtr srcLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktav_free(IntPtr ptr, UIntPtr len);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr ktav_version();

    // spec 0.9 lossless edit exports (ABI_VERSION 2: additive symbols +
    // version bump).
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_abi_version();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_edit_value(
        IntPtr src, UIntPtr srcLen,
        IntPtr path, UIntPtr pathLen,
        IntPtr replacement, UIntPtr replacementLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_edit_value_segments(
        IntPtr src, UIntPtr srcLen,
        IntPtr path, UIntPtr pathLen,
        IntPtr replacement, UIntPtr replacementLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_insert_value(
        IntPtr src, UIntPtr srcLen,
        IntPtr path, UIntPtr pathLen,
        IntPtr replacement, UIntPtr replacementLen,
        out IntPtr outBuf, out UIntPtr outLen,
        out IntPtr outErr, out UIntPtr outErrLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_insert_value_segments(IntPtr src, UIntPtr srcLen, IntPtr path, UIntPtr pathLen, IntPtr replacement, UIntPtr replacementLen, out IntPtr outBuf, out UIntPtr outLen, out IntPtr outErr, out UIntPtr outErrLen);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_remove_value(IntPtr src, UIntPtr srcLen, IntPtr path, UIntPtr pathLen, out IntPtr outBuf, out UIntPtr outLen, out IntPtr outErr, out UIntPtr outErrLen);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_remove_value_segments(IntPtr src, UIntPtr srcLen, IntPtr path, UIntPtr pathLen, out IntPtr outBuf, out UIntPtr outLen, out IntPtr outErr, out UIntPtr outErrLen);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_rename_key(IntPtr src, UIntPtr srcLen, IntPtr path, UIntPtr pathLen, IntPtr newKey, UIntPtr newKeyLen, out IntPtr outBuf, out UIntPtr outLen, out IntPtr outErr, out UIntPtr outErrLen);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int ktav_rename_key_segments(IntPtr src, UIntPtr srcLen, IntPtr path, UIntPtr pathLen, IntPtr newKey, UIntPtr newKeyLen, out IntPtr outBuf, out UIntPtr outLen, out IntPtr outErr, out UIntPtr outErrLen);
#endif
}

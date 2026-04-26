#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ktav;

/// <summary>
/// Resolves the on-disk path of <c>ktav_cabi</c> and hooks it into
/// .NET's library loader. Resolution order mirrors the Go / Java
/// bindings:
/// <list type="number">
///   <item><c>$KTAV_LIB_PATH</c>, if set and points at an existing file.</item>
///   <item>The NuGet <c>runtimes/&lt;rid&gt;/native/</c> layout — picked
///   up automatically by .NET's default resolver.</item>
///   <item>User cache: <c>&lt;userCache&gt;/ktav-dotnet/v&lt;VERSION&gt;/&lt;asset&gt;</c>,
///   if already downloaded.</item>
///   <item>Downloaded from the matching GitHub Release asset into (3).</item>
/// </list>
/// On <c>netstandard2.0</c> targets the resolver API does not exist;
/// consumers there rely on the default loader (NuGet runtimes layout
/// or LD_LIBRARY_PATH).
/// </summary>
internal static class NativeLoader
{
    /// <summary>Version of <c>ktav_cabi</c> this build expects. Bump per release.</summary>
    public const string LibVersion = "0.1.0";

    private const string ReleaseBase =
        "https://github.com/ktav-lang/csharp/releases/download/v";

    private static int s_initialised;
    private static string? s_testOverride;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref s_initialised, 1) != 0)
            return;
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
    }

    /// <summary>
    /// Test hook — pins the on-disk path the resolver will dlopen.
    /// Production users override via <c>$KTAV_LIB_PATH</c>.
    /// </summary>
    internal static void SetLibraryPath(string path) => s_testOverride = path;

    private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (libraryName != NativeMethods.LibName)
            return IntPtr.Zero;

        if (!string.IsNullOrEmpty(s_testOverride))
        {
            if (!File.Exists(s_testOverride))
                throw new DllNotFoundException(
                    $"SetLibraryPath(\"{s_testOverride}\"): file not found");
            return NativeLibrary.Load(s_testOverride);
        }

        var env = Environment.GetEnvironmentVariable("KTAV_LIB_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            if (!File.Exists(env))
                throw new DllNotFoundException(
                    $"KTAV_LIB_PATH=\"{env}\": file not found");
            return NativeLibrary.Load(env);
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
            return handle;

        var asset = AssetName();
        var dir = Path.Combine(UserCacheDir(), "ktav-dotnet", "v" + LibVersion);
        var target = Path.Combine(dir, asset);

        if (!File.Exists(target))
        {
            Directory.CreateDirectory(dir);
            var url = ReleaseBase + LibVersion + "/" + asset;
            try { Download(url, target); }
            catch (Exception e)
            {
                throw new DllNotFoundException($"fetch {url}: {e.Message}", e);
            }
        }

        return NativeLibrary.Load(target);
    }

    private static string UserCacheDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(local)) return local!;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "Local");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Caches");
        }
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrEmpty(xdg)) return xdg!;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
    }

    private static string AssetName()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"unsupported arch: {RuntimeInformation.ProcessArchitecture}"),
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"ktav_cabi-windows-{arch}.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"libktav_cabi-darwin-{arch}.dylib";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"libktav_cabi-linux-{arch}.so";
        throw new PlatformNotSupportedException(
            $"unsupported OS: {RuntimeInformation.OSDescription}");
    }

    private static void Download(string url, string targetPath)
    {
        var tmp = targetPath + ".tmp";
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var resp = http.GetAsync(url).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        using (var fs = File.Create(tmp))
        using (var stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
        {
            stream.CopyTo(fs);
        }
        if (File.Exists(targetPath)) File.Delete(targetPath);
        File.Move(tmp, targetPath);
    }
}
#else
namespace Ktav;

internal static class NativeLoader
{
    public const string LibVersion = "0.1.0";
    public static void EnsureRegistered() { /* no-op on netstandard2.0 */ }
}
#endif

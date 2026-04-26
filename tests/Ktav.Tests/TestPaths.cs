using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Hardcoded paths for the test suite. We implement one specific spec
/// version and the cabi build lives in the same workspace, so there's
/// nothing to configure.
/// </summary>
[SetUpFixture]
public class TestPaths
{
    /// <summary>Repo root, derived from this source file's location at compile time.</summary>
    private static readonly string s_repo = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(SourceFile())!, "..", "..", ".."));

    public static readonly string Cabi = Path.Combine(s_repo, "target", "release", CabiName());
    public static readonly string Spec = Path.Combine(s_repo, "spec", "versions", "0.1", "tests");

    [OneTimeSetUp]
    public void Setup()
    {
        if (File.Exists(Cabi))
            NativeLoader.SetLibraryPath(Cabi);
    }

    public static bool CabiBuilt() => File.Exists(Cabi);
    public static bool SpecPresent() => Directory.Exists(Spec);

    private static string CabiName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "ktav_cabi.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libktav_cabi.dylib";
        return "libktav_cabi.so";
    }

    private static string SourceFile([CallerFilePath] string path = "") => path;
}

using System;
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
        Path.Combine(Path.GetDirectoryName(SourceFile())!, "..", ".."));

    public static readonly string Cabi = CabiPath();
    public static readonly string Spec = Path.Combine(s_repo, "spec", "versions", "0.8", "tests");
    private static readonly Lazy<CorpusManifest> s_corpus = new(() => CorpusManifest.ValidatePinned(Spec));

    internal static CorpusManifest Corpus => s_corpus.Value;

    /// <summary>
    /// Spec 0.9 corpus (diagnostics + edit). The submodule gitlink is
    /// updated by the parent; the guard fails closed while it is missing.
    /// </summary>
    public static readonly string Spec09 = Path.Combine(s_repo, "spec", "versions", "0.9", "tests");
    private static readonly Lazy<CorpusManifest> s_corpus09 = new(() =>
        CorpusManifest.ValidatePinnedWithCounts(Spec09, CorpusManifest.PinnedCounts09));

    internal static CorpusManifest Corpus09 => s_corpus09.Value;

    [OneTimeSetUp]
    public void Setup()
    {
        Assert.That(SpecPresent(), Is.True,
            $"spec submodule checkout missing/empty: {Spec}");
        _ = Corpus;
        Assert.That(Directory.Exists(Spec09), Is.True,
            $"spec 0.9 corpus checkout missing/empty (gitlink must point at the 0.9 revision): {Spec09}");
        _ = Corpus09;

        var overridePath = Environment.GetEnvironmentVariable("KTAV_LIB_PATH");
        if (!string.IsNullOrEmpty(overridePath))
        {
            Assert.That(File.Exists(overridePath), Is.True, $"native library missing: {overridePath}");
            NativeLoader.SetLibraryPath(overridePath);
        }
        else if (File.Exists(Cabi))
            NativeLoader.SetLibraryPath(Cabi);
        else if (Environment.GetEnvironmentVariable("KTAV_BACKEND") == "pure")
        {
            // Pure-backend CI job: the native build step is deliberately
            // omitted. Native-only tests carry the "NativeOnly" NUnit
            // category and are filtered out; everything else must run --
            // and fail, not skip -- against the pure backend.
        }
        else
            Assert.Fail($"native library missing: {Cabi}");
    }

    public static bool CabiBuilt() => File.Exists(Cabi);
    public static bool SpecPresent() => Directory.Exists(Spec);

    private static string CabiName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "ktav_cabi.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libktav_cabi.dylib";
        return "libktav_cabi.so";
    }

    private static string CabiPath()
    {
        var target = Environment.GetEnvironmentVariable("CARGO_TARGET_DIR");
        var basePath = string.IsNullOrEmpty(target)
            ? Path.Combine(s_repo, "target")
            : target;
        return Path.Combine(basePath, "release", CabiName());
    }

    private static string SourceFile([CallerFilePath] string path = "") => path;
}

using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// Backend-selection API: per-call overloads override the process
/// default, and UsePureBackend()/UseNativeBackend() swap it atomically.
/// No test here assumes the INCOMING process default is Native — CI runs
/// the whole suite under KTAV_BACKEND=pure — so every global-flip test
/// captures Ktav.DefaultBackend at entry and restores exactly that value
/// in finally. Проверка отсутствия native выполняется в отдельном
/// StandaloneConsumer: уже загруженные P/Invoke exports кэшируются CLR
/// и не выгружаются при смене пути в текущем тестовом процессе.
/// </summary>
[TestFixture]
public class BackendSelectionTests
{
    [Test]
    [Category("NativeOnly")]
    public void PerCallBackendOverridesProcessDefault()
    {
        // Per-call overloads ignore the process default entirely: pure
        // calls succeed under any default, and native calls succeed when
        // the library exists — regardless of which default is set.
        Assert.That(Ktav.Loads("a: 1\n", KtavBackend.Pure).ToString(), Is.Not.Empty);
        Assert.That(Ktav.Dumps(new KtavObject(new[]
        {
            new KeyValuePair<string, KtavValue>("a", KtavInteger.Of(1)),
        }), KtavBackend.Pure), Is.Not.Empty);

        Assert.That(Ktav.Loads("a: 1\n", KtavBackend.Native).ToString(), Is.Not.Empty);
        Assert.That(Ktav.Format("a: 1\n", KtavBackend.Native), Is.Not.Empty);
    }

    /// <summary>
    /// The ONE global-swap test: the atomic default swap pair, properly
    /// isolated — the incoming default is captured and restored, so the
    /// test is order-independent under both the native CI job and the
    /// KTAV_BACKEND=pure job.
    /// </summary>
    [Test]
    public void UsePureBackendAndBackSwapTheDefault()
    {
        var incoming = Ktav.DefaultBackend;
        try
        {
            Ktav.UsePureBackend();
            Assert.That(Ktav.DefaultBackend, Is.EqualTo(KtavBackend.Pure));
        }
        finally
        {
            if (incoming == KtavBackend.Pure) Ktav.UsePureBackend();
            else Ktav.UseNativeBackend();
        }
        Assert.That(Ktav.DefaultBackend, Is.EqualTo(incoming),
            "the swap pair must restore the incoming process default");
    }

    [Test]
    public void PureOpsSucceedWithNativePathPinnedAbsent()
    {
        // Свежий процесс проверяет native-отказ отдельно: CLR может уже
        // хранить ранее привязанные exports в этом тестовом процессе.
        NativeLoader.SetLibraryPath(MissingLibPath);
        try
        {
            var v = Ktav.Loads("a.b: [1, 2]\nc: :: raw\n", KtavBackend.Pure);
            Assert.That(v.ToString(), Is.Not.Empty);
            Assert.That(Ktav.LoadsStrict("a: 1\n", KtavBackend.Pure).ToString(), Is.Not.Empty);
            Assert.That(Ktav.Dumps(v, KtavBackend.Pure), Is.Not.Empty);
            Assert.That(Ktav.DumpsForceStrings(v, KtavBackend.Pure), Is.Not.Empty);
            Assert.That(Ktav.EmitCanonical(v, KtavBackend.Pure), Is.Not.Empty);
            Assert.That(Ktav.Format("## c\na: 1\n", KtavBackend.Pure), Is.Not.Empty);
            Assert.That(Ktav.CanonicalFromSource("a: 1\n", KtavBackend.Pure), Is.Not.Empty);
            Assert.That(Ktav.LoadsUtf8(new byte[] { 0x61, 0x3A, 0x20, 0x31, 0x0A }, KtavBackend.Pure)
                .ToString(), Is.Not.Empty);
        }
        finally
        {
            // Restore exactly the loader state this fixture's SetUp
            // established: the pinned Cabi path when it exists, otherwise
            // the (nonexistent) path — never an assumption of Native.
            NativeLoader.SetLibraryPath(TestPaths.Cabi);
        }
    }

    [Test]
    public void DefaultBackendFromEnvironmentIsHonored()
    {
        // KTAV_BACKEND=pure is documented for CI (pure conformance job);
        // the static initializer reads it once. We cannot re-run the
        // initializer here, so assert the contract holds in the current
        // process: if the env var said pure, the default must be pure.
        // Otherwise (env absent or any other value) the documented
        // startup default is Native — a Pure default with the env unset
        // would be a real regression and must fail here. (The packaged
        // consumer CI job additionally proves this for a fresh process
        // with the env explicitly removed.)
        var env = Environment.GetEnvironmentVariable("KTAV_BACKEND");
        if (env == "pure")
            Assert.That(Ktav.DefaultBackend, Is.EqualTo(KtavBackend.Pure));
        else
            Assert.That(Ktav.DefaultBackend, Is.EqualTo(KtavBackend.Native));
    }

    private static readonly string MissingLibPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ktav-no-such-native",
            CabiName());

    private static string CabiName() =>
        OperatingSystem.IsWindows() ? "ktav_cabi.dll"
        : OperatingSystem.IsMacOS() ? "libktav_cabi.dylib"
        : "libktav_cabi.so";
}

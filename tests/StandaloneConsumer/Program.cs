using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Ktav;

internal static class Program
{
    private static readonly string AppDir = Path.GetFullPath(AppContext.BaseDirectory);
    private static readonly string FrameworkDir = Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory());

    private static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            throw new InvalidOperationException("Unexpected assembly resolution: " + args.Name);
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) => CheckAssembly(args.LoadedAssembly);
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
            throw new InvalidOperationException("Unexpected native resolution: " + name);
        CheckAssemblies();
        if (KtavBackend.Pure != (KtavBackend)1) throw new InvalidOperationException("Pure enum mismatch");

        const string source = "a: 1\nb: [true, null]\nc:: raw\n";
        var expected = new KtavObject(new[]
        {
            new KeyValuePair<string, KtavValue>("a", new KtavInteger("1")),
            new KeyValuePair<string, KtavValue>("b", new KtavArray(new KtavValue[] { KtavBool.True, KtavNull.Instance })),
            new KeyValuePair<string, KtavValue>("c", new KtavString("raw")),
        });
        Equal(expected, global::Ktav.Ktav.Loads(source, KtavBackend.Pure), "parse tree");
        Equal(new KtavObject(new[] { new KeyValuePair<string, KtavValue>("n", new KtavInteger("1")) }),
            global::Ktav.Ktav.LoadsStrict("n: 1\n", KtavBackend.Pure), "strict tree");

        var simple = new KtavObject(new[] { new KeyValuePair<string, KtavValue>("a", new KtavInteger("1")) });
        Equal("a: 1\n", global::Ktav.Ktav.Dumps(simple, KtavBackend.Pure), "writer");
        Equal("a: 1\n", global::Ktav.Ktav.EmitCanonical(simple, KtavBackend.Pure), "canonical writer");
        Equal("a:: 1\n", global::Ktav.Ktav.DumpsForceStrings(simple, KtavBackend.Pure), "force strings");
        Equal("## note\na: 1\n", global::Ktav.Ktav.Format("## note\na:   1\n", KtavBackend.Pure), "formatter");
        Equal("a: 1\n", global::Ktav.Ktav.CanonicalFromSource("a: 1\n", KtavBackend.Pure), "canonical source");
        Equal(expected, global::Ktav.Ktav.Loads(global::Ktav.Ktav.Dumps(expected, KtavBackend.Pure), KtavBackend.Pure), "writer roundtrip");
        var forcedExpected = new KtavObject(new[]
        {
            new KeyValuePair<string, KtavValue>("a", new KtavString("1")),
            new KeyValuePair<string, KtavValue>("b", new KtavArray(new KtavValue[] { new KtavString("true"), new KtavString("null") })),
            new KeyValuePair<string, KtavValue>("c", new KtavString("raw")),
        });
        Equal(forcedExpected, global::Ktav.Ktav.Loads(global::Ktav.Ktav.DumpsForceStrings(expected, KtavBackend.Pure), KtavBackend.Pure), "force-string roundtrip");
        Equal(expected, global::Ktav.Ktav.Loads(global::Ktav.Ktav.EmitCanonical(expected, KtavBackend.Pure), KtavBackend.Pure), "canonical roundtrip");

        var duplicate = Error("DuplicateKey", () => global::Ktav.Ktav.Loads("a: 1\na: 2\n", KtavBackend.Pure));
        if (duplicate.Line != 2 || duplicate.SpecSection != "§6.2" || duplicate.Span?.Start != 5 || duplicate.Span?.End != 6)
            throw new InvalidOperationException("DuplicateKey fields");
        Error("UnclosedCompound", () => global::Ktav.Ktav.Loads("a: [", KtavBackend.Pure));
        var bom = Error("DuplicateKey", () => global::Ktav.Ktav.Loads("\uFEFFa: 1\na: 2\n", KtavBackend.Pure));
        if (bom.Span?.Start != 8 || bom.Span?.End != 9 || bom.Line != 2) throw new InvalidOperationException("BOM byte span");
        var lossy = Error("LossyScalar", () => global::Ktav.Ktav.LoadsStrict("n: 1.10\n", KtavBackend.Pure));
        if (lossy.Body != "1.10" || lossy.Canonical != "1.1") throw new InvalidOperationException("LossyScalar payload");
        var scalar = Error("UnrepresentableAt", () => global::Ktav.Ktav.Dumps(KtavNull.Instance, KtavBackend.Pure));
        if (scalar.Reason != "ScalarRoot" || scalar.SpecSection != "§5.9.0") throw new InvalidOperationException("ScalarRoot fields");
        var emptyKey = Error("UnrepresentableAt", () => global::Ktav.Ktav.Dumps(
            new KtavObject(new[] { new KeyValuePair<string, KtavValue>("", new KtavString("x")) }), KtavBackend.Pure));
        if (emptyKey.Reason != "EmptyKeyName") throw new InvalidOperationException("EmptyKeyName field");
        var formatError = Error("UnclosedCompound", () => global::Ktav.Ktav.Format("a: [", KtavBackend.Pure));
        if (formatError.Span?.Start != 3 || formatError.Span?.End != 4 || formatError.SpecSection != "§6.1")
            throw new InvalidOperationException("Formatter error span");
        CheckAssemblies();
        var missingNative = Environment.GetEnvironmentVariable("KTAV_LIB_PATH");
        if (string.IsNullOrEmpty(missingNative) || File.Exists(missingNative))
            throw new InvalidOperationException("Expected a pinned missing native path");
        bool nativeRefused = false;
        try
        {
            global::Ktav.Ktav.Loads("a: 1\n", KtavBackend.Native);
        }
        catch (DllNotFoundException error) when (error.Message.Contains(missingNative, StringComparison.Ordinal))
        {
            nativeRefused = true;
        }
        if (!nativeRefused) throw new InvalidOperationException("Native unexpectedly succeeded in standalone process");
        Console.WriteLine("KTAV_STANDALONE_NATIVE_ABSENT_OK");
        Console.WriteLine("KTAV_STANDALONE_PURE_OK");
        return 0;
    }

    private static void CheckAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) CheckAssembly(assembly);
    }

    private static void CheckAssembly(System.Reflection.Assembly assembly)
    {
        if (assembly.IsDynamic || assembly.GetName().Name == "System.Private.CoreLib") return;
        var name = assembly.GetName().Name;
        if (name == "StandaloneConsumer" || name == "Ktav")
        {
            var expectedPath = Path.GetFullPath(Path.Combine(AppDir, name + ".dll"));
            var actualPath = Path.GetFullPath(assembly.Location);
            if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("App assembly outside exact allowlist: " + actualPath);
            return;
        }
        var location = assembly.Location;
        if (string.IsNullOrEmpty(location)) throw new InvalidOperationException("Assembly has no file location: " + assembly.FullName);
        var full = Path.GetFullPath(location);
        if (IsUnder(full, FrameworkDir)) return;
        throw new InvalidOperationException("Assembly outside framework/app allowlist: " + full);
    }

    private static bool IsUnder(string path, string directory)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static KtavException Error(string category, Action action)
    {
        try { action(); }
        catch (KtavException error)
        {
            if (error.Error != category) throw new InvalidOperationException($"Expected {category}; got {error.Error}", error);
            return error;
        }
        throw new InvalidOperationException("Expected KtavException " + category);
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException("Mismatch: " + label + " expected [" + expected + "] actual [" + actual + "]");
    }

    private static void Equal(KtavValue expected, KtavValue actual, string label)
    {
        if (!ValueEquals(expected, actual)) throw new InvalidOperationException("Mismatch: " + label);
    }

    private static bool ValueEquals(KtavValue x, KtavValue y)
    {
        if (x.GetType() != y.GetType()) return false;
        if (x is KtavNull) return true;
        if (x is KtavBool xb) return xb.Value == ((KtavBool)y).Value;
        if (x is KtavInteger xi) return xi.Text == ((KtavInteger)y).Text;
        if (x is KtavFloat xf) return xf.Text == ((KtavFloat)y).Text;
        if (x is KtavString xs) return xs.Value == ((KtavString)y).Value;
        if (x is KtavArray xa)
        {
            var ya = (KtavArray)y;
            if (xa.Items.Count != ya.Items.Count) return false;
            for (var i = 0; i < xa.Items.Count; i++) if (!ValueEquals(xa.Items[i], ya.Items[i])) return false;
            return true;
        }
        var xo = (KtavObject)x;
        var yo = (KtavObject)y;
        if (xo.Entries.Count != yo.Entries.Count) return false;
        for (var i = 0; i < xo.Entries.Count; i++)
            if (xo.Entries[i].Key != yo.Entries[i].Key || !ValueEquals(xo.Entries[i].Value, yo.Entries[i].Value)) return false;
        return true;
    }
}

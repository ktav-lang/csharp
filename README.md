# ktav — .NET bindings

**Languages:** **English** · [Русский](README.ru.md) · [简体中文](README.zh.md)

.NET bindings for the [Ktav configuration format](https://github.com/ktav-lang/spec).
Thin wrapper around the reference Rust parser, loaded at runtime through
P/Invoke — **no native build on the consumer side**, plain `dotnet add
package` just works.

Targets **`net8.0`** (with AOT-ready `LibraryImport`) and **`netstandard2.0`**
(`DllImport`, no `NativeLibrary` resolver — use NuGet's `runtimes/`
layout or `KTAV_LIB_PATH`).

## Install

```bash
dotnet add package Ktav
```

## Quick start

```csharp
using Ktav;
using System.Collections.Generic;

string src = """
             service: web
             port:i 8080
             ratio:f 0.75
             tls: true
             tags: [
                 prod
                 eu-west-1
             ]
             db.host: primary.internal
             db.timeout:i 30
             """;

KtavValue cfg = Ktav.Loads(src);

// Pattern-match the seven variants:
string label = cfg switch {
    KtavObject o => $"top object with {o.Entries.Count} keys",
    _            => "unexpected shape",
};

// Render back:
var built = new KtavObject(new[] {
    new KeyValuePair<string, KtavValue>("name",  new KtavString("demo")),
    new KeyValuePair<string, KtavValue>("count", KtavInteger.Of(42)),
});
string text = Ktav.Dumps(built);
```

## API

| Member | Purpose |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | Parse a Ktav document into the `KtavValue` tree. |
| `Ktav.Dumps(KtavValue) -> string` | Render a `KtavValue` back as Ktav text. Top-level must be `KtavObject`. |
| `Ktav.NativeVersion()` | Version string reported by the loaded `ktav_cabi`. |
| `Ktav.ExpectedNativeVersion` | Version this build was compiled against. |

`KtavException` is thrown on any parse / render failure; the message is
the UTF-8 string produced by the native parser.

## Type mapping

Mirrors the Rust crate's `Value` enum — one record per Ktav primitive,
no lossy coercions:

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger` (text form — `ToBigInteger()` / `ToInt64()`) |
| `:f <number>`    | `KtavFloat` (text form — `ToDouble()`)                  |
| bare scalar      | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (key insertion order preserved)            |

Typed integers and floats are held as **text** so arbitrary precision
(digits beyond `long`) and exact decimal round-trip are preserved byte
for byte across parse / render cycles.

## How the native library is resolved

On `net8.0`, `NativeLoader` registers a
`NativeLibrary.SetDllImportResolver` callback. Resolution order:

1. **`$KTAV_LIB_PATH`** — absolute path to a local build. Most useful
   for development and air-gapped CI.
2. **NuGet `runtimes/<rid>/native/`** layout — picked up automatically
   by .NET's default loader when consumed via `Ktav.nupkg`.
3. **User cache** — `<userCache>/ktav-dotnet/v<version>/…`, downloaded
   on a previous call.
4. **GitHub Release download** — fetched once from
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   and cached under (3). Requires network on first call after install.

`<userCache>` is `%LOCALAPPDATA%` on Windows, `~/Library/Caches` on
macOS, `$XDG_CACHE_HOME` or `~/.cache` on Linux.

On `netstandard2.0` only step (2) applies — the `NativeLibrary` API
does not exist there.

## Runtime support

- `net8.0` (AOT-friendly via `LibraryImport`) and `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+).
- Prebuilt binaries for: `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`.
- Linux distros must use glibc 2.17+ (zigbuild baseline). Alpine
  (musl) support is planned.

## License

MIT — see [LICENSE](LICENSE).

Ktav spec: [ktav-lang/spec](https://github.com/ktav-lang/spec).
Reference Rust crate: [ktav-lang/rust](https://github.com/ktav-lang/rust).

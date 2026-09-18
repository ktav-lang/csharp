# ktav — .NET bindings

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** **English** · [Русский](docs/ru/README.ru.md) · [简体中文](docs/zh/README.zh.md)

**Playground:** convert JSON / YAML / TOML / INI ⇄ Ktav in your browser at **[ktav-lang.github.io](https://ktav-lang.github.io/)**.

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

### Parse — pull typed fields out of a document

```csharp
using Ktav;

const string src = """
                   service: web
                   port: 8080
                   ratio: 0.75
                   tls: true
                   tags: [
                       prod
                       eu-west-1
                   ]
                   db.host: primary.internal
                   db.timeout: 30
                   """;

var top = (KtavObject)Ktav.Loads(src);

string  service = ((KtavString)  top.TryGet("service")!).Value;
long    port    = ((KtavInteger) top.TryGet("port")!).ToInt64();
double  ratio   = ((KtavFloat)   top.TryGet("ratio")!).ToDouble();
bool    tls     = ((KtavBool)    top.TryGet("tls")!).Value;

var db = (KtavObject) top.TryGet("db")!;
string dbHost   = ((KtavString)  db.TryGet("host")!).Value;
long   dbTimeout = ((KtavInteger) db.TryGet("timeout")!).ToInt64();
```

### Walk — pattern-match on the sealed `KtavValue` hierarchy

```csharp
foreach (var entry in top.Entries)
{
    string kind = entry.Value switch
    {
        KtavNull        => "null",
        KtavBool b      => $"bool={b.Value}",
        KtavInteger i   => $"int={i.Text}",
        KtavFloat f     => $"float={f.Text}",
        KtavString s    => $"str=\"{s.Value}\"",
        KtavArray a     => $"array({a.Items.Count})",
        KtavObject o    => $"object({o.Entries.Count})",
        _               => throw new InvalidOperationException(),
    };
    Console.WriteLine($"{entry.Key} -> {kind}");
}
```

### Build & render — construct a document in code

```csharp
using System.Collections.Generic;

KtavObject Upstream(string host, long port) => new(new[]
{
    new KeyValuePair<string, KtavValue>("host", new KtavString(host)),
    new KeyValuePair<string, KtavValue>("port", KtavInteger.Of(port)),
});

var doc = new KtavObject(new[]
{
    new KeyValuePair<string, KtavValue>("name",      new KtavString("frontend")),
    new KeyValuePair<string, KtavValue>("port",      KtavInteger.Of(8443)),
    new KeyValuePair<string, KtavValue>("tls",       KtavBool.True),
    new KeyValuePair<string, KtavValue>("ratio",     KtavFloat.Of(0.95)),
    new KeyValuePair<string, KtavValue>("upstreams", new KtavArray(new KtavValue[]
    {
        Upstream("a.example", 1080),
        Upstream("b.example", 1080),
    })),
    new KeyValuePair<string, KtavValue>("notes",     KtavNull.Instance),
});

string text = Ktav.Dumps(doc);
```

A complete runnable version lives in [`examples/Basic`](examples/Basic/Program.cs).

## API

| Member | Purpose |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | Parse a Ktav document into the `KtavValue` tree. |
| `Ktav.LoadsStrict(string) -> KtavValue` | Parse with strict numeric spelling checks. |
| `Ktav.Dumps(KtavValue) -> string` | Render a `KtavValue` back as Ktav text. Top-level must be `KtavObject`. |
| `Ktav.DumpsForceStrings(KtavValue) -> string` | Like `Dumps`, but coerces every leaf scalar to a String via the raw `::` marker. Compounds keep their structure. |
| `Ktav.EmitCanonical(KtavValue) -> string` | Render a `KtavValue` as canonical Ktav (spec § 5.9). |
| `Ktav.Format(string) -> string` | Format Ktav source into its normalised spelling, **keeping every comment**. See below. |
| `Ktav.CanonicalFromSource(string) -> string` | Parse and re-emit as canonical Ktav in one call — `EmitCanonical(Loads(src))` with no intermediate `KtavValue`. Drops comments and blank lines like `EmitCanonical` does. |
| `Ktav.NativeVersion()` | Version string reported by the loaded `ktav_cabi`. |
| `Ktav.ExpectedNativeVersion` | Version this build was compiled against. |

## Formatting — canonical spelling, comments kept

`Format` and `EmitCanonical` are different operations:

- **`EmitCanonical`** takes a `KtavValue` and writes the canonical form.
  A value carries no comments, so none can survive.
- **`Format`** takes source *text* and rewrites its spelling while
  **preserving every comment verbatim** (spec § 3.4: a comment owns a
  whole line). Key order is never changed — spec § 5.9 has no sorting
  rule.

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

A run of two or more blank lines collapses to one, and blank padding
immediately inside a bracket is dropped, which makes formatting a fixed
point: `Format(Format(x)) == Format(x)`. For a document with no comments
and no blank lines, the output equals `EmitCanonical(Loads(src))`.

## Structured errors

`KtavException` is thrown on any parse or render failure. Beyond the
message it carries the same structured envelope every Ktav binding
carries, so a tool can act on the fields instead of parsing prose:

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

| Member | Meaning |
| --- | --- |
| `Message` | Human-readable rendering of the failure. |
| `Error` | Structured error class — `DuplicateKey`, `Unrepresentable`, `Message`. |
| `Reason` | Writer-time reason code (spec § 5.9.0) such as `NonFiniteFloat`; `null` for parse errors. |
| `Line` | 1-based source line; `null` when not applicable. |
| `LineText` | Text of the offending line. |
| `Span` | `KtavErrorSpan?` — byte offsets into the UTF-8 source. |
| `Path` | Exact decoded key segments. |
| `Body` | The offending value as written. |
| `Canonical` | What the canonical form would have been. |
| `SpecSection` | The clause violated, e.g. `§3.6/§5.2`. |

Two details that are easy to get wrong:

- **`Span` holds byte offsets into UTF-8**, not UTF-16 code units, which
  is what .NET strings are indexed by. Convert before handing them to
  anything that expects `string` indices, or to an LSP client that has
  not negotiated `positionEncoding: "utf-8"`.
- **`Path` is a list of segments, never a joined string.** A key
  literally named `a.b` is one segment and cannot be confused with a
  two-segment path.

## Type mapping

Mirrors the Rust crate's `Value` enum — one record per Ktav primitive,
no lossy coercions:

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| bare integer     | `KtavInteger` (text form — `ToBigInteger()` / `ToInt64()`) |
| bare decimal     | `KtavFloat` (text form — `ToDouble()`)                  |
| other scalar     | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (key insertion order preserved)            |

Integers and floats are held as **text** so arbitrary precision
(digits beyond `long`) and exact decimal round-trip are preserved byte
for byte across parse / render cycles.

## Key escaping

Since spec 0.6.4 a literal `.` or `:` inside a key segment is written
with a backslash:

```text
a\.b: v        // key is the single segment "a.b" -> { "a.b": "v" }
a\:b: v        // key contains a colon            -> { "a:b": "v" }
x.y\.z: v      // split on the first dot only     -> { "x": { "y.z": "v" } }
```

A literal backslash in a key is `\\`.

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

MIT OR Apache-2.0 — see [LICENSE-MIT](LICENSE-MIT) and [LICENSE-APACHE](LICENSE-APACHE).

## Other Ktav implementations

- [`spec`](https://github.com/ktav-lang/spec) — specification + conformance suite
- [`rust`](https://github.com/ktav-lang/rust) — reference Rust crate (`cargo add ktav`)
- [`golang`](https://github.com/ktav-lang/golang) — Go (`go get github.com/ktav-lang/golang`)
- [`java`](https://github.com/ktav-lang/java) — Java / JVM (`io.github.ktav-lang:ktav` on Maven Central)
- [`js`](https://github.com/ktav-lang/js) — JS / TS (`npm install @ktav-lang/ktav`)
- [`php`](https://github.com/ktav-lang/php) — PHP (`composer require ktav-lang/ktav`)
- [`python`](https://github.com/ktav-lang/python) — Python (`pip install ktav`)

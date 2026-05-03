# Changelog

**Languages:** **English** · [Русский](CHANGELOG.ru.md) · [简体中文](CHANGELOG.zh.md)

All notable changes to the `Ktav` NuGet package are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning: [Semantic Versioning](https://semver.org/) with the pre-1.0
convention that a MINOR bump is breaking.

This changelog tracks **binding releases**, not changes to the Ktav
format itself — see
[`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md).

## 0.1.2 — 2026-05-03

### Changed

- **Picked up `ktav 0.1.5`** — the upstream Rust crate now exposes
  `Error::Structured(ErrorKind)` with byte-offset spans, retroactive
  `#[non_exhaustive]` on the error enums, and a public `ktav::thin`
  event-based parser. The .NET binding's user-visible behaviour is
  unchanged: `KtavException` carries the same human-readable message
  (Display strings for the seven canonical categories are byte-
  identical to ktav 0.1.4 — verified by ktav's own pinning tests).
  Mapping `ktav::ErrorKind` to a structured .NET exception hierarchy
  (`KtavMissingSeparatorSpaceException`, `KtavDuplicateKeyException`,
  etc.) is separate follow-up work tracked in the workspace's
  [`STRUCTURED_ERRORS.md`](https://github.com/ktav-lang/.github/blob/main/STRUCTURED_ERRORS.md).

NuGet package: **`Ktav`**, version 0.1.2.

## 0.1.1 — 2026-04-26

### Changed

- **Picked up `ktav 0.1.4`** — the upstream Rust crate's untyped
  `parse() → Value` path (which `cabi` uses) is now ~30% faster on
  small documents and ~13% faster on large ones, just from a one-
  line `Frame::Object` capacity tweak (4 → 8). Every `Ktav.Loads`
  call benefits transparently.

NuGet package: **`Ktav`**, version 0.1.1.

## 0.1.0 — first public release

First release. Targets **Ktav format 0.1**.

### Coordinates

NuGet package: **`Ktav`**, version 0.1.0.

### Public API

- `Ktav.Loads(string) -> KtavValue` — parse a Ktav document.
- `Ktav.Dumps(KtavValue) -> string` — render a `KtavValue` as Ktav text.
- `Ktav.NativeVersion()` — version of the loaded `ktav_cabi`.
- `Ktav.ExpectedNativeVersion` — version this build was compiled against.
- `KtavException` — parse / render error with the native-side message.
- `KtavValue` — sealed `record` hierarchy with seven variants
  (`KtavNull`, `KtavBool`, `KtavInteger`, `KtavFloat`, `KtavString`,
  `KtavArray`, `KtavObject`), mirroring the Rust crate's `Value` enum.

### Architecture

- **Native core** — the reference Rust `ktav` crate, wrapped with a
  tiny `extern "C"` C ABI (`crates/cabi`) and distributed as a prebuilt
  `.so` / `.dylib` / `.dll`.
- **.NET loader** — P/Invoke (`LibraryImport` on `net8.0`, `DllImport`
  on `netstandard2.0`). The library is resolved at first call from
  `$KTAV_LIB_PATH`, the bundled `runtimes/<rid>/native/` layout, or
  downloaded once into the user cache from the matching GitHub Release
  asset.
- **Wire format** — JSON between Rust and .NET, with `{"$i":"..."}` /
  `{"$f":"..."}` tagged wrappers for lossless typed-integer / typed-float
  round-trips and arbitrary-precision integers (`BigInteger`).

### Type mapping

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger` (text form — arbitrary precision)         |
| `:f <number>`    | `KtavFloat` (text form — exact round-trip)              |
| bare scalar      | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |

### Platforms

Prebuilt native binaries ship for:

- `linux-x64`, `linux-arm64` (glibc 2.17+ via cargo-zigbuild)
- `osx-x64`, `osx-arm64`
- `win-x64`, `win-arm64`

Alpine (musl) is planned for a follow-up.

### Test coverage

Runs the full Ktav 0.1 conformance suite (all `valid/` and `invalid/`
fixtures) on .NET 8 across Linux / macOS / Windows.

### Credits

Built on top of the reference `ktav` Rust crate. JSON streaming via
`System.Text.Json`. Native loader via `System.Runtime.InteropServices.NativeLibrary`.

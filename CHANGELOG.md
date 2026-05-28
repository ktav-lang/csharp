# Changelog

**Languages:** **English** · [Русский](CHANGELOG.ru.md) · [简体中文](CHANGELOG.zh.md)

All notable changes to the `Ktav` NuGet package are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning: [Semantic Versioning](https://semver.org/) with the pre-1.0
convention that a MINOR bump is breaking.

This changelog tracks **binding releases**, not changes to the Ktav
format itself — see
[`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md).

## 0.5.0 — 2026-05-28

### Breaking

- **Spec 0.5.0 — typed markers removed.** The `:i` / `:f` prefixes no
  longer exist in the format. Bare numbers are now typed automatically:
  integers parse as `KtavInteger`, decimals as `KtavFloat`. Documents
  written with `:i` / `:f` markers must be migrated (replace `port:i 8080`
  with `port: 8080`).
- **`KtavValue` type inference updated.** `KtavString` is now only
  produced for non-numeric, non-keyword bare scalars or explicit `:: text`
  raw-marker values.

### Added

- **`Ktav.EmitCanonical(KtavValue)`** — renders a value to the
  normalised canonical Ktav form defined by spec § 5.9. The output is
  idempotent: parsing it and calling `EmitCanonical` again yields
  byte-identical output. Useful for normalising config files, diffing,
  and storing a canonical source of truth.
- **`ktav_emit_canonical` C ABI export** in `crates/cabi`.

### Changed

- **Picked up `ktav 0.5.0`** — tracks upstream Rust crate `0.5.0`.
  See the [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.
- **License changed to `MIT OR Apache-2.0`** (dual-licensed). Added
  `LICENSE-MIT` and `LICENSE-APACHE` files; the former `LICENSE` file
  is kept as `LICENSE-MIT`.

### Spec

- spec submodule synced to `v0.5.0` — adds the canonical-form fixtures
  (`*.canonical.ktav`) alongside each `valid/` fixture. The
  `SpecConformance` test skips `.canonical.ktav` files during the
  round-trip suite (they are used only by the canonical-emit tests).

NuGet package: **`Ktav`**, version 0.5.0.


## 0.3.1 — 2026-05-10

### Added

- **`Ktav.DumpsForceStrings(KtavValue)`** — render any value with every
  scalar coerced to a `String` (typed integers `:i`, typed floats `:f`,
  booleans, and `null` are flattened to their textual form via the
  raw-marker `::`). Compounds preserve their structure; only leaf
  scalars are coerced. Re-parsing the output yields the same set of
  `KtavString` scalars. Useful for "everything is a string" dumps and
  diff-friendly canonical text.
- **Top-level Array support** (spec § 5.0.1, added in spec 0.1.1) — a
  document whose first content line is an array-item shape (bare
  scalar, `:: text`, `:i 42`, `:f 3.14`, lone `{` / `[`, multi-line
  opener `(` / `((`) now parses as a root-level `KtavArray` instead of
  raising. Empty / comments-only docs still default to `KtavObject`.
  `Ktav.Dumps` now accepts a top-level `KtavArray` (renders as bare
  item-per-line, no surrounding brackets).

### Changed

- **Picked up `ktav 0.3.1`** — tracks upstream Rust crate `0.3.1`. See
  the [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.

### Spec

- spec submodule synced to `0.1.1` — adds `valid/top_level_array/`
  fixtures (bare scalars, typed items, multi-line items, nested
  arrays, nested objects, comments-and-blanks).

NuGet package: **`Ktav`**, version 0.3.1.


## 0.3.0 — 2026-05-08

### Changed

- **Picked up `ktav 0.3.0`** — tracks upstream Rust crate `0.3.0`.
  Inline `(value)` / `((value))` shapes are now an error
  (`InlineNonEmptyCompound`); the canonical way to encode a string
  starting with `(` is the raw-marker form `key:: (value)`. Round-trip
  and the public .NET API are unchanged.
  See the
  [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.

### Spec

- spec submodule synced (paren-fixtures: `partial_parens.ktav`
  reduced to still-valid shapes; new `invalid/inline_paren_string_*`
  fixtures pin the new strictness).


## 0.2.0 — 2026-05-07

### Changed (breaking)

- **Picked up `ktav 0.2.0`** — multi-line strings now serialize in the
  indented stripped `( ... )` form by default (verbatim `(( ... ))`
  remains as fallback for content with leading whitespace or sole-`)`
  lines). `:f 42` accepts integer literals (parsed as `42.0`).
  See the
  [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md#020--2026-05-07)
  for the full delta.

  Code comparing serialized output byte-for-byte to a baked-in
  `((...))` literal must be updated. Round-trip is unchanged.

### Spec

- spec submodule synced (typed_float_without_decimal moved invalid →
  valid/typed_float_integer_body).


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

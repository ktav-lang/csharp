# Contributing to Ktav (C# / .NET)

**Languages:** **English** · [Русский](ru/CONTRIBUTING.ru.md) · [简体中文](zh/CONTRIBUTING.zh.md)

## Core rules

### 1. Every bug fix ships with a regression test

When you find a bug, **before fixing it**, write a test that reproduces
it — the test **must fail on `main`** and pass after the fix. Include
both in the same PR.

Tests live under `tests/`:

| File                         | Scope                                        |
|------------------------------|----------------------------------------------|
| `BasicTests.cs`              | Core parse/render/roundtrip behaviour.       |
| `SpecConformance.cs`         | Cross-language conformance against the spec. |
| `ReadmeDocCheckTests.cs`     | Executable README examples.                  |
| `DocsReleaseRegressionTests.cs` | Multilingual documentation examples.       |

### 2. Don't reinvent the format in the bindings

These C# bindings are deliberately a thin wrapper. Parser / format
behaviour belongs in the Rust crate
([`ktav-lang/rust`](https://github.com/ktav-lang/rust)) — changing it
there updates every language binding at once. Only **C#-specific
ergonomics** (exception types, KtavObjectMap, factory methods) belong
in this repo.

If your change requires a format change, start a discussion in
[`ktav-lang/spec`](https://github.com/ktav-lang/spec) first.

### 3. Public API changes note compatibility

If you touch anything exported from the `Ktav` namespace, say in the
PR description whether it is:

- **semver-compatible** (additions, looser types, doc changes); or
- **semver-breaking** (renamed / removed items, changed signatures,
  tightened types) — in which case the version bump lands in the next
  MINOR while we are pre-1.0.

Update the CHANGELOG source units under `root-docs/CHANGELOG/` (all
three `>>>>> lang=` blocks) in the same PR and regenerate the output.

### 4. One concept per commit

Commits should be atomic. Don't prefix commit messages with `feat:` /
`fix:` — no conventional commits here.

## Dev setup

You need:

- .NET **8 SDK** (or newer) for building and testing.
- A Rust toolchain via [`rustup`](https://rustup.rs/). MSRV: **1.71**.

Layout during development:

```
ktav-lang/
├── csharp/       ← this repo
│   └── spec/     ← pinned conformance-fixture submodule
└── rust/         ← sibling Rust crate (optional, published to crates.io)
```

### Build

```
cargo build --release -p ktav-cabi
dotnet build src/Ktav/Ktav.csproj -c Release
```

### Test

```
dotnet test tests/Ktav.Tests/Ktav.Tests.csproj -c Release
```

The `SpecConformance` module runs the cross-language fixture suite
from `ktav-lang/spec`, reading `spec/versions/0.8/tests`. The submodule
must be checked out; a missing corpus is a test failure, not a skip.

## Language policy

This repo participates in the org-wide three-language policy (EN / RU /
ZH). Every prose file lives in three parallel versions — see
[`ktav-lang/.github/AGENTS.md`](https://github.com/ktav-lang/.github/blob/main/AGENTS.md)
for the naming convention and the "update all three in one commit"
rule.

If you don't speak one of the languages, still open the PR in the
language you do speak, mark the untouched versions with
`<!-- TODO: sync with <name>.md -->` at the top, and a maintainer or
community contributor will fill the gap before merge.

### License of contributions

Unless you explicitly state otherwise, any contribution intentionally
submitted for inclusion in this project by you, as defined in the
Apache-2.0 license, shall be dual-licensed as **MIT OR Apache-2.0**,
without any additional terms or conditions.

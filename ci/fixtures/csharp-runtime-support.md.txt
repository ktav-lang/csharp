>>>>> lang=en
## Runtime support

- `net8.0` (AOT-friendly via `LibraryImport`) and `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+).
- Prebuilt binaries for: `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`.
- Linux distros must use glibc 2.17+ (zigbuild baseline). Alpine
  (musl) support is planned.

The prepared **0.8.1** Windows native distribution uses genuine
`x86_64-pc-windows-gnullvm` and `aarch64-pc-windows-gnullvm` targets with
the pinned LLVM MinGW UCRT SDK, not the MSVC build path. The public
`win-x64` / `win-arm64` RIDs and DLL filenames remain unchanged. Genuine
x64/ARM64 .NET 8 consumers run the original source/spec tests without
Rust or SDK paths before raw and RID payloads are staged. Each DLL
retains its embedded Cargo/std, Rust compiler and GNU runtime source/grant
resource; raw sidecars and NuGet `runtimes/<rid>/NOTICE.json` plus
`NOTICE.manifest.json` preserve the complete original materials and
binary/source identities. Managed frameworks and dependencies are
unchanged. Consumers do not need Rust or a native compiler; the pure
managed backend remains self-contained. This preparation does not
publish a package or replace historical release assets.

>>>>> lang=ru
## Поддержка runtime

- `net8.0` (совместим с AOT через `LibraryImport`) и `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+).
- Готовые бинарники для: `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`.
- Дистрибутивы Linux должны использовать glibc 2.17+ (базовая линия
  zigbuild). Поддержка Alpine (musl) запланирована.

Подготовленная нативная Windows-дистрибуция **0.8.1** использует настоящие
цели `x86_64-pc-windows-gnullvm` и `aarch64-pc-windows-gnullvm` с
закреплённым LLVM MinGW UCRT SDK, а не сборку MSVC. Публичные RIDs
`win-x64` / `win-arm64` и имена DLL сохранены. Перед staging raw- и
RID-payloads настоящие x64/ARM64 .NET 8 consumers выполняют исходные
source/spec tests без путей Rust или SDK. Каждая DLL сохраняет встроенный
ресурс Cargo/std, Rust compiler и исходных текстов/лицензий GNU runtime;
raw-sidecars и NuGet `runtimes/<rid>/NOTICE.json` вместе с
`NOTICE.manifest.json` сохраняют полные оригинальные материалы и
идентичности бинарника/исходников. Managed frameworks и зависимости не
изменены. Потребителям не нужны Rust или нативный компилятор; чистый
управляемый бэкенд остаётся самодостаточным. Эта подготовка не публикует
пакет и не заменяет исторические release assets.

>>>>> lang=zh
## 运行时支持

- `net8.0`（通过 `LibraryImport` 支持 AOT）与 `netstandard2.0`
  （Mono / Unity / .NET Framework 4.7.2+）。
- 预编译二进制：`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`、
  `win-x64`、`win-arm64`。
- Linux 发行版需 glibc 2.17+（zigbuild 基线）。Alpine（musl）支持已规划。

准备中的 **0.8.1** Windows 原生分发使用真正的
`x86_64-pc-windows-gnullvm` 和 `aarch64-pc-windows-gnullvm` 目标及固定的
LLVM MinGW UCRT SDK，而非 MSVC 构建路径。公开的 `win-x64` / `win-arm64`
RID 和 DLL 文件名保持不变。在暂存 raw 和 RID 载荷之前，真正的 x64/ARM64
.NET 8 使用者会在移除 Rust 和 SDK 路径后运行原始源码与规范测试。每个 DLL
保留内嵌的 Cargo/std、Rust 编译器及 GNU runtime 源码/许可资源；raw 配套文件
以及 NuGet 的 `runtimes/<rid>/NOTICE.json` 和 `NOTICE.manifest.json`
保留完整原文及二进制/源码身份。托管框架和依赖保持不变。使用者无需 Rust
或原生编译器；纯托管后端仍然自包含。这些准备不会发布软件包，也不会替换
历史 release 资产。


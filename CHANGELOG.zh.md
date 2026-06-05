# Changelog

**语言:** [English](CHANGELOG.md) · [Русский](CHANGELOG.ru.md) · **简体中文**

NuGet 包 `Ktav` 的所有显著变更记录于此。格式:
[Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/);版本号:
[Semantic Versioning](https://semver.org/),采用 pre-1.0 约定:
MINOR 递增视为破坏性变更。

本 changelog 跟踪 **绑定发布**,不覆盖 Ktav 格式自身的变更 ——
后者见 [`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md)。

## [0.6.1] — 2026-06-05

- 文档：将所有 README 示例改写为 spec 0.6 语法（裸数字替代已移除的 `:i`/`:f` 标记；`##` 注释替代 `#`）。

## 0.6.0 —— 2026-06-01

同步至 Ktav 0.6.0 —— 键现在支持转义。

### 新增

- 键处理完整的 §3.7 转义集合,并新增两个转义:
  - `\.` → `.`(字面量点 —— **不**会切分 dotted-path)
  - `\:` → `:`(字面量冒号 —— **不**作为键/值分隔符)
- 示例: `a\.b: v` → `{"a.b": "v"}`,`a\:b: v` → `{"a:b": "v"}`,
  `x.y\.z: v` → `{"x": {"y.z": "v"}}`。

### 破坏性变更

- 键中的字面量反斜杠现在需要写作 `\\`(此前键中的 `\` 是普通字节)。
  实际中很少出现;按 pre-1.0 SemVer 为 MINOR bump。

### 变更

- 跟踪 ktav-rust 0.6.0 / Ktav 规范 0.6.0。绑定源码未改动 —— escape
  语义的变化完全在 Rust 内核中实现,P/Invoke 边界对其透明。

---

## 0.1.2 —— 2026-05-03

### 变更

- **已采用 `ktav 0.1.5`** —— 上游 Rust crate 引入了结构化错误 API
  (`Error::Structured(ErrorKind)` 带字节偏移 span)、对错误枚举追溯
  应用了 `#[non_exhaustive]`,以及公开的事件式解析器 `ktav::thin`。
  .NET 绑定对用户可见的行为没有变化:`KtavException` 仍携带相同的
  人类可读消息(七个标准类别的 Display 字符串与 ktav 0.1.4 完全
  字节相同,由 ktav 自己的 pinning 测试验证)。将 `ktav::ErrorKind`
  映射到结构化 .NET 异常层级(`KtavMissingSeparatorSpaceException`、
  `KtavDuplicateKeyException` 等)是单独的后续工作,记录在
  [`STRUCTURED_ERRORS.md`](https://github.com/ktav-lang/.github/blob/main/STRUCTURED_ERRORS.md)。

NuGet 包:**`Ktav`**,版本 0.1.2。

## 0.1.1 —— 2026-04-26

### 变更

- **升级到 `ktav 0.1.4`** —— 上游 Rust crate 中 `cabi` 使用的 untyped
  `parse() → Value` 路径,小文档加速约 30%、大文档加速约 13%,只是
  `Frame::Object` 的初始容量微调(4 → 8)。每次 `Ktav.Loads` 都会
  透明地受益。

NuGet 包:**`Ktav`**,版本 0.1.1。

## 0.1.0 —— 首次公开发布

首次发布。目标格式版本:**Ktav 0.1**。

### 构件坐标

NuGet 包:**`Ktav`**,版本 0.1.0。

### 公共 API

- `Ktav.Loads(string) -> KtavValue` —— 解析 Ktav 文档。
- `Ktav.Dumps(KtavValue) -> string` —— 渲染为 Ktav 文本。
- `Ktav.NativeVersion()` —— 已加载 `ktav_cabi` 的版本。
- `Ktav.ExpectedNativeVersion` —— 本次构建的预期版本。
- `KtavException` —— 解析 / 渲染错误,消息来自原生侧。
- `KtavValue` —— 七变体的 sealed `record` 层级
  (`KtavNull`、`KtavBool`、`KtavInteger`、`KtavFloat`、`KtavString`、
  `KtavArray`、`KtavObject`),与 Rust crate 的 `Value` 枚举一一对应。

### 架构

- **原生核心** —— 参考 Rust crate `ktav`,通过极简的 `extern "C"` C
  ABI (`crates/cabi`) 封装,分发为预编译的 `.so` / `.dylib` / `.dll`。
- **.NET 加载器** —— P/Invoke(`net8.0` 上是 `LibraryImport`,
  `netstandard2.0` 上是 `DllImport`)。库在首次调用时
  从 `$KTAV_LIB_PATH`、打包的 `runtimes/<rid>/native/` 解析,或从
  对应的 GitHub Release 资产一次性下载到用户缓存。
- **Wire 格式** —— Rust 与 .NET 之间使用 JSON,带有
  `{"$i":"..."}` / `{"$f":"..."}` 标记包装,实现带类型的
  整数 / 浮点无损往返及任意精度整数(`BigInteger`)。

### 类型映射

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger`(文本形式 —— 任意精度)                    |
| `:f <number>`    | `KtavFloat`(文本形式 —— 精确往返)                      |
| 裸 scalar        | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |

### 平台

- `linux-x64`、`linux-arm64`(通过 cargo-zigbuild 锁定 glibc 2.17+)
- `osx-x64`、`osx-arm64`
- `win-x64`、`win-arm64`

Alpine(musl) —— 计划在后续版本加入。

### 测试覆盖

在 .NET 8 × Linux / macOS / Windows 上运行完整的 Ktav 0.1
conformance 套件(所有 `valid/` 与 `invalid/` fixture)。

### 致谢

基于参考 Rust crate `ktav` 构建。Streaming JSON 通过
`System.Text.Json`。原生加载器通过
`System.Runtime.InteropServices.NativeLibrary`。

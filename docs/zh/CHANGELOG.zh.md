# Changelog

## Unreleased

### 新增

- 一致性运行器：spec 0.7 的固定值类别 `unrepresentable/`（写入方必须拒绝
  —— 当前为 5 个 `.json` 输入）与 `parseable-unrepresentable/`（可解析，
  但 canonical 输出必须拒绝 —— 4 个固定值）现在会实际执行并断言。守护测试
  在 spec 子模块未检出、出现未知类别目录或任一类别为空时硬性失败。
- 0.7 行为的 API 冒烟测试：带引号键（§ 5.3.3）、值位置中的引号保持字面量、
  inline 复合值中的 `\uXXXX` 转义（§ 3.7.1），包括孤立代理对拒绝（§ 6.13）
  与裸对值的字面量行为（§ 3.7 范围）。

### 变更

- 跟踪 `ktav 0.7` 与 spec 0.7.0 —— 带引号键（§ 5.3.3）、`\uXXXX`
  unicode 转义（§ 3.7.1）、固定枚举的空白字符集（§ 3.3），以及 §§ 6.11–6.16
  的新错误类别。公开 API 无变化：Rust 错误仍以消息字符串跨边界传递。
- Rust MSRV 提升至 1.71（ktav 0.7 的真实 MSRV）。

### 修复

- 一致性：测试中的仓库根目录推导偏差一级，可能静默指向兄弟 `spec`
  检出 —— 或者在 CI 与 worktree 中零固定值仍保持绿色。运行器现在指向本
  仓库自己的子模块。invalid-UTF-8 固定值（§ 6.15）改为通过字节级原生入口
  驱动，而不是会掩盖缺陷的有损文本解码。

## 0.6.4 —— 2026-08-23

### 新增

- **`Ktav.LoadsStrict(string)`** —— 通过 .NET 和 `ktav_loads_strict`
  P/Invoke/C ABI 符号暴露 strict numeric parser。

### 变更

- 跟踪 `ktav 0.6.4` 与 spec 0.6.4，包括规范化 float 边界和
  `notation_boundaries` fixture。
- 原生库加载器现在指向精确的 `v0.6.4` release asset。

**语言:** [English](../../CHANGELOG.md) · [Русский](../ru/CHANGELOG.ru.md) · **简体中文**

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

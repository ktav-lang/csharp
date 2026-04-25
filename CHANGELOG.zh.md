# Changelog

**语言:** [English](CHANGELOG.md) · [Русский](CHANGELOG.ru.md) · **简体中文**

NuGet 包 `Ktav` 的所有显著变更记录于此。格式:
[Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/);版本号:
[Semantic Versioning](https://semver.org/),采用 pre-1.0 约定:
MINOR 递增视为破坏性变更。

本 changelog 跟踪 **绑定发布**,不覆盖 Ktav 格式自身的变更 ——
后者见 [`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md)。

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

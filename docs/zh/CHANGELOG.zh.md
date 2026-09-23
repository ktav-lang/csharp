# 变更日志

**Languages:** [English](../../CHANGELOG.md) · [Русский](../ru/CHANGELOG.ru.md) · **简体中文**

NuGet 包 `Ktav` 的所有显著变更均记录于此。格式:
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/);版本号:
[Semantic Versioning](https://semver.org/),并采用 pre-1.0 约定:
MINOR 递增视为破坏性变更。

本 changelog 跟踪 **绑定发布**,不覆盖 Ktav 格式自身的变更 ——
后者见 [`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md)。

## Unreleased

### 新增

- 一致性运行器：spec 0.7 的固定值类别 `unrepresentable/`（写入方必须拒绝
  —— 当前为 5 个 `.json` 输入）与 `parseable-unrepresentable/`（可解析，
  但 canonical 输出必须拒绝 —— 4 个固定值）现在会实际执行并断言。守护测试
  在 spec 子模块未检出、出现未知类别目录或任一类别为空时硬性失败。
- 0.7 行为的 API 冒烟测试：带引号键（§ 5.3.3）、值位置中的引号保持字面量、
  inline 复合值中的 `\uXXXX` 转义（§ 3.7.1），包括孤立代理对拒绝（§ 6.13）
  与裸对值的字面量行为（§ 3.7 范围）。

- **`Ktav.Format(string)`** —— C ABI 符号 `ktav_format`，作为保留注释的
  Ktav 源文本格式化器对外开放。每条注释都逐字保留（spec § 3.4：注释独占
  一整行）；连续两行及以上的空行会合并为一行，紧贴括号内侧的空行填充会被
  丢弃，因此格式化是一个不动点：`Format(Format(x)) == Format(x)`。键顺序
  绝不改变（spec § 5.9 没有排序规则）；对于没有注释也没有空行的文档，
  输出等同于 `EmitCanonical(Loads(src))`。

### 变更

- 跟踪 `ktav 0.7` 与 spec 0.7.0 —— 带引号键（§ 5.3.3）、`\uXXXX`
  unicode 转义（§ 3.7.1）、固定枚举的空白字符集（§ 3.3），以及 §§ 6.11–6.16
  的新错误类别。
- **`KtavException` 现在以一等属性携带结构化错误信封** —— `Error`、
  `Reason`、`Line`、`LineText`、`Span`、`Path`（精确解码后的键段，绝不是
  拼接字符串）、`Body`、`Canonical`、`SpecSection` —— 因此工具可以针对
  字段处理，而不必解析 `Message`。`Span` 保存的是 UTF-8 源文本中的字节
  偏移，而不是 .NET 字符串所用的 UTF-16 码元；用作 `string` 索引之前请先
  转换。
- Rust MSRV 提升至 1.71（ktav 0.7 的真实 MSRV）。
- `crates/cabi` 改为单次调用 `ktav::declare_cabi!()`（ktav 的 `cabi` 特性），取代手写的 C ABI 垫片；导出的符号集不变，因此绑定 API 不受影响。依赖下限提升至 `ktav 0.8`，spec 子模块重新固定到 `v0.8.0`（新增 § 5.2：带有多余前导零的十进制数解析为 String，而非 Integer）。
- 包版本升至 **0.8.0**，与核心和规范同步；预编译库的回退下载现在指向 `v0.8.0` 发布资产。

### 修复

- 一致性：测试中的仓库根目录推导偏差一级，可能静默指向兄弟 `spec`
  检出 —— 或者在 CI 与 worktree 中零固定值仍保持绿色。运行器现在指向本
  仓库自己的子模块。invalid-UTF-8 固定值（§ 6.15）改为通过字节级原生入口
  驱动，而不是会掩盖缺陷的有损文本解码。
- 一致性测试在子模块重新固定到 `0.8.0` 之后，仍读取
  `spec/versions/0.7/tests`——路径是硬编码的，并非从固定版本推导而来。
  现在读取 `spec/versions/0.8/tests`，并执行语料中的每个类别，包括
  新增的 `strict-lossy/`（`Loads` 必须等于 lax 值，`LoadsStrict` 必须以
  匹配的原因、body 与规范形式抛出异常）。一个 guard 测试会在语料中
  出现无法识别的类别目录时使构建失败，以防止这个问题再次悄然发生。

## 0.6.4 —— 2026-08-23

### 新增

- **`Ktav.LoadsStrict(string)`** —— 通过 .NET 和 `ktav_loads_strict`
  P/Invoke/C ABI 符号暴露 strict numeric parser。

### 变更

- 跟踪 `ktav 0.6.4` 与 spec 0.6.4,包括规范化 float 边界和
  `notation_boundaries` fixture。
- 原生库加载器现在指向精确的 `v0.6.4` release asset。

## [0.6.1] — 2026-06-05

- 文档:将所有 README 示例改写为 spec 0.6 语法(裸数字替代已移除的 `:i`/`:f` 标记;`##` 注释替代 `#`)。

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

## 0.5.0 —— 2026-05-28

### 破坏性变更

- **规范 0.5.0 —— 类型化标记已移除。** 前缀 `:i` / `:f` 在格式中不再
  存在。裸数字现在自动定型：整数解析为 `KtavInteger`，小数解析为
  `KtavFloat`。使用 `:i` / `:f` 标记编写的文档必须迁移
  （将 `port:i 8080` 替换为 `port: 8080`）。
- **`KtavValue` 类型推断已更新。** `KtavString` 现在仅对非数值、
  非关键字的裸标量，或显式的 `:: text` raw 标记值产生。

### 新增

- **`Ktav.EmitCanonical(KtavValue)`** —— 将值渲染为规范 § 5.9 定义的
  规范化 canonical Ktav 形式。输出是幂等的：解析该输出并再次调用
  `EmitCanonical` 会得到字节相同的输出。适用于规范化配置文件、
  做 diff 以及保存 canonical 事实来源。
- **`ktav_emit_canonical` C ABI 导出**，位于 `crates/cabi`。

### 变更

- **已采用 `ktav 0.5.0`** —— 跟踪上游 Rust crate `0.5.0`。
  完整差异见
  [`ktav` crate 的 CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)。
- **许可证更改为 `MIT OR Apache-2.0`**（双许可）。新增
  `LICENSE-MIT` 与 `LICENSE-APACHE` 文件；原 `LICENSE`
  文件保留为 `LICENSE-MIT`。

### Spec

- spec 子模块同步至 `v0.5.0` —— 在每个 `valid/` fixture 旁新增
  canonical 形式 fixture（`*.canonical.ktav`）。`SpecConformance`
  测试在 round-trip 套件中跳过 `.canonical.ktav` 文件
  （它们仅由 canonical 输出测试使用）。

NuGet 包：**`Ktav`**，版本 0.5.0。

## 0.3.1 —— 2026-05-10

### 新增

- **`Ktav.DumpsForceStrings(KtavValue)`** —— 将任意值渲染为所有标量均
  强制为 `String` 的形式(带类型的整数 `:i`、带类型的浮点 `:f`、布尔值
  与 `null` 会通过 raw 标记 `::` 摊平为其文本形式)。复合值保留其结构;
  仅强制转换叶子标量。重新解析输出会得到同一组 `KtavString`
  标量。适用于"一切皆字符串"转储以及对 diff 友好的规范化文本。
- **顶层 Array 支持**(spec § 5.0.1,于 spec 0.1.1 加入)—— 首行内容为
  数组元素形态(裸标量、`:: text`、`:i 42`、`:f 3.14`、单独的 `{` /
  `[`、多行 opener `(` / `((`)的文档,现在会解析为顶层 `KtavArray`,
  而不再抛出异常。空文档或仅含注释的文档仍默认为 `KtavObject`。
  `Ktav.Dumps` 现在接受顶层 `KtavArray`(渲染为逐行裸元素,
  不带外层括号)。

### 变更

- **已采用 `ktav 0.3.1`** —— 跟踪上游 Rust crate `0.3.1`。完整差异见
  [`ktav` crate 的 CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)。

### Spec

- spec 子模块同步至 `0.1.1` —— 新增 `valid/top_level_array/` fixture
  (裸标量、带类型元素、多行元素、嵌套数组、嵌套对象、
  注释与空行)。

NuGet 包:**`Ktav`**,版本 0.3.1。

## 0.3.0 —— 2026-05-08

### 变更

- **已采用 `ktav 0.3.0`** —— 跟踪上游 Rust crate `0.3.0`。Inline
  `(value)` / `((value))` 形态现在会报错
  (`InlineNonEmptyCompound`);对以 `(` 开头的字符串进行编码的规范方式是
  raw 标记形式 `key:: (value)`。Round-trip 与公开 .NET API 不变。
  完整差异见
  [`ktav` crate 的 CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)。

### Spec

- spec 子模块已同步(paren-fixtures:`partial_parens.ktav`
  缩减为仍然有效的形态;新增 `invalid/inline_paren_string_*`
  fixture 固定新的严格性)。

## 0.2.0 —— 2026-05-07

### 变更(破坏性)

- **已采用 `ktav 0.2.0`** —— 多行字符串现在默认序列化为带缩进的
  stripped 形式 `( ... )`(逐字节的 `(( ... ))` 仍作为回退,用于带有
  前导空白或仅含 `)` 的行的内容)。`:f 42` 现在接受整数字面量
  (解析为 `42.0`)。
  完整差异见
  [`ktav` crate 的 CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md#020--2026-05-07)。

  将序列化输出与内置的 `((...))` 字面量做字节比较的代码需要更新。
  Round-trip 不变。

### Spec

- spec 子模块已同步(typed_float_without_decimal 从 invalid 移至
  valid/typed_float_integer_body)。

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

首次发布。目标格式版本：**Ktav 0.1**。

### 构件坐标

NuGet 包：**`Ktav`**，版本 0.1.0。

### 公共 API

- `Ktav.Loads(string) -> KtavValue` —— 解析 Ktav 文档。
- `Ktav.Dumps(KtavValue) -> string` —— 渲染为 Ktav 文本。
- `Ktav.NativeVersion()` —— 已加载 `ktav_cabi` 的版本。
- `Ktav.ExpectedNativeVersion` —— 本次构建的预期版本。
- `KtavException` —— 解析 / 渲染错误，消息来自原生侧。
- `KtavValue` —— 七变体的 sealed `record` 层级
  (`KtavNull`、`KtavBool`、`KtavInteger`、`KtavFloat`、`KtavString`、
  `KtavArray`、`KtavObject`)，与 Rust crate 的 `Value` 枚举一一对应。

### 架构

- **原生核心** —— 参考 Rust crate `ktav`，通过极简的 `extern "C"` C
  ABI (`crates/cabi`) 封装，分发为预编译的 `.so` / `.dylib` / `.dll`。
- **.NET 加载器** —— P/Invoke（`net8.0` 上是 `LibraryImport`，
  `netstandard2.0` 上是 `DllImport`）。库在首次调用时
  从 `$KTAV_LIB_PATH`、打包的 `runtimes/<rid>/native/` 解析，或从
  对应的 GitHub Release 资产一次性下载到用户缓存。
- **Wire 格式** —— Rust 与 .NET 之间使用 JSON，带有
  `{"$i":"..."}` / `{"$f":"..."}` 标记包装，实现带类型的
  整数 / 浮点无损往返及任意精度整数（`BigInteger`）。

### 类型映射

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger`（文本形式 —— 任意精度）                   |
| `:f <number>`    | `KtavFloat`（文本形式 —— 精确往返）                     |
| 裸 scalar        | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |

### 平台

提供以下平台的预编译原生二进制文件：

- `linux-x64`、`linux-arm64`（通过 cargo-zigbuild 锁定 glibc 2.17+）
- `osx-x64`、`osx-arm64`
- `win-x64`、`win-arm64`

Alpine（musl）—— 计划在后续版本加入。

### 测试覆盖

在 .NET 8 × Linux / macOS / Windows 上运行完整的 Ktav 0.1
conformance 套件（所有 `valid/` 与 `invalid/` fixture）。

### 致谢

基于参考 Rust crate `ktav` 构建。Streaming JSON 通过
`System.Text.Json`。原生加载器通过
`System.Runtime.InteropServices.NativeLibrary`。

# 为 Ktav 做贡献（C# / .NET）

**Languages:** [English](../CONTRIBUTING.md) · [Русский](../ru/CONTRIBUTING.ru.md) · **简体中文**

## 核心规则

### 1. 每个 bug 修复都必须附带回归测试

发现 bug 时，**在修复之前**先写一个能复现它的测试 —— 该测试在 `main` 上
**必须失败**，修复后才能通过。两者放在同一个 PR 中。

测试位于 `tests/`：

| 文件                         | 范围                                         |
|------------------------------|----------------------------------------------|
| `BasicTests.cs`              | 核心的解析 / 渲染 / roundtrip 行为。         |
| `SpecConformance.cs`         | 对照规范的跨语言一致性测试。                 |
| `ReadmeDocCheckTests.cs`     | 可执行的 README 示例。                       |
| `DocsReleaseRegressionTests.cs` | 三语文档示例。                            |

### 2. 不要在绑定中重新发明格式

这些 C# 绑定刻意保持为薄封装。解析器 / 格式行为属于 Rust crate
（[`ktav-lang/rust`](https://github.com/ktav-lang/rust)）—— 在那里
修改一次，就会同时更新所有语言绑定。本仓库只放 **C# 特定的人体工程学
设计**（异常类型、KtavObjectMap、工厂方法）。

如果你的改动需要格式变更，请先在
[`ktav-lang/spec`](https://github.com/ktav-lang/spec) 发起讨论。

### 3. 公共 API 变更需注明兼容性

如果你修改了 `Ktav` 命名空间中导出的任何内容，请在 PR 描述中说明它是：

- **semver 兼容**（新增、更宽松的类型、文档变更）；或
- **semver 破坏性**（重命名 / 移除的项、更改的签名、收紧的类型）
  —— 这种情况下版本号提升会落在下一个 MINOR 中，毕竟我们还处于
  pre-1.0。

在同一个 PR 中更新 `root-docs/CHANGELOG/` 下的 CHANGELOG 源单元
(全部三个 `>>>>> lang=` 块)并重新生成产物。

### 4. 一个提交一个概念

提交应当是原子性的。不要给提交信息加 `feat:` / `fix:` 前缀 ——
这里不用 conventional commits。

## 开发环境

需要：

- .NET **8 SDK**（或更新版本）用于构建和测试。
- 通过 [`rustup`](https://rustup.rs/) 安装 Rust 工具链。MSRV：**1.71**。

开发期间的目录结构：

```
ktav-lang/
├── csharp/       ← 本仓库
│   └── spec/     ← 固定的一致性测试语料子模块
└── rust/         ← 相邻 Rust crate（可选，已发布到 crates.io）
```

### 构建

```
cargo build --release -p ktav-cabi
dotnet build src/Ktav/Ktav.csproj -c Release
```

### 测试

```
dotnet test tests/Ktav.Tests/Ktav.Tests.csproj -c Release
```

`SpecConformance` 模块运行来自 `ktav-lang/spec` 的跨语言固定值测试套件，
读取 `spec/versions/0.8/tests`。子模块必须已检出；语料缺失会导致测试
失败，而不会跳过。

## 语言政策

本仓库参与组织级三语言政策（EN / RU / ZH）。每个散文文件都有三个平行版本
 —— 命名约定以及“一次提交更新全部三种语言”的规则见
[`ktav-lang/.github/AGENTS.md`](https://github.com/ktav-lang/.github/blob/main/AGENTS.md)。

如果你不掌握其中某种语言，仍然可以用你掌握的语言提交 PR，并在未更新的
版本顶部标注 `<!-- TODO: sync with <name>.md -->`，维护者或社区贡献者会
在合并前补齐空缺。

### 贡献的许可

除非您另有明确声明，否则您有意提交以纳入本项目的任何贡献（按
Apache-2.0 许可证中的定义）均按 **MIT OR Apache-2.0** 双重许可，
不附加任何额外条款或条件。

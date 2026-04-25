# 贡献指南 — Ktav (C# / .NET)

**语言：** [English](CONTRIBUTING.md) · [Русский](CONTRIBUTING.ru.md) · **简体中文**

## 核心规则

### 1. 每个 bug 修复必须附带回归测试

修复前先写一个失败的测试，测试和修复放在同一个 PR 中。

### 2. 不要在绑定中重新发明格式

解析器和格式行为属于 Rust crate（[`ktav-lang/rust`](https://github.com/ktav-lang/rust)）。
此处仅包含 C# 特定的人体工程学改进。

### 3. 公共 API 变更需注明兼容性

在 PR 描述中说明是 semver 兼容还是 semver 破坏性变更。
在同一 PR 中更新 `CHANGELOG.md` 及其两个翻译版本。

### 4. 一个概念一次提交

不使用 `feat:` / `fix:` 前缀。

## 开发环境

需要 .NET 8 SDK 和 Rust 工具链（MSRV 1.70）。

```
cargo build --release -p ktav-ffi
dotnet test -c Release
```

## 语言政策

本仓库遵循组织级三语言政策（EN / RU / ZH）。所有散文文件有三个平行版本，
原子更新，一次提交。

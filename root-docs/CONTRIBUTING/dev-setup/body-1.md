>>>>> lang=en
## Dev setup

You need:

- .NET **8 SDK** (or newer) for building and testing.
- A Rust toolchain via [`rustup`](https://rustup.rs/). MSRV: **1.70**.

Layout during development — clone the sibling repos:

```
ktav-lang/
├── csharp/   ← this repo
├── rust/     ← sibling Rust crate (optional, published to crates.io)
└── spec/     ← conformance fixtures (git submodule)
```

>>>>> lang=ru
## Настройка разработки

Нужно:

- .NET **8 SDK** (или новее) для сборки и тестирования.
- Rust-тулчейн через [`rustup`](https://rustup.rs/). MSRV: **1.70**.

Раскладка во время разработки — клонируйте соседние репозитории:

```
ktav-lang/
├── csharp/   ← этот репозиторий
├── rust/     ← соседний Rust-крейт (необязательно, публикуется в crates.io)
└── spec/     ← conformance-фикстуры (git submodule)
```

>>>>> lang=zh
## 开发环境

需要：

- .NET **8 SDK**（或更新版本）用于构建和测试。
- 通过 [`rustup`](https://rustup.rs/) 安装 Rust 工具链。MSRV：**1.70**。

开发期间的目录结构 —— 克隆相邻的仓库：

```
ktav-lang/
├── csharp/   ← 本仓库
├── rust/     ← 相邻 Rust crate（可选，已发布到 crates.io）
└── spec/     ← 一致性测试固件（git submodule）
```


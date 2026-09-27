>>>>> lang=en
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

>>>>> lang=ru
## Настройка разработки

Нужно:

- .NET **8 SDK** (или новее) для сборки и тестирования.
- Rust-тулчейн через [`rustup`](https://rustup.rs/). MSRV: **1.71**.

Раскладка во время разработки:

```
ktav-lang/
├── csharp/       ← этот репозиторий
│   └── spec/     ← закреплённый сабмодуль conformance-фикстур
└── rust/         ← соседний Rust-крейт (необязательно, опубликован в crates.io)
```

>>>>> lang=zh
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


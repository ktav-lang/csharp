>>>>> lang=en
# ktav — .NET bindings

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** **English** · [Русский](docs/ru/README.ru.md) · [简体中文](docs/zh/README.zh.md)

**Playground:** convert JSON / YAML / TOML / INI ⇄ Ktav in your browser at **[ktav-lang.github.io](https://ktav-lang.github.io/)**.

.NET bindings for the [Ktav configuration format](https://github.com/ktav-lang/spec).
Thin wrapper around the reference Rust parser, loaded at runtime through
P/Invoke — **no native build on the consumer side**, plain `dotnet add
package` just works.

Targets **`net8.0`** (with AOT-ready `LibraryImport`) and **`netstandard2.0`**
(`DllImport`, no `NativeLibrary` resolver — use NuGet's `runtimes/`
layout or system native-library search paths; `KTAV_LIB_PATH` is ignored).

>>>>> lang=ru
# ktav — .NET-биндинги

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** [English](../../README.md) · **Русский** · [简体中文](../zh/README.zh.md)

**Песочница:** конвертация JSON / YAML / TOML / INI ⇄ Ktav прямо в браузере — **[ktav-lang.github.io](https://ktav-lang.github.io/)**.

.NET-биндинги к [формату конфигурации Ktav](https://github.com/ktav-lang/spec).
Тонкая обёртка над эталонным парсером на Rust, подгружаемая во время
выполнения через P/Invoke — **никакой сборки нативной части на стороне
потребителя**, обычный `dotnet add package` просто работает.

Цели сборки: **`net8.0`** (с `LibraryImport`, готовым к AOT) и
**`netstandard2.0`** (`DllImport`, без резолвера `NativeLibrary` —
используйте компоновку NuGet `runtimes/` или системные пути поиска
нативных библиотек; `KTAV_LIB_PATH` игнорируется).

>>>>> lang=zh
# ktav — .NET 绑定

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** [English](../../README.md) · [Русский](../ru/README.ru.md) · **简体中文**

**演练场：** 在浏览器中把 JSON / YAML / TOML / INI ⇄ Ktav 互转 —— **[ktav-lang.github.io](https://ktav-lang.github.io/)**。

[Ktav 配置格式](https://github.com/ktav-lang/spec) 的 .NET 绑定。
在参考 Rust 解析器之上的一层薄封装，运行时经 P/Invoke 加载 ——
**使用方无需编译原生代码**，常规的 `dotnet add package` 即可。

目标框架：**`net8.0`**（`LibraryImport`，支持 AOT）与 **`netstandard2.0`**
（`DllImport`，无 `NativeLibrary` 解析器 —— 使用 NuGet 的 `runtimes/`
布局或系统原生库搜索路径；`KTAV_LIB_PATH` 会被忽略）。


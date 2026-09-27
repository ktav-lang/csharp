>>>>> lang=en
## Core rules

### 1. Every bug fix ships with a regression test

When you find a bug, **before fixing it**, write a test that reproduces
it — the test **must fail on `main`** and pass after the fix. Include
both in the same PR.

Tests live under `tests/`:

| File                         | Scope                                        |
|------------------------------|----------------------------------------------|
| `BasicTests.cs`              | Core parse/render/roundtrip behaviour.       |
| `SpecConformance.cs`         | Cross-language conformance against the spec. |
| `ReadmeDocCheckTests.cs`     | Executable README examples.                  |
| `DocsReleaseRegressionTests.cs` | Multilingual documentation examples.       |

>>>>> lang=ru
## Основные правила

### 1. Каждый баг-фикс сопровождается регрессионным тестом

Когда вы находите баг, **перед его исправлением** напишите тест, который
его воспроизводит — тест **должен падать на `main`** и проходить после
фикса. И то и другое — в одном PR.

Тесты живут в `tests/`:

| Файл                         | Область                                      |
|------------------------------|----------------------------------------------|
| `BasicTests.cs`              | Основы разбора / рендеринга / roundtrip.     |
| `SpecConformance.cs`         | Межъязыковая конформация со спецификацией.   |
| `ReadmeDocCheckTests.cs`     | Исполняемые примеры README.                  |
| `DocsReleaseRegressionTests.cs` | Примеры документации на трёх языках.      |

>>>>> lang=zh
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


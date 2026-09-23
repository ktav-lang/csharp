>>>>> lang=en
### Changed

- Tracks `ktav 0.7` and spec 0.7.0 — quoted keys (§ 5.3.3), `\uXXXX`
  unicode escapes (§ 3.7.1), the exhaustively enumerated whitespace set
  (§ 3.3), and the new error categories of §§ 6.11–6.16.
- **`KtavException` now carries the structured error envelope** as
  first-class properties — `Error`, `Reason`, `Line`, `LineText`,
  `Span`, `Path` (exact decoded key segments, never a joined string),
  `Body`, `Canonical`, `SpecSection` — so a tool can act on the fields
  instead of parsing `Message`. `Span` holds byte offsets into the UTF-8
  source, not UTF-16 code units, which is what .NET strings are indexed
  by; convert before using them as `string` indices.
- Rust MSRV raised to 1.71 (ktav 0.7's MSRV).
- Migrated `crates/cabi` to a single `ktav::declare_cabi!()` invocation
  (ktav's `cabi` feature) instead of a hand-rolled C ABI shim; the
  exported symbol surface is unchanged, so the .NET API is unaffected.
  Dependency floor raised to `ktav 0.8`, spec submodule re-pinned to
  `v0.8.0` (adds § 5.2: a decimal with a redundant leading zero parses
  as a String, not an Integer).
- The package version moves to **0.8.0**, in step with the core and the
  specification; the prebuilt-library download fallback now targets the
  `v0.8.0` release asset.

>>>>> lang=ru
### Изменено

- Binding отслеживает `ktav 0.7` и spec 0.7.0 — кавыченные ключи
  (§ 5.3.3), unicode-эскейпы `\uXXXX` (§ 3.7.1), фиксированный набор
  whitespace (§ 3.3) и новые категории ошибок §§ 6.11–6.16.
- **`KtavException` теперь несёт структурированный конверт ошибки**
  первоклассными свойствами — `Error`, `Reason`, `Line`, `LineText`,
  `Span`, `Path` (точные декодированные сегменты ключа, а не склеенная
  строка), `Body`, `Canonical`, `SpecSection`, — поэтому инструмент
  может работать с полями, а не разбирать `Message`. `Span` хранит
  байтовые смещения в UTF-8-источнике, а не кодовые единицы UTF-16,
  которыми индексируются строки .NET; преобразуйте их, прежде чем
  использовать как индексы `string`.
- Rust MSRV поднят до 1.71 (реальный MSRV ktav 0.7).
- `crates/cabi` переведён на один вызов `ktav::declare_cabi!()` (фича `cabi` крейта ktav) вместо рукописной C ABI-прослойки; набор экспортируемых символов не изменился, поэтому API биндинга не затронут. Нижняя граница зависимости поднята до `ktav 0.8`, подмодуль spec перезакреплён на `v0.8.0` (добавлен § 5.2: десятичное число с избыточным ведущим нулём разбирается как String, а не Integer).
- Версия пакета — **0.8.0**, синхронно с ядром и спецификацией; резервная загрузка готовой библиотеки теперь берёт ассет релиза `v0.8.0`.

>>>>> lang=zh
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


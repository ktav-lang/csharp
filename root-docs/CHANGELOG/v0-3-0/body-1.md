>>>>> lang=en
## 0.3.0 — 2026-05-08

### Changed

- **Picked up `ktav 0.3.0`** — tracks upstream Rust crate `0.3.0`.
  Inline `(value)` / `((value))` shapes are now an error
  (`InlineNonEmptyCompound`); the canonical way to encode a string
  starting with `(` is the raw-marker form `key:: (value)`. Round-trip
  and the public .NET API are unchanged.
  See the
  [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.

### Spec

- spec submodule synced (paren-fixtures: `partial_parens.ktav`
  reduced to still-valid shapes; new `invalid/inline_paren_string_*`
  fixtures pin the new strictness).

>>>>> lang=ru
## 0.3.0 — 2026-05-08

### Изменено

- **Подхвачен `ktav 0.3.0`** — отслеживает upstream Rust crate
  `0.3.0`. Inline-формы `(value)` / `((value))` теперь являются
  ошибкой (`InlineNonEmptyCompound`); канонический способ закодировать
  строку, начинающуюся с `(`, — raw-маркер формы `key:: (value)`.
  Round-trip и публичный .NET API не изменились.
  Полный diff см. в
  [`CHANGELOG` крейта `ktav`](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md).

### Spec

- подмодуль spec синхронизирован (paren-fixtures: `partial_parens.ktav`
  сведён к по-прежнему валидным формам; новые фикстуры
  `invalid/inline_paren_string_*` фиксируют новую строгость).

>>>>> lang=zh
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


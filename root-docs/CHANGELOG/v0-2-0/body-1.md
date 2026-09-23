>>>>> lang=en
## 0.2.0 — 2026-05-07

### Changed (breaking)

- **Picked up `ktav 0.2.0`** — multi-line strings now serialize in the
  indented stripped `( ... )` form by default (verbatim `(( ... ))`
  remains as fallback for content with leading whitespace or sole-`)`
  lines). `:f 42` accepts integer literals (parsed as `42.0`).
  See the
  [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md#020--2026-05-07)
  for the full delta.

  Code comparing serialized output byte-for-byte to a baked-in
  `((...))` literal must be updated. Round-trip is unchanged.

### Spec

- spec submodule synced (typed_float_without_decimal moved invalid →
  valid/typed_float_integer_body).

>>>>> lang=ru
## 0.2.0 — 2026-05-07

### Изменено (ломающее)

- **Подхвачен `ktav 0.2.0`** — многострочные строки теперь по
  умолчанию сериализуются в отступной stripped-форме `( ... )`
  (побайтовая `(( ... ))` остаётся запасным вариантом для контента с
  ведущими пробелами или строками из одной `)`). `:f 42` принимает
  целочисленные литералы (парсятся как `42.0`).
  Полный diff см. в
  [`CHANGELOG` крейта `ktav`](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md#020--2026-05-07).

  Код, сравнивающий сериализованный вывод побайтово с зашитым
  литералом `((...))`, должен быть обновлён. Round-trip не изменился.

### Spec

- подмодуль spec синхронизирован (typed_float_without_decimal перенесён
  из invalid → valid/typed_float_integer_body).

>>>>> lang=zh
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


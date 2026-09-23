>>>>> lang=en
## 0.3.1 — 2026-05-10

### Added

- **`Ktav.DumpsForceStrings(KtavValue)`** — render any value with every
  scalar coerced to a `String` (typed integers `:i`, typed floats `:f`,
  booleans, and `null` are flattened to their textual form via the
  raw-marker `::`). Compounds preserve their structure; only leaf
  scalars are coerced. Re-parsing the output yields the same set of
  `KtavString` scalars. Useful for "everything is a string" dumps and
  diff-friendly canonical text.
- **Top-level Array support** (spec § 5.0.1, added in spec 0.1.1) — a
  document whose first content line is an array-item shape (bare
  scalar, `:: text`, `:i 42`, `:f 3.14`, lone `{` / `[`, multi-line
  opener `(` / `((`) now parses as a root-level `KtavArray` instead of
  raising. Empty / comments-only docs still default to `KtavObject`.
  `Ktav.Dumps` now accepts a top-level `KtavArray` (renders as bare
  item-per-line, no surrounding brackets).

### Changed

- **Picked up `ktav 0.3.1`** — tracks upstream Rust crate `0.3.1`. See
  the [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.

### Spec

- spec submodule synced to `0.1.1` — adds `valid/top_level_array/`
  fixtures (bare scalars, typed items, multi-line items, nested
  arrays, nested objects, comments-and-blanks).

NuGet package: **`Ktav`**, version 0.3.1.

>>>>> lang=ru
## 0.3.1 — 2026-05-10

### Добавлено

- **`Ktav.DumpsForceStrings(KtavValue)`** — рендерит любое значение,
  приводя каждый скаляр к `String` (типизированные целые `:i`,
  типизированные дробные `:f`, булевы и `null` сплющиваются в их
  текстовую форму через raw-маркер `::`). Компаунды сохраняют
  структуру; приводятся только листовые скаляры. Повторный парсинг
  вывода даёт тот же набор `KtavString`-скаляров. Полезно для
  дампов "всё — строка" и канонического текста, дружественного к diff.
- **Поддержка top-level Array** (spec § 5.0.1, добавлена в spec 0.1.1) —
  документ, чья первая содержательная строка имеет форму элемента
  массива (голый скаляр, `:: text`, `:i 42`, `:f 3.14`, одиночные
  `{` / `[`, многострочный opener `(` / `((`) теперь парсится как
  корневой `KtavArray`, а не приводит к ошибке. Пустые документы и
  документы только с комментариями по-прежнему дают `KtavObject`.
  `Ktav.Dumps` теперь принимает top-level `KtavArray` (рендерит как
  голые элементы построчно, без обрамляющих скобок).

### Изменено

- **Подхвачен `ktav 0.3.1`** — отслеживает upstream Rust crate
  `0.3.1`. Полный diff см. в
  [`CHANGELOG` крейта `ktav`](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md).

### Spec

- подмодуль spec синхронизирован с `0.1.1` — добавляет фикстуры
  `valid/top_level_array/` (голые скаляры, типизированные элементы,
  многострочные элементы, вложенные массивы, вложенные объекты,
  комментарии и пустые строки).

NuGet-пакет: **`Ktav`**, версия 0.3.1.

>>>>> lang=zh
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


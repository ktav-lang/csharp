>>>>> lang=en
## 0.5.0 — 2026-05-28

### Breaking

- **Spec 0.5.0 — typed markers removed.** The `:i` / `:f` prefixes no
  longer exist in the format. Bare numbers are now typed automatically:
  integers parse as `KtavInteger`, decimals as `KtavFloat`. Documents
  written with `:i` / `:f` markers must be migrated (replace `port:i 8080`
  with `port: 8080`).
- **`KtavValue` type inference updated.** `KtavString` is now only
  produced for non-numeric, non-keyword bare scalars or explicit `:: text`
  raw-marker values.

### Added

- **`Ktav.EmitCanonical(KtavValue)`** — renders a value to the
  normalised canonical Ktav form defined by spec § 5.9. The output is
  idempotent: parsing it and calling `EmitCanonical` again yields
  byte-identical output. Useful for normalising config files, diffing,
  and storing a canonical source of truth.
- **`ktav_emit_canonical` C ABI export** in `crates/cabi`.

>>>>> lang=ru
## 0.5.0 — 2026-05-28

### Ломающие изменения

- **Спецификация 0.5.0 — типизированные маркеры удалены.** Префиксы
  `:i` / `:f` больше не существуют в формате. Голые числа теперь
  типизируются автоматически: целые парсятся как `KtavInteger`,
  десятичные — как `KtavFloat`. Документы, написанные с маркерами
  `:i` / `:f`, должны быть мигрированы (замените `port:i 8080`
  на `port: 8080`).
- **Вывод типов `KtavValue` обновлён.** `KtavString` теперь
  выдаётся только для нечисловых, не ключевых слов, голых скаляров
  или значений с явным raw-маркером `:: text`.

### Добавлено

- **`Ktav.EmitCanonical(KtavValue)`** — рендерит значение в
  нормализованную каноническую форму Ktav, определённую спецификацией
  § 5.9. Результат идемпотентен: парсинг вывода и повторный вызов
  `EmitCanonical` дают побайтово идентичный вывод. Полезно для
  нормализации конфигов, diff-ов и хранения канонического источника
  истины.
- **Экспорт C ABI `ktav_emit_canonical`** в `crates/cabi`.

>>>>> lang=zh
## 0.5.0 —— 2026-05-28

### 破坏性变更

- **规范 0.5.0 —— 类型化标记已移除。** 前缀 `:i` / `:f` 在格式中不再
  存在。裸数字现在自动定型：整数解析为 `KtavInteger`，小数解析为
  `KtavFloat`。使用 `:i` / `:f` 标记编写的文档必须迁移
  （将 `port:i 8080` 替换为 `port: 8080`）。
- **`KtavValue` 类型推断已更新。** `KtavString` 现在仅对非数值、
  非关键字的裸标量，或显式的 `:: text` raw 标记值产生。

### 新增

- **`Ktav.EmitCanonical(KtavValue)`** —— 将值渲染为规范 § 5.9 定义的
  规范化 canonical Ktav 形式。输出是幂等的：解析该输出并再次调用
  `EmitCanonical` 会得到字节相同的输出。适用于规范化配置文件、
  做 diff 以及保存 canonical 事实来源。
- **`ktav_emit_canonical` C ABI 导出**，位于 `crates/cabi`。


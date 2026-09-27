>>>>> lang=en
## 0.8.0 — 2026-09-28

### Added

- `Ktav.Format(string)` exposes the comment-preserving formatter; comments
  stay verbatim, blank-line normalization is idempotent, and key order is
  preserved.
- Writer failures expose structured error details through `KtavException`.
- Regression coverage captures spec 0.7 parsing behavior: quoted keys,
  literal quotes in value position, scoped Unicode escapes with lone-
  surrogate rejection, and bare pair values remaining literal.
- Conformance coverage follows the pinned spec 0.8 corpus, executes every
  fixture category, and guards against missing, empty, or unknown
  categories. Documentation examples have behavior-based regression tests.

### Changed

>>>>> lang=ru
## 0.8.0 — 2026-09-28

### Добавлено

- `Ktav.Format(string)` открывает форматтер, сохраняющий комментарии;
  комментарии остаются дословными, нормализация пустых строк идемпотентна,
  а порядок ключей сохраняется.
- Ошибки записи предоставляют структурированные сведения через
  `KtavException`.
- Регрессионные тесты фиксируют поведение разбора spec 0.7: кавыченные
  ключи, литеральные кавычки в значениях, контекстные Unicode-эскейпы с
  отвержением одиночного суррогата и литеральные значения bare-пар.
- Конформанс-тесты следуют за закреплённым корпусом spec 0.8, выполняют
  все категории фикстур и обнаруживают отсутствующие, пустые и неизвестные
  категории. Примеры документации проверяются тестами поведения.

### Изменено

>>>>> lang=zh
## 0.8.0 —— 2026-09-28

### 新增

- `Ktav.Format(string)` 提供保留注释的格式化器；注释逐字保留，空行
  规范化具有幂等性，键顺序也会保留。
- 写入错误通过 `KtavException` 提供结构化详情。
- 回归测试覆盖 spec 0.7 的解析行为：带引号键、值位置中的引号保持字面量、
  按上下文处理的 Unicode 转义（拒绝孤立代理项），以及裸键值对保持字面量。
- 一致性测试跟随固定的 spec 0.8 语料，执行全部固定值类别，并检查
  类别缺失、为空或未知的情况。文档示例有基于实际行为的回归测试。

### 变更


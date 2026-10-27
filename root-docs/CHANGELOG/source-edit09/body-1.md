>>>>> lang=en
## Unreleased — 0.9 preparation

- Add insertion of final object keys, value removal, and object-key renaming to the native and standalone pure source-edit APIs, alongside value replacement; operations fail closed when no safe lossless splice is available.
- Apply 0.9 source diagnostics and the pinned 412-case conformance corpus, including 72 diagnostic fixtures with raw UTF-8 byte locations and EOF opener spans.
- Native uses the immutable preparing-0.9 core and C ABI 2; pure remains independent of native binaries. Package versions are unchanged; this functionality is not published in 0.8.1.

### Documentation

- Expand README examples for dotted and literal-key paths, mixed arrays, String/Integer identity, multiline unfolding, byte-identical no-op and BOM/CRLF/EOF preservation. Clarify 0.9 byte diagnostics, safe refusal, ABI 2 and standalone pure usage; these additions are not part of published 0.8.1.

>>>>> lang=ru
## Ещё не выпущено — подготовка 0.9

- Добавлена вставка конечных ключей объекта, удаление значений и переименование ключей объекта в native- и самостоятельный pure-API правки исходника наряду с заменой значений; операции безопасно отказывают, если нет надёжного lossless-сплайса.
- Применена диагностика исходника 0.9 и закреплён corpus из 412 фикстур, включая 72 диагностические фикстуры с позициями сырых байтов UTF-8 и EOF spans от opener.
- Native использует immutable ядро подготовки 0.9 и C ABI 2; pure не требует native-бинарников. Версии пакетов не менялись; функционал не опубликован в 0.8.1.

### Документация

- Дополнены примеры README: dotted-пути и буквальные ключи, смешанные массивы, различие String/Integer, раскрытие многострочной замены, побайтовый no-op и сохранение BOM/CRLF/EOF. Уточнены байтовая диагностика 0.9, безопасные отказы, ABI 2 и самостоятельный pure-бэкенд; дополнения не относятся к опубликованному 0.8.1.

>>>>> lang=zh
## 尚未发布 — 0.9 准备版本

- 原生及独立纯实现的源文本编辑 API 在替换值之外，新增对象末尾键插入、值删除和对象键重命名；若无法安全无损拼接，操作会安全拒绝。
- 应用 0.9 源诊断及固定的 412 个一致性 fixture，包括 72 个诊断 fixture，检查原始 UTF-8 字节位置及从起始标记到 EOF 的区间。
- 原生后端使用不可变的 0.9 准备核心及 C ABI 2；纯实现不需要原生二进制。包版本不变，这些功能尚未包含在已发布的 0.8.1 中。

### 文档

- 扩展 README 示例：点分路径与字面键、混合数组、String/Integer 区别、多行展开、逐字节 no-op 及 BOM/CRLF/EOF 保留。明确 0.9 字节诊断、安全拒绝、ABI 2 和独立纯实现用法；这些新增功能不属于已发布的 0.8.1。


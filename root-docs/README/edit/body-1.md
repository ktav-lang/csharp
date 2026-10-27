>>>>> lang=en
## Editing source text

`Ktav.EditValue` replaces the value at a path **in the source text
itself** — a lossless splice, not a parse→mutate→serialize round trip:

```csharp
var out1 = Ktav.EditValue(src, "targets.storagehost.address", new KtavString("10.0.0.9"));
var out2 = Ktav.EditValueSegments(src, new[] { "targets", "storagehost", "address" }, new KtavString("10.0.0.9"));
```

| Member | Purpose |
| --- | --- |
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | Replace the value at a dotted textual path (`key1.key2.0.key4`; `\.` is a literal dot, `\\` a literal backslash). |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | Same, with pre-decoded literal segments (no extra unescaping). |

Guarantees:

- `Ktav.EditValue` replaces an existing value only; it does not create, remove, or rename values. Missing keys, out-of-range array indices, or descending through a scalar produce `EditNotFound`; malformed paths produce `EditInvalidPath` (`Body` is the offender's UTF-8 byte offset). `EditValue` returns `EditUnsupported` when no lossless patch is possible, such as for a noncontiguous dotted/reopen target or comments inside a structurally replaced container.
- Bytes outside an edit are preserved exactly: comments, whitespace, key spelling and order, BOM, CR/LF/CRLF, and whether a final newline is present. A no-op replacement returns the input byte-identically, even with non-canonical spelling.
- Inline containers are expanded minimally (four-space indentation, source line endings) only when a replacement would otherwise hurt readability; untouched nested branches stay inline. Readable whole inline containers stay inline; emptied block containers keep their braces.
- After each patch the result is reparsed and semantically verified, so corruption is not silently returned.

Insert creates only a new final Object key under an existing Object parent: no overwriting, implicit intermediate containers or Array insertion. Remove accepts a leaf, subtree or Array element and never prunes empty ancestors. Rename takes one literal `newKey`, not a path, preserves member order and value, and returns the input byte-identically for the same key. Collisions return `EditAlreadyExists`; missing targets or parents return `EditNotFound`. Native and standalone Pure follow this contract and are checked independently in CI.

| Member | Purpose |
| --- | --- |
| `Ktav.InsertValue(string src, string path, KtavValue value) -> string` | Insert a new final object key; textual paths use the same escaping rules. Supported; Pure may return `EditUnsupported` when it cannot make a lossless splice. |
| `Ktav.InsertValueSegments(string src, IReadOnlyList<string> segments, KtavValue value) -> string` | Same insertion using pre-decoded literal segments. Supported; Pure may return `EditUnsupported` when it cannot make a lossless splice. |
| `Ktav.RemoveValue` / `Ktav.RemoveValueSegments` | Remove a value. Supported by Native and Pure; Pure may return `EditUnsupported` for targets with comments or noncontiguous dotted/reopen targets. |
| `Ktav.RenameKey` / `Ktav.RenameKeySegments` | Rename an object key. Supported by Native and Pure; Pure may return `EditUnsupported` for array elements or noncontiguous dotted/reopen targets. |

>>>>> lang=ru
## Редактирование исходного текста

`Ktav.EditValue` заменяет значение по пути **в самом исходном тексте** —
это lossless-сплайс, а не разбор→мутация→сериализация всего документа:

```csharp
var out1 = Ktav.EditValue(src, "targets.storagehost.address", new KtavString("10.0.0.9"));
var out2 = Ktav.EditValueSegments(src, new[] { "targets", "storagehost", "address" }, new KtavString("10.0.0.9"));
```

| Член | Назначение |
| --- | --- |
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | Заменить значение по текстовому пути с точками (`key1.key2.0.key4`; `\.` — литеральная точка, `\\` — литеральный обратный слэш). |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | То же, но с уже декодированными литеральными сегментами (без повторной распаковки escape-последовательностей). |

Гарантии:

- `Ktav.EditValue` заменяет только существующее значение; она не создаёт, не удаляет и не переименовывает значения. Отсутствующий ключ, индекс массива вне диапазона или спуск сквозь скаляр дают `EditNotFound`; некорректный путь — `EditInvalidPath` (`Body` содержит UTF-8-байтовый offset проблемного места). `EditValue` возвращает `EditUnsupported`, если lossless-патч невозможен, например для noncontiguous-цели из dotted/reopen-строк или при комментариях внутри структурно заменяемого контейнера.
- Байты вне правки сохраняются точно: комментарии, пробелы, написание и порядок ключей, BOM, CR/LF/CRLF и наличие или отсутствие финального перевода строки. No-op-замена возвращает вход byte-identical даже при неканоничном написании.
- Inline-контейнеры минимально раскрываются (отступ 4 пробела, переводы строк как в исходнике), только если иначе пострадает читаемость; нетронутые вложенные ветви остаются inline. Читаемые inline-контейнеры целиком остаются inline; опустевшие блочные контейнеры сохраняют скобки.
- После каждого патча результат заново разбирается и семантически проверяется, поэтому повреждённый документ не возвращается молча.

Insert создаёт только новый конечный ключ Object у существующего родителя Object: без перезаписи, неявного создания промежуточных контейнеров и вставки в Array. Remove удаляет лист, поддерево или элемент Array и не удаляет пустых предков. Rename принимает один литеральный `newKey`, а не путь, сохраняет порядок и значение пары и возвращает исходник побайтно неизменным при том же имени. Коллизии дают `EditAlreadyExists`, отсутствующая цель или родитель — `EditNotFound`. Native и самостоятельный Pure следуют этому контракту и независимо проверяются в CI.

| Член | Назначение |
| --- | --- |
| `Ktav.InsertValue(string src, string path, KtavValue value) -> string` | Вставить новый конечный ключ объекта; текстовые пути используют те же правила экранирования. Поддерживается; Pure может вернуть `EditUnsupported`, если не удаётся выполнить lossless-сплайс. |
| `Ktav.InsertValueSegments(string src, IReadOnlyList<string> segments, KtavValue value) -> string` | Та же вставка с уже декодированными литеральными сегментами. Поддерживается; Pure может вернуть `EditUnsupported`, если не удаётся выполнить lossless-сплайс. |
| `Ktav.RemoveValue` / `Ktav.RemoveValueSegments` | Удалить значение. Поддерживается Native и Pure; Pure может вернуть `EditUnsupported` для целей с комментариями или noncontiguous-целей dotted/reopen. |
| `Ktav.RenameKey` / `Ktav.RenameKeySegments` | Переименовать ключ объекта. Поддерживается Native и Pure; Pure может вернуть `EditUnsupported` для элементов массива или noncontiguous-целей dotted/reopen. |

>>>>> lang=zh
## 编辑源文本

`Ktav.EditValue` 直接**在源文本中**替换路径上的值 —— 这是无损拼接
（splice），而不是“解析→修改→再序列化”的往返：

```csharp
var out1 = Ktav.EditValue(src, "targets.storagehost.address", new KtavString("10.0.0.9"));
var out2 = Ktav.EditValueSegments(src, new[] { "targets", "storagehost", "address" }, new KtavString("10.0.0.9"));
```

| 成员 | 用途 |
| --- | --- |
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | 按点分文本路径替换值（`key1.key2.0.key4`；`\.` 表示字面点号，`\\` 表示字面反斜杠）。 |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | 同上，但传入已解码的字面段（不再额外反转义）。 |

保证：


- `Ktav.EditValue` 只替换已有值；它不会创建、删除或重命名值。缺失键、数组索引越界或穿过标量下钻会产生 `EditNotFound`；非法路径会产生 `EditInvalidPath`（`Body` 是问题位置的 UTF-8 字节偏移）。如果无法进行无损补丁，例如目标由 dotted/reopen 行组成而不连续，或结构替换的容器内含注释，`EditValue` 会返回 `EditUnsupported`。
- 编辑范围之外的字节原样保留：注释、空白、键的写法与顺序、BOM、CR/LF/CRLF，以及是否存在末尾换行。即使写法不规范，无变化的替换也会按字节原样返回输入。
- 只有当替换会损害可读性时才最小化展开 inline 容器（4 空格缩进，沿用源文件换行）；未触及的嵌套分支保持 inline。可读的整体 inline 容器保持 inline；被清空的块容器保留花括号。
- 每次补丁后都会重新解析并进行语义验证，因此不会悄悄返回损坏的文档。

Insert 只在已存在的 Object 父级下创建新的末尾键：不覆盖、不隐式创建中间容器，也不向 Array 插入。Remove 可删除叶值、子树或 Array 元素，但不会清除空祖先。Rename 接受单个字面 `newKey` 而非路径，保留成员顺序和值；名称不变时逐字节返回原文。键冲突返回 `EditAlreadyExists`，目标或父级不存在返回 `EditNotFound`。Native 和独立 Pure 遵循此契约，并在 CI 中分别验证。

| 成员 | 用途 |
| --- | --- |
| `Ktav.InsertValue(string src, string path, KtavValue value) -> string` | 在对象末尾插入新键；文本路径使用相同的转义规则。此操作受支持；若无法无损拼接，Pure 可能返回 `EditUnsupported`。 |
| `Ktav.InsertValueSegments(string src, IReadOnlyList<string> segments, KtavValue value) -> string` | 使用已解码的字面段执行相同的插入。此操作受支持；若无法无损拼接，Pure 可能返回 `EditUnsupported`。 |
| `Ktav.RemoveValue` / `Ktav.RemoveValueSegments` | 删除值。Native 和 Pure 均支持；对于含注释或 noncontiguous dotted/reopen 目标，Pure 可能返回 `EditUnsupported`。 |
| `Ktav.RenameKey` / `Ktav.RenameKeySegments` | 重命名对象键。Native 和 Pure 均支持；对于数组元素或 noncontiguous dotted/reopen 目标，Pure 可能返回 `EditUnsupported`。 |


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

- Only *replacement of an existing value* — no create/remove/rename.
  A missing key, an out-of-bounds index, or descending through a scalar
  is `EditNotFound`; a malformed path is `EditInvalidPath` (its
  `Body` is the UTF-8 byte offset of the offender); when no lossless
  patch exists — a noncontiguous dotted/reopen target, comments inside a
  structurally replaced container — the call refuses with
  `EditUnsupported`.
- Bytes outside the edit are preserved exactly: comments, whitespace,
  key spelling and order, BOM, CR/LF/CRLF, and the presence or absence
  of a final newline. A no-op replacement returns the input
  byte-identical, even for non-canonical spellings.
- Inline containers are expanded minimally (four-space indent, source
  line endings) only when a replacement would otherwise hurt
  readability; untouched nested branches stay inline. Whole inline
  containers stay inline when readable; emptied block containers keep their braces.
- After every patch the result is re-parsed and semantically verified,
  so a silent corruption is never returned. Both backends (native and
  pure) implement the same behaviour and are cross-checked in CI.

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
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | Заменить значение по текстовому пути с точками (`key1.key2.0.key4`; `\.` — literal точка, `\\` — literal backslash). |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | То же, но с уже декодированными сегментами (без повторного unescape). |

Гарантии:

- Только *замена существующего значения* — без create/remove/rename.
  Отсутствующий ключ, выход за границы массива или спуск сквозь скаляр —
  `EditNotFound`; некорректный путь — `EditInvalidPath` (в `Body` —
  UTF-8 byte offset проблемного места); если lossless-патч невозможен —
  noncontiguous-цель из dotted/reopen-строк, комментарии внутри
  структурно заменяемого контейнера — отказ `EditUnsupported`.
- Байты вне правки сохраняются точно: комментарии, пробелы, написание и
  порядок ключей, BOM, CR/LF/CRLF и наличие или отсутствие финального
  перевода строки. No-op-замена возвращает вход byte-identical, даже
  при неканоничном написании.
- Inline-контейнеры раскрываются минимально (отступ 4 пробела, EOL
  исходника) только когда замена иначе ухудшает читаемость; нетронутые
  вложенные ветки остаются inline. Целые inline-контейнеры остаются
  inline, пока это читаемо; опустевшие блочные сохраняют скобки.
- После каждого патча результат заново разбирается и семантически
  проверяется, поэтому молчаливая порча документа невозможна. Оба
  бэкенда (native и pure) ведут себя одинаково и сверяются в CI.

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
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | 同上，但传入已解码的字面段（不再做额外反转义）。 |

保证：

- 只支持*替换已有值* —— 不做创建/删除/重命名。缺失键、数组越界或
  穿过标量下钻是 `EditNotFound`；路径非法是 `EditInvalidPath`
  （`Body` 是出问题的 UTF-8 字节偏移）；当不存在无损补丁时 ——
  由 dotted/reopen 行拼成的 noncontiguous 目标、结构化替换容器内部的
  注释 —— 调用会以 `EditUnsupported` 拒绝。
- 编辑范围之外的字节原样保留：注释、空白、键的写法与顺序、BOM、
  CR/LF/CRLF 以及末尾换行的有无。无变化（no-op）的替换会按字节原样
  返回输入，即使写法不规范。
- 只有当替换会损害可读性时才最小化地展开 inline 容器（4 空格缩进、
  沿用源文件换行）；未触及的嵌套分支保持 inline。整体 inline 容器
  在可读时保持 inline；被清空的块容器保留花括号。
- 每次补丁后都会重新解析并做语义校验，因此不会悄悄返回损坏的文档。
  两个后端（native 与 pure）行为一致，并在 CI 中交叉验证。


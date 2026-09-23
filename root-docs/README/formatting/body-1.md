>>>>> lang=en
## Formatting — canonical spelling, comments kept

`Format` and `EmitCanonical` are different operations:

- **`EmitCanonical`** takes a `KtavValue` and writes the canonical form.
  A value carries no comments, so none can survive.
- **`Format`** takes source *text* and rewrites its spelling while
  **preserving every comment verbatim** (spec § 3.4: a comment owns a
  whole line). Key order is never changed — spec § 5.9 has no sorting
  rule.

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

A run of two or more blank lines collapses to one, and blank padding
immediately inside a bracket is dropped, which makes formatting a fixed
point: `Format(Format(x)) == Format(x)`. For a document with no comments
and no blank lines, the output equals `EmitCanonical(Loads(src))`.

>>>>> lang=ru
## Форматирование — каноническое написание, комментарии сохраняются

`Format` и `EmitCanonical` — разные операции:

- **`EmitCanonical`** принимает `KtavValue` и пишет каноническую форму.
  Значение не несёт комментариев, поэтому сохранить их нечем.
- **`Format`** принимает исходный *текст* и переписывает его написание,
  **сохраняя каждый комментарий дословно** (spec § 3.4: комментарий
  занимает строку целиком). Порядок ключей не меняется — в spec § 5.9
  нет правила сортировки.

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

Серия из двух и более пустых строк сворачивается в одну, а пустые строки
прямо внутри скобок удаляются, поэтому форматирование является
неподвижной точкой: `Format(Format(x)) == Format(x)`. Для документа без
комментариев и пустых строк результат совпадает с `EmitCanonical(Loads(src))`.

>>>>> lang=zh
## 格式化 —— 规范写法，保留注释

`Format` 和 `EmitCanonical` 是不同的操作：

- **`EmitCanonical`** 接受 `KtavValue`，写出规范形式。值不携带注释，
  因此没有注释可供保留。
- **`Format`** 接受源*文本*，重写其写法，同时**逐字保留每条注释**
  （spec § 3.4：注释独占一整行）。键顺序绝不改变 —— spec § 5.9 中没有
  排序规则。

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

连续两个及以上空行会折叠成一个，紧贴括号内侧的空行填充会被丢弃，
因此格式化是一个不动点：`Format(Format(x)) == Format(x)`。对于没有注释、
没有空行的文档，输出等同于 `EmitCanonical(Loads(src))`。


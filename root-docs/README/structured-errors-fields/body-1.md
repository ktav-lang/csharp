>>>>> lang=en
| Member | Meaning |
| --- | --- |
| `Message` | Human-readable rendering of the failure. |
| `Error` | Structured error class — `DuplicateKey`, `Unrepresentable`, `Message`. |
| `Reason` | Writer-time reason code (spec § 5.9.0) such as `NonFiniteFloat`; `null` for parse errors. |
| `Line` | 1-based source line; `null` when not applicable. |
| `LineText` | Text of the offending line. |
| `Span` | `KtavErrorSpan?` — byte offsets into the UTF-8 source. |
| `Path` | Exact decoded key segments. |
| `Body` | The offending value as written. |
| `Canonical` | What the canonical form would have been. |
| `SpecSection` | The clause violated, e.g. `§3.6/§5.2`. |

Two details that are easy to get wrong:

- **`Span` holds byte offsets into UTF-8**, not UTF-16 code units, which
  is what .NET strings are indexed by. Convert before handing them to
  anything that expects `string` indices, or to an LSP client that has
  not negotiated `positionEncoding: "utf-8"`.
- **`Path` is a list of segments, never a joined string.** A key
  literally named `a.b` is one segment and cannot be confused with a
  two-segment path.

>>>>> lang=ru
| Член | Значение |
| --- | --- |
| `Message` | Человекочитаемое представление сбоя. |
| `Error` | Класс структурированной ошибки — `DuplicateKey`, `Unrepresentable`, `Message`. |
| `Reason` | Код причины на стороне записи (spec § 5.9.0), например `NonFiniteFloat`; `null` для ошибок разбора. |
| `Line` | Строка исходного текста, счёт с 1; `null`, если неприменимо. |
| `LineText` | Текст проблемной строки. |
| `Span` | `KtavErrorSpan?` — байтовые смещения в UTF-8-исходнике. |
| `Path` | Точные декодированные сегменты ключа. |
| `Body` | Проблемное значение в том виде, как оно записано. |
| `Canonical` | Какой была бы каноническая форма. |
| `SpecSection` | Нарушенный пункт спецификации, например `§3.6/§5.2`. |

Две детали, в которых легко ошибиться:

- **`Span` хранит байтовые смещения в UTF-8**, а не кодовые единицы
  UTF-16, которыми индексируются строки .NET. Преобразуйте их, прежде
  чем передавать туда, где ожидаются индексы `string`, или LSP-клиенту,
  который не договорился о `positionEncoding: "utf-8"`.
- **`Path` — список сегментов, а не склеенная строка.** Ключ, буквально
  названный `a.b`, — это один сегмент, и его нельзя спутать с путём из
  двух сегментов.

>>>>> lang=zh
| 成员 | 含义 |
| --- | --- |
| `Message` | 失败的人类可读表述。 |
| `Error` | 结构化错误类别 —— `DuplicateKey`、`Unrepresentable`、`Message`。 |
| `Reason` | 写入侧的原因码（spec § 5.9.0），例如 `NonFiniteFloat`；解析错误时为 `null`。 |
| `Line` | 从 1 起算的源行号；不适用时为 `null`。 |
| `LineText` | 出错那一行的文本。 |
| `Span` | `KtavErrorSpan?` —— UTF-8 源文本中的字节偏移。 |
| `Path` | 精确解码后的键段。 |
| `Body` | 出错的值，按原样写出。 |
| `Canonical` | 规范形式本应是什么。 |
| `SpecSection` | 被违反的条款，如 `§3.6/§5.2`。 |

两处容易弄错的细节：

- **`Span` 保存的是 UTF-8 字节偏移**，而不是 .NET 字符串所用的 UTF-16
  码元索引。在交给任何期待 `string` 索引的代码，或尚未协商
  `positionEncoding: "utf-8"` 的 LSP 客户端之前，请先转换。
- **`Path` 是键段列表，绝不是拼接后的字符串。** 字面名为 `a.b` 的键
  是**一个**段，不会与两段路径混淆。


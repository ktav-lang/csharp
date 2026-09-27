>>>>> lang=en
## Type mapping

Mirrors the Rust crate's `Value` enum — one record per Ktav primitive,
no lossy coercions:

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| bare integer in the core's signed 64-bit range | `KtavInteger` (text form — `ToBigInteger()` / `ToInt64()`) |
| bare decimal     | `KtavFloat` (text form — `ToDouble()`)                  |
| other scalar     | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (key insertion order preserved)            |

An integer outside the core's signed 64-bit range loads as `KtavString`,
not `KtavInteger`. `KtavInteger` and `KtavFloat` expose their stored text,
but this does not promise arbitrary-precision Ktav numbers or preservation
of the source's decimal spelling: for example, `1.10` loads as `1.1`.
The public records can be constructed with other text, but native writing
still enforces the core spec domain.

>>>>> lang=ru
## Отображение типов

Повторяет enum `Value` из Rust-крейта — по одной записи на каждый примитив
Ktav, без потерьных приведений:

| Ktav             | вариант `KtavValue`                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| голое целое в диапазоне знакового 64-битного числа | `KtavInteger` (текстовая форма — `ToBigInteger()` / `ToInt64()`) |
| голое десятичное | `KtavFloat` (текстовая форма — `ToDouble()`)            |
| прочий скаляр    | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (порядок вставки сохраняется)              |

Целое вне диапазона знакового 64-битного числа ядра загружается как
`KtavString`, а не `KtavInteger`. `KtavInteger` и `KtavFloat` предоставляют
хранимый текст, но это не означает поддержку чисел произвольной точности
или сохранение исходной записи дроби: например, `1.10` загружается как
`1.1`. Публичные record-типы можно создать с другим текстом, но нативная
запись всё равно соблюдает числовую область спецификации ядра.

>>>>> lang=zh
## 类型映射

与 Rust crate 的 `Value` 枚举一致 —— Ktav 的每个原语对应一个 record，
没有有损转换：

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| 核心有符号 64 位范围内的裸整数 | `KtavInteger`（文本形式 —— `ToBigInteger()` / `ToInt64()`） |
| 裸小数           | `KtavFloat`（文本形式 —— `ToDouble()`）                   |
| 其他标量         | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`（保留插入顺序）                             |

超出核心有符号 64 位范围的整数会加载为 `KtavString`，而不是
`KtavInteger`。`KtavInteger` 与 `KtavFloat` 会公开其保存的文本，但这
不代表 Ktav 数字支持任意精度，也不保证保留源十进制写法：例如
`1.10` 会加载为 `1.1`。公开 record 类型可以用其他文本构造，但原生
写入仍会遵守核心规范的数值范围。


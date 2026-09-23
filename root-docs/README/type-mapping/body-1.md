>>>>> lang=en
## Type mapping

Mirrors the Rust crate's `Value` enum — one record per Ktav primitive,
no lossy coercions:

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| bare integer     | `KtavInteger` (text form — `ToBigInteger()` / `ToInt64()`) |
| bare decimal     | `KtavFloat` (text form — `ToDouble()`)                  |
| other scalar     | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (key insertion order preserved)            |

Integers and floats are held as **text** so arbitrary precision
(digits beyond `long`) and exact decimal round-trip are preserved byte
for byte across parse / render cycles.

>>>>> lang=ru
## Отображение типов

Повторяет enum `Value` из Rust-крейта — по одной записи на каждый примитив
Ktav, без потерьных приведений:

| Ktav             | вариант `KtavValue`                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| голое целое      | `KtavInteger` (текстовая форма — `ToBigInteger()` / `ToInt64()`) |
| голое десятичное | `KtavFloat` (текстовая форма — `ToDouble()`)            |
| прочий скаляр    | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (порядок вставки сохраняется)              |

Целые и дробные числа хранятся **как текст**, поэтому произвольная
точность (количество цифр сверх `long`) и точный десятичный round-trip
побайтово сохраняются между циклами разбора и вывода.

>>>>> lang=zh
## 类型映射

与 Rust crate 的 `Value` 枚举一致 —— Ktav 的每个原语对应一个 record，
没有有损转换：

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| 裸整数           | `KtavInteger`（文本形式 —— `ToBigInteger()` / `ToInt64()`） |
| 裸小数           | `KtavFloat`（文本形式 —— `ToDouble()`）                   |
| 其他标量         | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`（保留插入顺序）                             |

整数与浮点数以 **文本** 形式保存，因此任意精度（超出 `long` 的位数）
与十进制的精确表示都能在解析 / 渲染之间逐字节保留。


>>>>> lang=en
## Key escaping

Since spec 0.6.4 a literal `.` or `:` inside a key segment is written
with a backslash:

```text
a\.b: v
a\:b: v
x.y\.z: v
```

These parse respectively to the single keys `a.b` and `a:b`, and to the
nested keys `x` then `y.z`. Keep explanations outside Ktav examples: inline
`//` text is value content, not a comment. Ktav comments use a whole line
starting with `##`.

A literal backslash in a key is `\\`.

>>>>> lang=ru
## Экранирование в ключах

Начиная со spec 0.6.4, литеральные `.` или `:` внутри сегмента ключа
записываются с обратной косой чертой:

```text
a\.b: v
a\:b: v
x.y\.z: v
```

Они разбираются соответственно в одиночные ключи `a.b` и `a:b`, а также
во вложенные ключи `x`, затем `y.z`. Пояснения следует размещать вне
примеров Ktav: текст `//` в строке является содержимым значения, а не
комментарием. Комментарий Ktav занимает отдельную строку и начинается с `##`.

Литеральная обратная косая черта в ключе записывается как `\\`.

>>>>> lang=zh
## 键的转义

自 spec 0.6.4 起，键段内的字面量 `.` 或 `:` 以反斜杠书写：

```text
a\.b: v
a\:b: v
x.y\.z: v
```

它们分别解析为单段键 `a.b` 和 `a:b`，以及依次为 `x`、`y.z` 的嵌套键。
请将说明写在 Ktav 示例之外：行内 `//` 文本是值内容，不是注释。Ktav
注释必须独占一行并以 `##` 开头。

键中的字面量反斜杠写作 `\\`。


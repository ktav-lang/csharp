>>>>> lang=en
Result:

```ktav
a: [
    ((
x
y
    ))
    {keep: yes}
]
```

#### 6. No-op preserves noncanonical spelling

The Integer value is already 1. Equality returns the original bytes, including `+1` and the original separator spacing; readability thresholds are bypassed.

```csharp
var source = "n  : +1\n";
var updated = KtavApi.EditValue(source, "n", new KtavInteger("1"), KtavBackend.Pure);
if (updated != source) throw new System.InvalidOperationException("no-op changed source");
```

Result:

```ktav
n  : +1
```

>>>>> lang=ru
Результат:

```ktav
a: [
    ((
x
y
    ))
    {keep: yes}
]
```

#### 6. No-op сохраняет неканоническое написание

Значение Integer уже равно 1. Проверка равенства возвращает исходные байты, включая `+1` и пробелы разделителя; критерии читаемости не применяются.

```csharp
var source = "n  : +1\n";
var updated = KtavApi.EditValue(source, "n", new KtavInteger("1"), KtavBackend.Pure);
if (updated != source) throw new System.InvalidOperationException("no-op changed source");
```

Результат:

```ktav
n  : +1
```

>>>>> lang=zh
结果:

```ktav
a: [
    ((
x
y
    ))
    {keep: yes}
]
```

#### 6. 无变化时保留非规范拼写

Integer 的值已经是 1。相等判断直接返回原始字节，包括 `+1` 及分隔符周围的空白，不应用可读性阈值。

```csharp
var source = "n  : +1\n";
var updated = KtavApi.EditValue(source, "n", new KtavInteger("1"), KtavBackend.Pure);
if (updated != source) throw new System.InvalidOperationException("no-op changed source");
```

结果:

```ktav
n  : +1
```


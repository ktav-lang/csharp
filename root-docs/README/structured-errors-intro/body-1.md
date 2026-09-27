>>>>> lang=en
## Structured errors

Native parse, format, and render failures are reported as
`KtavException`. Beyond the message it carries the native structured
error envelope, so a tool can act on the fields instead of parsing prose.
This does not make every failure a `KtavException`: null arguments use
`ArgumentNullException`, host-side argument validation uses standard .NET
argument exceptions, and native library loading can raise loader exceptions:

```csharp
try { global::Ktav.Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

>>>>> lang=ru
## Структурированные ошибки

Ошибки разбора, форматирования и вывода на нативной стороне сообщаются
через `KtavException`. Помимо сообщения оно несёт структурированный
нативный конверт ошибки, поэтому инструмент может работать с полями, а не
разбирать прозу. Не всякий сбой является `KtavException`: для null-
аргументов используется `ArgumentNullException`, проверка аргументов на
стороне .NET — стандартные исключения аргументов, а загрузка нативной
библиотеки может выбросить исключения загрузчика:

```csharp
try { global::Ktav.Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

>>>>> lang=zh
## 结构化错误

原生解析、格式化和渲染失败会报告为 `KtavException`。除了消息之外，
它还携带原生结构化错误信封，因此工具可以直接使用字段，而不必解析
文本。但并非所有失败都是 `KtavException`：null 参数使用
`ArgumentNullException`，宿主端参数校验使用标准 .NET 参数异常，而
原生库加载可能抛出加载器异常：

```csharp
try { global::Ktav.Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```


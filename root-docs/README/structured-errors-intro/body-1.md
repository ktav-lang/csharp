>>>>> lang=en
## Structured errors

`KtavException` is thrown on any parse or render failure. Beyond the
message it carries the same structured envelope every Ktav binding
carries, so a tool can act on the fields instead of parsing prose:

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

>>>>> lang=ru
## Структурированные ошибки

`KtavException` бросается при любой ошибке разбора или вывода. Помимо
сообщения оно несёт тот же структурированный конверт, что и все остальные
биндинги Ktav, поэтому инструмент может опираться на эти поля, а не
разбирать прозу:

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

>>>>> lang=zh
## 结构化错误

解析或渲染失败时会抛出 `KtavException`。除了消息之外，它还携带与所有
Ktav 绑定相同的结构化信封，因此工具可以直接使用这些字段，而不必解析
人类可读的文本：

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```


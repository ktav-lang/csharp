>>>>> lang=en
### 2. Don't reinvent the format in the bindings

These C# bindings are deliberately a thin wrapper. Parser / format
behaviour belongs in the Rust crate
([`ktav-lang/rust`](https://github.com/ktav-lang/rust)) — changing it
there updates every language binding at once. Only **C#-specific
ergonomics** (exception types, KtavObjectMap, factory methods) belong
in this repo.

If your change requires a format change, start a discussion in
[`ktav-lang/spec`](https://github.com/ktav-lang/spec) first.

>>>>> lang=ru
### 2. Не изобретайте формат заново в биндингах

Эти C#-биндинги намеренно остаются тонкой обёрткой. Поведение парсера и
формата определено в Rust-крейте
([`ktav-lang/rust`](https://github.com/ktav-lang/rust)) — изменение его
там обновляет все языковые биндинги сразу. В этом репозитории только
**C#-специфичная эргономика** (типы исключений, KtavObjectMap,
фабричные методы).

Если ваше изменение требует изменения формата, начните с обсуждения в
[`ktav-lang/spec`](https://github.com/ktav-lang/spec).

>>>>> lang=zh
### 2. 不要在绑定中重新发明格式

这些 C# 绑定刻意保持为薄封装。解析器 / 格式行为属于 Rust crate
（[`ktav-lang/rust`](https://github.com/ktav-lang/rust)）—— 在那里
修改一次，就会同时更新所有语言绑定。本仓库只放 **C# 特定的人体工程学
设计**（异常类型、KtavObjectMap、工厂方法）。

如果你的改动需要格式变更，请先在
[`ktav-lang/spec`](https://github.com/ktav-lang/spec) 发起讨论。


>>>>> lang=en
### Test

```
dotnet test tests/Ktav.Tests/Ktav.Tests.csproj -c Release
```

The `SpecConformance` module runs the cross-language fixture suite
from `ktav-lang/spec`, reading `spec/versions/0.8/tests`. The submodule
must be checked out; a missing corpus is a test failure, not a skip.

>>>>> lang=ru
### Тесты

```
dotnet test tests/Ktav.Tests/Ktav.Tests.csproj -c Release
```

Модуль `SpecConformance` запускает межъязыковой набор фикстур из
`ktav-lang/spec`, читая `spec/versions/0.8/tests`. Сабмодуль должен быть
выкачан; отсутствие корпуса приводит к падению теста, а не к пропуску.

>>>>> lang=zh
### 测试

```
dotnet test tests/Ktav.Tests/Ktav.Tests.csproj -c Release
```

`SpecConformance` 模块运行来自 `ktav-lang/spec` 的跨语言固定值测试套件，
读取 `spec/versions/0.8/tests`。子模块必须已检出；语料缺失会导致测试
失败，而不会跳过。


>>>>> lang=en
### Test

```
dotnet test -c Release
```

The `SpecConformance` module runs the cross-language fixture suite
from `ktav-lang/spec`. It resolves the spec directory via:

1. `KTAV_SPEC_DIR` environment variable, if set.
2. `<repo>/spec` (the git submodule).
3. `<repo>/../spec` (sibling fallback).

When none resolves, conformance tests **skip** rather than fail.

>>>>> lang=ru
### Тесты

```
dotnet test -c Release
```

Модуль `SpecConformance` запускает межъязыковой набор фикстур из
`ktav-lang/spec`. Он находит каталог spec через:

1. Переменную окружения `KTAV_SPEC_DIR`, если задана.
2. `<repo>/spec` (git submodule).
3. `<repo>/../spec` (соседний fallback).

Если ничего не найдено, conformance-тесты **пропускаются**, а не падают.

>>>>> lang=zh
### 测试

```
dotnet test -c Release
```

`SpecConformance` 模块运行来自 `ktav-lang/spec` 的跨语言固件测试套件。
它通过以下方式解析 spec 目录：

1. 环境变量 `KTAV_SPEC_DIR`（如已设置）。
2. `<repo>/spec`（git submodule）。
3. `<repo>/../spec`（相邻仓库回退）。

都无法解析时，一致性测试会**跳过**而不是失败。


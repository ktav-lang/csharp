>>>>> lang=en
### Source-edit cookbook

These examples describe the unreleased 0.9 API, not the published 0.8.1 package. They illustrate replacement of existing values; insertion, removal and key renaming are documented in the source-edit API section. Each snippet is independent; Rust `?` assumes an enclosing `Result`-returning function. Go examples panic only for these fixed, known-valid inputs; applications should handle the returned error.

The binding examples use the standalone pure backend, without a native binary. Native entry points accept the same paths and values: `ktav.EditValue` (Go), `Ktav.editValue` (Java), `KtavApi.EditValue(..., KtavBackend.Native)` (C#), `Ktav::editValue` (PHP), the package's native/FFI exports (JS), and `ktav.edit_value` (Python). C-ABI backends require ABI 2; PyO3/N-API/WASM require the matching preparing-0.9 core. Bun/Deno FFI calls return promises: use `await`; pure/Node/WASM examples shown here are synchronous.

```csharp
using Ktav;
using KtavApi = global::Ktav.Ktav;
```

#### 1. Dotted deployment settings

Only the address changes; the comment, dotted form and neighbouring user entry remain byte-for-byte unchanged.

```csharp
var source = "## deployment\ntargets.storagehost.address: old\ntargets.storagehost.user: mcp-agent\n";
var updated = KtavApi.EditValue(source, "targets.storagehost.address", new KtavString("192.168.0.88:22"), KtavBackend.Pure);
```

Result:

```ktav
## deployment
targets.storagehost.address: 192.168.0.88:22
targets.storagehost.user: mcp-agent
```

>>>>> lang=ru
### Практические примеры правки исходника

Примеры относятся к ещё не выпущенному API 0.9, а не к опубликованному пакету 0.8.1. Они показывают замену существующих значений; вставка, удаление и переименование ключей описаны в разделе API правки исходника. Фрагменты независимы; Rust `?` предполагает внешнюю функцию с результатом `Result`. В Go panic используется только для фиксированных корректных данных примера; приложение должно обработать возвращённую ошибку.

Примеры привязок используют самостоятельный pure-бэкенд без native-бинарника. Нативные точки входа принимают те же пути и значения: `ktav.EditValue` (Go), `Ktav.editValue` (Java), `KtavApi.EditValue(..., KtavBackend.Native)` (C#), `Ktav::editValue` (PHP), native/FFI-экспорты пакета (JS), `ktav.edit_value` (Python). C ABI требует версию 2; PyO3/N-API/WASM — совместимое ядро подготовки 0.9. FFI-вызовы Bun/Deno возвращают promise и требуют `await`; показанные pure/Node/WASM-варианты синхронны.

```csharp
using Ktav;
using KtavApi = global::Ktav.Ktav;
```

#### 1. Настройка развёртывания через dotted-ключ

Меняется только адрес; комментарий, dotted-форма и соседнее поле пользователя сохраняются байт-в-байт.

```csharp
var source = "## deployment\ntargets.storagehost.address: old\ntargets.storagehost.user: mcp-agent\n";
var updated = KtavApi.EditValue(source, "targets.storagehost.address", new KtavString("192.168.0.88:22"), KtavBackend.Pure);
```

Результат:

```ktav
## deployment
targets.storagehost.address: 192.168.0.88:22
targets.storagehost.user: mcp-agent
```

>>>>> lang=zh
### 源文本编辑示例

以下示例适用于尚未发布的 0.9 API，而非已发布的 0.8.1 包。这些示例展示已有值的替换；插入、删除及键重命名见源文本编辑 API 一节。各代码片段相互独立；Rust 的 `?` 假定外层函数返回 `Result`。Go 示例仅针对固定且有效的输入使用 panic；应用程序应处理返回的错误。

绑定示例使用无需原生二进制的独立纯实现。原生入口接收相同的路径和值：Go 的 `ktav.EditValue`、Java 的 `Ktav.editValue`、C# 的 `KtavApi.EditValue(..., KtavBackend.Native)`、PHP 的 `Ktav::editValue`、JS 包的 native/FFI 导出及 Python 的 `ktav.edit_value`。C ABI 后端要求 ABI 2；PyO3/N-API/WASM 要求兼容的 0.9 准备核心。Bun/Deno FFI 返回 promise，需要 `await`；此处 pure/Node/WASM 示例为同步调用。

```csharp
using Ktav;
using KtavApi = global::Ktav.Ktav;
```

#### 1. 通过点分键修改部署配置

只修改地址；注释、点分形式及相邻的用户字段逐字节保持不变。

```csharp
var source = "## deployment\ntargets.storagehost.address: old\ntargets.storagehost.user: mcp-agent\n";
var updated = KtavApi.EditValue(source, "targets.storagehost.address", new KtavString("192.168.0.88:22"), KtavBackend.Pure);
```

结果:

```ktav
## deployment
targets.storagehost.address: 192.168.0.88:22
targets.storagehost.user: mcp-agent
```


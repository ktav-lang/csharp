>>>>> lang=en
## The pure backend

Since 0.8.1 the library ships a complete managed parser, writer, canonical emitter and comment-preserving formatter. It is intended to match native output and error envelopes; CI conformance checks parity. Pure does not load, download, or invoke native code.

Select globally with `Ktav.UsePureBackend()` or pass `KtavBackend.Pure` per call (which takes precedence).

```csharp
var value = Ktav.Loads(src, KtavBackend.Pure);
var text = Ktav.Dumps(value, KtavBackend.Pure);
var canonical = Ktav.EmitCanonical(value, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

`KTAV_BACKEND=pure` sets the initial default once at startup. Per-call selection takes precedence. Default changes are atomic. `NativeVersion()` still requires native. CI checks pure error parity; spans use original UTF-8 byte offsets, including BOM and line endings.

Run Pure with the ready `net8.0` `Ktav.dll` and a compatible installed .NET 8 `Microsoft.NETCore.App` framework. A later major version alone does not guarantee a net8 consumer will run; that requires host-configured supported roll-forward. Framework APIs such as System.Text.Json and Memory are available on net8, but pure execution does not use native JSON wire/envelope helpers. For standalone use, copy the DLL and reference it via assembly `Reference`/`HintPath`, without a Ktav `PackageReference`. Pure does not load, download, invoke, or fall back to native, and needs no network.

The multi-target NuGet package is not dependency-free: `netstandard2.0` retains `System.Memory` and `System.Text.Json` PackageReferences for compatibility and native wire/error-envelope support. Native netstandard support remains. SDK, NuGet restore/build, tests, and documentation tools are development tooling, not Pure runtime dependencies.

>>>>> lang=ru
## Чистый бэкенд

С версии 0.8.1 библиотека включает управляемые parser, writer, canonical emitter и форматтер с сохранением комментариев. Предполагается соответствие native-результатам и ошибкам; parity проверяется CI. Pure не загружает, не скачивает и не вызывает native-код.

Выберите режим через `Ktav.UsePureBackend()` или передавайте `KtavBackend.Pure` для вызова (он имеет приоритет).

```csharp
var value = Ktav.Loads(src, KtavBackend.Pure);
var text = Ktav.Dumps(value, KtavBackend.Pure);
var canonical = Ktav.EmitCanonical(value, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

`KTAV_BACKEND=pure` задаёт начальный дефолт один раз при старте; явный backend вызова приоритетнее. Смена дефолта атомарна. `NativeVersion()` требует native-библиотеку. Соответствие ошибок проверяется CI; spans — исходные UTF-8 byte offsets, включая BOM и байты переносов строк.

Для Pure используйте готовый `net8.0` `Ktav.dll` и установленную совместимую среду .NET 8 `Microsoft.NETCore.App`. Более новая major-версия сама по себе не гарантирует запуск net8-потребителя без настроенного хостом поддерживаемого roll-forward. Framework API вроде System.Text.Json и Memory доступны в net8, но pure execution не использует native JSON wire/envelope helpers. Для отдельного использования скопируйте DLL и добавьте assembly `Reference`/`HintPath`, без Ktav `PackageReference`. Pure не загружает, не скачивает, не вызывает и не использует fallback native-кода; сеть не нужна.

Обычный multi-target NuGet-пакет не свободен от зависимостей: `netstandard2.0` сохраняет `System.Memory` и `System.Text.Json` для совместимости и native wire/error-envelope. Native netstandard поддерживается. SDK, NuGet restore/build, тесты и документационные инструменты — средства разработки, не зависимости Pure runtime.

>>>>> lang=zh
## 纯托管后端

从 0.8.1 起，本库提供托管 parser、writer、canonical emitter 和保留注释的 formatter。目标是匹配 native 输出和错误信封；CI 检查一致性。Pure 不加载、下载或调用 native 代码。

可通过 `Ktav.UsePureBackend()` 全局选择，或按调用传入 `KtavBackend.Pure`（其优先级更高）。

```csharp
var value = Ktav.Loads(src, KtavBackend.Pure);
var text = Ktav.Dumps(value, KtavBackend.Pure);
var canonical = Ktav.EmitCanonical(value, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

`KTAV_BACKEND=pure` 在启动时设置一次默认值；按次选择优先。默认值原子切换。`NativeVersion()` 仍需要 native 库。CI 检查 Pure 错误一致性；spans 使用原始 UTF-8 字节偏移，包含 BOM 和换行字节。

Pure 需要现成的 `net8.0` `Ktav.dll` 和已安装兼容的 .NET 8 `Microsoft.NETCore.App` framework。仅安装更高 major 版本本身不保证 net8 consumer 可运行；这需要 host 配置受支持的 roll-forward。System.Text.Json、Memory 等 framework API 在 net8 可用，但 pure execution 不使用 native JSON wire/envelope helpers。独立使用时复制 DLL 并通过 assembly `Reference`/`HintPath` 引用，不添加 Ktav `PackageReference`。Pure 不加载、下载、调用或 fallback 到 native，也不需要网络。

多目标 NuGet 包并非无依赖：为兼容性及 native wire/error-envelope，`netstandard2.0` 保留 `System.Memory` 和 `System.Text.Json` PackageReference。保留 native netstandard 支持。SDK、NuGet restore/build、测试及文档工具属于开发工具，不是 Pure runtime 依赖。


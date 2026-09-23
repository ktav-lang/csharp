>>>>> lang=en
## 0.1.0 — first public release

First release. Targets **Ktav format 0.1**.

### Coordinates

NuGet package: **`Ktav`**, version 0.1.0.

### Public API

- `Ktav.Loads(string) -> KtavValue` — parse a Ktav document.
- `Ktav.Dumps(KtavValue) -> string` — render a `KtavValue` as Ktav text.
- `Ktav.NativeVersion()` — version of the loaded `ktav_cabi`.
- `Ktav.ExpectedNativeVersion` — version this build was compiled against.
- `KtavException` — parse / render error with the native-side message.
- `KtavValue` — sealed `record` hierarchy with seven variants
  (`KtavNull`, `KtavBool`, `KtavInteger`, `KtavFloat`, `KtavString`,
  `KtavArray`, `KtavObject`), mirroring the Rust crate's `Value` enum.

### Architecture

>>>>> lang=ru
## 0.1.0 — первый публичный релиз

Первый релиз. Цель — **формат Ktav 0.1**.

### Координаты

NuGet-пакет: **`Ktav`**, версия 0.1.0.

### Публичный API

- `Ktav.Loads(string) -> KtavValue` — разобрать документ Ktav.
- `Ktav.Dumps(KtavValue) -> string` — отрендерить `KtavValue` в текст.
- `Ktav.NativeVersion()` — версия загруженного `ktav_cabi`.
- `Ktav.ExpectedNativeVersion` — версия, под которую собран пакет.
- `KtavException` — ошибка парсинга/рендера с сообщением от нативной
  стороны.
- `KtavValue` — sealed `record`-иерархия с семью вариантами
  (`KtavNull`, `KtavBool`, `KtavInteger`, `KtavFloat`, `KtavString`,
  `KtavArray`, `KtavObject`), повторяет enum `Value` Rust-крейта.

### Архитектура

>>>>> lang=zh
## 0.1.0 —— 首次公开发布

首次发布。目标格式版本：**Ktav 0.1**。

### 构件坐标

NuGet 包：**`Ktav`**，版本 0.1.0。

### 公共 API

- `Ktav.Loads(string) -> KtavValue` —— 解析 Ktav 文档。
- `Ktav.Dumps(KtavValue) -> string` —— 渲染为 Ktav 文本。
- `Ktav.NativeVersion()` —— 已加载 `ktav_cabi` 的版本。
- `Ktav.ExpectedNativeVersion` —— 本次构建的预期版本。
- `KtavException` —— 解析 / 渲染错误，消息来自原生侧。
- `KtavValue` —— 七变体的 sealed `record` 层级
  (`KtavNull`、`KtavBool`、`KtavInteger`、`KtavFloat`、`KtavString`、
  `KtavArray`、`KtavObject`)，与 Rust crate 的 `Value` 枚举一一对应。

### 架构


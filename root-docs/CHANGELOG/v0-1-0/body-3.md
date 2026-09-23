>>>>> lang=en
### Platforms

Prebuilt native binaries ship for:

- `linux-x64`, `linux-arm64` (glibc 2.17+ via cargo-zigbuild)
- `osx-x64`, `osx-arm64`
- `win-x64`, `win-arm64`

Alpine (musl) is planned for a follow-up.

### Test coverage

Runs the full Ktav 0.1 conformance suite (all `valid/` and `invalid/`
fixtures) on .NET 8 across Linux / macOS / Windows.

### Credits

Built on top of the reference `ktav` Rust crate. JSON streaming via
`System.Text.Json`. Native loader via `System.Runtime.InteropServices.NativeLibrary`.
>>>>> lang=ru
### Платформы

Прекомпилированные нативные бинарники поставляются для:

- `linux-x64`, `linux-arm64` (glibc 2.17+ через cargo-zigbuild)
- `osx-x64`, `osx-arm64`
- `win-x64`, `win-arm64`

Alpine (musl) — в следующем релизе.

### Протестировано на

Полная conformance-сьюта Ktav 0.1 (все `valid/` и `invalid/` фикстуры)
на .NET 8 × Linux / macOS / Windows.

### Благодарности

Построено поверх reference-Rust-крейта `ktav`. Streaming JSON через
`System.Text.Json`. Нативный лоадер через
`System.Runtime.InteropServices.NativeLibrary`.
>>>>> lang=zh
### 平台

提供以下平台的预编译原生二进制文件：

- `linux-x64`、`linux-arm64`（通过 cargo-zigbuild 锁定 glibc 2.17+）
- `osx-x64`、`osx-arm64`
- `win-x64`、`win-arm64`

Alpine（musl）—— 计划在后续版本加入。

### 测试覆盖

在 .NET 8 × Linux / macOS / Windows 上运行完整的 Ktav 0.1
conformance 套件（所有 `valid/` 与 `invalid/` fixture）。

### 致谢

基于参考 Rust crate `ktav` 构建。Streaming JSON 通过
`System.Text.Json`。原生加载器通过
`System.Runtime.InteropServices.NativeLibrary`。

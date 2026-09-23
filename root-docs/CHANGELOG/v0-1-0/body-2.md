>>>>> lang=en
- **Native core** — the reference Rust `ktav` crate, wrapped with a
  tiny `extern "C"` C ABI (`crates/cabi`) and distributed as a prebuilt
  `.so` / `.dylib` / `.dll`.
- **.NET loader** — P/Invoke (`LibraryImport` on `net8.0`, `DllImport`
  on `netstandard2.0`). The library is resolved at first call from
  `$KTAV_LIB_PATH`, the bundled `runtimes/<rid>/native/` layout, or
  downloaded once into the user cache from the matching GitHub Release
  asset.
- **Wire format** — JSON between Rust and .NET, with `{"$i":"..."}` /
  `{"$f":"..."}` tagged wrappers for lossless typed-integer / typed-float
  round-trips and arbitrary-precision integers (`BigInteger`).

### Type mapping

| Ktav             | `KtavValue` variant                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger` (text form — arbitrary precision)         |
| `:f <number>`    | `KtavFloat` (text form — exact round-trip)              |
| bare scalar      | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |

>>>>> lang=ru
- **Нативное ядро** — референсный Rust-крейт `ktav`, обёрнутый тонким
  `extern "C"` C ABI (`crates/cabi`) и распространяемый как
  прекомпилированный `.so` / `.dylib` / `.dll`.
- **.NET-лоадер** — P/Invoke (`LibraryImport` на `net8.0`, `DllImport`
  на `netstandard2.0`). Библиотека резолвится на первый вызов из
  `$KTAV_LIB_PATH`, упакованного `runtimes/<rid>/native/` или
  скачивается один раз в пользовательский кэш из соответствующего
  GitHub Release asset.
- **Wire-формат** — JSON между Rust и .NET с тегированными обёртками
  `{"$i":"..."}` / `{"$f":"..."}` для lossless round-trip
  типизированных integer / float и произвольной точности (`BigInteger`).

### Соответствие типов

| Ktav             | вариант `KtavValue`                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger` (текстовая форма — произвольная точность) |
| `:f <number>`    | `KtavFloat` (текстовая форма — точный round-trip)       |
| scalar без маркера | `KtavString`                                          |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |

>>>>> lang=zh
- **原生核心** —— 参考 Rust crate `ktav`，通过极简的 `extern "C"` C
  ABI (`crates/cabi`) 封装，分发为预编译的 `.so` / `.dylib` / `.dll`。
- **.NET 加载器** —— P/Invoke（`net8.0` 上是 `LibraryImport`，
  `netstandard2.0` 上是 `DllImport`）。库在首次调用时
  从 `$KTAV_LIB_PATH`、打包的 `runtimes/<rid>/native/` 解析，或从
  对应的 GitHub Release 资产一次性下载到用户缓存。
- **Wire 格式** —— Rust 与 .NET 之间使用 JSON，带有
  `{"$i":"..."}` / `{"$f":"..."}` 标记包装，实现带类型的
  整数 / 浮点无损往返及任意精度整数（`BigInteger`）。

### 类型映射

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger`（文本形式 —— 任意精度）                   |
| `:f <number>`    | `KtavFloat`（文本形式 —— 精确往返）                     |
| 裸 scalar        | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`                                            |


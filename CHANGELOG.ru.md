# Changelog

**Языки:** [English](CHANGELOG.md) · **Русский** · [简体中文](CHANGELOG.zh.md)

Все значимые изменения NuGet-пакета `Ktav` документируются здесь.
Формат: [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/);
версионирование: [Semantic Versioning](https://semver.org/) с pre-1.0
соглашением, что MINOR bump — ломающий.

Этот changelog отслеживает **релизы биндинга**, а не изменения самого
формата Ktav — для последнего см.
[`ktav-lang/spec`](https://github.com/ktav-lang/spec/blob/main/CHANGELOG.md).

## [0.6.1] — 2026-06-05

- Документация: все примеры в README переписаны под синтаксис спецификации 0.6 (голые числа вместо удалённых маркеров `:i`/`:f`; комментарии `##` вместо `#`).

## 0.6.0 — 2026-06-01

Синхронизация с Ktav 0.6.0 — ключи теперь поддерживают экранирование.

### Добавлено

- Ключи обрабатывают полный набор escape-последовательностей §3.7,
  включая два новых:
  - `\.` → `.` (литеральная точка — **не** делит dotted-path)
  - `\:` → `:` (литеральное двоеточие — **не** работает как разделитель
    ключ/значение)
- Примеры: `a\.b: v` → `{"a.b": "v"}`, `a\:b: v` → `{"a:b": "v"}`,
  `x.y\.z: v` → `{"x": {"y.z": "v"}}`.

### Ломающие изменения

- Литеральный backslash внутри ключа теперь требует `\\` (раньше `\` в
  ключе был обычным байтом). На практике встречается редко; по pre-1.0
  SemVer — MINOR bump.

### Изменено

- Отслеживает ktav-rust 0.6.0 / Ktav spec 0.6.0. Исходники биндинга не
  менялись — изменение escape-семантики целиком внутри Rust-ядра и
  прозрачно через границу P/Invoke.

---

## 0.1.2 — 2026-05-03

### Изменено

- **Подхватили `ktav 0.1.5`** — в upstream Rust crate появился API
  структурированных ошибок (`Error::Structured(ErrorKind)` с
  byte-offset spans), retroactive `#[non_exhaustive]` на error-enum-ах,
  и публичный event-based парсер `ktav::thin`. Поведение .NET-биндинга
  для пользователя не меняется: `KtavException` несёт то же читаемое
  сообщение (Display-строки семи канонических категорий byte-identical
  к ktav 0.1.4 — проверено собственными pinning-тестами ktav). Маппинг
  `ktav::ErrorKind` на структурную .NET-иерархию исключений
  (`KtavMissingSeparatorSpaceException`, `KtavDuplicateKeyException` и
  т.д.) — отдельная follow-up работа, описанная в
  [`STRUCTURED_ERRORS.md`](https://github.com/ktav-lang/.github/blob/main/STRUCTURED_ERRORS.md).

NuGet package: **`Ktav`**, версия 0.1.2.

## 0.1.1 — 2026-04-26

### Изменено

- **Подхватили `ktav 0.1.4`** — untyped путь `parse() → Value` в
  upstream Rust crate (тот, что использует `cabi`) теперь ~30%
  быстрее на маленьких документах и ~13% на больших, благодаря
  однострочной правке initial capacity для `Frame::Object` (4 → 8).
  Каждый `Ktav.Loads` получит ускорение прозрачно.

NuGet-пакет: **`Ktav`**, версия 0.1.1.

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

### Платформы

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

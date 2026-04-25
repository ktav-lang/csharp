# ktav — биндинги для .NET

**Languages:** [English](README.md) · **Русский** · [简体中文](README.zh.md)

.NET-биндинги к [формату конфигурации Ktav](https://github.com/ktav-lang/spec).
Тонкая обёртка над эталонным парсером на Rust, подгружаемая в runtime
через P/Invoke — **никакой сборки нативки на стороне потребителя**,
обычный `dotnet add package` просто работает.

Цели сборки: **`net8.0`** (AOT-ready, через `LibraryImport`) и
**`netstandard2.0`** (`DllImport`, без `NativeLibrary`-резолвера —
используется NuGet `runtimes/` layout либо `KTAV_LIB_PATH`).

## Установка

```bash
dotnet add package Ktav
```

## Быстрый старт

```csharp
using Ktav;
using System.Collections.Generic;

string src = """
             service: web
             port:i 8080
             ratio:f 0.75
             tls: true
             tags: [
                 prod
                 eu-west-1
             ]
             db.host: primary.internal
             db.timeout:i 30
             """;

KtavValue cfg = Ktav.Loads(src);

// Pattern-match по семи вариантам:
string label = cfg switch {
    KtavObject o => $"top object with {o.Entries.Count} keys",
    _            => "unexpected shape",
};

// Обратный рендер:
var built = new KtavObject(new[] {
    new KeyValuePair<string, KtavValue>("name",  new KtavString("demo")),
    new KeyValuePair<string, KtavValue>("count", KtavInteger.Of(42)),
});
string text = Ktav.Dumps(built);
```

## API

| Член | Назначение |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | Разобрать Ktav-документ в дерево `KtavValue`. |
| `Ktav.Dumps(KtavValue) -> string` | Отрендерить `KtavValue` в Ktav-текст. Верхний уровень — `KtavObject`. |
| `Ktav.NativeVersion()` | Версия загруженного `ktav_cabi`. |
| `Ktav.ExpectedNativeVersion` | Версия, под которую собран этот пакет. |

На любой ошибке — `KtavException` (сообщение от нативного парсера, UTF-8).

## Маппинг типов

Повторяет enum `Value` из Rust-крейта — один record на каждый
примитив Ktav, без лоссных приведений:

| Ktav             | вариант `KtavValue`                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger` (текстовая форма — `ToBigInteger()` / `ToInt64()`) |
| `:f <number>`    | `KtavFloat` (текстовая форма — `ToDouble()`)            |
| scalar без маркера | `KtavString`                                          |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (порядок вставки сохраняется)              |

Типизированные целые и float хранятся **как текст**, чтобы произвольная
точность и точное представление десятичного числа побайтово сохранялись
между циклами parse/render.

## Как резолвится нативная библиотека

На `net8.0` `NativeLoader` регистрирует
`NativeLibrary.SetDllImportResolver`. Порядок:

1. **`$KTAV_LIB_PATH`** — абсолютный путь к локальной сборке. Полезно
   для разработки и air-gapped CI.
2. **NuGet `runtimes/<rid>/native/`** — подхватывается автоматически
   при потреблении через `Ktav.nupkg`.
3. **Кэш пользователя** — `<userCache>/ktav-dotnet/v<версия>/…`,
   скачанный предыдущим вызовом.
4. **Скачивание с GitHub Release** — соответствующий ассет один раз
   тянется с
   `github.com/ktav-lang/csharp/releases/download/v<версия>/<имя>`
   и кладётся в (3). На первом вызове после установки нужна сеть.

`<userCache>` это `%LOCALAPPDATA%` на Windows, `~/Library/Caches` на
macOS, `$XDG_CACHE_HOME` или `~/.cache` на Linux.

На `netstandard2.0` доступен только пункт (2) — `NativeLibrary` API
там нет.

## Поддержка

- `net8.0` (AOT через `LibraryImport`) и `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+).
- Собранные бинарники для: `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`.
- Linux — glibc 2.17+ (zigbuild baseline). Alpine (musl) — запланировано.

## Лицензия

MIT — см. [LICENSE](LICENSE).

Спецификация: [ktav-lang/spec](https://github.com/ktav-lang/spec).
Эталонный Rust-крейт: [ktav-lang/rust](https://github.com/ktav-lang/rust).

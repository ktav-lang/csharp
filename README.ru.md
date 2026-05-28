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

### Парсинг — типизированно вытаскиваем поля

```csharp
using Ktav;

const string src = """
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

var top = (KtavObject)Ktav.Loads(src);

string  service = ((KtavString)  top.TryGet("service")!).Value;
long    port    = ((KtavInteger) top.TryGet("port")!).ToInt64();
double  ratio   = ((KtavFloat)   top.TryGet("ratio")!).ToDouble();
bool    tls     = ((KtavBool)    top.TryGet("tls")!).Value;

var db = (KtavObject) top.TryGet("db")!;
string dbHost   = ((KtavString)  db.TryGet("host")!).Value;
long   dbTimeout = ((KtavInteger) db.TryGet("timeout")!).ToInt64();
```

### Обход — pattern matching по sealed-иерархии `KtavValue`

```csharp
foreach (var entry in top.Entries)
{
    string kind = entry.Value switch
    {
        KtavNull        => "null",
        KtavBool b      => $"bool={b.Value}",
        KtavInteger i   => $"int={i.Text}",
        KtavFloat f     => $"float={f.Text}",
        KtavString s    => $"str=\"{s.Value}\"",
        KtavArray a     => $"array({a.Items.Count})",
        KtavObject o    => $"object({o.Entries.Count})",
        _               => throw new InvalidOperationException(),
    };
    Console.WriteLine($"{entry.Key} -> {kind}");
}
```

### Билд + рендер — собираем документ в коде

```csharp
using System.Collections.Generic;

KtavObject Upstream(string host, long port) => new(new[]
{
    new KeyValuePair<string, KtavValue>("host", new KtavString(host)),
    new KeyValuePair<string, KtavValue>("port", KtavInteger.Of(port)),
});

var doc = new KtavObject(new[]
{
    new KeyValuePair<string, KtavValue>("name",      new KtavString("frontend")),
    new KeyValuePair<string, KtavValue>("port",      KtavInteger.Of(8443)),
    new KeyValuePair<string, KtavValue>("tls",       KtavBool.True),
    new KeyValuePair<string, KtavValue>("ratio",     KtavFloat.Of(0.95)),
    new KeyValuePair<string, KtavValue>("upstreams", new KtavArray(new KtavValue[]
    {
        Upstream("a.example", 1080),
        Upstream("b.example", 1080),
    })),
    new KeyValuePair<string, KtavValue>("notes",     KtavNull.Instance),
});

string text = Ktav.Dumps(doc);
```

Полный запускаемый пример — в [`examples/Basic`](examples/Basic/Program.cs).

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

MIT OR Apache-2.0 — см. [LICENSE-MIT](LICENSE-MIT) и [LICENSE-APACHE](LICENSE-APACHE).

## Другие реализации Ktav

- [`spec`](https://github.com/ktav-lang/spec) — спецификация + conformance-тесты
- [`rust`](https://github.com/ktav-lang/rust) — эталонный Rust crate (`cargo add ktav`)
- [`golang`](https://github.com/ktav-lang/golang) — Go (`go get github.com/ktav-lang/golang`)
- [`java`](https://github.com/ktav-lang/java) — Java / JVM (`io.github.ktav-lang:ktav` на Maven Central)
- [`js`](https://github.com/ktav-lang/js) — JS / TS (`npm install @ktav-lang/ktav`)
- [`php`](https://github.com/ktav-lang/php) — PHP (`composer require ktav-lang/ktav`)
- [`python`](https://github.com/ktav-lang/python) — Python (`pip install ktav`)

# ktav — .NET-биндинги

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** [English](../../README.md) · **Русский** · [简体中文](../zh/README.zh.md)

**Песочница:** конвертация JSON / YAML / TOML / INI ⇄ Ktav прямо в браузере — **[ktav-lang.github.io](https://ktav-lang.github.io/)**.

.NET-биндинги к [формату конфигурации Ktav](https://github.com/ktav-lang/spec).
Тонкая обёртка над эталонным парсером на Rust, подгружаемая во время
выполнения через P/Invoke — **никакой сборки нативной части на стороне
потребителя**, обычный `dotnet add package` просто работает.

Цели сборки: **`net8.0`** (с `LibraryImport`, готовым к AOT) и
**`netstandard2.0`** (`DllImport`, без резолвера `NativeLibrary` —
используйте компоновку NuGet `runtimes/` или системные пути поиска
нативных библиотек; `KTAV_LIB_PATH` игнорируется).

## Установка

```bash
dotnet add package Ktav
```

## Быстрый старт

### Разбор — вытаскиваем типизированные поля

```csharp
using Ktav;

const string src = """
                   service: web
                   port: 8080
                   ratio: 0.75
                   tls: true
                   tags: [
                       prod
                       eu-west-1
                   ]
                   db.host: primary.internal
                   db.timeout: 30
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

### Сборка и рендер — создаём документ в коде

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

Полный запускаемый пример — в [`examples/Basic`](../../examples/Basic/Program.cs).

## API

| Член | Назначение |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | Разобрать Ktav-документ в дерево `KtavValue`. |
| `Ktav.LoadsStrict(string) -> KtavValue` | Разбор со строгой проверкой написания чисел. |
| `Ktav.Dumps(KtavValue) -> string` | Вывести `KtavValue` обратно как Ktav-текст. Верхний уровень должен быть `KtavObject` или `KtavArray`. |
| `Ktav.DumpsForceStrings(KtavValue) -> string` | Как `Dumps`, но каждый листовой скаляр приводится к String через сырой маркер `::`. Составные значения сохраняют структуру. |
| `Ktav.EmitCanonical(KtavValue) -> string` | Вывести `KtavValue` как канонический Ktav (spec § 5.9). |
| `Ktav.Format(string) -> string` | Привести исходный Ktav-текст к нормализованному написанию, **сохраняя все комментарии**. См. ниже. |
| `Ktav.CanonicalFromSource(string) -> string` | Разобрать и заново выдать канонический Ktav одним вызовом — `EmitCanonical(Loads(src))` без промежуточного `KtavValue`. Комментарии и пустые строки отбрасываются, как и в `EmitCanonical`. |
| `Ktav.NativeVersion()` | Строка версии загруженной `ktav_cabi`. |
| `Ktav.ExpectedNativeVersion` | Версия, под которую собран этот билд. |

## Форматирование — каноническое написание, комментарии сохраняются

`Format` и `EmitCanonical` — разные операции:

- **`EmitCanonical`** принимает `KtavValue` и пишет каноническую форму.
  Значение не несёт комментариев, поэтому сохранить их нечем.
- **`Format`** принимает исходный *текст* и переписывает его написание,
  **сохраняя каждый комментарий дословно** (spec § 3.4: комментарий
  занимает строку целиком). Порядок ключей не меняется — в spec § 5.9
  нет правила сортировки.

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

Серия из двух и более пустых строк сворачивается в одну, а пустые строки
прямо внутри скобок удаляются, поэтому форматирование является
неподвижной точкой: `Format(Format(x)) == Format(x)`. Для документа без
комментариев и пустых строк результат совпадает с `EmitCanonical(Loads(src))`.

## Структурированные ошибки

Ошибки разбора, форматирования и вывода на нативной стороне сообщаются
через `KtavException`. Помимо сообщения оно несёт структурированный
нативный конверт ошибки, поэтому инструмент может работать с полями, а не
разбирать прозу. Не всякий сбой является `KtavException`: для null-
аргументов используется `ArgumentNullException`, проверка аргументов на
стороне .NET — стандартные исключения аргументов, а загрузка нативной
библиотеки может выбросить исключения загрузчика:

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

| Член | Значение |
| --- | --- |
| `Message` | Человекочитаемое представление сбоя. |
| `Error` | Класс структурированной ошибки — `DuplicateKey`, `Unrepresentable`, `Message`. |
| `Reason` | Код причины на стороне записи (spec § 5.9.0), например `NonFiniteFloat`; `null` для ошибок разбора. |
| `Line` | Строка исходного текста, счёт с 1; `null`, если неприменимо. |
| `LineText` | Текст проблемной строки. |
| `Span` | `KtavErrorSpan?` — байтовые смещения в UTF-8-исходнике. |
| `Path` | Точные декодированные сегменты ключа. |
| `Body` | Проблемное значение в том виде, как оно записано. |
| `Canonical` | Какой была бы каноническая форма. |
| `SpecSection` | Нарушенный пункт спецификации, например `§3.6/§5.2`. |

Две детали, в которых легко ошибиться:

- **`Span` хранит байтовые смещения в UTF-8**, а не кодовые единицы
  UTF-16, которыми индексируются строки .NET. Преобразуйте их, прежде
  чем передавать туда, где ожидаются индексы `string`, или LSP-клиенту,
  который не договорился о `positionEncoding: "utf-8"`.
- **`Path` — список сегментов, а не склеенная строка.** Ключ, буквально
  названный `a.b`, — это один сегмент, и его нельзя спутать с путём из
  двух сегментов.

## Отображение типов

Повторяет enum `Value` из Rust-крейта — по одной записи на каждый примитив
Ktav, без потерьных приведений:

| Ktav             | вариант `KtavValue`                                     |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| голое целое в диапазоне знакового 64-битного числа | `KtavInteger` (текстовая форма — `ToBigInteger()` / `ToInt64()`) |
| голое десятичное | `KtavFloat` (текстовая форма — `ToDouble()`)            |
| прочий скаляр    | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject` (порядок вставки сохраняется)              |

Целое вне диапазона знакового 64-битного числа ядра загружается как
`KtavString`, а не `KtavInteger`. `KtavInteger` и `KtavFloat` предоставляют
хранимый текст, но это не означает поддержку чисел произвольной точности
или сохранение исходной записи дроби: например, `1.10` загружается как
`1.1`. Публичные record-типы можно создать с другим текстом, но нативная
запись всё равно соблюдает числовую область спецификации ядра.

## Экранирование в ключах

Начиная со spec 0.6.4, литеральные `.` или `:` внутри сегмента ключа
записываются с обратной косой чертой:

```text
a\.b: v
a\:b: v
x.y\.z: v
```

Они разбираются соответственно в одиночные ключи `a.b` и `a:b`, а также
во вложенные ключи `x`, затем `y.z`. Пояснения следует размещать вне
примеров Ktav: текст `//` в строке является содержимым значения, а не
комментарием. Комментарий Ktav занимает отдельную строку и начинается с `##`.

Литеральная обратная косая черта в ключе записывается как `\\`.

## Как резолвится нативная библиотека

На `net8.0` `NativeLoader` регистрирует колбэк
`NativeLibrary.SetDllImportResolver`. Порядок разрешения:

1. **`$KTAV_LIB_PATH`** — абсолютный путь к локальной сборке. Полезнее
   всего для разработки и изолированного CI.
2. **Компоновка NuGet `runtimes/<rid>/native/`** — подхватывается
   автоматически штатным загрузчиком .NET при потреблении через
   `Ktav.nupkg`.
3. **Кэш пользователя** — `<userCache>/ktav-dotnet/v<version>/…`,
   загруженный предыдущим вызовом.
4. **Загрузка с GitHub Release** — файл один раз скачивается с
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   и кладётся в (3). Требует сети на первом вызове после установки.

`<userCache>` — это `%LOCALAPPDATA%` на Windows, `~/Library/Caches` на
macOS, `$XDG_CACHE_HOME` или `~/.cache` на Linux.

На `netstandard2.0` собственного резолвера нет: переменная `KTAV_LIB_PATH`
и резервные кэш/загрузка не используются. Используйте компоновку NuGet
с нативными файлами или стандартные пути поиска библиотек платформы.

## Поддержка runtime

- `net8.0` (совместим с AOT через `LibraryImport`) и `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+).
- Готовые бинарники для: `linux-x64`, `linux-arm64`, `osx-x64`,
  `osx-arm64`, `win-x64`, `win-arm64`.
- Дистрибутивы Linux должны использовать glibc 2.17+ (базовая линия
  zigbuild). Поддержка Alpine (musl) запланирована.

## Лицензия

MIT OR Apache-2.0 — см. [LICENSE-MIT](../../LICENSE-MIT) и [LICENSE-APACHE](../../LICENSE-APACHE).

## Другие реализации Ktav

- [`spec`](https://github.com/ktav-lang/spec) — спецификация + conformance-тесты
- [`rust`](https://github.com/ktav-lang/rust) — эталонный Rust crate (`cargo add ktav`)
- [`golang`](https://github.com/ktav-lang/golang) — Go (`go get github.com/ktav-lang/golang`)
- [`java`](https://github.com/ktav-lang/java) — Java / JVM (`io.github.ktav-lang:ktav` на Maven Central)
- [`js`](https://github.com/ktav-lang/js) — JS / TS (`npm install @ktav-lang/ktav`)
- [`php`](https://github.com/ktav-lang/php) — PHP (`composer require ktav-lang/ktav`)
- [`python`](https://github.com/ktav-lang/python) — Python (`pip install ktav`)

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

Пакет работает и без каких-либо нативных бинарников — см.
[Чистый бэкенд](#pure-backend) (`Ktav.UsePureBackend()` или
`KtavBackend.Pure` на вызов).

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

var top = (KtavObject)global::Ktav.Ktav.Loads(src);

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

string text = global::Ktav.Ktav.Dumps(doc);
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
| `Ktav.EditValue(string, string, KtavValue) -> string` | Lossless-замена значения по точечному текстовому пути в исходном тексте. См. раздел «Редактирование». |
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
global::Ktav.Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

Серия из двух и более пустых строк сворачивается в одну, а пустые строки
прямо внутри скобок удаляются, поэтому форматирование является
неподвижной точкой: `Format(Format(x)) == Format(x)`. Для документа без
комментариев и пустых строк результат совпадает с `EmitCanonical(Loads(src))`.

> **Ещё не выпущено (подготовка 0.9):** Эти API редактирования и обновлённая диагностика не входят в опубликованные пакеты 0.8.1. Native-backend требует закреплённое ядро 0.9 (C ABI 2 для C-FFI); pure-backend остаётся самостоятельным.

## Редактирование исходного текста

`Ktav.EditValue` заменяет значение по пути **в самом исходном тексте** —
это lossless-сплайс, а не разбор→мутация→сериализация всего документа:

```csharp
var out1 = Ktav.EditValue(src, "targets.storagehost.address", new KtavString("10.0.0.9"));
var out2 = Ktav.EditValueSegments(src, new[] { "targets", "storagehost", "address" }, new KtavString("10.0.0.9"));
```

| Член | Назначение |
| --- | --- |
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | Заменить значение по текстовому пути с точками (`key1.key2.0.key4`; `\.` — литеральная точка, `\\` — литеральный обратный слэш). |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | То же, но с уже декодированными литеральными сегментами (без повторной распаковки escape-последовательностей). |

Гарантии:

- `Ktav.EditValue` заменяет только существующее значение; она не создаёт, не удаляет и не переименовывает значения. Отсутствующий ключ, индекс массива вне диапазона или спуск сквозь скаляр дают `EditNotFound`; некорректный путь — `EditInvalidPath` (`Body` содержит UTF-8-байтовый offset проблемного места). `EditValue` возвращает `EditUnsupported`, если lossless-патч невозможен, например для noncontiguous-цели из dotted/reopen-строк или при комментариях внутри структурно заменяемого контейнера.
- Байты вне правки сохраняются точно: комментарии, пробелы, написание и порядок ключей, BOM, CR/LF/CRLF и наличие или отсутствие финального перевода строки. No-op-замена возвращает вход byte-identical даже при неканоничном написании.
- Inline-контейнеры минимально раскрываются (отступ 4 пробела, переводы строк как в исходнике), только если иначе пострадает читаемость; нетронутые вложенные ветви остаются inline. Читаемые inline-контейнеры целиком остаются inline; опустевшие блочные контейнеры сохраняют скобки.
- После каждого патча результат заново разбирается и семантически проверяется, поэтому повреждённый документ не возвращается молча.

Insert создаёт только новый конечный ключ Object у существующего родителя Object: без перезаписи, неявного создания промежуточных контейнеров и вставки в Array. Remove удаляет лист, поддерево или элемент Array и не удаляет пустых предков. Rename принимает один литеральный `newKey`, а не путь, сохраняет порядок и значение пары и возвращает исходник побайтно неизменным при том же имени. Коллизии дают `EditAlreadyExists`, отсутствующая цель или родитель — `EditNotFound`. Native и самостоятельный Pure следуют этому контракту и независимо проверяются в CI.

| Член | Назначение |
| --- | --- |
| `Ktav.InsertValue(string src, string path, KtavValue value) -> string` | Вставить новый конечный ключ объекта; текстовые пути используют те же правила экранирования. Поддерживается; Pure может вернуть `EditUnsupported`, если не удаётся выполнить lossless-сплайс. |
| `Ktav.InsertValueSegments(string src, IReadOnlyList<string> segments, KtavValue value) -> string` | Та же вставка с уже декодированными литеральными сегментами. Поддерживается; Pure может вернуть `EditUnsupported`, если не удаётся выполнить lossless-сплайс. |
| `Ktav.RemoveValue` / `Ktav.RemoveValueSegments` | Удалить значение. Поддерживается Native и Pure; Pure может вернуть `EditUnsupported` для целей с комментариями или noncontiguous-целей dotted/reopen. |
| `Ktav.RenameKey` / `Ktav.RenameKeySegments` | Переименовать ключ объекта. Поддерживается Native и Pure; Pure может вернуть `EditUnsupported` для элементов массива или noncontiguous-целей dotted/reopen. |

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

#### 2. Буквальные точки: текстовый путь или сегменты

Ключ исходника в кавычках буквально называется `a.b`. В текстовом пути точка экранируется; API сегментов получает декодированное имя напрямую. Оба вызова возвращают одинаковый результат.

```csharp
var source = "\"a.b\": {port: 80}\n";
var updated = KtavApi.EditValue(source, "a\\.b.port", new KtavInteger("90"), KtavBackend.Pure);
var same = KtavApi.EditValueSegments(source, new[] {"a.b", "port"}, new KtavInteger("90"), KtavBackend.Pure);
```

Результат:

```ktav
"a.b": {port: 90}
```

#### 3. Лист внутри массива со смешанными формами

Здесь `0` — индекс массива. В объекте такой же сегмент обозначал бы ключ с именем `0`; нетронутые контейнеры остаются inline.

```csharp
var source = "servers: [{host: old, keep: yes}, standby]\n";
var updated = KtavApi.EditValueSegments(source, new[] {"servers", "0", "host"}, new KtavString("new"), KtavBackend.Pure);
```

Результат:

```ktav
servers: [{host: new, keep: yes}, standby]
```

#### 4. Похожая на число строка остаётся строкой

Замена имеет тип String, а не Integer. Raw-маркер сохраняет это различие. Для числового значения передавайте типизированный Integer.

```csharp
var source = "port: 8080\n";
var updated = KtavApi.EditValue(source, "port", new KtavString("9090"), KtavBackend.Pure);
```

Результат:

```ktav
port:: 9090
```

#### 5. Многострочная замена: раскрывается только необходимое

Изменённый массив раскрывается с отступом четыре пробела. Нетронутый объект остаётся inline, содержимое verbatim-строки не переотступается.

```csharp
var source = "a: [old, {keep: yes}]\n";
var updated = KtavApi.EditValue(source, "a.0", new KtavString("x\ny"), KtavBackend.Pure);
```

Результат:

```ktav
a: [
    ((
x
y
    ))
    {keep: yes}
]
```

#### 6. No-op сохраняет неканоническое написание

Значение Integer уже равно 1. Проверка равенства возвращает исходные байты, включая `+1` и пробелы разделителя; критерии читаемости не применяются.

```csharp
var source = "n  : +1\n";
var updated = KtavApi.EditValue(source, "n", new KtavInteger("1"), KtavBackend.Pure);
if (updated != source) throw new System.InvalidOperationException("no-op changed source");
```

Результат:

```ktav
n  : +1
```

#### 7. BOM, CRLF и последняя строка без перевода

Результат сохраняет BOM и CRLF, не добавляя финальный перевод строки. Экранированное представление делает эти байты видимыми.

```csharp
var source = "\ufeffport: 8080\r\nhost: db";
var updated = KtavApi.EditValue(source, "port", new KtavInteger("9090"), KtavBackend.Pure);
```

Результат:

```text
"\uFEFFport: 9090\r\nhost: db"
```

Критерии раскрытия фиксированы: многострочная замена, рост сверх `max(100, исходная длина строки)` либо не менее четырёх экранирований с накладными расходами от 25% для отдельной строки замены. Ширина считается в Unicode scalar values, а не байтах UTF-8 или колонках терминала. Уже длинная строка без роста сохраняется. Новые уровни используют четыре пробела и EOL исходника. Если CR или строка данных `))` мешают lossless-кодированию вне inline, сохраняется корректный inline-вариант; эти символы не означают автоматический отказ любой inline-правки.

#### 8. Точная замена Integer

Домен Integer в исходнике — знаковый 64-битный. Это значение больше безопасного диапазона JavaScript Number, но помещается в i64; используйте точное host-представление, а не округлённое число с плавающей точкой. Типизированный Integer вне i64 невозможно сохранить в исходнике как Integer: правка отказывает, не обрезая значение. Для текстового большого идентификатора используйте String. Float различает отрицательный и положительный нуль; no-op для Object не зависит от порядка ключей, порядок Array значим.

```csharp
var updated = KtavApi.EditValue("n: 1\n", "n", new KtavInteger("9007199254740993"), KtavBackend.Pure);
```

```ktav
n: 9007199254740993
```

### Диагностика и безопасные отказы в 0.9

Координаты исходника — байтовые смещения UTF-8 с учётом BOM; spans полуоткрыты `[start, end)`, строки нумеруются с 1. Поддерживаются LF, отдельный CR и CRLF; CRLF считается одним переводом строки. Один Unicode-символ может занимать несколько байтов.

Незавершённый `a: [\n` даёт `UnclosedCompound`, строку 1 и span `3..5`: начало — самая внутренняя незакрытая скобка, а не начало ключа или строки. Для сырых байтов `61 3A 20 FF` (`a: ` и невалидный UTF-8) валидация сообщает `InvalidUtf8`, строку 1 и покрывающий span `3..4`; `line_text` равен null, а не придуманному тексту после lossy-декодирования. Проверка UTF-8 предшествует грамматике.

Некорректный текстовый путь, включая пустой путь или пустой текстовый сегмент, даёт `EditInvalidPath`; смещение считается в байтах UTF-8 самого пути. Пустой список сегментов, отсутствующий ключ, индекс вне диапазона или спуск через скаляр дают `EditNotFound`. В объекте числовой сегмент остаётся ключом; индекс массива содержит ASCII-цифры, допускает ведущие нули, но не знаки.

Целые промежуточные объекты из dotted/reopened-определений могут занимать несколько физических диапазонов и давать `EditUnsupported`; их существующие листья остаются редактируемыми. Структурная замена контейнера не должна удалять внутренние комментарии. Ошибка не возвращает частичный патч и не меняет переданный исходник. Результат повторно разбирается и семантически проверяется: цель должна равняться замене, остальное дерево значений — исходному.

В привязках ошибочный исходник даёт обычный конверт парсера до проверки пути. Некорректный wire-payload — ошибка `Message` без выдуманных координат. Wire-конверт содержит `error`, `reason`, `line`, `line_text`, `span`, `path`, `body`, `canonical`, `spec_section`, `message`; имена host-accessors описаны в разделе ошибок. В `body` находятся десятичное байтовое смещение пути для `EditInvalidPath`, null для `EditNotFound` и причина отказа для `EditUnsupported`.

## Структурированные ошибки

Ошибки разбора, форматирования и вывода на нативной стороне сообщаются
через `KtavException`. Помимо сообщения оно несёт структурированный
нативный конверт ошибки, поэтому инструмент может работать с полями, а не
разбирать прозу. Не всякий сбой является `KtavException`: для null-
аргументов используется `ArgumentNullException`, проверка аргументов на
стороне .NET — стандартные исключения аргументов, а загрузка нативной
библиотеки может выбросить исключения загрузчика:

```csharp
try { global::Ktav.Ktav.Loads("a: 1\na: 2\n"); }
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


Всё написанное выше относится только к **нативному** бэкенду. Чистый
управляемый бэкенд ([Чистый бэкенд](#pure-backend)) не обращается ни к
`$KTAV_LIB_PATH`, ни к компоновке `runtimes/`, ни к кэшу, ни к сети.

Перед первым нативным вызовом управляемый слой сверяет
`ktav_abi_version()` с требуемой этим билдом версией формы ABI
(**2** начиная с edit-экспортов 0.9). Более старый бинарник отклоняется
с понятной ошибкой несовпадения версий — он никогда не подменяется
тихим переключением на pure-бэкенд.

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

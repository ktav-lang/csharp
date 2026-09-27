# ktav — .NET 绑定

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** [English](../../README.md) · [Русский](../ru/README.ru.md) · **简体中文**

**演练场：** 在浏览器中把 JSON / YAML / TOML / INI ⇄ Ktav 互转 —— **[ktav-lang.github.io](https://ktav-lang.github.io/)**。

[Ktav 配置格式](https://github.com/ktav-lang/spec) 的 .NET 绑定。
在参考 Rust 解析器之上的一层薄封装，运行时经 P/Invoke 加载 ——
**使用方无需编译原生代码**，常规的 `dotnet add package` 即可。

目标框架：**`net8.0`**（`LibraryImport`，支持 AOT）与 **`netstandard2.0`**
（`DllImport`，无 `NativeLibrary` 解析器 —— 使用 NuGet 的 `runtimes/`
布局或系统原生库搜索路径；`KTAV_LIB_PATH` 会被忽略）。

## 安装

```bash
dotnet add package Ktav
```

## 快速开始

### 解析 —— 取出带类型的字段

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

### 遍历 —— 在 sealed `KtavValue` 层级上做模式匹配

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

### 构建并渲染 —— 用代码搭建文档

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

完整可运行的示例见 [`examples/Basic`](../../examples/Basic/Program.cs)。

## API

| 成员 | 用途 |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | 把 Ktav 文档解析成 `KtavValue` 树。 |
| `Ktav.LoadsStrict(string) -> KtavValue` | 用严格的数字写法检查来解析文档。 |
| `Ktav.Dumps(KtavValue) -> string` | 把 `KtavValue` 渲染回 Ktav 文本。顶层必须是 `KtavObject` 或 `KtavArray`。 |
| `Ktav.DumpsForceStrings(KtavValue) -> string` | 同 `Dumps`，但通过原始标记 `::` 把每个叶子标量强制为 String。复合值保持结构。 |
| `Ktav.EmitCanonical(KtavValue) -> string` | 把 `KtavValue` 输出为规范 Ktav（spec § 5.9）。 |
| `Ktav.Format(string) -> string` | 把 Ktav 源文本格式化为规范化写法，**保留全部注释**。见下文。 |
| `Ktav.CanonicalFromSource(string) -> string` | 一次调用完成解析并重新输出为规范 Ktav —— 相当于 `EmitCanonical(Loads(src))`，中间不产生 `KtavValue`。像 `EmitCanonical` 一样丢弃注释与空行。 |
| `Ktav.NativeVersion()` | 已加载的 `ktav_cabi` 报告的版本字符串。 |
| `Ktav.ExpectedNativeVersion` | 本次构建所对应的版本。 |

## 格式化 —— 规范写法，保留注释

`Format` 和 `EmitCanonical` 是不同的操作：

- **`EmitCanonical`** 接受 `KtavValue`，写出规范形式。值不携带注释，
  因此没有注释可供保留。
- **`Format`** 接受源*文本*，重写其写法，同时**逐字保留每条注释**
  （spec § 3.4：注释独占一整行）。键顺序绝不改变 —— spec § 5.9 中没有
  排序规则。

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

连续两个及以上空行会折叠成一个，紧贴括号内侧的空行填充会被丢弃，
因此格式化是一个不动点：`Format(Format(x)) == Format(x)`。对于没有注释、
没有空行的文档，输出等同于 `EmitCanonical(Loads(src))`。

## 结构化错误

原生解析、格式化和渲染失败会报告为 `KtavException`。除了消息之外，
它还携带原生结构化错误信封，因此工具可以直接使用字段，而不必解析
文本。但并非所有失败都是 `KtavException`：null 参数使用
`ArgumentNullException`，宿主端参数校验使用标准 .NET 参数异常，而
原生库加载可能抛出加载器异常：

```csharp
try { Ktav.Loads("a: 1\na: 2\n"); }
catch (KtavException e)
{
    Console.WriteLine(e.Error);       // "DuplicateKey"
    Console.WriteLine(e.Line);        // 2
    Console.WriteLine(e.SpecSection); // "§6.2"
}
```

| 成员 | 含义 |
| --- | --- |
| `Message` | 失败的人类可读表述。 |
| `Error` | 结构化错误类别 —— `DuplicateKey`、`Unrepresentable`、`Message`。 |
| `Reason` | 写入侧的原因码（spec § 5.9.0），例如 `NonFiniteFloat`；解析错误时为 `null`。 |
| `Line` | 从 1 起算的源行号；不适用时为 `null`。 |
| `LineText` | 出错那一行的文本。 |
| `Span` | `KtavErrorSpan?` —— UTF-8 源文本中的字节偏移。 |
| `Path` | 精确解码后的键段。 |
| `Body` | 出错的值，按原样写出。 |
| `Canonical` | 规范形式本应是什么。 |
| `SpecSection` | 被违反的条款，如 `§3.6/§5.2`。 |

两处容易弄错的细节：

- **`Span` 保存的是 UTF-8 字节偏移**，而不是 .NET 字符串所用的 UTF-16
  码元索引。在交给任何期待 `string` 索引的代码，或尚未协商
  `positionEncoding: "utf-8"` 的 LSP 客户端之前，请先转换。
- **`Path` 是键段列表，绝不是拼接后的字符串。** 字面名为 `a.b` 的键
  是**一个**段，不会与两段路径混淆。

## 类型映射

与 Rust crate 的 `Value` 枚举一致 —— Ktav 的每个原语对应一个 record，
没有有损转换：

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| 核心有符号 64 位范围内的裸整数 | `KtavInteger`（文本形式 —— `ToBigInteger()` / `ToInt64()`） |
| 裸小数           | `KtavFloat`（文本形式 —— `ToDouble()`）                   |
| 其他标量         | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`（保留插入顺序）                             |

超出核心有符号 64 位范围的整数会加载为 `KtavString`，而不是
`KtavInteger`。`KtavInteger` 与 `KtavFloat` 会公开其保存的文本，但这
不代表 Ktav 数字支持任意精度，也不保证保留源十进制写法：例如
`1.10` 会加载为 `1.1`。公开 record 类型可以用其他文本构造，但原生
写入仍会遵守核心规范的数值范围。

## 键的转义

自 spec 0.6.4 起，键段内的字面量 `.` 或 `:` 以反斜杠书写：

```text
a\.b: v
a\:b: v
x.y\.z: v
```

它们分别解析为单段键 `a.b` 和 `a:b`，以及依次为 `x`、`y.z` 的嵌套键。
请将说明写在 Ktav 示例之外：行内 `//` 文本是值内容，不是注释。Ktav
注释必须独占一行并以 `##` 开头。

键中的字面量反斜杠写作 `\\`。

## 原生库的解析顺序

在 `net8.0` 上，`NativeLoader` 会注册一个
`NativeLibrary.SetDllImportResolver` 回调。解析顺序如下：

1. **`$KTAV_LIB_PATH`** —— 指向本地构建的绝对路径。最适合开发和离线
   CI。
2. **NuGet `runtimes/<rid>/native/` 布局** —— 通过 `Ktav.nupkg` 消费时，
   由 .NET 的默认加载器自动选取。
3. **用户缓存** —— `<userCache>/ktav-dotnet/v<version>/…`，由之前的调用
   下载。
4. **从 GitHub Release 下载** —— 一次性从
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   获取，并缓存到 (3)。安装后首次调用需要联网。

`<userCache>` 在 Windows 上是 `%LOCALAPPDATA%`，macOS 上是
`~/Library/Caches`，Linux 上是 `$XDG_CACHE_HOME` 或 `~/.cache`。

在 `netstandard2.0` 上没有自定义解析器：不会使用 `KTAV_LIB_PATH`
环境变量，也不会使用缓存/下载回退。请依赖 NuGet 原生资产布局或
平台常规的原生库搜索路径。

## 运行时支持

- `net8.0`（通过 `LibraryImport` 支持 AOT）与 `netstandard2.0`
  （Mono / Unity / .NET Framework 4.7.2+）。
- 预编译二进制：`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`、
  `win-x64`、`win-arm64`。
- Linux 发行版需 glibc 2.17+（zigbuild 基线）。Alpine（musl）支持已规划。

## 许可证

MIT OR Apache-2.0 —— 见 [LICENSE-MIT](../../LICENSE-MIT) 和 [LICENSE-APACHE](../../LICENSE-APACHE)。

## 其他 Ktav 实现

- [`spec`](https://github.com/ktav-lang/spec) —— 规范 + 一致性测试套件
- [`rust`](https://github.com/ktav-lang/rust) —— 参考 Rust crate（`cargo add ktav`）
- [`golang`](https://github.com/ktav-lang/golang) —— Go（`go get github.com/ktav-lang/golang`）
- [`java`](https://github.com/ktav-lang/java) —— Java / JVM（`io.github.ktav-lang:ktav`，Maven Central）
- [`js`](https://github.com/ktav-lang/js) —— JS / TS（`npm install @ktav-lang/ktav`）
- [`php`](https://github.com/ktav-lang/php) —— PHP（`composer require ktav-lang/ktav`）
- [`python`](https://github.com/ktav-lang/python) —— Python（`pip install ktav`）

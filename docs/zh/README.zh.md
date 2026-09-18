# ktav — .NET 绑定

[![NuGet](https://img.shields.io/nuget/v/Ktav?style=flat-square&logo=nuget&logoColor=white&label=NuGet)](https://www.nuget.org/packages/Ktav)
[![CI](https://img.shields.io/github/actions/workflow/status/ktav-lang/csharp/CI.yml?style=flat-square&logo=github&label=CI)](https://github.com/ktav-lang/csharp/actions)
![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue?style=flat-square)
[![Playground](https://img.shields.io/badge/playground-try%20online-7c3aed?style=flat-square&logo=rocket&logoColor=white)](https://ktav-lang.github.io/)

**Languages:** [English](../../README.md) · [Русский](../ru/README.ru.md) · **简体中文**

**演练场：** 在浏览器中互转 JSON / YAML / TOML / INI ⇄ Ktav — **[ktav-lang.github.io](https://ktav-lang.github.io/)**。

[Ktav 配置格式](https://github.com/ktav-lang/spec) 的 .NET 绑定。
在参考 Rust 解析器之上的一层薄封装,运行时通过 P/Invoke 加载 ——
**使用方无需编译原生代码**,常规 `dotnet add package` 即可。

构建目标:**`net8.0`**(通过 `LibraryImport` 支持 AOT)与
**`netstandard2.0`**(`DllImport`,无 `NativeLibrary` 解析器 ——
依赖 NuGet `runtimes/` 布局或 `KTAV_LIB_PATH`)。

## 安装

```bash
dotnet add package Ktav
```

## 快速开始

### 解析 —— 按类型读取字段

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

### 遍历 —— 在 sealed `KtavValue` 层级上做 pattern matching

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

完整可运行示例:[`examples/Basic`](../../examples/Basic/Program.cs)。

## API

| 成员 | 用途 |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | 将 Ktav 文档解析为 `KtavValue` 树。 |
| `Ktav.LoadsStrict(string) -> KtavValue` | 使用严格数字词法检查解析文档。 |
| `Ktav.Dumps(KtavValue) -> string` | 将 `KtavValue` 渲染为 Ktav 文本。顶层须为 `KtavObject`。 |
| `Ktav.NativeVersion()` | 已加载 `ktav_cabi` 的版本字符串。 |
| `Ktav.DumpsForceStrings(KtavValue) -> string` | 与 `Dumps` 相同,但把每个叶子标量用原始标记 `::` 强制为 String。复合值保持结构。 |
| `Ktav.EmitCanonical(KtavValue) -> string` | 把 `KtavValue` 输出为规范 Ktav(spec § 5.9)。 |
| `Ktav.Format(string) -> string` | 把 Ktav 源文本格式化为规范化写法,**保留全部注释**。见下文。 |
| `Ktav.CanonicalFromSource(string) -> string` | 一次调用完成解析并重新输出为规范 Ktav —— 相当于 `EmitCanonical(Loads(src))`,但中间没有 `KtavValue`。像 `EmitCanonical` 一样丢弃注释与空行。 |
| `Ktav.ExpectedNativeVersion` | 本次构建对应的预期版本。 |

## 格式化 —— 规范写法,保留注释

`Format` 与 `EmitCanonical` 是两种不同的操作:

- **`EmitCanonical`** 接受 `KtavValue` 并写出规范形式。值不携带注释,
  因此没有注释可以保留。
- **`Format`** 接受源**文本**,重写其写法,同时**逐字保留每条注释**
  (spec § 3.4:注释独占一整行)。键顺序绝不改变 —— spec § 5.9 没有
  排序规则。

```csharp
Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// 注释保留;inline 复合值变为规范的多行形式
```

连续两行及以上的空行会合并为一行,紧贴括号内侧的空行填充会被丢弃,
因此格式化是一个不动点:`Format(Format(x)) == Format(x)`。对于没有
注释也没有空行的文档,输出等同于 `EmitCanonical(Loads(src))`。

## 结构化错误

解析或渲染失败时抛出 `KtavException`。除消息之外,它还携带与所有 Ktav
绑定相同的结构化信封,因此工具可以针对字段处理,而不必解析文本:

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
| `Reason` | writer 侧的原因码(spec § 5.9.0),如 `NonFiniteFloat`;解析错误时为 `null`。 |
| `Line` | 1 起算的源行号;不适用时为 `null`。 |
| `LineText` | 出错那一行的文本。 |
| `Span` | `KtavErrorSpan?` —— UTF-8 源文本中的字节偏移。 |
| `Path` | 精确解码后的键段。 |
| `Body` | 出错的值,按写法原样。 |
| `Canonical` | 规范形式本应是什么。 |
| `SpecSection` | 被违反的条款,如 `§3.6/§5.2`。 |

两处容易弄错的细节:

- **`Span` 保存的是 UTF-8 字节偏移**,而 .NET 字符串按 UTF-16 码元
  索引。在交给任何期待 `string` 索引的地方之前,或交给尚未协商
  `positionEncoding: "utf-8"` 的 LSP 客户端之前,请先转换。
- **`Path` 是键段列表,绝不是拼接后的字符串。** 字面名为 `a.b` 的键
  是**一个**段,不可能与两段路径混淆。

## 类型映射

与 Rust crate 的 `Value` 枚举完全一致 —— Ktav 每个原语一个 record,
没有有损转换:

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| 裸整数           | `KtavInteger`(文本形式 —— `ToBigInteger()` / `ToInt64()`) |
| 裸小数           | `KtavFloat`(文本形式 —— `ToDouble()`)                   |
| 其他标量         | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`(保留插入顺序)                             |

整数与浮点数以 **文本** 保存,从而任意精度与十进制的精确表示
都能在 parse / render 之间逐字节保持一致。

## 键的转义

自 spec 0.6.4 起,键段内的字面量 `.` 或 `:` 通过反斜杠书写:

```text
a\.b: v        // 键是单个段 "a.b"        -> { "a.b": "v" }
a\:b: v        // 键中包含冒号            -> { "a:b": "v" }
x.y\.z: v      // 只按第一个点切分        -> { "x": { "y.z": "v" } }
```

键中的字面量反斜杠写作 `\\`。

## 原生库的查找顺序

在 `net8.0` 上,`NativeLoader` 注册了
`NativeLibrary.SetDllImportResolver`。顺序:

1. **`$KTAV_LIB_PATH`** —— 指向本地构建的绝对路径。开发与离线 CI。
2. **NuGet `runtimes/<rid>/native/`** —— 通过 `Ktav.nupkg` 消费时
   由 .NET 默认加载器自动选取。
3. **用户缓存** —— `<userCache>/ktav-dotnet/v<版本>/…`,之前调用
   下载过的。
4. **从 GitHub Release 下载** —— 一次性从
   `github.com/ktav-lang/csharp/releases/download/v<版本>/<名称>`
   下载并缓存到 (3)。安装后首次调用需要网络。

`<userCache>` 在 Windows 是 `%LOCALAPPDATA%`,macOS 是
`~/Library/Caches`,Linux 是 `$XDG_CACHE_HOME` 或 `~/.cache`。

`netstandard2.0` 上仅 (2) 可用 —— `NativeLibrary` API 在那里不存在。

## 运行时支持

- `net8.0`(`LibraryImport` 走 AOT)与 `netstandard2.0`
  (Mono / Unity / .NET Framework 4.7.2+)。
- 预编译二进制覆盖:`linux-x64`、`linux-arm64`、`osx-x64`、
  `osx-arm64`、`win-x64`、`win-arm64`。
- Linux 需 glibc 2.17+(zigbuild 基线)。Alpine(musl)已规划。

## 许可证

MIT OR Apache-2.0 —— 见 [LICENSE-MIT](../../LICENSE-MIT) 和 [LICENSE-APACHE](../../LICENSE-APACHE)。

## 其他 Ktav 实现

- [`spec`](https://github.com/ktav-lang/spec) —— 规范 + 一致性测试套件
- [`rust`](https://github.com/ktav-lang/rust) —— 参考 Rust crate(`cargo add ktav`)
- [`golang`](https://github.com/ktav-lang/golang) —— Go(`go get github.com/ktav-lang/golang`)
- [`java`](https://github.com/ktav-lang/java) —— Java / JVM(`io.github.ktav-lang:ktav`,Maven Central)
- [`js`](https://github.com/ktav-lang/js) —— JS / TS(`npm install @ktav-lang/ktav`)
- [`php`](https://github.com/ktav-lang/php) —— PHP(`composer require ktav-lang/ktav`)
- [`python`](https://github.com/ktav-lang/python) —— Python(`pip install ktav`)

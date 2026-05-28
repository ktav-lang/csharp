# ktav — .NET 绑定

**Languages:** [English](README.md) · [Русский](README.ru.md) · **简体中文**

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

完整可运行示例:[`examples/Basic`](examples/Basic/Program.cs)。

## API

| 成员 | 用途 |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | 将 Ktav 文档解析为 `KtavValue` 树。 |
| `Ktav.Dumps(KtavValue) -> string` | 将 `KtavValue` 渲染为 Ktav 文本。顶层须为 `KtavObject`。 |
| `Ktav.NativeVersion()` | 已加载 `ktav_cabi` 的版本字符串。 |
| `Ktav.ExpectedNativeVersion` | 本次构建对应的预期版本。 |

解析 / 渲染出错时抛出 `KtavException`(消息为原生侧的 UTF-8 字符串)。

## 类型映射

与 Rust crate 的 `Value` 枚举完全一致 —— Ktav 每个原语一个 record,
没有有损转换:

| Ktav             | `KtavValue` 变体                                         |
| ---------------- | ------------------------------------------------------- |
| `null`           | `KtavNull.Instance`                                     |
| `true` / `false` | `KtavBool`                                              |
| `:i <digits>`    | `KtavInteger`(文本形式 —— `ToBigInteger()` / `ToInt64()`) |
| `:f <number>`    | `KtavFloat`(文本形式 —— `ToDouble()`)                   |
| 裸 scalar        | `KtavString`                                            |
| `[ ... ]`        | `KtavArray` (`IReadOnlyList<KtavValue>`)                |
| `{ ... }`        | `KtavObject`(保留插入顺序)                             |

带类型的整数与浮点数以 **文本** 保存,从而任意精度与十进制的精确表示
都能在 parse / render 之间逐字节保持一致。

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

MIT OR Apache-2.0 —— 见 [LICENSE-MIT](LICENSE-MIT) 和 [LICENSE-APACHE](LICENSE-APACHE)。

## 其他 Ktav 实现

- [`spec`](https://github.com/ktav-lang/spec) —— 规范 + 一致性测试套件
- [`rust`](https://github.com/ktav-lang/rust) —— 参考 Rust crate(`cargo add ktav`)
- [`golang`](https://github.com/ktav-lang/golang) —— Go(`go get github.com/ktav-lang/golang`)
- [`java`](https://github.com/ktav-lang/java) —— Java / JVM(`io.github.ktav-lang:ktav`,Maven Central)
- [`js`](https://github.com/ktav-lang/js) —— JS / TS(`npm install @ktav-lang/ktav`)
- [`php`](https://github.com/ktav-lang/php) —— PHP(`composer require ktav-lang/ktav`)
- [`python`](https://github.com/ktav-lang/python) —— Python(`pip install ktav`)

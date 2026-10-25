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

本包无需任何原生二进制也能开箱即用 —— 参见[纯托管后端](#pure-backend)
（`Ktav.UsePureBackend()` 或按调用传入 `KtavBackend.Pure`）。

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

var top = (KtavObject)global::Ktav.Ktav.Loads(src);

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

string text = global::Ktav.Ktav.Dumps(doc);
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
| `Ktav.EditValue(string, string, KtavValue) -> string` | 在源文本中按点分路径无损替换值。见“编辑源文本”一节。 |
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
global::Ktav.Ktav.Format("## why\na:   {x: 1}\n");
// "## why\na: {\n    x: 1\n}\n"
// the comment survives; the inline compound becomes canonical
// multi-line form
```

连续两个及以上空行会折叠成一个，紧贴括号内侧的空行填充会被丢弃，
因此格式化是一个不动点：`Format(Format(x)) == Format(x)`。对于没有注释、
没有空行的文档，输出等同于 `EmitCanonical(Loads(src))`。

> **尚未发布（0.9 准备版本）：** 这些编辑 API 和更新后的诊断不包含在已发布的 0.8.1 包中。原生后端需要固定的 0.9 核心（C-FFI 使用 C ABI 2）；纯实现仍完全独立。

## 编辑源文本

`Ktav.EditValue` 直接**在源文本中**替换路径上的值 —— 这是无损拼接
（splice），而不是“解析→修改→再序列化”的往返：

```csharp
var out1 = Ktav.EditValue(src, "targets.storagehost.address", new KtavString("10.0.0.9"));
var out2 = Ktav.EditValueSegments(src, new[] { "targets", "storagehost", "address" }, new KtavString("10.0.0.9"));
```

| 成员 | 用途 |
| --- | --- |
| `Ktav.EditValue(string src, string path, KtavValue replacement) -> string` | 按点分文本路径替换值（`key1.key2.0.key4`；`\.` 表示字面点号，`\\` 表示字面反斜杠）。 |
| `Ktav.EditValueSegments(string src, IReadOnlyList<string> segments, KtavValue replacement) -> string` | 同上，但传入已解码的字面段（不再做额外反转义）。 |

保证：

- 只支持*替换已有值* —— 不做创建/删除/重命名。缺失键、数组越界或
  穿过标量下钻是 `EditNotFound`；路径非法是 `EditInvalidPath`
  （`Body` 是出问题的 UTF-8 字节偏移）；当不存在无损补丁时 ——
  由 dotted/reopen 行拼成的 noncontiguous 目标、结构化替换容器内部的
  注释 —— 调用会以 `EditUnsupported` 拒绝。
- 编辑范围之外的字节原样保留：注释、空白、键的写法与顺序、BOM、
  CR/LF/CRLF 以及末尾换行的有无。无变化（no-op）的替换会按字节原样
  返回输入，即使写法不规范。
- 只有当替换会损害可读性时才最小化地展开 inline 容器（4 空格缩进、
  沿用源文件换行）；未触及的嵌套分支保持 inline。整体 inline 容器
  在可读时保持 inline；被清空的块容器保留花括号。
- 每次补丁后都会重新解析并做语义校验，因此不会悄悄返回损坏的文档。
  两个后端（native 与 pure）行为一致，并在 CI 中交叉验证。

### 源文本编辑示例

以下示例适用于尚未发布的 0.9 API，而非已发布的 0.8.1 包。只能替换已有值：不会重命名键、创建缺失路径或删除条目。各代码片段相互独立；Rust 的 `?` 假定外层函数返回 `Result`。Go 示例仅针对固定且有效的输入使用 panic；应用程序应处理返回的错误。

绑定示例使用无需原生二进制的独立纯实现。原生入口接收相同的路径和值：Go 的 `ktav.EditValue`、Java 的 `Ktav.editValue`、C# 的 `KtavApi.EditValue(..., KtavBackend.Native)`、PHP 的 `Ktav::editValue`、JS 包的 native/FFI 导出及 Python 的 `ktav.edit_value`。C ABI 后端要求 ABI 2；PyO3/N-API/WASM 要求兼容的 0.9 准备核心。Bun/Deno FFI 返回 promise，需要 `await`；此处 pure/Node/WASM 示例为同步调用。

```csharp
using Ktav;
using KtavApi = global::Ktav.Ktav;
```

#### 1. 通过点分键修改部署配置

只修改地址；注释、点分形式及相邻的用户字段逐字节保持不变。

```csharp
var source = "## deployment\ntargets.storagehost.address: old\ntargets.storagehost.user: mcp-agent\n";
var updated = KtavApi.EditValue(source, "targets.storagehost.address", new KtavString("192.168.0.88:22"), KtavBackend.Pure);
```

结果:

```ktav
## deployment
targets.storagehost.address: 192.168.0.88:22
targets.storagehost.user: mcp-agent
```

#### 2. 字面点号：文本路径或路径段

源码中带引号的键名就是 `a.b`。文本路径需要转义点号；路径段 API 直接接收解码后的键名。两个调用返回相同结果。

```csharp
var source = "\"a.b\": {port: 80}\n";
var updated = KtavApi.EditValue(source, "a\\.b.port", new KtavInteger("90"), KtavBackend.Pure);
var same = KtavApi.EditValueSegments(source, new[] {"a.b", "port"}, new KtavInteger("90"), KtavBackend.Pure);
```

结果:

```ktav
"a.b": {port: 90}
```

#### 3. 修改混合形式数组中的叶子值

此处 `0` 是数组索引；在对象中，同一个路径段表示名称为 `0` 的键。未修改的容器保持行内形式。

```csharp
var source = "servers: [{host: old, keep: yes}, standby]\n";
var updated = KtavApi.EditValueSegments(source, new[] {"servers", "0", "host"}, new KtavString("new"), KtavBackend.Pure);
```

结果:

```ktav
servers: [{host: new, keep: yes}, standby]
```

#### 4. 看似数字的字符串仍然是字符串

替换值是 String，而不是 Integer。raw 标记保留这种区别；需要数值时请传入类型化 Integer。

```csharp
var source = "port: 8080\n";
var updated = KtavApi.EditValue(source, "port", new KtavString("9090"), KtavBackend.Pure);
```

结果:

```ktav
port:: 9090
```

#### 5. 多行替换：仅展开必要的容器

修改的数组以四空格缩进展开。未修改的对象保持行内形式，verbatim 字符串内容不会重新缩进。

```csharp
var source = "a: [old, {keep: yes}]\n";
var updated = KtavApi.EditValue(source, "a.0", new KtavString("x\ny"), KtavBackend.Pure);
```

结果:

```ktav
a: [
    ((
x
y
    ))
    {keep: yes}
]
```

#### 6. 无变化时保留非规范拼写

Integer 的值已经是 1。相等判断直接返回原始字节，包括 `+1` 及分隔符周围的空白，不应用可读性阈值。

```csharp
var source = "n  : +1\n";
var updated = KtavApi.EditValue(source, "n", new KtavInteger("1"), KtavBackend.Pure);
if (updated != source) throw new System.InvalidOperationException("no-op changed source");
```

结果:

```ktav
n  : +1
```

#### 7. BOM、CRLF 及无换行符的末行

返回值保留 BOM 和 CRLF，且不会增加末尾换行。以下转义表示使这些字节清晰可见。

```csharp
var source = "\ufeffport: 8080\r\nhost: db";
var updated = KtavApi.EditValue(source, "port", new KtavInteger("9090"), KtavBackend.Pure);
```

结果:

```text
"\uFEFFport: 9090\r\nhost: db"
```

展开条件固定：替换中包含多行字符串、修改后行长增长并超过 `max(100, 原始行长)`，或单个替换字符串需要至少四个转义且编码开销达到 25%。宽度按 Unicode 标量值计算，而非 UTF-8 字节或终端列。原本已很长但没有增长的行保持不变。新层级使用四空格及源文本的 EOL。若 CR 或数据行 `))` 无法无损转换为独立行，则保留有效的行内候选；这些字符不会使所有行内编辑自动失败。

#### 8. 精确替换 Integer

源文本的 Integer 域是有符号 64 位。此值超过 JavaScript Number 的安全整数范围，但仍在 i64 内；应使用精确的宿主表示，而非已舍入的浮点值。超出 i64 的类型化 Integer 无法在源码中保持 Integer 身份，因此会被拒绝而不是截断。大型文本标识符应使用 String。Float 区分负零和正零；Object 的 no-op 相等忽略键顺序，而 Array 顺序仍然重要。

```csharp
var updated = KtavApi.EditValue("n: 1\n", "n", new KtavInteger("9007199254740993"), KtavBackend.Pure);
```

```ktav
n: 9007199254740993
```

### 0.9 的诊断与安全失败

源码坐标为包含 BOM 的 UTF-8 字节偏移，span 使用半开区间 `[start, end)`，行号从 1 开始。支持 LF、独立 CR 和 CRLF，且 CRLF 计为一个换行。一个 Unicode 字符可能占用多个字节。

未完成的 `a: [\n` 报告 `UnclosedCompound`、第 1 行和 span `3..5`：区间从最内层未闭合的起始括号开始，而非键或行的开头。原始字节 `61 3A 20 FF`（`a: ` 后接无效 UTF-8）报告 `InvalidUtf8`、第 1 行及覆盖区间 `3..4`；`line_text` 为 null，不会通过有损解码伪造文本。UTF-8 校验优先于语法处理。

非法文本路径（包括空路径或空文本段）报告 `EditInvalidPath`，偏移按路径本身的 UTF-8 字节计算。空路径段列表、缺失键、索引越界或穿过标量下钻报告 `EditNotFound`。对象中的数字段仍是键；数组索引仅接受 ASCII 数字，允许前导零，不允许正负号。

由 dotted/reopened 定义拼成的整个中间对象可能占据多个物理范围，因此报告 `EditUnsupported`；已有叶子值仍可修改。容器结构替换不能删除内部注释。失败调用不会返回部分补丁，也不会修改传入的源码。返回文本会重新解析并进行语义检查：目标必须等于替换值，其余值树必须与原文相等。

在绑定中，无效源码会先于路径验证报告普通解析错误信封。非法 wire 载荷是无虚构源码坐标的 `Message` 错误。wire 信封包含 `error`、`reason`、`line`、`line_text`、`span`、`path`、`body`、`canonical`、`spec_section`、`message`；宿主语言的访问器名称见错误章节。`body` 分别承载 `EditInvalidPath` 的十进制路径字节偏移、`EditNotFound` 的 null，以及 `EditUnsupported` 的拒绝原因。

## 结构化错误

原生解析、格式化和渲染失败会报告为 `KtavException`。除了消息之外，
它还携带原生结构化错误信封，因此工具可以直接使用字段，而不必解析
文本。但并非所有失败都是 `KtavException`：null 参数使用
`ArgumentNullException`，宿主端参数校验使用标准 .NET 参数异常，而
原生库加载可能抛出加载器异常：

```csharp
try { global::Ktav.Ktav.Loads("a: 1\na: 2\n"); }
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

## 纯托管后端

从 0.8.1 起，本库提供托管 parser、writer、canonical emitter 和保留注释的 formatter。目标是匹配 native 输出和错误信封；CI 检查一致性。Pure 不加载、下载或调用 native 代码。

可通过 `Ktav.UsePureBackend()` 全局选择，或按调用传入 `KtavBackend.Pure`（其优先级更高）。

```csharp
var value = Ktav.Loads(src, KtavBackend.Pure);
var text = Ktav.Dumps(value, KtavBackend.Pure);
var canonical = Ktav.EmitCanonical(value, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

`KTAV_BACKEND=pure` 在启动时设置一次默认值；按次选择优先。默认值原子切换。`NativeVersion()` 仍需要 native 库。CI 检查 Pure 错误一致性；spans 使用原始 UTF-8 字节偏移，包含 BOM 和换行字节。

Pure 需要现成的 `net8.0` `Ktav.dll` 和已安装兼容的 .NET 8 `Microsoft.NETCore.App` framework。仅安装更高 major 版本本身不保证 net8 consumer 可运行；这需要 host 配置受支持的 roll-forward。System.Text.Json、Memory 等 framework API 在 net8 可用，但 pure execution 不使用 native JSON wire/envelope helpers。独立使用时复制 DLL 并通过 assembly `Reference`/`HintPath` 引用，不添加 Ktav `PackageReference`。Pure 不加载、下载、调用或 fallback 到 native，也不需要网络。

多目标 NuGet 包并非无依赖：为兼容性及 native wire/error-envelope，`netstandard2.0` 保留 `System.Memory` 和 `System.Text.Json` PackageReference。保留 native netstandard 支持。SDK、NuGet restore/build、测试及文档工具属于开发工具，不是 Pure runtime 依赖。

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


以上内容仅适用于**原生**后端。纯托管后端（[纯托管后端](#pure-backend)）
不会使用 `$KTAV_LIB_PATH`、`runtimes/` 布局、缓存或网络。

在第一次原生调用之前，托管层会把 `ktav_abi_version()` 与本构建要求的
ABI 形态版本（自 0.9 的编辑导出起为 **2**）进行比对。较旧的二进制会被
以清晰的版本不匹配错误拒绝 —— 绝不会静默回退到纯托管后端。

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

>>>>> lang=en
## API

| Member | Purpose |
| --- | --- |
| `Ktav.Loads(string) -> KtavValue` | Parse a Ktav document into the `KtavValue` tree. |
| `Ktav.LoadsStrict(string) -> KtavValue` | Parse with strict numeric spelling checks. |
| `Ktav.Dumps(KtavValue) -> string` | Render a `KtavValue` back as Ktav text. Top-level must be `KtavObject` or `KtavArray`. |
| `Ktav.DumpsForceStrings(KtavValue) -> string` | Like `Dumps`, but coerces every leaf scalar to a String via the raw `::` marker. Compounds keep their structure. |
| `Ktav.EmitCanonical(KtavValue) -> string` | Render a `KtavValue` as canonical Ktav (spec § 5.9). |
| `Ktav.Format(string) -> string` | Format Ktav source into its normalised spelling, **keeping every comment**. See below. |
| `Ktav.CanonicalFromSource(string) -> string` | Parse and re-emit as canonical Ktav in one call — `EmitCanonical(Loads(src))` with no intermediate `KtavValue`. Drops comments and blank lines like `EmitCanonical` does. |
| `Ktav.NativeVersion()` | Version string reported by the loaded `ktav_cabi`. |
| `Ktav.ExpectedNativeVersion` | Version this build was compiled against. |

>>>>> lang=ru
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

>>>>> lang=zh
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


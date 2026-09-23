>>>>> lang=en
## Unreleased

### Added

- Conformance runner: the spec 0.7 fixture categories
  `unrepresentable/` (writer must refuse — currently 5 `.json` inputs)
  and `parseable-unrepresentable/` (parses fine, canonical emit must
  refuse — 4 fixtures) now execute and assert. A guard test hard-fails
  when the spec submodule is not checked out, when an unknown fixture
  category directory appears, or when any category is empty.
- API smoke tests for 0.7 behaviours: quoted keys (§ 5.3.3), quote
  characters staying literal in value position, `\uXXXX` escapes in
  inline-compound values (§ 3.7.1) including lone-surrogate rejection
  (§ 6.13) and the bare-pair-value literal behaviour (§ 3.7 scope).

- **`Ktav.Format(string)`** — the `ktav_format` C ABI symbol, exposed as
  a comment-preserving formatter over Ktav source text. Every comment
  survives verbatim (spec § 3.4: a comment owns a whole line); a run of
  two or more blank lines collapses to one and blank padding immediately
  inside a bracket is dropped, so formatting is a fixed point:
  `Format(Format(x)) == Format(x)`. Key order is never changed (spec
  § 5.9 has no sorting rule); for a document with no comments and no
  blank lines the output equals `EmitCanonical(Loads(src))`.

>>>>> lang=ru
## Unreleased

### Добавлено

- Конформный раннер: категории фикстур spec 0.7 `unrepresentable/`
  (писатель обязан отказать — сейчас 5 `.json`-входов) и
  `parseable-unrepresentable/` (парсится, canonical emit обязан
  отказать — 4 фикстуры) выполняются и проверяются. Guard-тест падает,
  если сабмодуль spec не выкачан, появилась неизвестная директория
  категории или какая-то категория пуста.
- API smoke-тесты поведения 0.7: кавыченные ключи (§ 5.3.3), литеральность
  кавычек в позиции значения, `\uXXXX`-эскейпы в значениях inline-компаундов
  (§ 3.7.1), включая отвержение одиночного суррогата (§ 6.13) и литеральное
  поведение bare-значения пары (область § 3.7).

- **`Ktav.Format(string)`** — символ C ABI `ktav_format`, открытый как
  форматтер Ktav-источника, сохраняющий комментарии. Каждый комментарий
  переживает форматирование дословно (spec § 3.4: комментарий занимает
  строку целиком); серия из двух и более пустых строк схлопывается в
  одну, а пустая отбивка сразу внутри скобки убирается — поэтому
  форматирование является неподвижной точкой:
  `Format(Format(x)) == Format(x)`. Порядок ключей не меняется (в spec
  § 5.9 нет правила сортировки); для документа без комментариев и пустых
  строк результат совпадает с `EmitCanonical(Loads(src))`.

>>>>> lang=zh
## Unreleased

### 新增

- 一致性运行器：spec 0.7 的固定值类别 `unrepresentable/`（写入方必须拒绝
  —— 当前为 5 个 `.json` 输入）与 `parseable-unrepresentable/`（可解析，
  但 canonical 输出必须拒绝 —— 4 个固定值）现在会实际执行并断言。守护测试
  在 spec 子模块未检出、出现未知类别目录或任一类别为空时硬性失败。
- 0.7 行为的 API 冒烟测试：带引号键（§ 5.3.3）、值位置中的引号保持字面量、
  inline 复合值中的 `\uXXXX` 转义（§ 3.7.1），包括孤立代理对拒绝（§ 6.13）
  与裸对值的字面量行为（§ 3.7 范围）。

- **`Ktav.Format(string)`** —— C ABI 符号 `ktav_format`，作为保留注释的
  Ktav 源文本格式化器对外开放。每条注释都逐字保留（spec § 3.4：注释独占
  一整行）；连续两行及以上的空行会合并为一行，紧贴括号内侧的空行填充会被
  丢弃，因此格式化是一个不动点：`Format(Format(x)) == Format(x)`。键顺序
  绝不改变（spec § 5.9 没有排序规则）；对于没有注释也没有空行的文档，
  输出等同于 `EmitCanonical(Loads(src))`。


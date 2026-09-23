>>>>> lang=en
### Fixed

- Conformance: the test repo-root derivation was off by one directory
  and could silently target a sibling `spec` checkout — or, in CI and
  worktrees, run zero fixture tests while staying green. The runner now
  resolves this repo's own submodule. Invalid-UTF-8 fixtures (§ 6.15)
  are exercised through the byte-level native entry point instead of
  lossy text decoding that hid the defect.
- Conformance also read `spec/versions/0.7/tests` after the submodule
  was re-pinned to `0.8.0` — the path was hardcoded, not derived from
  the pin. It now reads `spec/versions/0.8/tests` and executes every
  fixture category the corpus ships, including the new `strict-lossy/`
  (`Loads` must equal the lax value, `LoadsStrict` must throw with the
  matching reason, body and canonical form). A guard test fails the
  build if an unrecognized category directory appears under the
  corpus, so a future addition can't repeat this silently.

>>>>> lang=ru
### Исправлено

- Конформа: вывод корня репозитория в тестах был смещён на одну
  директорию и мог молча указывать на соседний checkout `spec` — или, в
  CI и worktree, выполнять ноль фикстур при зелёном статусе. Раннер
  теперь нацелен на собственный сабмодуль этого репозитория. Байтовые
  фикстуры invalid UTF-8 (§ 6.15) гоняются через байтовый вход в
  нативный слой, а не через потерьное текстовое декодирование,
  скрывавшее дефект.
- Conformance также читал `spec/versions/0.7/tests` после того, как
  сабмодуль был перезакреплён на `0.8.0` — путь был захардкожен, а не
  выведен из пина. Теперь читает `spec/versions/0.8/tests` и исполняет
  все категории корпуса, включая новую `strict-lossy/` (`Loads` обязан
  совпасть с lax-значением, `LoadsStrict` обязан отказать с
  соответствующей причиной, телом и канонической формой). Guard-тест
  обрушивает сборку при появлении нераспознанной категории в корпусе,
  чтобы это не повторилось молча.

>>>>> lang=zh
### 修复

- 一致性：测试中的仓库根目录推导偏差一级，可能静默指向兄弟 `spec`
  检出 —— 或者在 CI 与 worktree 中零固定值仍保持绿色。运行器现在指向本
  仓库自己的子模块。invalid-UTF-8 固定值（§ 6.15）改为通过字节级原生入口
  驱动，而不是会掩盖缺陷的有损文本解码。
- 一致性测试在子模块重新固定到 `0.8.0` 之后，仍读取
  `spec/versions/0.7/tests`——路径是硬编码的，并非从固定版本推导而来。
  现在读取 `spec/versions/0.8/tests`，并执行语料中的每个类别，包括
  新增的 `strict-lossy/`（`Loads` 必须等于 lax 值，`LoadsStrict` 必须以
  匹配的原因、body 与规范形式抛出异常）。一个 guard 测试会在语料中
  出现无法识别的类别目录时使构建失败，以防止这个问题再次悄然发生。


>>>>> lang=en
### 3. Public API changes note compatibility

If you touch anything exported from the `Ktav` namespace, say in the
PR description whether it is:

- **semver-compatible** (additions, looser types, doc changes); or
- **semver-breaking** (renamed / removed items, changed signatures,
  tightened types) — in which case the version bump lands in the next
  MINOR while we are pre-1.0.

Update the CHANGELOG source units under `root-docs/CHANGELOG/` (all
three `>>>>> lang=` blocks) in the same PR and regenerate the output.

>>>>> lang=ru
### 3. Изменения публичного API отмечают совместимость

Если вы трогаете что-либо, экспортируемое из пространства имён `Ktav`,
укажите в описании PR, является ли оно:

- **semver-совместимым** (добавления, более свободные типы, изменения
  в документации); или
- **semver-breaking** (переименованные / удалённые элементы,
  изменённые сигнатуры, ужесточённые типы) — в этом случае бамп версии
  попадёт в следующий MINOR, пока мы pre-1.0.

Обновите CHANGELOG-юниты под `root-docs/CHANGELOG/` (все три блока
`>>>>> lang=`) в том же PR и перегенерируйте вывод.

>>>>> lang=zh
### 3. 公共 API 变更需注明兼容性

如果你修改了 `Ktav` 命名空间中导出的任何内容，请在 PR 描述中说明它是：

- **semver 兼容**（新增、更宽松的类型、文档变更）；或
- **semver 破坏性**（重命名 / 移除的项、更改的签名、收紧的类型）
  —— 这种情况下版本号提升会落在下一个 MINOR 中，毕竟我们还处于
  pre-1.0。

在同一个 PR 中更新 `root-docs/CHANGELOG/` 下的 CHANGELOG 源单元
(全部三个 `>>>>> lang=` 块)并重新生成产物。


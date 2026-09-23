>>>>> lang=en
### Changed

- **Picked up `ktav 0.5.0`** — tracks upstream Rust crate `0.5.0`.
  See the [`ktav` crate CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)
  for the full delta.
- **License changed to `MIT OR Apache-2.0`** (dual-licensed). Added
  `LICENSE-MIT` and `LICENSE-APACHE` files; the former `LICENSE` file
  is kept as `LICENSE-MIT`.

### Spec

- spec submodule synced to `v0.5.0` — adds the canonical-form fixtures
  (`*.canonical.ktav`) alongside each `valid/` fixture. The
  `SpecConformance` test skips `.canonical.ktav` files during the
  round-trip suite (they are used only by the canonical-emit tests).

NuGet package: **`Ktav`**, version 0.5.0.

>>>>> lang=ru
### Изменено

- **Подхвачен `ktav 0.5.0`** — отслеживает upstream Rust crate
  `0.5.0`. Полный diff см. в
  [`CHANGELOG` крейта `ktav`](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md).
- **Лицензия изменена на `MIT OR Apache-2.0`** (двойная). Добавлены
  файлы `LICENSE-MIT` и `LICENSE-APACHE`; прежний файл `LICENSE`
  сохранён как `LICENSE-MIT`.

### Spec

- подмодуль spec синхронизирован с `v0.5.0` — добавляет канонические
  фикстуры (`*.canonical.ktav`) рядом с каждой фикстурой `valid/`.
  Тест `SpecConformance` пропускает файлы `.canonical.ktav` в
  round-trip наборе (они используются только тестами канонической
  эмиссии).

NuGet-пакет: **`Ktav`**, версия 0.5.0.

>>>>> lang=zh
### 变更

- **已采用 `ktav 0.5.0`** —— 跟踪上游 Rust crate `0.5.0`。
  完整差异见
  [`ktav` crate 的 CHANGELOG](https://github.com/ktav-lang/rust/blob/main/CHANGELOG.md)。
- **许可证更改为 `MIT OR Apache-2.0`**（双许可）。新增
  `LICENSE-MIT` 与 `LICENSE-APACHE` 文件；原 `LICENSE`
  文件保留为 `LICENSE-MIT`。

### Spec

- spec 子模块同步至 `v0.5.0` —— 在每个 `valid/` fixture 旁新增
  canonical 形式 fixture（`*.canonical.ktav`）。`SpecConformance`
  测试在 round-trip 套件中跳过 `.canonical.ktav` 文件
  （它们仅由 canonical 输出测试使用）。

NuGet 包：**`Ktav`**，版本 0.5.0。


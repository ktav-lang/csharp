>>>>> lang=en
## 0.1.1 — 2026-04-26

### Changed

- **Picked up `ktav 0.1.4`** — the upstream Rust crate's untyped
  `parse() → Value` path (which `cabi` uses) is now ~30% faster on
  small documents and ~13% faster on large ones, just from a one-
  line `Frame::Object` capacity tweak (4 → 8). Every `Ktav.Loads`
  call benefits transparently.

NuGet package: **`Ktav`**, version 0.1.1.

>>>>> lang=ru
## 0.1.1 — 2026-04-26

### Изменено

- **Подхватили `ktav 0.1.4`** — untyped путь `parse() → Value` в
  upstream Rust crate (тот, что использует `cabi`) теперь ~30%
  быстрее на маленьких документах и ~13% на больших, благодаря
  однострочной правке initial capacity для `Frame::Object` (4 → 8).
  Каждый `Ktav.Loads` получит ускорение прозрачно.

NuGet-пакет: **`Ktav`**, версия 0.1.1.

>>>>> lang=zh
## 0.1.1 —— 2026-04-26

### 变更

- **升级到 `ktav 0.1.4`** —— 上游 Rust crate 中 `cabi` 使用的 untyped
  `parse() → Value` 路径,小文档加速约 30%、大文档加速约 13%,只是
  `Frame::Object` 的初始容量微调(4 → 8)。每次 `Ktav.Loads` 都会
  透明地受益。

NuGet 包:**`Ktav`**,版本 0.1.1。


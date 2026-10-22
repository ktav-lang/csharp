>>>>> lang=en
## Unreleased

### Added

- A complete pure managed backend (shipped in 0.8.1) (`KtavBackend.Pure`): parser
  (lax/strict), writer, force-strings writer, canonical emitter and the
  comment-preserving formatter, all in managed code with no native
  binary. Choose it with `Ktav.UsePureBackend()`, the per-call
  `KtavBackend` parameter on every operation, or the
  `KTAV_BACKEND=pure` environment variable. Designed to produce the same
  output and error envelopes as the native backend; parity is enforced
  by the CI conformance and cross-backend suites. Error spans remain
  original UTF-8 byte offsets.

### Changed

- Upgraded to Rust `ktav` 0.8.1 (spec 0.8.1) and bumped the package
  version to 0.8.1.

>>>>> lang=ru
## Unreleased

### Добавлено

- Полностью чистый управляемый бэкенд (`KtavBackend.Pure`): парсер
  (lax/strict), райтер, райтер с приведением к строкам, канонический
  эмиттер и форматтер с сохранением комментариев — весь управляемый
  код, без нативного бинарника. Включается через `Ktav.UsePureBackend()`,
  параметр `KtavBackend` в каждом вызове или переменную окружения
  `KTAV_BACKEND=pure`. Спроектирован так, чтобы выдавать тот же вывод
  и те же конверты ошибок, что и нативный бэкенд; соответствие
  контролируют CI-наборы конформности и кросс-бэкенд-тестов. Смещения
  ошибок остаются исходными байтовыми смещениями UTF-8.

### Изменено

- Обновление до Rust `ktav` 0.8.1 (spec 0.8.1); версия пакета поднята
  до 0.8.1.

>>>>> lang=zh
## Unreleased

### 新增

- 完整的纯托管后端（`KtavBackend.Pure`）：解析器（lax/strict）、写出器、
  强制字符串写出器、规范化输出器和保留注释的格式化器，全部为托管代码，
  无需原生二进制。可通过 `Ktav.UsePureBackend()`、每次调用的
  `KtavBackend` 参数或环境变量 `KTAV_BACKEND=pure` 启用。设计目标与
  原生后端产出相同的输出和错误信封；一致性由 CI 的规范符合性与
  跨后端测试套件保证。错误区间仍为 UTF-8 原始字节偏移。

### 变更

- 升级到 Rust `ktav` 0.8.1（spec 0.8.1），包版本提升至 0.8.1。


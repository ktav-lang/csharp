>>>>> lang=en
- Targets `ktav 0.8` and spec 0.8.0, including the 0.8 numeric inference
  rules. This release also includes spec 0.7 behavior: quoted keys,
  scoped Unicode escapes, and the corresponding parse/error rules.
- Rust MSRV is 1.71. The C ABI crate now uses `ktav::declare_cabi!()`;
  its exported symbols are unchanged, and its dependency floor is `ktav 0.8`.
- The spec submodule is pinned to v0.8.0. The package and native library
  expectation are 0.8.0, and the prebuilt-library fallback targets the
  matching v0.8.0 release asset.
- `KtavException.Message` uses the native error envelope's message when
  provided; structured fields remain available for programmatic handling.
- Host-side argument validation continues to use standard .NET argument
  exceptions; native library loading can raise loader exceptions.

### Fixed

- Conformance tests resolve this checkout's pinned fixture corpus rather
  than a sibling checkout, and invalid UTF-8 inputs use the byte-level API.
- Fixture manifest discovery and expected-result oracles are corrected,
  so the executed corpus is the pinned one and assertions match its cases.
- Strict-lossy fixtures verify that lax parsing yields the expected value
  while strict parsing reports the matching reason, body, and canonical form.
- Numeric and API documentation now reflects the core's representable
  domain and the behavior of both target frameworks.

>>>>> lang=ru
- Поддерживаются `ktav 0.8` и spec 0.8.0, включая правила вывода типов
  чисел версии 0.8. В этот релиз также вошло поведение spec 0.7:
  кавыченные ключи, ограниченные контекстом Unicode-эскейпы и связанные
  правила разбора и ошибок.
- MSRV Rust — 1.71. C ABI-крейт теперь использует `ktav::declare_cabi!()`;
  набор экспортируемых символов не изменился, нижняя граница зависимости — `ktav 0.8`.
- Сабмодуль spec закреплён на v0.8.0. Версии пакета и ожидаемой нативной
  библиотеки — 0.8.0; резервная загрузка готовой библиотеки берёт asset
  соответствующего релиза v0.8.0.
- `KtavException.Message` использует сообщение из нативного конверта ошибки,
  если оно предоставлено; структурированные поля остаются доступны для кода.
- Проверка аргументов на стороне .NET по-прежнему использует стандартные
  исключения аргументов; загрузка нативной библиотеки может выбросить
  исключения загрузчика.

### Исправлено

- Conformance-тесты находят закреплённый корпус именно этого checkout,
  а не соседний репозиторий; invalid UTF-8 проверяется через байтовый API.
- Поиск категорий в манифесте фикстур и эталонные ожидаемые результаты
  исправлены: выполняется закреплённый корпус, а проверки соответствуют его случаям.
- Фикстуры `strict-lossy` проверяют, что lax-разбор выдаёт ожидаемое значение,
  а strict-разбор сообщает соответствующие причину, тело и каноническую форму.
- Документация о числах и API приведена в соответствие с представимым
  доменом ядра и поведением обеих целевых платформ.

>>>>> lang=zh
- 跟踪 `ktav 0.8` 和 spec 0.8.0，包括 0.8 数字类型推断规则。NuGet
  包与预期原生库的版本均为 0.8.0。本次发布也包含 spec 0.7 行为：
  带引号键、按上下文处理的 Unicode 转义及相应的解析与错误规则。
- Rust MSRV 为 1.71。C ABI crate 现使用 `ktav::declare_cabi!()`；导出
  符号保持不变，依赖下限为 `ktav 0.8`。
- spec 子模块固定到 v0.8.0。预编译库回退下载指向对应的 v0.8.0 发布资产。
- 如果原生错误信封提供了消息，`KtavException.Message` 就采用该消息；
  结构化字段仍可供程序处理。
- 宿主端参数校验仍使用标准 .NET 参数异常；原生库加载可能抛出加载器异常。

### 修复

- 一致性测试解析本检出中的固定语料，而非相邻检出；invalid UTF-8
  输入通过字节级 API 测试。
- 固定值清单中的类别发现和预期结果校验已修正，因此执行的是固定语料，
  且断言与其中各用例相符。
- `strict-lossy` 固定值会验证 lax 解析得到预期值，而 strict 解析会报告
  匹配的原因、body 与规范形式。
- 数字与 API 文档已按核心可表示范围及两个目标框架的实际行为修正。


>>>>> lang=en
```csharp
var doc = Ktav.Loads(src, KtavBackend.Pure);
var outText = Ktav.Dumps(doc, KtavBackend.Pure);
var canon = Ktav.EmitCanonical(doc, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

The `KTAV_BACKEND=pure` environment variable sets the initial default
(handled once at startup), which is how the pure CI job runs the whole
test suite without a native build.

Limits and guarantees:

- The error envelope contract (messages, categories, spec sections, byte
  spans) is designed to match the native backend; parity is enforced by
  the CI conformance and cross-backend suites. Spans are UTF-8
  **original-byte** offsets (BOM
  and CRLF bytes included, never subtracted).
- Thread-safety: `UsePureBackend()` / `UseNativeBackend()` swap the
  process-wide default atomically (`Interlocked`); each call copies the
  backend choice once at entry, so a mid-flight flip cannot tear a call.
  The per-call parameter is authoritative — prefer setting the default
  once at startup, or always passing it explicitly.
- `NativeVersion()` still refers to the native library and requires it;
  everything else works without any native artifact.

>>>>> lang=ru
```csharp
var doc = Ktav.Loads(src, KtavBackend.Pure);
var outText = Ktav.Dumps(doc, KtavBackend.Pure);
var canon = Ktav.EmitCanonical(doc, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

Переменная окружения `KTAV_BACKEND=pure` задаёт начальный дефолт
(читается один раз при старте) — именно так CI-задача чистого бэкенда
прогоняет весь набор тестов без нативной сборки.

Ограничения и гарантии:

- Контракт конверта ошибок (сообщения, категории, секции спеки, байтовые
  смещения) спроектирован как совпадающий с нативным бэкендом;
  соответствие контролируют CI-наборы конформности и
  кросс-бэкенд-тестов. Смещения — это **исходные байты** UTF-8
  (включая BOM и CR/CRLF, никогда не вычитаются).
- Потокобезопасность: `UsePureBackend()` / `UseNativeBackend()` атомарно
  меняют процессный дефолт (`Interlocked`); каждый вызов фиксирует выбор
  бэкенда один раз на входе, поэтому смена дефолта «на лету» не может
  разорвать вызов. Авторитетен параметр вызова — задавайте дефолт один
  раз при старте или всегда передавайте явно.
- `NativeVersion()` по-прежнему относится к нативной библиотеке и
  требует её; всё остальное работает без нативных артефактов.

>>>>> lang=zh
```csharp
var doc = Ktav.Loads(src, KtavBackend.Pure);
var outText = Ktav.Dumps(doc, KtavBackend.Pure);
var canon = Ktav.EmitCanonical(doc, KtavBackend.Pure);
var formatted = Ktav.Format(src, KtavBackend.Pure);
var strict = Ktav.LoadsStrict(src, KtavBackend.Pure);
```

环境变量 `KTAV_BACKEND=pure` 会在启动时设置初始默认值（只读取一次），
纯后端 CI 任务正是以这种方式在没有原生构建的情况下运行全部测试。

限制与保证：

- 错误信封契约（消息、类别、规范章节、字节区间）的设计目标与原生
  后端一致；一致性由 CI 的规范符合性与跨后端测试套件保证。区间是
  UTF-8 的**原始字节**偏移（包含 BOM 与 CRLF 字节，从不做扣减）。
- 线程安全：`UsePureBackend()` / `UseNativeBackend()` 通过
  `Interlocked` 原子地交换进程级默认值；每次调用在入口处只读取一次
  后端选择，因此中途切换默认值不会撕裂调用。按调用传入的参数具有
  最终决定权 —— 建议在启动时设置一次默认值，或始终显式传参。
- `NativeVersion()` 仍然指向原生库且需要它；其余所有操作在没有原生
  工件时均可正常工作。


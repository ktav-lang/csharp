>>>>> lang=en
## The pure backend (no native binary)

Since 0.8.1 the library ships a complete managed parser, writer,
canonical emitter and comment-preserving formatter. It is designed to
produce the same output and error envelopes as the Rust-backed native
backend; parity is enforced by the CI conformance and cross-backend
suites. It never loads, downloads or shells out to a native
binary — useful for AOT-strict, air-gapped or no-native-artifact
environments.

Choose it globally:

```csharp
Ktav.UsePureBackend();   // process-wide default; UseNativeBackend() reverts
var doc = Ktav.Loads("a: 1\n");
```

…or per call (this parameter always wins over the process default):

>>>>> lang=ru
## Чистый бэкенд (без нативной библиотеки)

С версии 0.8.1 библиотека содержит полностью управляемый парсер,
канонический эмиттер и форматтер с сохранением комментариев.
Он спроектирован так, чтобы выдавать тот же результат и те же конверты
ошибок, что и Rust-нативный бэкенд; соответствие контролируют CI-наборы
конформности и кросс-бэкенд-тестов. Он никогда не загружает, не
скачивает и не запускает нативный бинарник —
удобно для AOT-строгих, изолированных сред и окружений без нативных
артефактов.

Включить глобально:

```csharp
Ktav.UsePureBackend();   // процессный дефолт; UseNativeBackend() возвращает обратно
var doc = Ktav.Loads("a: 1\n");
```

…или на уровне вызова (этот параметр всегда важнее процессного дефолта):

>>>>> lang=zh
## 纯托管后端（无需原生二进制）

从 0.8.1 起，本库内置完整的托管解析器、写出器、规范化输出器和保留
注释的格式化器。它的设计目标是与 Rust 原生后端产出相同的输出和错误
信封；一致性由 CI 的规范符合性与跨后端测试套件保证。它绝不加载、
下载或调用任何原生二进制 —— 适合 AOT 严格、离线隔离或没有
原生工件的环境。

全局启用：

```csharp
Ktav.UsePureBackend();   // 进程级默认；UseNativeBackend() 可切回
var doc = Ktav.Loads("a: 1\n");
```

……或按调用指定（该参数始终优先于进程级默认值）：


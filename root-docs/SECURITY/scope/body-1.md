>>>>> lang=en
## Scope

Issues that count as security problems for this package:

- Out-of-bounds reads / writes or panics in the native `ktav_cabi`
  shared library that crash or hang the host CLR. The library is
  loaded via P/Invoke through .NET's native loader, so a native crash
  tears down the entire process — no managed `try/catch` can stop it.
- Runaway memory or CPU when parsing crafted input.
- Incorrect FFI memory handling on either side of the boundary
  (double-free, missing free, reading freed buffers across the
  `ktav_free` boundary).
- Any behaviour that allows crafted Ktav input to escape the expected
  value-domain (arbitrary code execution in the loaded library,
  uninitialised-memory disclosure, etc.).
- A download-time vector: `NativeLoader` fetches the prebuilt binary
  from the matching GitHub Release. Reports about TLS / integrity-check
  gaps in that path belong here.

Issues that are **not** security problems here — please use regular
issues for these:

- Performance regressions without crash / hang characteristics.
- Problems in the Ktav format itself — those belong in
  [`ktav-lang/spec`](https://github.com/ktav-lang/spec).
>>>>> lang=ru
## Область

Что считается проблемой безопасности для этого пакета:

- Out-of-bounds чтения / записи или паники в нативной библиотеке
  `ktav_cabi`, которые роняют или вешают хост-CLR. Библиотека
  загружается через P/Invoke через нативный загрузчик .NET, поэтому
  нативный краш уносит весь процесс — никакой managed `try/catch` его
  не остановит.
- Неконтролируемое потребление памяти или CPU при разборе специально
  сформированного входа.
- Некорректное обращение с памятью на FFI-границе с любой стороны
  (double-free, missing free, чтение освобождённого буфера за границей
  `ktav_free`).
- Любое поведение, при котором сформированный Ktav-вход выходит за
  ожидаемый value-домен (произвольное выполнение кода в загруженной
  библиотеке, раскрытие неинициализированной памяти и т. п.).
- Вектор времени загрузки: `NativeLoader` скачивает прекомпилированный
  бинарь из соответствующего GitHub Release. Репорты про пробелы в TLS /
  проверке целостности на этом пути сюда.

Что здесь **не** считается проблемой безопасности — пожалуйста,
используйте обычные issue для этого:

- Регрессии производительности без характеристик crash / hang.
- Проблемы в самом формате Ktav — им место в
  [`ktav-lang/spec`](https://github.com/ktav-lang/spec).
>>>>> lang=zh
## 范围

以下问题会按本包的安全问题处理：

- 原生 `ktav_cabi` 共享库中的越界读写或 panic，导致宿主 CLR 崩溃或挂起。
  该库通过 P/Invoke 经 .NET 的原生加载器加载，因此原生崩溃会直接拉垮
  整个进程 —— 任何 managed `try/catch` 都拦不住。
- 解析构造输入时出现失控的内存或 CPU 消耗。
- FFI 边界任一侧的错误内存处理（double-free、missing free、跨
  `ktav_free` 边界读取已释放的缓冲区）。
- 任何允许构造的 Ktav 输入逃逸出预期值域的行为（加载库内的任意代码
  执行、未初始化内存泄露等）。
- 下载期向量：`NativeLoader` 会从对应的 GitHub Release 拉取预编译二进制。
  该路径上的 TLS / 完整性校验缺陷也上报到这里。

以下**不**算本包的安全问题 —— 请走普通 issue：

- 没有崩溃 / 挂起特征的性能回归。
- Ktav 格式本身的问题 —— 这类问题属于
  [`ktav-lang/spec`](https://github.com/ktav-lang/spec)。

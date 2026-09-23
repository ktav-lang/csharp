# 安全策略

**Languages:** [English](../SECURITY.md) · [Русский](../ru/SECURITY.ru.md) · **简体中文**

## 支持的版本

本包仍处于 pre-1.0 阶段，仅维护**最新发布的次版本**。安全修复会进入
`main`，并在数日内以 PATCH 发布。

| 版本    | 支持                   |
|---------|------------------------|
| 0.8.x   | ✅                     |
| 更早    | ❌ —— 请先升级         |

## 上报漏洞

**请不要为安全问题开公开 issue。**

请发邮件至 **phpcraftdream@gmail.com**，并提供：

- 对漏洞的简短描述。
- 复现步骤或代码片段（触发该行为的 Ktav 输入、受影响的 API、预期结果
  vs 实际结果）。
- 观察到问题时所用的 Ktav 版本（NuGet 包版本 +
  `Ktav.NativeVersion()` 的输出，以及 .NET 运行时、OS、arch —— 以便
  确认当时使用的是哪个预编译 `ktav_cabi`）。
- 你偏好的披露时间线（如有）。

你应在 **72 小时**内收到确认。对于高影响问题，已发布的修复通常在
**一周**内跟进；如果修复需要与 Rust crate 或格式规范协同推进，则可能
更久。

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

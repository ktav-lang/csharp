>>>>> lang=en
## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Email **phpcraftdream@gmail.com** with:

- A short description of the vulnerability.
- Steps or a snippet to reproduce it (Ktav input that triggers the
  behaviour, the affected API, expected vs actual).
- The Ktav version you observed it on (NuGet package version +
  `Ktav.NativeVersion()` output, plus the .NET runtime, OS, and arch
  so we know which prebuilt `ktav_cabi` was in use).
- Your disclosure timeline preference, if you have one.

You should get an acknowledgement within **72 hours**. A published
fix typically follows within **a week** for high-impact issues, longer
if the fix needs to coordinate with the Rust crate or the format spec.

>>>>> lang=ru
## Сообщение об уязвимости

**Пожалуйста, не открывайте публичные issue по проблемам безопасности.**

Напишите на **phpcraftdream@gmail.com** и укажите:

- Краткое описание уязвимости.
- Шаги или фрагмент для воспроизведения (Ktav-вход, который запускает
  поведение; затронутый API; ожидаемое против фактического).
- Версию Ktav, на которой наблюдалось (версия NuGet-пакета + вывод
  `Ktav.NativeVersion()`, плюс .NET-рантайм, OS и arch — чтобы понять,
  какой прекомпилированный `ktav_cabi` использовался).
- Предпочтительный таймлайн раскрытия, если у вас он есть.

Подтверждение получите в течение **72 часов**. Опубликованный фикс
обычно выходит в течение **недели** для высокоприоритетных проблем,
дольше — если фикс нужно согласовать с Rust-крейтом или со спецификацией
формата.

>>>>> lang=zh
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


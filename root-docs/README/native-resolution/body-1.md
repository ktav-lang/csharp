>>>>> lang=en
## How the native library is resolved

On `net8.0`, `NativeLoader` registers a
`NativeLibrary.SetDllImportResolver` callback. Resolution order:

1. **`$KTAV_LIB_PATH`** — absolute path to a local build. Most useful
   for development and air-gapped CI.
2. **NuGet `runtimes/<rid>/native/`** layout — picked up automatically
   by .NET's default loader when consumed via `Ktav.nupkg`.
3. **User cache** — `<userCache>/ktav-dotnet/v<version>/…`, downloaded
   on a previous call.
4. **GitHub Release download** — fetched once from
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   and cached under (3). Requires network on first call after install.

`<userCache>` is `%LOCALAPPDATA%` on Windows, `~/Library/Caches` on
macOS, `$XDG_CACHE_HOME` or `~/.cache` on Linux.

On `netstandard2.0`, there is no custom resolver: the `KTAV_LIB_PATH`
environment variable and the cache/download fallback are not used. Rely
on NuGet's native asset layout or the platform's normal native-library
search paths.

>>>>> lang=ru
## Как резолвится нативная библиотека

На `net8.0` `NativeLoader` регистрирует колбэк
`NativeLibrary.SetDllImportResolver`. Порядок разрешения:

1. **`$KTAV_LIB_PATH`** — абсолютный путь к локальной сборке. Полезнее
   всего для разработки и изолированного CI.
2. **Компоновка NuGet `runtimes/<rid>/native/`** — подхватывается
   автоматически штатным загрузчиком .NET при потреблении через
   `Ktav.nupkg`.
3. **Кэш пользователя** — `<userCache>/ktav-dotnet/v<version>/…`,
   загруженный предыдущим вызовом.
4. **Загрузка с GitHub Release** — файл один раз скачивается с
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   и кладётся в (3). Требует сети на первом вызове после установки.

`<userCache>` — это `%LOCALAPPDATA%` на Windows, `~/Library/Caches` на
macOS, `$XDG_CACHE_HOME` или `~/.cache` на Linux.

На `netstandard2.0` собственного резолвера нет: переменная `KTAV_LIB_PATH`
и резервные кэш/загрузка не используются. Используйте компоновку NuGet
с нативными файлами или стандартные пути поиска библиотек платформы.

>>>>> lang=zh
## 原生库的解析顺序

在 `net8.0` 上，`NativeLoader` 会注册一个
`NativeLibrary.SetDllImportResolver` 回调。解析顺序如下：

1. **`$KTAV_LIB_PATH`** —— 指向本地构建的绝对路径。最适合开发和离线
   CI。
2. **NuGet `runtimes/<rid>/native/` 布局** —— 通过 `Ktav.nupkg` 消费时，
   由 .NET 的默认加载器自动选取。
3. **用户缓存** —— `<userCache>/ktav-dotnet/v<version>/…`，由之前的调用
   下载。
4. **从 GitHub Release 下载** —— 一次性从
   `github.com/ktav-lang/csharp/releases/download/v<version>/<asset>`
   获取，并缓存到 (3)。安装后首次调用需要联网。

`<userCache>` 在 Windows 上是 `%LOCALAPPDATA%`，macOS 上是
`~/Library/Caches`，Linux 上是 `$XDG_CACHE_HOME` 或 `~/.cache`。

在 `netstandard2.0` 上没有自定义解析器：不会使用 `KTAV_LIB_PATH`
环境变量，也不会使用缓存/下载回退。请依赖 NuGet 原生资产布局或
平台常规的原生库搜索路径。


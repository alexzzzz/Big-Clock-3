# CONTEXT — Big Clock

## Glossary

- **Big Clock (Часы)** — Android-приложение, один экран, полноэкранные цифровые часы ЧЧ:ММ с мигающим `:`.
- **Display (Отображение)** — строка ЧЧ:ММ, где ЧЧ и ММ — двузначные, `:` — отдельный overlay-элемент, мигает 500 мс вкл / 500 мс выкл, синхронизировано к системной секунде, занимает половину обычной ширины.
- **Fullscreen (Полный экран)** — immersive режим, системные status/nav панели скрыты, `keepScreenOn=true`, принудительный `sensorLandscape`, отрисовка под вырезами `ShortEdges`.
- **Tick (Тик)** — обновление дисплея: ЧЧ:ММ — раз в минуту, `:` — раз в 500 мс, выравнивание к началу секунды через `Handler.postAtTime`.
- **Font (Шрифт)** — `digital-7 (mono).ttf` (`Resources/font/digital_7_mono.ttf`), моноширинный семисегментный, подключён как `@font/digital_7_mono`.
- **Compressed Space (Сжатый пробел)** — пробел между ЧЧ и ММ в `clockText`, сжатый `ScaleXSpan(0.5)` до 50% ширины, обеспечивает узкий зазор для оверлея двоеточия.
- **Colon Overlay (Оверлей двоеточия)** — отдельный `TextView` `colonText`, наложенный по центру поверх `clockText` (`bias 0.5`), мигает через `Alpha` без влияния на компоновку, визуально занимает 50% ширины благодаря узкому зазору.
- **Maximize (Максимизация)** — ручной подбор размера шрифта по `TextPaint` (`88 88` с 50% пробелом) бинарным поиском до заполнения `1.0×1.0` контейнера, без `autoSize`.

## Context Map

Single-context — один bounded context (отображение времени), без доменных sub-модулей.

## Build

- **TargetFramework** — `net11.0-android` (`Microsoft.NET.Sdk`), `SupportedOSPlatformVersion 26`, `ApplicationId com.alexzzzz.bigclock` (`BigClock.csproj:4`).
- **Solution** — `BigClock3.sln` (Visual Studio 17, Debug/Release AnyCPU).
- **Toolchain** — .NET 11 preview + workload `android` 37.0, JDK 21 LTS (`Android/jdk`), Android SDK 37 (`build-tools 36.0.0`), сборка `dotnet build -c Release`.

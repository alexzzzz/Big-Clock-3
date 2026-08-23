# Research: Выбор стека для Big Clock — .NET vs Kotlin

> Ticket: #2 — Выбор стека для Android: .NET MAUI / .NET for Android vs Kotlin+Compose
> Branch: research/stack-choice
> Date: 2026-08-23
> Status: draft (подлежит закрытию тикета после гриллинга #3)

## Варианты (ответ на вопрос "какие есть?")

### 1. Kotlin + Jetpack Compose (native, recommended default)
- **Язык/тулинг:** Kotlin, Android Studio / CLI Gradle (Kotlin DSL), `compileSdk 34/35`, `minSdk 24`.
- **UI:** `setContent { }` Composable, `Text` с `fontSize` подогнанным под `BoxWithConstraints`, `LaunchedEffect` + `delay`.
- **Системные API:** `WindowCompat.setDecorFitsSystemWindows`, `WindowInsetsControllerCompat`, `FLAG_KEEP_SCREEN_ON` / `android:keepScreenOn="true"`, `screenOrientation="sensorLandscape"` — все 1:1.
- **Размер APK:** ~2–4 MB (R8), холодный старт <300 мс.
- **Плюсы:** минимальный размер, нативный перф, весь StackOverflow/док, обновление OS — первый класс.
- **Минусы:** Kotlin не знаком, если только .NET-бэкграунд.

### 2. Kotlin + View XML (legacy View system)
- Аналогично #1, но layout в XML. Чуть проще для такого статичного экрана. Выбор между Compose и View — отдельный ADR, но оба native.

### 3. .NET for Android (`net10.0-android`) — Xamarin.Android successor
- **Язык:** C# 12/13, .NET 10 SDK, workload `android` (`dotnet workload install android`), проект `Sdk="Microsoft.Android.Sdk"`, `TargetFramework net10.0-android`.
- **UI:** либо AXML layouts + `TextView` (познакомее dotnet-разработчику с Android), либо через `AndroidX` + код.
- **Системные API:** полные биндинги `Android.Views.Window`, `AndroidX.Core.View.WindowCompat`, `WindowInsetsControllerCompat` — доступны, но иногда отстают на 1 версию от Kotlin.
- **Размер APK:** ~10–18 MB (AOT + runtime), даже для пустого проекта.
- **Плюсы:** остаёшься в C#/dotnet, можно шарить код, IDE — Rider/VS.
- **Минусы:** больший APK, меньше примеров для immersive/keepScreenOn, дольше сборка, нужен установленный .NET Android workload.

### 4. .NET MAUI (`net10.0-android` via MAUI)
- **Язык:** C# + XAML, один проект на Android/iOS/Win. Workload `maui` (`dotnet workload install maui`).
- **UI:** `ContentPage` + `Label` с большим `FontSize`, но для “растянуть на весь экран” придётся уходить в handler-маппинг или `MauiCompat`. Overkill для одного экрана.
- **Размер APK:** ~15–25 MB.
- **Плюсы:** если планируешь потом iOS-версию часов.
- **Минусы:** самый тяжёлый, больше абстракций, сложнее immersive (нужно лезть в `MauiActivity`).

### 5. Flutter / React Native / прочие кроссплатформы
- Не рекомендуются: +20–30 MB, bridge, лишнее для статичных часов.

## Сравнение по требованиям Big Clock

| Критерий | Kotlin+Compose | .NET for Android | .NET MAUI |
|---|---|---|---|
| KeepScreenOn / immersive | нативно, 1 строка | доступно через биндинг | доступно, но через кастом handler |
| sensorLandscape | `AndroidManifest` 1 атрибут | так же | так же |
| Мигающий `:` (500мс, выровнен) | `LaunchedEffect` | `Handler`/`Looper` | `DispatcherTimer` |
| Размер APK | 2–4 MB | 10–18 MB | 15–25 MB |
| Время сборки (clean) | 15–30с | 30–60с | 40–80с |
| Знакомство автора | новое | знакомо | знакомо |
| Риск устаревания API | минимальный | средний (биндинги) | выше |

## Предварительная рекомендация

- **Если приоритет — минимальный APK и простота системных API:** Kotlin+Compose (ticket #3 закроет гриллингом этот выбор).
- **Если приоритет — остаться в dotnet без изучения Kotlin:** .NET for Android (`net10.0-android`) с AXML layout — компромисс (меньше оверхеда чем MAUI).
- **MAUI — только если в Not-yet-specified появится iOS.**

## Что нужно решить в #3 (grilling)

1. Подтвердить выбор между Kotlin vs .NET for Android (MAUI отпадает для одного экрана).
2. Зафиксировать `minSdk` (24 для охвата 99% vs 26 для `WindowInsetsControllerCompat` без костылей) и `targetSdk 35`.
3. Зафиксировать `applicationId` и toolchain (Gradle 8.x / JDK 17 vs `dotnet 10` + `android` workload).

## Следующие шаги

- Закрыть research веткой ссылкой в комментарии к #2.
- Гриллинг #3 принимает ADR `docs/adr/0001-stack-choice.md`.

## Источники (для полного /research прогона)

- docs: developer.android.com — `WindowInsetsController`, `keepScreenOn`, `screenOrientation`
- dotnet/android, dotnet/maui — workloads, `TargetFramework` net10.0-android
- community benchmarks APK size (MAUI vs native)

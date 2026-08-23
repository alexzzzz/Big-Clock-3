# ADR-0001: Stack для Big Clock — .NET for Android (C#)

- **Статус:** Accepted (2026-08-23) — закрывает #3, базируется на research #2 (`research/stack-choice` ветка)
- **Контекст:** Автор привык к .NET, приложение — один экран `ЧЧ:ММ` с мигающим `:`, принудительный ландшафт, immersive, keepScreenOn. Рассматривались Kotlin+Compose (2–4 MB APK, native) vs .NET for Android (`net10.0-android`, 10–18 MB, C#) vs .NET MAUI (15–25 MB, overkill).
- **Решение:** **.NET for Android** (`TargetFramework net10.0-android`, C# 12/13, `Sdk="Microsoft.Android.Sdk"`). Один `Activity` (`BigClockActivity`), AXML или кодовый layout с тремя `TextView` (HH, `:`, MM) или одним с `Spannable` для мигания. Fallback Kotlin — отвергнут, MAUI — отвергнут (нет iOS-плана).
- **Границы SDK:** `minSdk 26` (Android 8.0, API 26 — `WindowInsetsControllerCompat` без фолбэка, охват ~97%), `targetSdk 35`, `compileSdk 35`, `applicationId com.alexzzzz.bigclock`, `versionCode 1`, `versionName 1.0.0`.
- **Toolchain:** .NET 10 SDK + workload `android` (`dotnet workload install android`), JDK 17, Android SDK 35, сборка `dotnet build -c Release` → APK/AAB, подпись `apksigner`. IDE — Rider или VS 2022; Gradle/Studio не требуется.
- **Ключевые API:** `AndroidManifest.xml` → `android:screenOrientation="sensorLandscape"`, `android:keepScreenOn="true"`; `WindowCompat.setDecorFitsSystemWindows(window, false)` + `WindowInsetsControllerCompat` для immersive; `Handler(Looper.MainLooper)` + `postAtTime` выровнен к следующей секунде для мигания `:` (500 мс).
- **Последствия:** APK больше чем native (~10–18 MB vs 2–4 MB), но без изучения Kotlin и в привычной экосистеме; тикеты #4–#6 теперь пишутся под .NET API; MAUI не тянем, при необходимости iOS — отдельный ADR.
- **Альтернативы отвергнуты:** Kotlin+Compose — минимальный APK, но требует Kotlin/Studio, автор предпочёл C#; MAUI — избыточен для одного экрана.
- **Ссылки:** `research/stack-choice.md` на ветке `research/stack-choice`, issue #2 resolution, issue #3 grilling.

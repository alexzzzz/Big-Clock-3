# Решение #5 — Полноэкранный режим, keepScreenOn, ландшафт, скрытие панелей

## Вопрос (из Wayfinder)
Как реализовать принудительный ландшафт, полноэкранный immersive-режим, `keepScreenOn` и обработку `configChanges` без пересоздания Activity под стек .NET for Android (ADR-0001, `net10.0-android`, minSdk 26)?

## Ответ — конкретные флаги и вызовы + скелет

### 1) Манифест `Properties/AndroidManifest.xml:12-22`

```xml
<activity
  android:name=".BigClockActivity"
  android:exported="true"
  android:launchMode="singleInstance"
  android:screenOrientation="sensorLandscape"
  android:configChanges="orientation|screenSize|screenLayout|smallestScreenSize|density|keyboardHidden|keyboard" >
```

- **`sensorLandscape` vs `landscape`:** выбран `sensorLandscape` — оба ландшафтных разворота (датчик). `landscape` фиксирует только один. Для часов важно: как ни положить планшет — цифры горизонтальны. Минус `sensorLandscape` — на некоторых девайсах поворот на 180° может дольше анимировать, но для одного экрана некритично.
- **`configChanges`:** `orientation|screenSize|screenLayout|smallestScreenSize|density|keyboardHidden|keyboard` — без пересоздания Activity при поворотах, изменении density (foldables), подключении клавиатуры. При `sensorLandscape` пересоздание и так редкое, но флаг обязателен для доки и мультиоконки.
- **`launchMode="singleInstance"`** — один инстанс часов, без стека.
- **`keepScreenOn` в манифесте не ставим** — атрибут `android:keepScreenOn` работает только на View, не на `<activity>`. Ставим в layout + кодом (см. ниже).

### 2) Layout `Resources/layout/activity_big_clock.xml:5`

```xml
<ConstraintLayout android:keepScreenOn="true" ...>
```

- `android:keepScreenOn="true"` на корневом view — флаг живёт пока view attached, снимается в `onPause` автоматически. Экономит батарею vs `WAKE_LOCK`.

### 3) Код `BigClockActivity.cs`

| Место | Вызов | Зачем |
|---|---|---|
| `BigClockActivity.cs:24` | `Window.AddFlags(KeepScreenOn)` | Дубль к layout-флагу — надёжно держит экран, снимается в фоне |
| `BigClockActivity.cs:28` | `WindowCompat.SetDecorFitsSystemWindows(Window, false)` | Edge-to-edge: контент под бары (иначе letterbox) |
| `BigClockActivity.cs:33` | `LayoutInDisplayCutoutMode = ShortEdges` (API 28+) | Контент заходит в вырез по коротким сторонам — важно для `sensorLandscape` с notch слева/справа |
| `BigClockActivity.cs:44` | `HideSystemBars()` | Прячем сразу в `onCreate` |
| `BigClockActivity.cs:53` | `OnWindowFocusChanged(hasFocus) → HideSystemBars()` | После диалога/свайпа бары всплывают — прячем снова |
| `BigClockActivity.cs:61` | `OnResume → HideSystemBars()` | При возврате из паузы |
| `BigClockActivity.cs:73-79` | `WindowInsetsControllerCompat.Hide(SystemBars)` + `BehaviorShowTransientBarsBySwipe` | Compat-путь для minSdk 26 (через `androidx.core`). Свайп покажет бары временно, затем спрячет. Без `WindowInsetsController` (API 30) |

**Почему `WindowInsetsControllerCompat`, а не `WindowInsetsController` / `SYSTEM_UI_FLAG_*`:** 
- `SYSTEM_UI_FLAG_*` deprecated с API 30, на 35 не работает стабильно.
- `WindowInsetsController` — только с API 30, а у нас minSdk 26.
- `WindowInsetsControllerCompat` из `Xamarin.AndroidX.Core:1.15.0.1` покрывает весь диапазон и один код-путь.

### 4) Вырезы и отступы `BigClockActivity.cs:83-98`

```csharp
ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener());
 // left = max(SystemBars.Left, DisplayCutout.Left) и т.д.
```

- Суммируем `SystemBars` + `DisplayCutout` — часы не уедут под notch. Layout уже центрирован (`constraintWidth_percent 0.92`), добавочный padding — страховка на узких вырезах.
- Альтернатива — `android:fitsSystemWindows` — отвергнута: она обрезает edge-to-edge.

### 5) `onPause` / `onResume`

- `onPause` — снимаем тикер из #6 (`Handler.RemoveCallbacks`), флаг `KeepScreenOn` снимается системой сам.
- `onResume` — снова `HideSystemBars()` + перезапуск тикера. Никаких `wakeLock` руками — только флаг окна.

### 6) Тема

`@android:style/Theme.Black.NoTitleBar` в манифесте + `android:background="#000000"` в layout — чёрный фон (OLED, батарея). `Theme.Material` не нужен.

## Скелет проекта (эта ветка)

```
BigClock.csproj              — net10.0-android, SupportedOSPlatformVersion 26
Properties/AndroidManifest.xml — см. выше
BigClockActivity.cs          — см. выше (полный immersive + keepScreenOn + cutout)
Resources/layout/activity_big_clock.xml — из #4 Variant A
Resources/values/colors.xml, strings.xml
docs/decisions/05-fullscreen.md — этот файл
```

Сборка: `dotnet workload install android` (один раз) → `dotnet build -c Release` → `dotnet build -c Release -t:SignAndroidPackage` (подпись — туман "Сборка и дистрибуция").

Проверка вручную: установить на устройство, положить горизонтально обоими способами — бары скрыты, свайп показывает их временно, экран не гаснет, поворот не пересоздаёт Activity (в логах нет `OnDestroy`), вырез слева/справа не перекрывает цифры.

## Связь с другими тикетами

- Зависит от #3 (стек) — закрыт, теперь код под `net10.0-android`.
- Разблокирует #6 (ticker): `onResume`/`onPause` места уже зарезервированы.
- Использует layout из #4 (Variant A) — скопирован как `activity_big_clock.xml`.

## Альтернативы, которые отвергнуты

- `landscape` вместо `sensorLandscape` — меньше гибкости.
- `SYSTEM_UI_FLAG_IMMERSIVE_STICKY` — deprecated, не работает на 35.
- `android:keepScreenOn` только в манифесте — не существует.
- `WakeLock` (`PowerManager`) — избыточен, требует permission и ручного release, `KeepScreenOn` достаточно.

## Чек-лист HITL (если AFK недостаточно)

- [ ] Проверить на планшете с вырезом слева в sensorLandscape — цифры не под notch
- [ ] Свайп от края — бары появились и спрятались
- [ ] Экран не гаснет 5 минут на столе (keepScreenOn)
- [ ] Поворот устройства на 180° — Activity не пересоздалась (adb logcat `ActivityTaskManager:I *BigClockActivity*`)

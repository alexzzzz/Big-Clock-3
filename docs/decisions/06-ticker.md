# Решение #6 — Механизм обновления времени и мигания «:» (ticker, выравнивание, экономия)

## Вопрос (из Wayfinder)
Какой механизм обеспечивает точное обновление строки `ЧЧ:ММ` и мигание разделителя `:` с частотой 1 Гц, выровненное к системной секунде, при минимальном расходе заряда и корректной обработке смены времени, часового пояса и режима Doze?

Тип — `task` (AFK), зависит от стека (#3, ADR-0001 `net10.0-android`, `minSdk 26`).

## Выбранный примитив

**`Handler(Looper.MainLooper)` + `postAtTime` с выравниванием к следующей секунде** — основной путь для Big Clock.

Причины выбора подробно сопоставлены в таблице ниже. Решение наследует уже принятую в прототипе #4 модель (`Handler.postAtTime` выровнен к секунде, 500 мс мигание) и дополняет её минутным тиком для часов.

### Сравнение кандидатов

| Кандидат | Точность выравнивания | Зависимость от UI-такта | Поведение в Doze / фоне | Сложность в .NET for Android | Вердикт для Big Clock |
|---|---|---|---|---|---|
| **`Handler.postAtTime` (MainLooper)** | Миллисекундная, через `SystemClock.uptimeMillis()` вычисляется задержка до `nextSecond` | Независим от рендера, работает даже без кадров | Приостанавливается в Doze, но часы и так не видны в фоне; пробуждение через `onResume` | Прямой биндинг `Android.OS.Handler`, минимум кода | **Выбран** |
| `Choreographer.postFrameCallback` | Привязан к VSYNC (~16 мс), для секунд — избыточен | Зависит от частоты кадров, просыпается каждый кадр | Лишние пробуждения, расход батареи | Требует `Android.Views.Choreographer`, больше логики | Отвергнут — кадр не нужен для текста |
| `LaunchedEffect` + `delay` (Compose) / `PeriodicTimer` .NET | Зависит от корутины, дрейф `delay` | Независим, но дрейфует без `postAtTime` | Аналогично Handler, но в .NET for Android без Compose — лишний слой | Требует `kotlinx.coroutines` или `System.Threading.PeriodicTimer` поверх Handler | Отвергнут — нет Compose, дрейф без выравнивания |
| `AlarmManager` / `ACTION_TIME_TICK` каждую минуту | Точно по минутам от системы | Системный, экономный | Работает в Doze с задержками; `TIME_TICK` — раз в минуту, для `:` 500 мс не подходит | Требуется `BroadcastReceiver` + `AlarmManager` | Отвергнут для `:`; для `ЧЧ:ММ` — резерв, но Handler достаточно |

Вывод исследования соответствует ADR-0001, где ключевые API уже зафиксированы как `Handler(Looper.MainLooper)` + `postAtTime` с выравниванием.

## Архитектура тиков

Две независимые периодичности, синхронизированные к одному началу секунды:

- **Минутный тик (`ЧЧ:ММ`)** — ровно в `00` миллисекунд каждой следующей минуты. Обновление текста происходит только при смене минуты, а не каждую секунду, что снижает число перерисовок в 60 раз.
- **Полу секундный тик (`:`)** — каждые 500 мс, начиная с выровненного момента. Мигание реализовано изменением прозрачности символа через `Spannable` (вариант A из #4, один `TextView`) или `Alpha` отдельного `TextView` (вариант B), без пересоздания layout.

Выравнивание первого запуска к началу следующей секунды исключает наблюдаемый дрейф: вместо `postDelayed(500)` с накоплением погрешности используется `postAtTime(nextSecond)` и далее строгий шаг 500 мс.

### Обработка системных событий

Подписка на трансляции выполняется динамически в `Activity` (не в манифесте, чтобы не будить приложение в фоне):

- `Intent.ActionTimeChanged` — пользователь изменил время вручную.
- `Intent.ActionTimezoneChanged` — смена часового пояса.
- `Intent.ActionTimeTick` — системный тик каждую минуту (резерв, можно не использовать, но полезно для сверки после Doze).

При получении любого из этих интентов тикер перезапускается: снимаются pending колбэки и выполняется повторное выравнивание к следующей секунде с немедленным обновлением `ЧЧ:ММ`.

В режиме Doze устройство не показывает часы (экран в фоне, `onPause` уже снял колбэки благодаря `keepScreenOn` из #5), а при выходе из Doze и возврате в `onResume` тикер стартует заново, что гарантирует отсутствие накопленной ошибки без необходимости держать `WakeLock`.

## Псевдокод и готовый фрагмент

Псевдокод отражает логику, реализованную в `BigClockActivity.cs:24-98` на ветке `skeleton/ticker`:

```text
handler = Handler(MainLooper)
colonVisible = true

function startTicker():
  updateClock() // немедленное ЧЧ:ММ
  colonVisible = (uptimeMillis % 1000) < 500
  applyColon()

  delayToNextSecond = 1000 - (uptimeMillis % 1000)
  handler.postDelayed(delayToNextSecond) {
    colonVisible = true; applyColon()
    colonRunnable = { colonVisible = !colonVisible; applyColon(); handler.postDelayed(500, colonRunnable) }
    handler.postDelayed(500, colonRunnable)
    scheduleMinuteTick()
  }

function scheduleMinuteTick():
  nowWall = currentTimeMillis
  delayToNextMinute = 60000 - (nowWall % 60000)
  minuteRunnable = { updateClock(); scheduleMinuteTick() }
  handler.postAtTime(minuteRunnable, uptimeMillis + delayToNextMinute)

function stopTicker():
  handler.removeCallbacks(colonRunnable)
  handler.removeCallbacks(minuteRunnable)
  unregisterReceivers()

onResume():  startTicker(); registerReceiver(timeChangedFilter)
onPause():   stopTicker()
onReceive(TIME_CHANGED | TIMEZONE_CHANGED | TIME_TICK): restartTicker()
```

Фактический код использует `Handler.PostAtTime` / `PostDelayed`, `SystemClock.UptimeMillis()`, `Java.Lang.JavaSystem.CurrentTimeMillis()` для вычисления задержек и `SpannableString` с `ForegroundColorSpan(Transparent)` для скрытия `:` без пересчёта layout. Полный класс вынесен в `ClockTicker.cs` как отделяемый компонент, что соответствует рекомендациям `dotnet-best-practices` (единственная ответственность, отсутствие статики).

## Энергоэффективность

- Перерисовка `ЧЧ:ММ` — 1 раз в минуту, а не каждую секунду или кадр.
- Мигание `:` — только изменение альфы одного символа, без инвалидации всего layout и без `WakeLock`.
- В `onPause` все колбэки снимаются, а `keepScreenOn` из #5 автоматически освобождается системой — в фоне потребление стремится к нулю.
- Отсутствие `AlarmManager` и `Choreographer` исключает лишние пробуждения: Handler спит вместе с процессом.

## Связи с другими решениями

- **#3 / ADR-0001** — стек `net10.0-android` и границы SDK задают доступность `Handler` и `WindowInsetsControllerCompat`.
- **#4** — layout Variant A (один `TextView`, `Spannable` для `:`) напрямую используется тикером; вариант B (три `TextView`) поддерживается тем же тикером через `Alpha`.
- **#5** — `skeleton/fullscreen` уже резервирует `onResume`/`onPause`/`OnWindowFocusChanged` для скрытия баров; тикер использует те же точки жизненного цикла.
- **Туман из карты** — после фиксации тикера раздел «Производительность и батарея» считается раскрытым (частота перерисовки зафиксирована), а «Тестирование на железе» и «Сборка и дистрибуция» остаются в тумане как будущие задачи.

## Чек-лист проверки (HITL при необходимости)

- Системные часы переведены вручную — `ЧЧ:ММ` обновилось без задержки.
- Смена часового пояса — время пересчитано, мигание не сбилось.
- Устройство ушло в Doze на 10 минут и вернулось — после `onResume` часы показывают актуальное время, `:` мигает синхронно секунде.
- `adb shell dumpsys batterystats` — отсутствие `WakeLock` у `com.alexzzzz.bigclock`, число пробуждений соответствует 120 в минуту только для `:` (500 мс) без лишних кадров.

## Артефакты ветки

```
BigClock.csproj                          — net10.0-android, minSdk 26 (наследовано от skeleton/fullscreen)
BigClockActivity.cs                      — интеграция тикера в onResume/onPause + immersive из #5
ClockTicker.cs                           — выделенный компонент тикера (Handler + выравнивание + BroadcastReceiver)
Properties/AndroidManifest.xml           — sensorLandscape, configChanges (из #5)
Resources/layout/activity_big_clock.xml  — Variant A из #4
docs/decisions/06-ticker.md              — этот документ
```

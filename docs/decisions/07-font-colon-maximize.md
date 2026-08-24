# Решение #7 — Шрифт digital-7, двоеточие 50% и максимальное заполнение

## Контекст (пост-Wayfinder, AFK по запросу)

После закрытия карты Wayfinder (тикеты #2–#6) поступили прямые требования: использовать шрифт `C:\Users\alexz\Desktop\digital-7 (mono).ttf` и сделать цифры максимально крупными, затем уточнение — двоеточие должно занимать половину обычной ширины без нарушения пропорций: между часами и минутами — сжатый до 50% пробел, двоеточие — наложением поверх часов.

## Решение

### Шрифт

- Файл `digital-7 (mono).ttf` (34 404 байта) скопирован как `Resources/font/digital_7_mono.ttf` (`BigClock.csproj` — `AndroidResource`, имя удовлетворяет `^[a-z0-9_]+$`).
- Подключён в `Resources/layout/activity_big_clock.xml:15` как `android:fontFamily="@font/digital_7_mono"` для `clockText` и `colonText`. Отдельный `fontFamily` XML не требуется.
- Сохранён как артефакт в `main` (коммит `aa2f81d`), доступен в `I:\OneDrive\AI\BigClock.apk`.

### Двоеточие 50% — сжатый пробел + overlay

- **Layout** `Resources/layout/activity_big_clock.xml:9` — `clockText` отображает `88 88` (реально `HH MM`), `colonText` — отдельный `TextView` `:` с `app:layout_constraint*` по центру `clockText` (`bias 0.5`), `0dp` высота, `wrap_content` ширина, тот же шрифт и размер.
- **Сжатый пробел** — в `ClockTicker.cs:101` строка `HH MM` формируется как `SpannableString` с `ScaleXSpan(0.5f)` на пробеле (индексы `2,3`), его измеряемая ширина — `0.5 * MeasureText(" ")`, визуально — половина.
- **Overlay двоеточия** — `colonText` не влияет на компоновку `clockText` (ширина блока `HH + 0.5*space + MM`), рисуется поверх зазора. Мигание — `colonText.Alpha = 1f/0f` в `ClockTicker.cs:113`, без `Spannable` и пересчёта layout. Ранее использовавшийся вариант `ForegroundColorSpan(Transparent)` для одного `TextView` сохранён как fallback, если `colonText == null`.
- Требование «двоеточие не сжато, сжат только пробел» выполнено: `colonText` не имеет `textScaleX`, сжат только пробел в `clockText`.

### Максимальное заполнение — ручной подбор размера

- **Проблема `autoSize`:** `autoSize="uniform"` выбирает `S ≤ min(ширина/2.75, высота/1.0)` для `88:88` (ширина текста `~2.75·H`), поэтому на ландшафте `16:9` ширина ограничивает, оставляя вертикальные поля `~382px` для `1920×1080`.
- **Решение:** `autoSize` удалён из `activity_big_clock.xml`, размер задаётся кодом `BigClockActivity.cs:98` `MaximizeClockTextSize()`:
  - Доступная площадь `clockText.Width/Height` (или `DisplayMetrics` fallback) с запасом `0.995`.
  - `TextPaint` с `88 88` (ширина `wHH + 0.5*wSpace + wMM`, высота `|ascent|+|descent|`) бинарным поиском `10..4000px` (22 итерации) находит максимальный `best`, где `w ≤ availW && h ≤ availH`.
  - `clockText.SetTextSize(Px, best)` и `colonText?.SetTextSize(Px, best)` синхронно.
  - Вызывается в `OnCreate` через `ViewTreeObserver.GlobalLayout` и `Post`, а также при изменениях конфигурации.
- **Safe padding:** `BigClockActivity.cs:144` `InsetsListener` теперь устанавливает `SetPadding(0,0,0,0)` (ранее `max(SystemBars, DisplayCutout)`), что при `ShortEdges` и скрытых барах убирает искусственные поля и позволяет использовать `width/height 1.0` (`activity_big_clock.xml:24`).

## Артефакты

```
Resources/font/digital_7_mono.ttf
Resources/layout/activity_big_clock.xml — clockText 1.0×1.0 + colonText overlay
BigClockActivity.cs — MaximizeClockTextSize, InsetsListener 0, colonText handling
ClockTicker.cs — ScaleXSpan 0.5 для пробела, colonText.Alpha
BigClock3.sln / BigClock.csproj — net11.0-android, Microsoft.NET.Sdk, AndroidX 1.16/1.7.6/2.2.1
I:\OneDrive\AI\BigClock.apk — 19–20 MB, Signed
```

## Связи

- Наследует #4 (layout Variant A) и #5 (immersive) — теперь `1.0×1.0` и `0` отступов; #6 (ticker) — теперь overlay.
- `CONTEXT.md` обновлён: Glossary `Font`, `Compressed Space`, `Colon Overlay`, `Maximize`, раздел `Build`.

## Проверка

- Установить `I:\OneDrive\AI\BigClock.apk` — цифры `88:88` заполняют высоту без полей, пробел между `HH` и `MM` — половина ширины двоеточия, `:` мигает 500 мс без сдвига layout, наложений цифр нет.
- `adb shell dumpsys window` — `Bounds` совпадает с `DisplayMetrics`, `TextSize` ≈ `availH`.

## Альтернативы, отвергнутые

- `letterSpacing -0.18` + `textScaleX 0.72` — давало наложения глифов (`f0a966a`).
- `autoSize 0.98/0.96` — оставляло вертикальные поля из-за соотношения `2.75:1` vs `1.78:1`.
- `WakeLock` / `AlarmManager` — как в #6, не требуется.

# #4 Prototype — Decision

## Question
Какой layout даёт "часы во весь экран" в ландшафте? Три элемента HH : MM по центру, автоподстройка шрифта, hardcoded цвета, monospace, вырезы.

## Prototype asset
- `prototype/layout/prototype.html` — 3 radically different variants, switchable via `?variant=` + floating bar (keyboard ← →), palette cycle.
  - **A — CenterSingle**: один TextView, HH:MM одной строкой, `:` мигает opacity. Рекомендуемый default — авто-масштаб равномерный, нет рассинхрона baseline.
  - **B — Triptych**: HH / : / MM как три TextView в LinearLayout, `:` отдельный элемент — можно анимировать scale/pulse, но сложнее синхронизировать autoSize.
  - **C — BleedFull**: растянутый во всю ширину + визуализация safe-inset/cutout (notch) — демонстрирует обработку вырезов в landscape.
- `prototype/layout/android/*.axml` — AXML для .NET for Android (ConstraintLayout + autoSize), `colors.xml`, `BigClockActivity.cs.snippet` (Handler.postAtTime выровнен к секунде, 500ms blink, minute tick).

Run: double-click `prototype.html` — no build needed. Variants share time source (Date.now) и blink-синхронизацию к системной секунде (как в ADR-0001).

## Вывод (HITL review checkpoint)

- **Шрифт:** `monospace` + `includeFontPadding=false` + `letterSpacing 0.02` + `fontFeatureSettings="tnum"` — без докупки шрифта, tabular-nums гарантирует отсутствие дрожания при смене цифр. Custom font (JetBrains Mono) — опционально позже, не блокирует.
- **Цвет:** хардкод `#000000` фон (OLED, экономия), `#FFFFFF` цифры — default. Альтернативы `#FFCC33` amber / `#7FFF7F` green в палитре — выбрать на review (переключатель palette в прототипе).
- **Масштаб:** `autoSizeTextType="uniform"` с `min 40sp / max 320sp / step 2sp` + `constraintWidth_percent 0.92` — покрывает телефоны 16:9 до планшетов 16:10 без кода. Fallback — кодовый `measure` если uniform даст дрожание на узких экранах (не обнаружено в HTML-прогоне).
- **Cutout:** отступ `margin 2vh 3vw` + padding из `DisplayCutout.SafeInsetLeft/Right` — визуализировано в variant C. Для immersive скрытие панелей — см. #5.
- **Мигание `:`:** opacity toggle (не visibility/GONE — чтобы не вызывать relayout), 500ms, выровнено к началу секунды (`uptimeMillis % 1000`). HH:MM обновляется только раз в минуту (экономия), `:` — раз в 500ms.
- **Рекомендация:** **Variant A (CenterSingle)** в `activity_big_clock_single.axml` — simplest, one measurement, atomарный autoSize, минимальный риск. Variant B держать как опцию если захочется анимировать `:` отдельно.

## Что линковать при закрытии тикета
- Asset: `prototype/layout/prototype.html` + `prototype/layout/android/*`
- Gist: выбор A как default, шрифт/палитра/масштаб/cutout зафиксированы выше.

## Следствие для #5/#6
- #5 (fullscreen) — уже совместим: `keepScreenOn`, `sensorLandscape`, `WindowInsetsController` — см. snippet.
- #6 (ticker) — snippet в `BigClockActivity.cs.snippet` реализует Handler+Runnable выровненный к секунде; #6 может переиспользовать без изменений.

## Как валидировать HITL
Открыть `prototype.html` на телефоне/планшете в ландшафте (или Chrome device toolbar), пролистать A/B/C, нажать palette, ресайзнуть окно — проверить отсутствие overflow, дрожания, синхронность мигания. Выбрать победителя: "header from B with..." формат обратной связи.

*Throwaway branch: этот прототип — throwaway, при fold в real code копировать только победивший AXML + colors в `Resources/layout/`.*

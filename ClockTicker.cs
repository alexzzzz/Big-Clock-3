using Android.Content;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Widget;

namespace BigClock;

/// <summary>
/// Выделенный компонент тикера для Big Clock (Wayfinder #6).
/// Отвечает за: выравнивание к следующей секунде, обновление ЧЧ:ММ раз в минуту,
/// мигание ':' каждые 500 мс, обработку TIME_CHANGED / TIMEZONE_CHANGED / TIME_TICK.
/// Использование: создать в BigClockActivity, вызывать Start() в OnResume и Stop() в OnPause.
/// </summary>
public sealed class ClockTicker : Java.Lang.Object
{
    readonly TextView clockText;
    readonly Handler handler;
    readonly Action<string> log; // опционально для диагностики

    Java.Lang.Runnable? colonRunnable;
    Java.Lang.Runnable? minuteRunnable;
    BroadcastReceiver? timeReceiver;
    bool colonVisible = true;
    bool isRunning;

    public ClockTicker(TextView clockText, Handler handler, Action<string>? log = null)
    {
        this.clockText = clockText;
        this.handler = handler;
        this.log = log ?? (_ => { });
    }

    public void Start(Context context)
    {
        if (isRunning) Stop();
        isRunning = true;

        UpdateClock();
        var now = SystemClock.UptimeMillis();
        colonVisible = (now % 1000) < 500;
        ApplyColon();

        var delayToNextSecond = 1000 - (now % 1000);
        log($"ClockTicker.Start delayToNextSecond={delayToNextSecond}ms");

        handler.PostDelayed(() =>
        {
            if (!isRunning) return;
            colonVisible = true;
            ApplyColon();

            colonRunnable = new Runnable(() =>
            {
                colonVisible = !colonVisible;
                ApplyColon();
                if (isRunning) handler.PostDelayed(colonRunnable!, 500);
            });
            handler.PostDelayed(colonRunnable!, 500);

            ScheduleMinuteTick();
        }, delayToNextSecond);

        RegisterReceivers(context);
    }

    public void Stop()
    {
        isRunning = false;
        if (colonRunnable != null) handler.RemoveCallbacks(colonRunnable);
        if (minuteRunnable != null) handler.RemoveCallbacks(minuteRunnable);
        colonRunnable = null;
        minuteRunnable = null;
    }

    public void Release(Context context)
    {
        Stop();
        if (timeReceiver != null)
        {
            try { context.UnregisterReceiver(timeReceiver); } catch { /* уже снят */ }
            timeReceiver = null;
        }
    }

    void ScheduleMinuteTick()
    {
        var nowWall = Java.Lang.JavaSystem.CurrentTimeMillis();
        var delayToNextMinute = 60000 - (nowWall % 60000);
        log($"scheduleMinuteTick delay={delayToNextMinute}ms wall={nowWall}");

        minuteRunnable = new Runnable(() =>
        {
            UpdateClock();
            ScheduleMinuteTick();
        });
        // Выравнивание к монотонному времени, чтобы не дрейфовать
        handler.PostAtTime(minuteRunnable!, SystemClock.UptimeMillis() + delayToNextMinute);
    }

    void UpdateClock()
    {
        var now = DateTime.Now;
        var hh = now.Hour.ToString("D2");
        var mm = now.Minute.ToString("D2");
        var text = $"{hh}:{mm}";
        // Сохраняем текущее состояние ':' — ApplyColon наложит Spannable поверх
        clockText.Text = text;
        ApplyColon();
    }

    void ApplyColon()
    {
        var text = clockText.Text;
        if (string.IsNullOrEmpty(text) || text.Length < 5) return;

        // Вариант A (один TextView): скрываем ':' через Transparent, без пересчёта layout
        var span = new SpannableString(text);
        var color = colonVisible ? Android.Graphics.Color.White : Android.Graphics.Color.Transparent;
        span.SetSpan(new ForegroundColorSpan(color), 2, 3, SpanTypes.ExclusiveExclusive);
        clockText.SetText(span, TextView.BufferType.Spannable);
    }

    void RegisterReceivers(Context context)
    {
        var filter = new IntentFilter();
        filter.AddAction(Intent.ActionTimeChanged);
        filter.AddAction(Intent.ActionTimezoneChanged);
        filter.AddAction(Intent.ActionTimeTick);

        timeReceiver = new TimeChangeReceiver(this);
        context.RegisterReceiver(timeReceiver, filter);
    }

    void Restart()
    {
        // Перезапуск с повторным выравниванием — используется при TIME_CHANGED / TIMEZONE_CHANGED
        var ctx = clockText.Context;
        Stop();
        // Небольшая задержка, чтобы система успела обновить DateTime.Now
        handler.PostDelayed(() => { if (ctx != null) Start(ctx); }, 200);
    }

    sealed class TimeChangeReceiver : BroadcastReceiver
    {
        readonly ClockTicker parent;
        public TimeChangeReceiver(ClockTicker parent) => this.parent = parent;
        public override void OnReceive(Context? context, Intent? intent)
        {
            var action = intent?.Action;
            parent.log($"OnReceive {action}");
            // Для TIME_TICK достаточно проверить, не отстало ли ЧЧ:ММ (Doze мог пропустить минуту)
            parent.Restart();
        }
    }

    sealed class Runnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        readonly Action action;
        public Runnable(Action action) => this.action = action;
        public void Run() => action();
    }
}

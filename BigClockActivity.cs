using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;

namespace BigClock;

// Wayfinder #5 + #6: полноэкранный immersive (sensorLandscape, KeepScreenOn, WindowInsetsControllerCompat)
// + тикер Handler.postAtTime, выровненный к секунде (ЧЧ:ММ раз в минуту, ':' 500 мс).
// Детали см. docs/decisions/05-fullscreen.md и docs/decisions/06-ticker.md
[Activity(
    Label = "Big Clock",
    Theme = "@android:style/Theme.Black.NoTitleBar",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation
                         | ConfigChanges.ScreenSize
                         | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize
                         | ConfigChanges.Density
                         | ConfigChanges.KeyboardHidden
                         | ConfigChanges.Keyboard)]
public sealed class BigClockActivity : Activity
{
    TextView? clockText;
    TextView? colonText;
    ClockTicker? ticker;
    Handler? mainHandler;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // #5 fullscreen
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
        WindowCompat.SetDecorFitsSystemWindows(Window, false);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            Window.Attributes!.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        }

        SetContentView(Resource.Layout.activity_big_clock);
        clockText = FindViewById<TextView>(Resource.Id.clockText);
        colonText = FindViewById<TextView>(Resource.Id.colonText);

        HideSystemBars();

        var root = FindViewById<View>(Android.Resource.Id.Content)!;
        ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener());

        // Максимальное заполнение: ручной подбор размера шрифта, чтобы текст 88 88
        // с 50% пробелом полностью вписывался без пустых полей.
        // Двоеточие — отдельный overlay, занимает половину ширины и рисуется поверх.
        if (clockText != null)
        {
            clockText.ViewTreeObserver.GlobalLayout += (_, _) => MaximizeClockTextSize();
            clockText.Post(MaximizeClockTextSize);
        }

        mainHandler = new Handler(Looper.MainLooper!);
        if (clockText != null)
        {
            ticker = new ClockTicker(clockText, colonText, mainHandler);
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) HideSystemBars();
    }

    protected override void OnResume()
    {
        base.OnResume();
        HideSystemBars();
        // #6 ticker стартует здесь, снимается в OnPause — экономия в фоне/Doze
        ticker?.Start(this);
    }

    protected override void OnPause()
    {
        ticker?.Stop();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        if (ticker != null && mainHandler != null)
        {
            ticker.Release(this);
        }
        base.OnDestroy();
    }

    void MaximizeClockTextSize()
    {
        if (clockText == null) return;
        int availW = clockText.Width;
        int availH = clockText.Height;
        if (availW <= 0 || availH <= 0)
        {
            var dm = Resources?.DisplayMetrics;
            if (dm == null) return;
            availW = dm.WidthPixels;
            availH = dm.HeightPixels;
        }
        availW = (int)(availW * 0.995f);
        availH = (int)(availH * 0.995f);
        if (availW <= 0 || availH <= 0) return;

        var paint = new Android.Text.TextPaint(clockText.Paint);
        float low = 10f, high = 4000f, best = low;
        for (int i = 0; i < 22; i++)
        {
            float mid = (low + high) / 2f;
            paint.TextSize = mid;
            // Ширина с 50% пробелом: HH + 0.5*space + MM, двоеточие overlay не учитывается (0 ширины)
            float wHH = paint.MeasureText("88");
            float wSpace = paint.MeasureText(" ");
            float wMM = paint.MeasureText("88");
            float w = wHH + wSpace * 0.5f + wMM;
            var fm = paint.GetFontMetrics();
            float h = fm != null ? Math.Abs(fm.Ascent) + Math.Abs(fm.Descent) : mid;
            if (w <= availW && h <= availH)
            {
                best = mid;
                low = mid;
            }
            else high = mid;
        }
        clockText.SetTextSize(Android.Util.ComplexUnitType.Px, best);
        colonText?.SetTextSize(Android.Util.ComplexUnitType.Px, best);
    }

    void HideSystemBars()
    {
        var controller = WindowCompat.GetInsetsController(Window!, Window.DecorView);
        if (controller == null) return;
        controller.Hide(WindowInsetsCompat.Type.SystemBars());
        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }

    sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(View? v, WindowInsetsCompat? insets)
        {
            // Максимальное заполнение: игнорируем отступы для вырезов и баров,
            // чтобы цифры занимали всю площадь экрана без пустых полей.
            // Immersive уже скрывает status/nav, ShortEdges разрешает отрисовку в вырезе.
            v?.SetPadding(0, 0, 0, 0);
            return WindowInsetsCompat.Consumed;
        }
    }
}

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;

namespace BigClock;

// Wayfinder #5: полноэкранный immersive + keepScreenOn + sensorLandscape + configChanges
// Решение зафиксировано в docs/decisions/05-fullscreen.md и в Properties/AndroidManifest.xml
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

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 1) keepScreenOn — держит экран включённым пока Activity в foreground.
        // Дублируем: в layout android:keepScreenOn="true" + флаг окна (надёжнее).
        // Флаг автоматически снимается в onPause — батарея.
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);

        // 2) Edge-to-edge + immersive. Для minSdk 26 используем AndroidX compat.
        // WindowCompat.setDecorFitsSystemWindows(window, false) — контент под системные бары.
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        // 3) Разрешаем контенту заходить в вырезы (cutout) по коротким сторонам — важно для sensorLandscape.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            Window.Attributes!.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        }

        SetContentView(Resource.Layout.activity_big_clock);
        clockText = FindViewById<TextView>(Resource.Id.clockText);

        // 4) Прячем системные бары сразу
        HideSystemBars();

        // 5) Применяем WindowInsets для safe padding (чтобы часы не уехали под вырез).
        // Делаем через OnApplyWindowInsetsListener — отступ = insets cutout + systemBars.
        var root = FindViewById<View>(Android.Resource.Id.Content)!;
        ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener());
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // При возврате фокуса (after dialog / swipe) бары могут всплыть — прячем снова.
        if (hasFocus) HideSystemBars();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // В onResume бары снова могут показаться — прячем.
        HideSystemBars();
        // Ticker из #6 стартует здесь (Handler.postAtTime), останавливается в onPause.
    }

    protected override void OnPause()
    {
        // Ticker из #6 должен снять колбэки здесь.
        base.OnPause();
        // KeepScreenOn флаг снимется автоматически при уходе в background.
    }

    void HideSystemBars()
    {
        var controller = WindowCompat.GetInsetsController(Window!, Window.DecorView);
        if (controller == null) return;

        // Скрываем status + navigation
        controller.Hide(WindowInsetsCompat.Type.SystemBars());
        // Поведение: свайп от края временно показывает бары, затем снова прячет.
        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }

    sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            var cutout = insets.GetInsets(WindowInsetsCompat.Type.DisplayCutout());
            // Суммируем бары + вырез, оставляем минимум 8dp уже в layout constraintWidth_percent.
            int left = Math.Max(bars.Left, cutout.Left);
            int right = Math.Max(bars.Right, cutout.Right);
            int top = Math.Max(bars.Top, cutout.Top);
            int bottom = Math.Max(bars.Bottom, cutout.Bottom);
            v.SetPadding(left, top, right, bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}

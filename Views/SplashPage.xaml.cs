using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;

namespace Raphael.Driver.Views
{
    /// <summary>
    /// The animated splash, design Q, shown once at a cold start. The system draws its own fixed
    /// frame first (MauiSplashScreen: the official logo centred on the map's ground colour), and
    /// this page starts on that same colour. When the animation ends, or on a tap, it hands over to
    /// the callback it was given, which builds the Shell.
    /// </summary>
    public partial class SplashPage : ContentPage
    {
        /// <summary>The rule's ground-coloured strip, each side of the line (Padding in the XAML).</summary>
        private const double RuleStrip = 3;

        private readonly Action _done;
        private readonly SplashDrawable _scene = new();
        /// <summary>How long to wait for the first frame before giving up on the animation.</summary>
        private const double FirstFrameLimit = 3;

        private readonly System.Diagnostics.Stopwatch _watch = new();
        private readonly System.Diagnostics.Stopwatch _waited = new();
        private IDispatcherTimer? _clock;
        private double _rise;
        private bool _finished;

        public SplashPage(Action done)
        {
            InitializeComponent();
            _done = done;
            Scene.Drawable = _scene;
            MapView.Drawable = new SplashMap();
            SizeChanged += (_, _) => Fit(Width);

#if ANDROID
            // The map is kept as a texture once painted; each frame then only copies it.
            MapView.HandlerChanged += (_, _) =>
                (MapView.Handler?.PlatformView as Android.Views.View)?.SetLayerType(Android.Views.LayerType.Hardware, null);
#endif
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // The status bar takes the ground colour, as it had on the launch frame. The Shell
            // repaints it with the theme when it takes over (App.ShowShell).
            try
            {
                StatusBar.SetColor(SplashMap.Ground);
                StatusBar.SetStyle(StatusBarStyle.DarkContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SplashPage] status bar not painted: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the timer once the page has a size. Not from OnAppearing with a MAUI Animation:
        /// on the first page of a cold start that one finished at once, before the layout, and the
        /// logo was never drawn. Elapsed time comes from a stopwatch, so a slow phone drops frames
        /// but never stretches the design's length. The stopwatch starts on the first frame really
        /// drawn, not before: the pins only last the first 1.45 s, and a slow phone spent that long
        /// on its first frame, so they never showed. If no frame is ever drawn, the app still opens.
        /// </summary>
        private void Start()
        {
            if (_clock is not null || _finished)
                return;

            // With the phone's animations turned off (Android) or Reduce Motion on (iOS): the finished
            // lockup, then the app.
            if (!AnimationsEnabled())
            {
                Finish();
                return;
            }

            Tick(0);
            _waited.Start();
            _clock = Dispatcher.CreateTimer();
            _clock.Interval = TimeSpan.FromMilliseconds(16);
            _clock.Tick += (_, _) =>
            {
                if (!_watch.IsRunning)
                {
                    if (_scene.Drawn)
                        _watch.Start();
                    else if (_waited.Elapsed.TotalSeconds > FirstFrameLimit)
                        Finish();
                    else
                        Tick(0);
                    return;
                }

                var t = _watch.Elapsed.TotalSeconds;
                if (t >= SplashDrawable.Duration)
                    Finish();
                else
                    Tick(t);
            };
            _clock.Start();
        }

        private static bool AnimationsEnabled()
        {
#if ANDROID
            return !OperatingSystem.IsAndroidVersionAtLeast(26) || Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#elif IOS || MACCATALYST
            return !UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
            return true;
#endif
        }

        // Nothing to go back to yet.
        protected override bool OnBackButtonPressed() => true;

        private void OnTapped(object? sender, TappedEventArgs e) => Finish();

        /// <summary>
        /// Sizes the lockup from the page width, as the design does (its units are % of the width):
        /// the logo is 40% wide and the name, the slogan and the gaps follow it.
        /// </summary>
        private void Fit(double width)
        {
            if (width <= 0)
                return;

            LogoSlot.WidthRequest = 0.40 * width;
            LogoSlot.HeightRequest = 0.512 * width;
            Lockup.Spacing = 0.03 * width;
            Lockup.TranslationY = -0.04 * width;
            Word.FontSize = 0.092 * width;
            Sub.FontSize = 0.041 * width;
            RuleLine.WidthRequest = 0.12 * width;
            Rule.Margin = new Thickness(0, 0.025 * width - RuleStrip, 0, -RuleStrip);
            Tag1.FontSize = 0.043 * width;
            Tag1.Margin = new Thickness(0, 0.01 * width, 0, 0);
            Tag2.FontSize = 0.039 * width;
            _rise = 0.034 * width;

            Start();
        }

        private void Tick(double t)
        {
            _scene.Mark = MarkCentre();
            _scene.Time = t;
            Scene.Invalidate();

            // CharacterSpacing is in sixteenths of an em: the name closes from .6em to .32em.
            const double start = SplashDrawable.TextStart;
            var name = SplashDrawable.Ease.Swift(SplashDrawable.Progress(t, start, start + 0.7));
            Word.Opacity = name;
            Sub.Opacity = name;
            Word.CharacterSpacing = SplashDrawable.Lerp(9.6, 5.1, name);
            Sub.CharacterSpacing = SplashDrawable.Lerp(9.6, 8, name);

            Rule.ScaleX = SplashDrawable.Ease.Swift(SplashDrawable.Progress(t, start + 0.25, start + 0.75));
            Rise(Tag1, SplashDrawable.Progress(t, start + 0.4, start + 1.0));
            Rise(Tag2, SplashDrawable.Progress(t, start + 0.55, start + 1.15));
        }

        /// <summary>
        /// The centre of the logo's slot in the lockup. The slot is read from the requested size,
        /// not the arranged one: Android lays a transparent BoxView out at 0×0. Its height still
        /// holds the room in the stack, so only its top is read from the layout. Until the
        /// lockup has been laid out there is no slot yet, and nothing is drawn.
        /// </summary>
        private Point MarkCentre() => Lockup.Height <= 0
            ? Point.Zero
            : new(Width / 2, Lockup.Y + Lockup.TranslationY + LogoSlot.Y + LogoSlot.HeightRequest / 2);

        private void Rise(View view, double p)
        {
            var e = SplashDrawable.Ease.Swift(p);
            view.Opacity = e;
            view.TranslationY = _rise * (1 - e);
        }

        private void Finish()
        {
            if (_finished)
                return;

            _finished = true;
            _clock?.Stop();

            // A tap jumps to the finished lockup, which stays while the Shell is being built.
            Tick(SplashDrawable.Duration);
            Dispatcher.Dispatch(_done);
        }
    }
}

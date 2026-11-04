using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.Services;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Controls.Menu
{
    /// <summary>
    /// The menu at the bottom of the main pages (RE-064), in the style chosen in Settings
    /// (<see cref="MenuStyleManager"/>). It replaces the side menu and reaches the same pages and
    /// commands: Dashboard, Schedule, Notifications, Settings, Messages, Contact and Sign out.
    /// Only the look changes from one style to another.
    /// Designs: <c>_meta/design/driver-navigation/round-4.html</c> and <c>_meta/design/identity</c>.
    /// </summary>
    /// <remarks>
    /// A layer over the whole page with no background, so a touch on an empty part reaches the
    /// page and only the menu's own pieces take touches. Each page has its own menu; the selection animates
    /// from the page the driver came from (<see cref="_lastKey"/>) to this one when it appears.
    /// Every style adds its pieces in its Build method and registers how to place them
    /// (<see cref="_placers"/>), recomputed whenever the size changes.
    /// </remarks>
    public sealed class NavMenuView : Grid
    {
        private sealed record Item(string Key, string Glyph, string Label, string? Route);

        private static readonly Item[] Main =
        [
            new("home", "", "Home", "DashboardPage"),
            new("schedule", "", "Schedule", "SchedulePage"),
            new("notifications", "", "Alerts", "NotificationsPage"),
            new("settings", "", "Settings", "SettingsPage"),
        ];

        private static readonly Item Messages = new("sms", "", "Messages", null);
        private static readonly Item Contact = new("contact", "", "Contact", "ContactPage");
        private static readonly Item SignOut = new("out", "", "Sign out", null);
        private static readonly Item AllOptions = new("all", "", "All options", null);
        private static readonly Item CallMe = new("callme", "", "Call me", null);
        private static readonly Item Documents = new("docs", "", "Documents", null); // RE-065
        private static readonly Item ChangeRoute = new("route", "", "Change route", "SchedulePage");

        private const double BarH = 76, CenterD = 70, Raise = 26, PanelGap = 96;

        private readonly string _current;
        private MenuStyle _style;
        private bool _leftHand;

        private BoxView _veil = null!;
        private View? _center;
        private Border _panel = null!, _confirm = null!;
        private readonly List<View> _fan = [];
        private readonly List<Label> _badges = [];
        private readonly List<Action> _placers = [];
        /// <summary>What each style plays when an item is tapped, from the page on screen to the
        /// tapped one, before the page changes: the selection moves where the driver is looking.</summary>
        private readonly List<Action<int, int>> _arrivals = [];
        private Action? _onOpen, _onClose, _onCancelOut;
        private Func<int, double, double, (double X, double Y)>? _fanAt;
        private double _fanW = 84, _fanH = 80;
        private int _fanFirst, _fanStep = 45;
        private bool _fanOpen, _panelOpen, _confirmOpen;
        private double _reserve = BarH + 4;

        /// <summary>The space the menu takes at the bottom, which the page keeps free.</summary>
        public double Reserve => _reserve;

        public event EventHandler? ReserveChanged;

        private readonly MenuStyle? _preview;

        /// <summary>A still picture of <paramref name="preview"/>, for the cards in Settings: it does
        /// not navigate and takes no touches, but follows the theme.</summary>
        public NavMenuView(MenuStyle preview) : this("home", preview) { InputTransparent = true; }

        public NavMenuView(string current, MenuStyle? preview = null)
        {
            _current = current;
            _preview = preview;
            // No InputTransparent here: on Android it also kept touches from the menu's own buttons.
            // An empty layer with no background does not take touches, so the page still gets them.
            Build();
            SizeChanged += (_, _) => Place();

            // Listen only while on screen. Today, History and Future are new pages on every visit,
            // and a menu that listened for ever rebuilt itself on every change long after its page
            // was gone: picking a style rebuilt dozens of menus at once and froze the app.
            EventHandler changed = (_, _) => MainThread.BeginInvokeOnMainThread(Rebuild);
            PropertyChangedEventHandler unread = (_, e) =>
            {
                if (e.PropertyName == nameof(NotificationStore.UnreadCount)) MainThread.BeginInvokeOnMainThread(ShowUnread);
            };
            Loaded += (_, _) =>
            {
                ThemeManager.ThemeChanged += changed;
                if (_preview != null) { if (_theme != ThemeManager.Effective) Rebuild(); return; }
                MenuStyleManager.StyleChanged += changed;
                if (Store is { } store) store.PropertyChanged += unread;
                ShowUnread();
                // Changed while this page was not on screen: catch up now.
                if (_style != MenuStyleManager.Current || _theme != ThemeManager.Effective) Rebuild();
            };
            Unloaded += (_, _) =>
            {
                MenuStyleManager.StyleChanged -= changed;
                ThemeManager.ThemeChanged -= changed;
                if (Store is { } store) store.PropertyChanged -= unread;
            };
        }

        private SkinTheme _theme;

        /// <summary>The same store the bell used to read: the count changes the moment a
        /// notification arrives or is read.</summary>
        private static NotificationStore? Store => ServiceHelper.GetService<NotificationStore>();

        /// <summary>Alerts with its unread count, in every style.</summary>
        private View Badged(View icon, Item it)
        {
            if (it.Key != "notifications") return icon;
            var badge = new Label
            {
                FontSize = 9.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            };
            var pill = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 8 }, Padding = new Thickness(4, 0), MinimumWidthRequest = 16, HeightRequest = 16,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, TranslationX = 12, TranslationY = -6,
                IsVisible = false, InputTransparent = true, Content = badge,
            };
            pill.SetDynamicResource(BackgroundColorProperty, "BadgeColor");
            pill.SetDynamicResource(Border.StrokeProperty, "CardBackgroundColor");
            pill.StrokeThickness = 1.5;
            var host = new Grid { HorizontalOptions = icon.HorizontalOptions, VerticalOptions = icon.VerticalOptions, Children = { icon, pill } };
            _badges.Add(badge);
            ShowUnread();
            return host;
        }

        private void ShowUnread()
        {
            var count = Store?.UnreadCount ?? 0;
            foreach (var b in _badges)
            {
                b.Text = count > 99 ? "99+" : count.ToString();
                if (b.Parent is View pill) pill.IsVisible = count > 0;
            }
        }

        /// <summary>Puts the menu on <paramref name="page"/>: its content and the menu share one grid.</summary>
        public static void Attach(ContentPage page, string current)
        {
            var content = page.Content;
            var menu = new NavMenuView(current);
            var bottom = content.Margin.Bottom;
            void Keep() => content.Margin = new Thickness(content.Margin.Left, content.Margin.Top, content.Margin.Right, bottom + menu.Reserve);
            Keep();
            menu.ReserveChanged += (_, _) => Keep();
            // Out of the page first, then into the grid. The other way round, replacing the page's
            // content afterwards cut the old content off from its parent: it stopped following the
            // theme (Settings kept its light cards in Dark) and never raised Loaded.
            page.Content = null;
            var root = new Grid { Children = { content, menu } };
            page.Content = root;
            KeepHeight(page, root);
            page.Disappearing += (_, _) => menu.CloseAll();
            page.Appearing += (_, _) =>
            {
                menu.Settle();
            };
        }

        private static double _visibleHeight;

        /// <summary>
        /// Android measures a page for a moment with the wrong height when its section is shown
        /// again. The menu follows the height of its page, so it was drawn cut off or stretched
        /// until the next pass. The page takes its height from the visible area of the window
        /// instead, which that pass does not change and the keyboard does.
        /// </summary>
        private static void KeepHeight(ContentPage page, Grid root)
        {
#if ANDROID
            if (_visibleHeight > 0)
            {
                root.VerticalOptions = LayoutOptions.Start;
                root.HeightRequest = _visibleHeight;
            }

            void Measure()
            {
                if (root.Handler?.PlatformView is not Android.Views.View view || !view.IsShown) return;
                var frame = new Android.Graphics.Rect();
                view.GetWindowVisibleDisplayFrame(frame);
                var at = new int[2];
                view.GetLocationOnScreen(at);
                var height = (frame.Bottom - at[1]) / DeviceDisplay.Current.MainDisplayInfo.Density;
                if (height < 100 || Math.Abs(height - root.HeightRequest) < .5) return;
                _visibleHeight = height;
                root.VerticalOptions = LayoutOptions.Start;
                root.HeightRequest = height;
            }

            root.SizeChanged += (_, _) => Measure();
            page.Appearing += (_, _) => page.Dispatcher.Dispatch(Measure);
#endif
        }


        private void Rebuild()
        {
            foreach (var child in Children.OfType<VisualElement>()) child.AbortAnimation("loop");
            Children.Clear();
            _fan.Clear();
            _badges.Clear();
            _settles.Clear();
            _placers.Clear();
            _arrivals.Clear();
            _panel = null!;
            _confirm = null!;
            _onOpen = _onClose = _onCancelOut = null;
            _fanAt = null;
            _center = null;
            _fanOpen = _panelOpen = _confirmOpen = false;
            Build();
            Place();
            ReserveChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Back on a page the driver left: the selection moved there when they tapped another item
        /// (the van drove off, the halo slid away), and the page is reused, so everything goes back
        /// to this page's item. Without this the van could be waiting at Alerts on Settings.
        /// </summary>
        private void Settle()
        {
            foreach (var v in Children.OfType<View>())
            {
                if (_fan.Contains(v) || v == _veil || v == _panel || v == _confirm) continue;
                v.AbortAnimation("TranslateTo");
                v.TranslationX = 0;
                v.TranslationY = 0;
                v.Rotation = 0;
                v.Scale = 1;
                v.Opacity = 1;
            }
            foreach (var settle in _settles) settle();
            Place();
        }

        private readonly List<Action> _settles = [];

        private static int Index(string key) => Array.FindIndex(Main, m => m.Key == key);

        // =====================================================================================
        //  Building
        // =====================================================================================

        private void Build()
        {
            _style = _preview ?? MenuStyleManager.Current;
            _theme = ThemeManager.Effective;
            _reserve = BarH + 4;

            _veil = new BoxView { Color = Colors.Black, Opacity = 0, IsVisible = false, ZIndex = 20 };
            _veil.GestureRecognizers.Add(Tap(() => { var asking = _confirmOpen; CloseAll(); if (asking) _onCancelOut?.Invoke(); }));
            Add(_veil);

            switch (_style)
            {
                case MenuStyle.AngelWings:
                case MenuStyle.AngelDove: BuildAngel(); break;
                case MenuStyle.Crescent: BuildCrescent(); break;
                case MenuStyle.Pulse: BuildPulse(); break;
                case MenuStyle.Radar: BuildRadar(); break;
                case MenuStyle.SteeringWheel: BuildSteering(); break;
                case MenuStyle.PinAndVan: BuildRoad(); break;
                case MenuStyle.Tire: BuildTire(); break;
                case MenuStyle.TrafficLights: BuildSignals(); break;
                case MenuStyle.Speedometer: BuildGauge(); break;
                case MenuStyle.Underline: BuildLogoRow(); break;
                case MenuStyle.Metro: BuildMetro(); break;
                case MenuStyle.HealthCross: BuildCross(); break;
                case MenuStyle.Island: BuildSheet(); break;
                case MenuStyle.Jump: BuildCapsule(); break;
                case MenuStyle.Christmas: BuildChristmas(); break;
            }

            if (_center != null)
            {
                _center.HorizontalOptions = LayoutOptions.Start;
                _center.VerticalOptions = LayoutOptions.Start;
                if (_center.ZIndex == 0) _center.ZIndex = 12;
                _center.GestureRecognizers.Add(Tap(OnCenter));
                SemanticProperties.SetDescription(_center, "All options");
                Add(_center);
            }

        }

        // ---------------- General ----------------

        /// <summary>K: the logo in the middle; its wings are folded beside it and open with the fan.</summary>
        private void BuildAngel()
        {
            // The tab in use in the accent: InkColor is near white in Dark and Infrared and did not
            // stand out from the others there.
            var bar = Bar(CardBrush(.97f), IsLightish ? "InkColor" : "AccentColor");
            var halo = Halo(30, 9, Gold());
            Add(halo);
            Bob(halo);
            // Both wings on one layer the size of the menu, folded beside the logo (see AngelWingsDrawable).
            var wingsDraw = new AngelWingsDrawable(_style == MenuStyle.AngelDove);
            var wings = new GraphicsView { Drawable = wingsDraw, ZIndex = 11, InputTransparent = true };
            Add(wings);
            _center = LogoButton(CenterD);
            CenterAt();
            Slide(halo, 15, () => Height - BarH + 4);
            // The design's wings were drawn for a 340-wide phone: they open in proportion to this one.
            _placers.Add(() => { wingsDraw.Center = new PointF((float)Cx, (float)Cy); wingsDraw.OpenSize = (float)Math.Max(1, Width / 340); wings.Invalidate(); });
            void Fold(float to, uint ms, Easing easing)
            {
                var from = wingsDraw.Open;
                new Animation(v => { wingsDraw.Open = (float)v; wings.Invalidate(); }, from, to)
                    .Commit(wings, "wings", 16, ms, easing);
            }
            // Open: the logo rises a little and glows gold, as in the design (round 3, «.fan-on .kc»).
            var rest = _center.Shadow;
            var glow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb(IsInfrared ? "#FF6F61" : "#F2C14E")), Radius = 30, Opacity = .9f, Offset = new Point(0, 0) };
            _onOpen = () =>
            {
                wings.ZIndex = 21;
                Fold(1, 550, Easing.SpringOut);
                _center!.Shadow = glow;
                _center.TranslateTo(0, -6, 350, Easing.SpringOut);
                _center.ScaleTo(1.06, 350, Easing.SpringOut);
            };
            _onClose = () =>
            {
                Fold(0, 350, Easing.CubicOut);
                wings.ZIndex = 11;
                _center!.Shadow = rest;
                _center.TranslateTo(0, 0, 250);
                _center.ScaleTo(1, 250);
            };
            ArcFan(124, [-62, -31, 0, 31, 62]);
        }

        /// <summary>N: the four on the arc the thumb draws, the logo in the corner, for either hand.</summary>
        private void BuildCrescent()
        {
            _reserve = 0;
            _leftHand = Preferences.Get("MenuLeftHand", false);
            var arc = new GraphicsView
            {
                // The size of the menu, so the band is never cut: it reaches past Home and Settings.
                Drawable = new ArcDrawable(() => _leftHand), ZIndex = 10, InputTransparent = true,
            };
            Add(arc);
            var dot = new Ellipse
            {
                WidthRequest = 54, HeightRequest = 54, ZIndex = 11, InputTransparent = true, Fill = new SolidColorBrush(Next().WithAlpha(.16f)),
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            };
            Add(dot);
            var navs = new List<View>();
            foreach (var it in Main)
            {
                var v = NavButton(it, "NextColor");
                v.WidthRequest = v.HeightRequest = 58;
                v.ZIndex = 12;
                v.HorizontalOptions = LayoutOptions.Start;
                v.VerticalOptions = LayoutOptions.Start;
                Add(v);
                navs.Add(v);
            }
            var hand = Circle(44, "CardBackgroundColor", Glyph("", 18, "SecondaryTextColor"));
            hand.ZIndex = 12;
            SemanticProperties.SetDescription(hand, "Move the menu to the other hand");
            hand.GestureRecognizers.Add(Tap(() => { _leftHand = !_leftHand; Preferences.Set("MenuLeftHand", _leftHand); arc.Invalidate(); Place(); }));
            Add(hand);
            _center = LogoButton(68);
            _center.ZIndex = 13;

            double[] angles = [178, 146, 114, 82];
            (double, double) Pos(int i)
            {
                double cx = _leftHand ? 40 : Width - 40, cy = Height - 40, a = angles[i] * Math.PI / 180, dx = Math.Cos(a) * 112;
                return ((_leftHand ? cx - dx : cx + dx) - 29, cy - Math.Sin(a) * 112 - 29);
            }
            _placers.Add(() =>
            {
                arc.Invalidate();
                for (var i = 0; i < navs.Count; i++) { var (x, y) = Pos(i); At(navs[i], x, y); }
                var c = Index(_current);
                if (c >= 0) { var (x, y) = Pos(c); At(dot, x + 2, y + 2); }
                double cx = _leftHand ? 40 : Width - 40, cy = Height - 40;
                At(_center!, cx - 34, cy - 34);
                At(hand, cx - 22, cy - 88);
            });
            _arrivals.Add((a, b) =>
            {
                if (a < 0 || b < 0 || a == b) return;
                var (x0, y0) = Pos(a); var (x1, y1) = Pos(b);
                dot.TranslateTo(x1 - x0, y1 - y0, 420, Easing.CubicOut);
            });
        }

        /// <summary>L: an ECG line beats over the selected item; the heart opens All options.</summary>
        private void BuildPulse()
        {
            Bar(CardBrush(.97f));
            var pulse = new PulseDrawable();
            var ecg = new GraphicsView { Drawable = pulse, HeightRequest = 26, ZIndex = 11, InputTransparent = true, VerticalOptions = LayoutOptions.Start };
            Add(ecg);
            var heart = new Border
            {
                WidthRequest = CenterD, HeightRequest = CenterD, StrokeShape = new Ellipse(), StrokeThickness = 5,
                Background = new LinearGradientBrush([new GradientStop(Color.FromArgb("#F79A8C"), 0), new GradientStop(Next(), .75f)], new Point(0, 0), new Point(1, 1)),
                Content = WhiteGlyph("", 28),
            };
            heart.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            Loop(heart, 1800, t => heart.Scale = t < .15 ? 1 + t / .15 * .08 : t < .3 ? 1.08 - (t - .15) / .15 * .08 : 1);
            _center = heart;
            CenterAt();
            _arrivals.Add((a, b) =>
            {
                var x = ItemX(b);
                if (x == null || a == b) return;
                pulse.SpikeX = (float)x.Value;
                Run(this, "pulse", 420, v => { pulse.Progress = (float)v; ecg.Invalidate(); });
            });
            _placers.Add(() =>
            {
                At(ecg, 0, Height - BarH - 13);
                ecg.WidthRequest = Width;
                pulse.SpikeX = (float)(ItemX(Index(_current)) ?? -100);
                Run(this, "pulse", 700, v => { pulse.Progress = (float)v; ecg.Invalidate(); });
            });
        }

        /// <summary>M: "you are here" marks the page; the GPS dot opens a radar with the rest.</summary>
        private void BuildRadar()
        {
            var bar = Bar(CardBrush(.95f));
            bar.ZIndex = 8;
            var acc = Accent();
            // "You are here": the GPS accuracy circle around the page, with the blue dot at its foot
            // (design «.mr .here»).
            var gps = Color.FromArgb(IsInfrared ? "#FF6F61" : "#318FB0");
            var here = new Grid
            {
                WidthRequest = 46, HeightRequest = 50, ZIndex = 9, InputTransparent = true,
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
                Children =
                {
                    new Ellipse { WidthRequest = 46, HeightRequest = 46, StrokeThickness = 1, VerticalOptions = LayoutOptions.Start, Fill = new SolidColorBrush(gps.WithAlpha(.16f)), Stroke = new SolidColorBrush(gps.WithAlpha(.4f)) },
                    new Ellipse { WidthRequest = 12, HeightRequest = 12, StrokeThickness = 2, Stroke = Brush.White, Fill = new SolidColorBrush(gps), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Shadow = new Shadow { Brush = new SolidColorBrush(gps), Radius = 6, Opacity = .6f } },
                },
            };
            Add(here);
            Slide(here, 23, () => Height - BarH + 6);

            var radarDraw = new RadarDrawable();
            var radar = new GraphicsView
            {
                Drawable = radarDraw, WidthRequest = 300, HeightRequest = 150, IsVisible = false, ZIndex = 21, InputTransparent = true,
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            };
            Add(radar);
            _placers.Add(() => At(radar, Cx - 150, Cy - 150));

            var ir = IsInfrared;
            var ground = ir ? Color.FromArgb("#3A1210") : Color.FromArgb("#10293A");
            var ping = ir ? Color.FromArgb("#FF6F61") : Color.FromArgb("#50ABC1");
            var bg = new Border { StrokeShape = new Ellipse(), StrokeThickness = 5, BackgroundColor = ground };
            bg.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            var g = new Grid { WidthRequest = CenterD, HeightRequest = CenterD, Children = { bg } };
            for (var i = 0; i < 2; i++)
            {
                var ring = new Ellipse { WidthRequest = 40, HeightRequest = 40, Stroke = new SolidColorBrush(ping), StrokeThickness = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
                g.Add(ring);
                var offset = i * .5;
                Loop(ring, 2200, t => { var k = (t + offset) % 1; ring.Scale = .4 + k * 1.2; ring.Opacity = 1 - k; });
            }
            g.Add(new Ellipse { WidthRequest = 20, HeightRequest = 20, Fill = new SolidColorBrush(ping), Stroke = Brush.White, StrokeThickness = 3, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true });
            _center = g;
            CenterAt();
            _onOpen = () =>
            {
                radar.IsVisible = true; radar.Opacity = 0; radar.FadeTo(1, 250);
                // Clockwise, from the left edge over the top to the right, as in the design.
                Loop(radar, 2600, t => { radarDraw.Sweep = (float)(180 - t * 360); radar.Invalidate(); });
            };
            _onClose = () => { radar.AbortAnimation("loop"); radar.FadeTo(0, 200); radar.IsVisible = false; };
            // The radar finds the items one after the other (design: 120 ms, then 110 ms apart).
            _fanFirst = 120;
            _fanStep = 110;
            ArcFan(124, [-62, -31, 0, 31, 62]);
        }

        // ---------------- Thematic ----------------

        /// <summary>G: a dashboard with warning lights, and the steering wheel in the middle.</summary>
        private void BuildSteering()
        {
            var ir = IsInfrared;
            var bar = Bar(CardBrush(1), "InkColor");
            bar.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");
            bar.StrokeThickness = 1;
            var lamp = new Ellipse { WidthRequest = 6, HeightRequest = 6, Fill = new SolidColorBrush(Next()), ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Shadow = new Shadow { Brush = new SolidColorBrush(Next()), Radius = 8, Opacity = .9f } };
            Add(lamp);
            Slide(lamp, 3, () => Height - BarH + 4);
            var wheel = new GraphicsView { Drawable = new SteeringDrawable(), WidthRequest = 44, HeightRequest = 44, InputTransparent = true };
            var b = new Border
            {
                WidthRequest = CenterD, HeightRequest = CenterD, StrokeShape = new Ellipse(), StrokeThickness = 4,
                Background = new RadialGradientBrush([new GradientStop(ir ? Color.FromArgb("#5A1C18") : Color.FromArgb("#14506F"), 0), new GradientStop(ir ? Color.FromArgb("#3A1210") : Color.FromArgb("#0B3550"), 1)]),
                Content = wheel,
            };
            b.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            _center = b;
            CenterAt();
            _onOpen = () => { wheel.Rotation = 0; wheel.RotateTo(-200, 500, Easing.CubicOut); };
            _onClose = () => wheel.RotateTo(0, 300);
        }

        /// <summary>H: a road with map pins; the Raphael van drives to the page, and the garage at
        /// the end is Sign out.</summary>
        private void BuildRoad()
        {
            _reserve = 112;
            var road = new Grid { HeightRequest = 112, VerticalOptions = LayoutOptions.End, ZIndex = 9, InputTransparent = true };
            road.SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
            var asphalt = new BoxView { Color = Color.FromArgb(IsInfrared ? "#2C1515" : "#2B3A42"), HeightRequest = 40, VerticalOptions = LayoutOptions.End };
            road.Add(asphalt);
            road.Add(new BoxView { Color = Color.FromArgb("#B9C7D0"), HeightRequest = 3, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 40) });
            var lanes = new HorizontalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 18) };
            for (var i = 0; i < 30; i++) lanes.Add(new BoxView { Color = Color.FromArgb("#F2C14E"), WidthRequest = 16, HeightRequest = 3 });
            road.Add(lanes);
            Add(road);

            var vanDraw = new VanDrawable();
            var vanG = new GraphicsView { Drawable = vanDraw, WidthRequest = 64, HeightRequest = 32 };
            // "Menu" beside the van, bobbing, so the driver knows the van opens All options (design «.van .hint»).
            var hint = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 7 }, Padding = new Thickness(7, 2), VerticalOptions = LayoutOptions.Start,
                BackgroundColor = Color.FromArgb("#10293A"), Content = new Label { Text = "Menu", FontSize = 9.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
            };
            Loop(hint, 2600, t => hint.TranslationY = -3 * Math.Sin(2 * Math.PI * t));
            var van = new HorizontalStackLayout { Spacing = 2, ZIndex = 13, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Children = { vanG, hint } };
            var pins = new List<View>();
            var garageDraw = new GarageDrawable();
            var garage = new GraphicsView { Drawable = garageDraw, WidthRequest = 44, HeightRequest = 36, HorizontalOptions = LayoutOptions.Center, InputTransparent = true };
            foreach (var it in Main.Append(SignOut))
            {
                View v;
                if (it == SignOut)
                {
                    // The garage: the Pull In closes the day, so Sign out is where the road ends.
                    var lab = new Label { Text = "Sign out", FontSize = 9.5, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
                    lab.SetDynamicResource(Label.TextColorProperty, "DangerColor");
                    v = new VerticalStackLayout { Spacing = 1, WidthRequest = 60, Margin = new Thickness(0, 8, 0, 0), Children = { garage, lab } };
                }
                else
                {
                    var on = it.Key == _current;
                    var pinDraw = new PinDrawable(on);
                    var pin = new GraphicsView { Drawable = pinDraw, WidthRequest = 34, HeightRequest = 44, InputTransparent = true };
                    var icon = Glyph(it.Glyph, 11, null);
                    icon.TextColor = on ? Next() : Brand();
                    icon.Margin = new Thickness(0, 0, 0, 10);
                    var g = new Grid { WidthRequest = 34, HeightRequest = 44, HorizontalOptions = LayoutOptions.Center, Children = { pin, icon } };
                    var lab = new Label { Text = it.Label, FontSize = 9.5, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
                    lab.SetDynamicResource(Label.TextColorProperty, on ? "InkColor" : "SecondaryTextColor");
                    v = new VerticalStackLayout { Spacing = 0, WidthRequest = 60, Children = { Badged(g, it), lab } };
                    var index = Array.IndexOf(Main, it);
                    // The tapped pin drops, lands, and its eye blinks (design «.stop.go»).
                    _arrivals.Add(async (a, b) =>
                    {
                        if (b != index || a == b) return;
                        g.TranslationY = -24;
                        await g.TranslateTo(0, 0, 420, Easing.BounceOut);
                        await Animate(v => { pinDraw.Blink = (float)v; icon.ScaleY = 1 - v * .9; pin.Invalidate(); }, 0, 1, 110);
                        await Animate(v => { pinDraw.Blink = (float)v; icon.ScaleY = 1 - v * .9; pin.Invalidate(); }, 1, 0, 140);
                    });
                }
                v.ZIndex = 12;
                v.HorizontalOptions = LayoutOptions.Start;
                v.VerticalOptions = LayoutOptions.Start;
                SemanticProperties.SetDescription(v, it.Label);
                var item = it;
                v.GestureRecognizers.Add(Tap(() => { if (item == SignOut) ParkAndAsk(); else Run(item); }));
                Add(v);
                pins.Add(v);
            }

            SemanticProperties.SetDescription(van, "All options");
            van.GestureRecognizers.Add(Tap(OnCenter));
            Add(van);
            double PinX(int i) => Width * (new[] { .1, .29, .48, .67, .88 })[i];
            int Here() => Math.Max(Index(_current), 0);
            _placers.Add(() =>
            {
                for (var i = 0; i < pins.Count; i++) At(pins[i], PinX(i) - 30, Height - 112);
                At(van, PinX(Here()) - 32, Height - 38);
            });
            _arrivals.Add((a, b) =>
            {
                if (a < 0 || b < 0 || a == b) return;
                DriveVan(b);
            });

            // The van drives to stop i, facing the way it goes; the RAPHAEL on its side stays readable.
            Task DriveVan(int i)
            {
                var dx = PinX(i) - PinX(Here());
                vanDraw.FacingLeft = dx < van.TranslationX;
                vanG.Invalidate();
                return van.TranslateTo(dx, 0, 750, Easing.CubicInOut);
            }

            // Sign out: the van drives into the garage and the door closes, then the question.
            // Cancel opens the door and the van comes back.
            async void ParkAndAsk()
            {
                await DriveVan(4);
                _ = van.FadeTo(0, 200);
                await Animate(v => { garageDraw.Door = (float)v; garage.Invalidate(); }, 0, 1, 450);
                OpenConfirm();
            }
            _settles.Add(() => { vanDraw.FacingLeft = false; garageDraw.Door = 0; vanG.Invalidate(); garage.Invalidate(); });
            _onCancelOut = async () =>
            {
                await Animate(v => { garageDraw.Door = (float)v; garage.Invalidate(); }, 1, 0, 350);
                _ = van.FadeTo(1, 200);
                await DriveVan(Here());
            };
        }

        private Task Animate(Action<double> step, double from, double to, uint ms)
        {
            var done = new TaskCompletionSource();
            new Animation(step, from, to).Commit(this, "step", 16, ms, Easing.CubicInOut, (_, _) => done.TrySetResult());
            return done.Task;
        }

        /// <summary>O: the bar is asphalt; a tire in the middle and a small one rolls to the page.</summary>
        private void BuildTire()
        {
            var ir = IsInfrared;
            var bar = Bar(new SolidColorBrush(Color.FromArgb(ir ? "#2C1515" : "#2B3A42")), null, "#F2C14E", "#C9D5DC");
            bar.Shadow = null;
            var edge = new BoxView { Color = Color.FromArgb("#F2C14E"), HeightRequest = 2, VerticalOptions = LayoutOptions.End, Margin = new Thickness(18, 0, 18, BarH - 6), ZIndex = 11, InputTransparent = true };
            Add(edge);
            var small = new GraphicsView { Drawable = new TireDrawable(), WidthRequest = 18, HeightRequest = 18, ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(small);
            Slide(small, 9, () => Height - 20, (v, dx) => v.RotateTo(dx / (Math.PI * 18) * 360, 420, Easing.CubicInOut));
            var tire = new GraphicsView { Drawable = new TireDrawable(), WidthRequest = CenterD, HeightRequest = CenterD };
            _center = tire;
            CenterAt();
            _onOpen = () => { tire.Rotation = 0; tire.RotateTo(360, 450, Easing.CubicOut); };
        }

        /// <summary>Q: each item is a road sign (diamond, circle, triangle, square) and the traffic
        /// light in the middle goes red, amber, green and opens All options.</summary>
        private void BuildSignals()
        {
            Bar(CardBrush(.97f), null, null, null, (it, on) =>
            {
                var i = Array.IndexOf(Main, it);
                var shape = new Border
                {
                    WidthRequest = 34, HeightRequest = 34, StrokeThickness = 2,
                    StrokeShape = i switch { 1 => new Ellipse(), 3 => new RoundRectangle { CornerRadius = 5 }, _ => new RoundRectangle { CornerRadius = 4 } },
                    Rotation = i == 0 ? 45 : 0, HorizontalOptions = LayoutOptions.Center,
                    Content = i == 2 ? null : new Label { Text = it.Glyph, FontFamily = "FontAwesomeSolid", FontSize = 14, Rotation = i == 0 ? -45 : 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = on ? Colors.White : Brand() },
                };
                shape.BackgroundColor = on ? Next() : Colors.Transparent;
                shape.Stroke = new SolidColorBrush(on ? Next() : Brand());
                View face = shape;
                if (i == 2)
                {
                    var tri = new Polygon { Points = [new Point(17, 2), new Point(33, 31), new Point(1, 31)], StrokeThickness = 2, Stroke = new SolidColorBrush(on ? Next() : Brand()), Fill = on ? new SolidColorBrush(Next()) : Brush.Transparent };
                    face = new Grid { WidthRequest = 34, HeightRequest = 34, HorizontalOptions = LayoutOptions.Center, Children = { tri, new Label { Text = it.Glyph, FontFamily = "FontAwesomeSolid", FontSize = 11, Margin = new Thickness(0, 8, 0, 0), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = on ? Colors.White : Brand() } } };
                }
                _arrivals.Add((a, b) => { if (b == i && a != b) { face.RotationY = 180; face.RotateYTo(0, 450, Easing.CubicOut); } });
                return face;
            });
            var lamps = new[] { "#E5584A", "#F2C14E", "#3DC48A" }.Select(c => new Ellipse { WidthRequest = 16, HeightRequest = 16, Fill = new SolidColorBrush(Color.FromArgb("#2B3A42")), Stroke = new SolidColorBrush(Color.FromArgb(c)), StrokeThickness = 1, ClassId = c }).ToArray();
            var light = new Border
            {
                WidthRequest = 40, HeightRequest = 78, StrokeShape = new RoundRectangle { CornerRadius = 14 }, StrokeThickness = 4,
                BackgroundColor = Color.FromArgb(IsInfrared ? "#3A1210" : "#10293A"), Padding = new Thickness(0, 5),
                Content = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, Children = { lamps[0], lamps[1], lamps[2] } },
            };
            light.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            _center = light;
            _placers.Add(() => At(light, Cx - 20, Height - BarH - 36));
            void Lit(int k) { for (var i = 0; i < 3; i++) lamps[i].Fill = new SolidColorBrush(i == k ? Color.FromArgb(lamps[i].ClassId) : Color.FromArgb("#2B3A42")); }
            Lit(0);
            _onOpen = () => Lit(2);
            _onClose = () => Lit(0);
        }

        private int _panelDelay;

        /// <summary>R: a speedometer instead of a bar; the needle points at the page and the logo
        /// is its hub.</summary>
        private void BuildGauge()
        {
            _reserve = 150;
            double[] angles = [165, 125, 55, 15];
            var gauge = new GaugeDrawable { Angle = (float)angles[Math.Max(Index(_current), 0)] };
            var dial = new GraphicsView { Drawable = gauge, WidthRequest = 262, HeightRequest = 150, ZIndex = 10, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(dial);
            var navs = new List<View>();
            foreach (var it in Main)
            {
                var on = it.Key == _current;
                var icon = Glyph(it.Glyph, 17, null);
                icon.TextColor = on ? Color.FromArgb("#FF8A7A") : Color.FromArgb("#C9DCE5");
                var lab = new Label { Text = it.Label, FontSize = 8.5, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? Colors.White : Color.FromArgb("#C9DCE5") };
                var v = new VerticalStackLayout { Spacing = 1, WidthRequest = 50, HeightRequest = 44, ZIndex = 12, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Children = { Badged(icon, it), lab } };
                SemanticProperties.SetDescription(v, it.Label);
                var item = it;
                v.GestureRecognizers.Add(Tap(() => Run(item)));
                Add(v);
                navs.Add(v);
            }
            var needle = new BoxView { WidthRequest = 4, HeightRequest = 74, Color = Color.FromArgb("#E5584A"), CornerRadius = 2, ZIndex = 11, AnchorX = .5, AnchorY = 1, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(needle);
            _center = LogoButton(60);
            _center.ZIndex = 13;
            double HubY() => Height - 23;
            _placers.Add(() =>
            {
                At(dial, Cx - 131, Height - 154);
                for (var i = 0; i < navs.Count; i++)
                {
                    var a = angles[i] * Math.PI / 180;
                    At(navs[i], Cx + Math.Cos(a) * 80 - 25, HubY() - Math.Sin(a) * 80 - 22);
                }
                At(needle, Cx - 2, HubY() - 74);
                needle.Rotation = 90 - gauge.Angle;
                At(_center!, Cx - 30, HubY() - 30);
            });
            _arrivals.Add((a, b) =>
            {
                if (b < 0 || a == b) return;
                needle.RotateTo(90 - angles[b], 700, Easing.SpringOut);
            });
        }

        // ---------------- Neutral ----------------

        /// <summary>C (Underline): the logo is the menu, bottom-right where a right thumb rests; a glass
        /// row of icons beside it and a coral chip that jumps to the page.</summary>
        private void BuildLogoRow()
        {
            _reserve = 76;
            var glass = new Border
            {
                HeightRequest = 58, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 29 }, ZIndex = 9, InputTransparent = true,
                VerticalOptions = LayoutOptions.End, Margin = new Thickness(14, 0, 88, 14), Background = CardBrush(.92f),
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .2f, Radius = 12, Offset = new Point(0, 4) },
            };
            Add(glass);
            var chip = new BoxView { WidthRequest = 44, HeightRequest = 44, CornerRadius = 22, Color = Next(), ZIndex = 10, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(chip);
            var row = new Grid { ColumnDefinitions = Columns("*,*,*,*"), HeightRequest = 58, ZIndex = 11, VerticalOptions = LayoutOptions.End, Margin = new Thickness(14, 0, 88, 14) };
            var glyphs = new List<Label>();
            for (var i = 0; i < Main.Length; i++)
            {
                var it = Main[i];
                var on = it.Key == _current;
                var g = Glyph(it.Glyph, 19, on ? null : "SecondaryTextColor");
                if (on) g.TextColor = Colors.White;
                glyphs.Add(g);
                SemanticProperties.SetDescription(g, it.Label);
                g.GestureRecognizers.Add(Tap(() => Run(it)));
                row.Add(Badged(g, it), i);
            }
            Add(row);
            double ChipX(int i) => 14 + (Width - 102) / 4 * (i + .5) - 22;
            _placers.Add(() => At(chip, ChipX(Math.Max(Index(_current), 0)), Height - 14 - 51));
            _arrivals.Add((a, b) =>
            {
                if (a < 0 || b < 0 || a == b) return;
                var dx = ChipX(b) - ChipX(a);
                // The icon the chip leaves takes the bar's colour; the one it lands on turns white.
                glyphs[a].TextColor = ThemeManager.GetColor("SecondaryTextColor", Colors.Gray);
                Run(chip, "jump", 420, t =>
                {
                    chip.TranslationX = dx * t;
                    chip.TranslationY = -28 * Math.Sin(Math.PI * t);
                    if (t > .85) glyphs[b].TextColor = Colors.White;
                });
            });
            _center = LogoButton(64);
            _placers.Add(() => At(_center!, Width - 14 - 64, Height - 14 - 61));
        }

        /// <summary>S: a metro line; the items are stations and a train runs to the page.</summary>
        private void BuildMetro()
        {
            Bar(CardBrush(.97f), IsLightish ? "InkColor" : "AccentColor");
            var line = new BoxView { HeightRequest = 4, Color = ThemeManager.GetColor("SeparatorColor", Colors.LightGray), ZIndex = 11, InputTransparent = true, VerticalOptions = LayoutOptions.Start, HorizontalOptions = LayoutOptions.Start };
            var fill = new BoxView { HeightRequest = 4, Color = Accent(), ZIndex = 11, InputTransparent = true, VerticalOptions = LayoutOptions.Start, HorizontalOptions = LayoutOptions.Start };
            var train = new BoxView { WidthRequest = 28, HeightRequest = 10, CornerRadius = 5, Color = Next(), ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(line); Add(fill); Add(train);
            _placers.Add(() =>
            {
                At(line, 18, Height - BarH + 4);
                line.WidthRequest = Width - 36;
                var x = ItemX(Math.Max(Index(_current), 0)) ?? 0;
                At(fill, 18, Height - BarH + 4);
                fill.WidthRequest = Math.Max(0, x - 18);
            });
            Slide(train, 14, () => Height - BarH + 1);
            // The line fills in blue up to the station the train is going to (design «.st .fill»).
            _arrivals.Add((a, b) =>
            {
                var xa = ItemX(a); var xb = ItemX(b);
                if (xa == null || xb == null || a == b) return;
                Run(fill, "fill", 420, t => fill.WidthRequest = Math.Max(0, xa.Value + (xb.Value - xa.Value) * t - 18));
            });
            var c = LogoButton(CenterD);
            c.SetDynamicResource(Border.StrokeProperty, "AccentColor");
            _center = c;
            CenterAt();
        }

        /// <summary>T: a coral plus turns into ×, and a column of buttons rises over the thumb.</summary>
        private void BuildCross()
        {
            Bar(CardBrush(.97f));
            var ring = new Ellipse { WidthRequest = 44, HeightRequest = 44, StrokeThickness = 2, Stroke = new SolidColorBrush(Next().WithAlpha(.6f)), ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(ring);
            Slide(ring, 22, () => Height - BarH + 6, (v, _) => v.ScaleTo(1.5, 300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => v.ScaleTo(1, 200))));
            var plus = WhiteGlyph("", 28);
            var b = new Border
            {
                WidthRequest = CenterD, HeightRequest = CenterD, StrokeShape = new Ellipse(), StrokeThickness = 5, Content = plus,
                Background = new LinearGradientBrush([new GradientStop(Color.FromArgb("#F26B5B"), 0), new GradientStop(Next(), 1)], new Point(0, 0), new Point(1, 1)),
            };
            b.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            _center = b;
            CenterAt();
            _onOpen = () => plus.RotateTo(135, 350, Easing.SpringOut);
            _onClose = () => plus.RotateTo(0, 250);
            _fanW = 190; _fanH = 52;
            BuildFanItems(column: true);
            _fanAt = (i, cx, cy) => (cx - 155, cy - 100 - i * 62);
        }

        /// <summary>F: a sheet with a handle; the four peek out and the rest is underneath.</summary>
        private void BuildSheet()
        {
            _reserve = 92;
            var handle = new BoxView { WidthRequest = 40, HeightRequest = 5, CornerRadius = 3, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 8, 0, 4) };
            handle.SetDynamicResource(BoxView.ColorProperty, "BorderColor");
            var grip = new Grid { HeightRequest = 22, Children = { handle } };
            SemanticProperties.SetDescription(grip, "All options");
            var row = new Grid { ColumnDefinitions = Columns("*,*,*,*"), Padding = new Thickness(6, 0, 6, 8) };
            for (var i = 0; i < Main.Length; i++) row.Add(NavButton(Main[i], "NextColor"), i);
            var sheet = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) }, VerticalOptions = LayoutOptions.End, ZIndex = 10,
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .2f, Radius = 16, Offset = new Point(0, -4) },
                Content = new VerticalStackLayout { Children = { grip, row } },
            };
            sheet.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            Add(sheet);
            var line = new BoxView { HeightRequest = 3, WidthRequest = 26, CornerRadius = 2, Color = Next(), ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            Add(line);
            double X(int i) => 6 + (Width - 12) / 4 * (i + .5);
            _placers.Add(() => At(line, X(Math.Max(Index(_current), 0)) - 13, Height - 10));
            _arrivals.Add((a, b) =>
            {
                if (a < 0 || b < 0 || a == b) return;
                line.ScaleTo(.2, 150).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    line.TranslationX = X(b) - X(a);
                    line.ScaleTo(1, 260, Easing.CubicOut);
                }));
            });
            // The handle lives inside the sheet, so it is not the shared centre button: a view can
            // have one parent only, and adding it twice closed the app.
            grip.GestureRecognizers.Add(Tap(OnCenter));
        }

        /// <summary>J: a dark capsule floating at the bottom, with a glowing coral dot under the page
        /// and an arrow that opens All options.</summary>
        private void BuildCapsule()
        {
            _reserve = 84;
            var cap = new Border
            {
                HeightRequest = 62, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 31 }, VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(16, 0, 16, 14), ZIndex = 10, BackgroundColor = Color.FromArgb(IsInfrared ? "#2C1515" : "#10181D"),
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .35f, Radius = 16, Offset = new Point(0, 6) },
            };
            var grid = new Grid { ColumnDefinitions = Columns("*,*,*,*,52"), Padding = new Thickness(6, 0) };
            for (var i = 0; i < Main.Length; i++)
            {
                var it = Main[i];
                var g = Glyph(it.Glyph, 19, null);
                g.TextColor = it.Key == _current ? Colors.White : Color.FromArgb("#8EA2AD");
                SemanticProperties.SetDescription(g, it.Label);
                g.GestureRecognizers.Add(Tap(() => Run(it)));
                grid.Add(Badged(g, it), i);
            }
            var arrow = Glyph("", 16, null);
            arrow.TextColor = Colors.White;
            _center = arrow;
            cap.Content = grid;
            Add(cap);
            var dot = new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4, Color = Color.FromArgb("#FF6F61"), ZIndex = 11, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#FF6F61")), Radius = 8, Opacity = 1 } };
            Add(dot);
            double X(int i) => 22 + (Width - 32 - 12 - 52) / 4 * (i + .5);
            _placers.Add(() =>
            {
                At(dot, X(Math.Max(Index(_current), 0)) - 4, Height - 22);
                At(arrow, Width - 16 - 6 - 46, Height - 14 - 62);
                arrow.WidthRequest = 46; arrow.HeightRequest = 62;
            });
            _arrivals.Add((a, b) =>
            {
                if (a < 0 || b < 0 || a == b) return;
                dot.ScaleX = 2.2; dot.ScaleY = .6;
                dot.TranslateTo(X(b) - X(a), 0, 420, Easing.CubicOut);
                dot.ScaleTo(1, 420, Easing.SpringOut);
            });
            arrow.ZIndex = 12;
        }

        // ---------------- Season ----------------

        /// <summary>U: a night-blue bar with snow on top and coloured lights, snow falling, and a gold
        /// star over the page.</summary>
        private void BuildChristmas()
        {
            var bar = Bar(new LinearGradientBrush([new GradientStop(Color.FromArgb("#123456"), 0), new GradientStop(Color.FromArgb("#0A1E33"), 1)], new Point(0, 0), new Point(0, 1)), null, "#FFE8A3", "#C6D8E6");
            var snow = new Border { HeightRequest = 12, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12, 12, 6, 6) }, BackgroundColor = Colors.White, VerticalOptions = LayoutOptions.End, Margin = new Thickness(4, 0, 4, BarH - 6), ZIndex = 11, InputTransparent = true };
            Add(snow);
            var lights = new HorizontalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Fill, Margin = new Thickness(14, 0, 14, BarH - 18), ZIndex = 11, InputTransparent = true };
            string[] colours = ["#FF6F61", "#FFD24A", "#5FD38F", "#7CC8E0"];
            for (var i = 0; i < 12; i++)
            {
                var bulb = new BoxView { WidthRequest = 6, HeightRequest = 9, CornerRadius = 3, Color = Color.FromArgb(colours[i % 4]), Margin = new Thickness(0, 0, 22, 0) };
                lights.Add(bulb);
                var off = i * .23;
                Loop(bulb, 1600, t => bulb.Opacity = .45 + .55 * Math.Abs(Math.Cos(Math.PI * (t + off))));
            }
            Add(lights);
            var star = Glyph("", 20, null);
            star.TextColor = Color.FromArgb("#FFD24A");
            star.ZIndex = 11;
            star.InputTransparent = true;
            star.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#FFD24A")), Radius = 8, Opacity = .9f };
            star.HorizontalOptions = LayoutOptions.Start;
            star.VerticalOptions = LayoutOptions.Start;
            star.WidthRequest = 24;
            Add(star);
            Slide(star, 12, () => Height - BarH - 30, (v, _) => v.RotateTo(360, 420, Easing.CubicOut));
            var rnd = new Random(7);
            for (var i = 0; i < 16; i++)
            {
                var flake = new BoxView { WidthRequest = 4, HeightRequest = 4, CornerRadius = 2, Color = IsLightish ? Color.FromArgb("#BFD6E6") : Colors.White, ZIndex = 5, InputTransparent = true, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Opacity = .85 };
                Add(flake);
                var fx = rnd.NextDouble();
                var dur = (uint)(7000 + rnd.Next(7000));
                var start = rnd.NextDouble();
                _placers.Add(() => At(flake, fx * Width, 0));
                Loop(flake, dur, t => { var k = (t + start) % 1; flake.TranslationY = k * Math.Max(Height, 600); flake.TranslationX = Math.Sin(k * 6) * 10; });
            }
            var c = LogoButton(74);
            c.Stroke = new SolidColorBrush(Color.FromArgb("#FFD24A"));
            c.StrokeThickness = 4;
            c.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#FFD24A")), Radius = 18, Opacity = .6f };
            _center = c;
            _placers.Add(() => At(c, Cx - 37, Height - BarH - 30));
            var crown = Halo(46, 12, new SolidColorBrush(Color.FromArgb("#FFD24A")));
            Add(crown);
            Bob(crown);
            _placers.Add(() => At(crown, Cx - 23, Height - BarH - 46));
        }

        // =====================================================================================
        //  Shared pieces
        // =====================================================================================

        /// <summary>The 2 + centre + 2 bar most styles share. <paramref name="face"/> draws an item
        /// differently; the colours default to the theme's ink and secondary text.</summary>
        private Border Bar(Brush background, string? onKey = "InkColor", string? onHex = null, string? offHex = null, Func<Item, bool, View>? face = null)
        {
            var bar = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22, 22, 0, 0) },
                HeightRequest = BarH, VerticalOptions = LayoutOptions.End, ZIndex = 10, Background = background,
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .18f, Radius = 14, Offset = new Point(0, -3) },
            };
            var grid = new Grid { ColumnDefinitions = Columns("*,*,84,*,*"), Padding = new Thickness(6, 0, 6, 6) };
            int[] cols = [0, 1, 3, 4];
            for (var i = 0; i < Main.Length; i++)
            {
                var it = Main[i];
                var on = it.Key == _current;
                View icon;
                if (face != null) icon = face(it, on);
                else
                {
                    var g = Glyph(it.Glyph, 20, onHex == null && offHex == null ? (on ? onKey ?? "InkColor" : "SecondaryTextColor") : null);
                    if (onHex != null || offHex != null) g.TextColor = Color.FromArgb(on ? onHex ?? "#FFFFFF" : offHex ?? "#8EA2AD");
                    else if (on && _style == MenuStyle.SteeringWheel) g.SetDynamicResource(Label.TextColorProperty, "NextColor");
                    icon = g;
                }
                var text = new Label { Text = it.Label, FontSize = 10.5, FontAttributes = on ? FontAttributes.Bold : FontAttributes.None, HorizontalTextAlignment = TextAlignment.Center };
                if (onHex != null || offHex != null) text.TextColor = Color.FromArgb(on ? onHex ?? "#FFFFFF" : offHex ?? "#8EA2AD");
                else text.SetDynamicResource(Label.TextColorProperty, on ? onKey ?? "InkColor" : "SecondaryTextColor");
                var v = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 8), Children = { Badged(icon, it), text } };
                SemanticProperties.SetDescription(v, it.Label);
                v.GestureRecognizers.Add(Tap(() => Run(it)));
                grid.Add(v, cols[i]);
            }
            bar.Content = grid;
            Add(bar);
            return bar;
        }

        private View NavButton(Item it, string onKey)
        {
            var on = it.Key == _current;
            var icon = Glyph(it.Glyph, 20, on ? onKey : "SecondaryTextColor");
            var text = new Label { Text = it.Label, FontSize = 10.5, FontAttributes = on ? FontAttributes.Bold : FontAttributes.None, HorizontalTextAlignment = TextAlignment.Center };
            text.SetDynamicResource(Label.TextColorProperty, on ? "InkColor" : "SecondaryTextColor");
            var v = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 8), Children = { Badged(icon, it), text } };
            SemanticProperties.SetDescription(v, it.Label);
            v.GestureRecognizers.Add(Tap(() => Run(it)));
            return v;
        }

        /// <summary>The marker under or over the selected item of the bar: placed at the page, and
        /// sliding to the item the driver taps before the page changes.</summary>
        private void Slide(View marker, double half, Func<double> y, Action<View, double>? extra = null)
        {
            _placers.Add(() => { var x = ItemX(Index(_current)); At(marker, (x ?? -200) - half, y()); });
            _arrivals.Add((a, b) =>
            {
                var xa = ItemX(a); var xb = ItemX(b);
                if (xa == null || xb == null || a == b) return;
                var dx = xb.Value - xa.Value;
                marker.TranslateTo(dx, 0, 420, Easing.CubicInOut);
                extra?.Invoke(marker, dx);
            });
        }

        private void CenterAt() => _placers.Add(() => At(_center!, Cx - CenterD / 2, Height - BarH - Raise));

        private static Ellipse Halo(double w, double h, Brush stroke) => new()
        {
            WidthRequest = w, HeightRequest = h, StrokeThickness = 2.5, Stroke = stroke, Fill = Brush.Transparent, ZIndex = 11,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, InputTransparent = true,
        };

        /// <summary>The fan of K and M: Messages, Contact, Sign out and All options on an arc.</summary>
        private void ArcFan(double radius, double[] angles)
        {
            BuildFanItems(column: false);
            _fanAt = (i, cx, cy) =>
            {
                // The design's radius was for a 340-wide phone: in proportion here, so the names never touch.
                var a = angles[i] * Math.PI / 180;
                var r = radius * Math.Max(1, Width / 340);
                return (cx + Math.Sin(a) * r - 42, cy - Math.Cos(a) * r - 28);
            };
        }

        private void BuildFanItems(bool column)
        {
            // The design's fan: Messages, Contact, Change route, Sign out and All options. Angel puts
            // Call me in Contact's place: drivers use it far more (Contact stays in All options).
            var second = _style == MenuStyle.AngelWings ? CallMe : Contact;
            foreach (var it in new[] { Messages, second, ChangeRoute, SignOut, AllOptions })
            {
                var circle = new Border
                {
                    WidthRequest = 50, HeightRequest = 50, StrokeShape = new Ellipse(), StrokeThickness = 1.5, HorizontalOptions = LayoutOptions.Center,
                    Content = Glyph(it.Glyph, 20, it == AllOptions ? null : it == SignOut ? "DangerColor" : "InkColor"),
                };
                if (it == AllOptions)
                {
                    circle.BackgroundColor = Brand();
                    ((Label)circle.Content).TextColor = Colors.White;
                    circle.Stroke = Brush.Transparent;
                }
                else if (_style == MenuStyle.Radar && it != SignOut)
                {
                    // Radar: dark contacts with a cyan rim, as the radar shows them (design «.mrf .fi»).
                    var ir = IsInfrared;
                    circle.BackgroundColor = Color.FromArgb(ir ? "#3A1210" : "#10293A");
                    circle.Stroke = new SolidColorBrush(Color.FromArgb(ir ? "#FF6F61" : "#50ABC1"));
                    circle.StrokeThickness = 2;
                    ((Label)circle.Content).TextColor = Color.FromArgb(ir ? "#FFB4A9" : "#9FD3E6");
                    circle.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb(ir ? "#FF6F61" : "#50ABC1")), Radius = 12, Opacity = .6f };
                }
                else
                {
                    circle.SetDynamicResource(BackgroundColorProperty, it == SignOut ? "DangerSoftColor" : "CardBackgroundColor");
                    circle.Stroke = it == SignOut ? new SolidColorBrush(Danger()) : Gold();
                }
                var name = new Border
                {
                    StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 7 }, Padding = new Thickness(7, 2), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                    Content = new Label { Text = it.Label, FontSize = column ? 12 : 10.5, FontAttributes = FontAttributes.Bold },
                };
                name.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
                ((Label)name.Content).SetDynamicResource(Label.TextColorProperty, "InkColor");

                Layout v = column
                    ? new Grid { WidthRequest = _fanW, HeightRequest = 52, Children = { new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.End, Children = { name, circle } } } }
                    : new VerticalStackLayout { Spacing = 4, WidthRequest = 84, Children = { circle, name } };
                v.ZIndex = 22; v.IsVisible = false; v.Opacity = 0;
                v.HorizontalOptions = LayoutOptions.Start; v.VerticalOptions = LayoutOptions.Start;
                SemanticProperties.SetDescription(v, it.Label);
                var item = it;
                v.GestureRecognizers.Add(Tap(() => Run(item)));
                _fan.Add(v);
                Add(v);
            }
            _placers.Add(() =>
            {
                if (_fanAt == null) return;
                for (var i = 0; i < _fan.Count; i++) { var (x, y) = _fanAt(i, Cx, Cy); At(_fan[i], x, y); }
            });
        }

        /// <summary>All options: the logo, then MAIN and MORE in two even columns, icon and name
        /// only, and Sign out at the bottom.</summary>
        private void BuildPanel()
        {
            var tile = new Border { WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = Logo(32) };
            tile.SetDynamicResource(BackgroundColorProperty, "LogoTileColor");
            var title = new Label { Text = "RAPHAEL DRIVER", FontSize = 14, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.5, VerticalOptions = LayoutOptions.Center };
            title.SetDynamicResource(Label.TextColorProperty, "InkColor");
            var close = Glyph("", 18, "SecondaryTextColor");
            close.Padding = new Thickness(10);
            SemanticProperties.SetDescription(close, "Close");
            close.GestureRecognizers.Add(Tap(CloseAll));
            var head = new Grid { ColumnDefinitions = Columns("Auto,*,Auto"), ColumnSpacing = 12 };
            head.Add(tile, 0); head.Add(title, 1); head.Add(close, 2);

            var stack = new VerticalStackLayout { Spacing = 8, Children = { head, Section("MAIN"), Tiles(Main), Section("MORE"), Tiles([Messages, Contact, ChangeRoute, CallMe, Documents]) } };
            var outGrid = new Grid { ColumnDefinitions = Columns("28,*"), ColumnSpacing = 10, Padding = new Thickness(14, 12) };
            outGrid.Add(Glyph(SignOut.Glyph, 17, "DangerColor"), 0);
            var outText = new Label { Text = "Sign out", FontAttributes = FontAttributes.Bold, FontSize = 14, VerticalOptions = LayoutOptions.Center };
            outText.SetDynamicResource(Label.TextColorProperty, "DangerColor");
            outGrid.Add(outText, 1);
            var outRow = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, Content = outGrid, Margin = new Thickness(0, 4, 0, 0) };
            outRow.SetDynamicResource(BackgroundColorProperty, "DangerSoftColor");
            outRow.GestureRecognizers.Add(Tap(() => Run(SignOut)));
            SemanticProperties.SetDescription(outRow, "Sign out");
            stack.Add(outRow);

            _panel = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 22 }, Padding = new Thickness(14), VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(12, 0, 12, Math.Max(PanelGap, _reserve + 20)), IsVisible = false, Opacity = 0, ZIndex = 24, Content = stack,
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .35f, Radius = 24, Offset = new Point(0, 10) },
            };
            _panel.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            Add(_panel);
        }

        private static Label Section(string text)
        {
            var l = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.2, Margin = new Thickness(2, 6, 0, 0) };
            l.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");
            return l;
        }

        private Grid Tiles(IReadOnlyList<Item> items)
        {
            var g = new Grid { ColumnDefinitions = Columns("*,*"), ColumnSpacing = 8, RowSpacing = 8 };
            for (var i = 0; i < items.Count; i++)
            {
                if (i % 2 == 0) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var it = items[i];
                var on = it.Key == _current;
                var row = new Grid { ColumnDefinitions = Columns("26,*"), ColumnSpacing = 8, Padding = new Thickness(12) };
                row.Add(Glyph(it.Glyph, 16, on ? "NextColor" : "AccentColor"), 0);
                var name = new Label { Text = it == Main[2] ? "Notifications" : it.Label, FontSize = 13.5, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
                name.SetDynamicResource(Label.TextColorProperty, "InkColor");
                row.Add(name, 1);
                var b = new Border { StrokeThickness = on ? 1.5 : 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, Content = row };
                b.SetDynamicResource(BackgroundColorProperty, "TintColor");
                if (on) b.SetDynamicResource(Border.StrokeProperty, "NextColor");
                b.GestureRecognizers.Add(Tap(() => Run(it)));
                SemanticProperties.SetDescription(b, name.Text);
                g.Add(b, i % 2, i / 2);
            }
            return g;
        }

        /// <summary>The sign-out question. The app used to sign out on a single tap.</summary>
        private void BuildConfirm()
        {
            var t = new Label { Text = "Sign out?", FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
            t.SetDynamicResource(Label.TextColorProperty, "InkColor");
            var p = new Label { Text = "You'll need to sign in again to see your schedule.", FontSize = 13.5, HorizontalTextAlignment = TextAlignment.Center };
            p.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");
            var cancel = new Button { Text = "Cancel", FontAttributes = FontAttributes.Bold, CornerRadius = 12, HeightRequest = 46 };
            cancel.SetDynamicResource(Button.BackgroundColorProperty, "TintColor");
            cancel.SetDynamicResource(Button.TextColorProperty, "InkColor");
            cancel.Clicked += (_, _) => { CloseAll(); _onCancelOut?.Invoke(); };
            var yes = new Button { Text = "Sign out", FontAttributes = FontAttributes.Bold, CornerRadius = 12, HeightRequest = 46, TextColor = Colors.White };
            yes.SetDynamicResource(Button.BackgroundColorProperty, "DangerColor");
            yes.Clicked += (_, _) => { CloseAll(); ShellVm().SignOutCommand.Execute(null); };
            var buttons = new Grid { ColumnDefinitions = Columns("*,*"), ColumnSpacing = 10, Margin = new Thickness(0, 8, 0, 0) };
            buttons.Add(cancel, 0); buttons.Add(yes, 1);
            _confirm = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 22 }, Padding = new Thickness(18, 20, 18, 16), Margin = new Thickness(28, 0),
                VerticalOptions = LayoutOptions.Center, IsVisible = false, Opacity = 0, ZIndex = 30,
                Content = new VerticalStackLayout { Spacing = 8, Children = { Glyph(SignOut.Glyph, 24, "DangerColor"), t, p, buttons } },
            };
            _confirm.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            Add(_confirm);
        }

        private static Label Glyph(string glyph, double size, string? colorKey)
        {
            var l = new Label { Text = glyph, FontFamily = "FontAwesomeSolid", FontSize = size, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            if (colorKey != null) l.SetDynamicResource(Label.TextColorProperty, colorKey);
            return l;
        }

        private static Label WhiteGlyph(string glyph, double size)
        {
            var l = Glyph(glyph, size, null);
            l.TextColor = Colors.White;
            return l;
        }

        /// <summary>The official logo, with its halo, in the theme's version (red in Infrared).</summary>
        private static Image Logo(double h)
        {
            var img = new Image { HeightRequest = h, WidthRequest = h * 100 / 128, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            img.SetDynamicResource(Image.SourceProperty, "OfficialLogoImage");
            return img;
        }

        private static Border LogoButton(double d)
        {
            var b = new Border
            {
                WidthRequest = d, HeightRequest = d, StrokeShape = new Ellipse(), StrokeThickness = 5, Content = Logo(d * .62),
                Shadow = new Shadow { Brush = Colors.Black, Opacity = .3f, Radius = 12, Offset = new Point(0, 5) },
            };
            b.SetDynamicResource(BackgroundColorProperty, "LogoTileColor");
            b.SetDynamicResource(Border.StrokeProperty, "PageBackgroundColor");
            return b;
        }

        private static Border Circle(double d, string bgKey, View content)
        {
            var b = new Border { WidthRequest = d, HeightRequest = d, StrokeThickness = 0, StrokeShape = new Ellipse(), Content = content, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            b.SetDynamicResource(BackgroundColorProperty, bgKey);
            return b;
        }

        // =====================================================================================
        //  Placing
        // =====================================================================================

        private double Cx => Width / 2;
        private double Cy => Height - BarH - Raise + CenterD / 2;

        private void Place()
        {
            if (Width <= 0 || Height <= 0) return;
            foreach (var place in _placers) place();
        }

        /// <summary>The centre of item <paramref name="i"/> on the 2 + centre + 2 bar.</summary>
        private double? ItemX(int i)
        {
            if (i < 0) return null;
            var col = (Width - 12 - 84) / 4;
            return i switch
            {
                0 => 6 + col / 2,
                1 => 6 + col * 1.5,
                2 => 6 + col * 2 + 84 + col / 2,
                _ => 6 + col * 3 + 84 + col / 2,
            };
        }

        private static void At(View v, double x, double y) => v.Margin = new Thickness(x, y, 0, 0);

        // =====================================================================================
        //  Behaviour
        // =====================================================================================

        private void OnCenter()
        {
            if (_fanOpen || _panelOpen) { CloseAll(); return; }
            if (_fan.Count > 0) OpenFan(); else OpenPanel();
        }

        private async void Run(Item it)
        {
            switch (it.Key)
            {
                case "all":
                    await CloseFan();
                    OpenPanel(skipDelay: true);
                    return;
                case "out":
                    await CloseFan();
                    HidePanel();
                    OpenConfirm();
                    return;
                case "sms":
                    CloseAll();
                    ShellVm().SendSmsCommand.Execute(null);
                    return;
                case "route":
                    // Change route: back to the Run Login, as the route icon of Schedule does.
                    await CloseFan();
                    CloseAll();
                    try { await Shell.Current.GoToAsync("//SchedulePage"); } catch (Exception) { }
                    return;
                case "callme":
                    CloseAll();
                    CallRequest();
                    return;
                case "docs":
                    // Under Settings, where MY DOCUMENTS lives: Back returns there.
                    CloseAll();
                    try { await Shell.Current.GoToAsync($"//SettingsPage/{nameof(Views.DocumentsPage)}"); } catch (Exception) { }
                    return;
            }
            CloseAll();
            if (it.Key == _current || it.Route == null) return;
            try
            {
                // Schedule opens Today, with its History · Today · Future tabs, when the run is
                // known, as the Dashboard's SCHEDULE button does; otherwise the Run Login page.
                if (it.Key == "schedule" && !string.IsNullOrWhiteSpace(ActiveRun.RunLogin))
                    await Shell.Current.GoToAsync($"//{nameof(Views.TodaySchedulePage)}", animate: false, new Dictionary<string, object> { { "runLogin", ActiveRun.RunLogin! } });
                else
                    await Shell.Current.GoToAsync("//" + it.Route, animate: false);
            }
            catch (Exception) { /* stays on this page; the menu is still there to try again */ }
        }

        private void ShowVeil()
        {
            _veil.IsVisible = true;
            _veil.Opacity = .32;
        }

        private void OpenFan()
        {
            _fanOpen = true;
            ShowVeil();
            if (_center != null) _center.ZIndex = 23;
            _onOpen?.Invoke();
            for (var i = 0; i < _fan.Count; i++)
            {
                var v = _fan[i];
                v.IsVisible = true;
                v.TranslationX = Cx - (v.Margin.Left + _fanW / 2);
                v.TranslationY = Cy - (v.Margin.Top + _fanH / 2);
                v.Scale = .3;
                v.TranslationX = 0;
                v.TranslationY = 0;
                v.Scale = 1;
                v.Opacity = 1;
            }
        }

        private Task CloseFan()
        {
            if (!_fanOpen) return Task.CompletedTask;
            _fanOpen = false;
            _onClose?.Invoke();
            foreach (var v in _fan)
            {
                v.Opacity = 0;
                v.IsVisible = false;
            }
            if (_center != null) _center.ZIndex = 12;
            return Task.CompletedTask;
        }

        private void OpenPanel(bool skipDelay = false)
        {
            _panelOpen = true;
            ShowVeil();
            if (_center != null) _center.ZIndex = 23;
            if (!skipDelay)
            {
                _onOpen?.Invoke();
            }
            if (!_panelOpen) return;
            // Built the first time it opens: every page pays for its menu, not for this.
            if (_panel == null) BuildPanel();
            _panel.IsVisible = true;
            _panel.TranslationY = 0;
            _panel.Opacity = 1;
        }

        private void HidePanel()
        {
            if (_panelOpen && _fan.Count == 0) _onClose?.Invoke();
            _panelOpen = false;
            if (_panel != null)
            {
                _panel.Opacity = 0;
                _panel.IsVisible = false;
            }
            if (_center != null) _center.ZIndex = 12;
        }

        private void OpenConfirm()
        {
            _confirmOpen = true;
            ShowVeil();
            if (_confirm == null) BuildConfirm();
            _confirm.IsVisible = true;
            _confirm.Scale = 1;
            _confirm.Opacity = 1;
        }

        private void CloseAll()
        {
            _ = CloseFan();
            HidePanel();
            _confirmOpen = false;
            if (_confirm != null)
            {
                _confirm.Opacity = 0;
                _confirm.IsVisible = false;
            }
            _veil.Opacity = 0;
            _veil.IsVisible = false;
        }

        // ---------------- animations ----------------

        /// <summary>A repeating animation that stops by itself once the view leaves the screen.</summary>
        private static void Loop(View v, uint ms, Action<double> step) =>
            new Animation(step, 0, 1).Commit(v, "loop", 16, ms, Easing.Linear, repeat: () => v.IsLoaded);

        private static void Run(IAnimatable owner, string name, uint ms, Action<double> step) =>
            new Animation(step, 0, 1).Commit(owner, name, 16, ms, Easing.CubicOut);

        private static void Bob(View v) => Loop(v, 2400, t => v.TranslationY = -3 * Math.Sin(Math.PI * t));

        // ---------------- helpers ----------------

        private static TapGestureRecognizer Tap(Action a)
        {
            var t = new TapGestureRecognizer();
            t.Tapped += (_, _) => a();
            return t;
        }

        private static ColumnDefinitionCollection Columns(string spec) => (ColumnDefinitionCollection)new ColumnDefinitionCollectionTypeConverter().ConvertFromInvariantString(spec)!;

        /// <summary>Call me from All options: the page's own Call me button when it has one, so the
        /// request carries its route and stop; otherwise a request for the driver's route today.</summary>
        private void CallRequest()
        {
            Element? e = this;
            while (e != null && e is not ContentPage) e = e.Parent;
            var roots = new List<IView>();
            if (e is ContentPage page)
            {
                if (page.Content != null) roots.Add(page.Content);
                if (Shell.GetTitleView(page) is View title) roots.Add(title);
            }
            var button = roots.SelectMany(Descendants).OfType<CallRequestButton>().FirstOrDefault();
            if (button != null) { button.Tap(); return; }
            var vm = ServiceHelper.GetService<CallRequestButtonViewModel>();
            vm?.TapCommand.Execute(null);
        }

        private static IEnumerable<IView> Descendants(IView v) =>
            new[] { v }.Concat(v is Layout l ? l.Children.SelectMany(Descendants) : v is IContentView c && c.PresentedContent is IView p ? Descendants(p) : []);

        private static AppShellViewModel ShellVm() => (AppShellViewModel)Shell.Current.BindingContext;

        private static bool IsInfrared => ThemeManager.Effective == SkinTheme.Infrared;
        private static bool IsLightish => ThemeManager.Effective is SkinTheme.Light or SkinTheme.Gray;
        private static Brush CardBrush(float alpha) => new SolidColorBrush(ThemeManager.GetColor("CardBackgroundColor", Colors.White).WithAlpha(alpha));
        private static Color Accent() => ThemeManager.GetColor("AccentColor", Color.FromArgb("#1A6A88"));
        private static Color Next() => ThemeManager.GetColor("NextColor", Color.FromArgb("#C9412F"));
        private static Color Danger() => ThemeManager.GetColor("DangerColor", Color.FromArgb("#C2362F"));
        private static Color Brand() => ThemeManager.GetColor("BrandBlueColor", Color.FromArgb("#07577A"));
        private static SolidColorBrush Gold() => new(Color.FromArgb(IsInfrared ? "#F5B94A" : "#E9A23B"));
    }

    /// <summary>Crescent (N): the band of the arc the thumb draws, in the card colour.</summary>
    internal sealed class ArcDrawable(Func<bool> leftHand) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF r)
        {
            var left = leftHand();
            float cx = left ? 40 : r.Width - 40, cy = r.Height - 40;
            var card = ThemeManager.GetColor("CardBackgroundColor", Colors.White);
            canvas.SetShadow(new SizeF(0, 3), 10, Colors.Black.WithAlpha(.25f));
            canvas.StrokeColor = card.WithAlpha(.96f);
            canvas.StrokeSize = 64;
            canvas.StrokeLineCap = LineCap.Round;
            // The items sit from 82° to 178° on a 112 radius; the band runs 16° past each end.
            if (left) canvas.DrawArc(cx - 112, cy - 112, 224, 224, -14, 114, false, false);
            else canvas.DrawArc(cx - 112, cy - 112, 224, 224, 66, 194, false, false);
        }
    }
}

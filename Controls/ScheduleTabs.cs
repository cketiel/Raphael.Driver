using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.Views;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// History · Today · Future at the top of the three schedule pages (RE-064), in time order: what
    /// is done today, what is left today, and tomorrow. It replaces the VIEW HISTORY and CHANGE
    /// ROUTE buttons; the route icon on the right does what CHANGE ROUTE did.
    /// Design: <c>_meta/design/driver-navigation/round-4.html</c>, «Schedule es una sola pantalla
    /// con tres pestañas».
    /// </summary>
    /// <remarks>
    /// Each tab is still its own page with its own loading, so nothing about how a schedule is
    /// fetched changes. A tap replaces the page on screen (<c>../</c>), so Back never walks through
    /// the tabs one by one.
    /// </remarks>
    public sealed class ScheduleTabs : Grid
    {
        public static readonly BindableProperty SelectedProperty =
            BindableProperty.Create(nameof(Selected), typeof(string), typeof(ScheduleTabs), "today", propertyChanged: (b, _, _) => ((ScheduleTabs)b).Render());

        public static readonly BindableProperty RunLoginProperty =
            BindableProperty.Create(nameof(RunLogin), typeof(string), typeof(ScheduleTabs), string.Empty);

        /// <summary>history, today or future.</summary>
        public string Selected { get => (string)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

        public string RunLogin { get => (string)GetValue(RunLoginProperty); set => SetValue(RunLoginProperty, value); }

        private static readonly (string Key, string Glyph, string Label, string Page)[] Tabs =
        [
            ("history", "", "History", nameof(HistoryPage)),
            ("today", "", "Today", nameof(TodaySchedulePage)),
            ("future", "", "Future", nameof(FutureSchedulePage)),
        ];

        private bool _busy;
        private readonly List<View> _icons = [];

        public ScheduleTabs()
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)];
            ColumnSpacing = 8;
            Padding = new Thickness(10, 10, 10, 4);
            Render();
        }

        private Border _track = null!;
        private Grid _thumb = null!;

        private void Render()
        {
            Children.Clear();
            var i = Array.FindIndex(Tabs, t => t.Key == Selected);

            _icons.Clear();
            // The thumb is a card on the tinted track, in the theme's colours, with a coral line
            // under the tab in use (design: «.stabs .th»).
            var line = new BoxView { HeightRequest = 3, WidthRequest = 22, CornerRadius = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 5) };
            line.SetDynamicResource(BoxView.ColorProperty, "NextColor");
            var card = new Border { StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Shadow = new Shadow { Brush = Colors.Black, Opacity = .12f, Radius = 6, Offset = new Point(0, 2) } };
            card.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            card.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");
            _thumb = new Grid { Margin = new Thickness(3), Children = { card, line } };
            var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], HeightRequest = 44 };
            grid.Add(_thumb, Math.Max(i, 0));
            for (var k = 0; k < Tabs.Length; k++)
            {
                var t = Tabs[k];
                var on = k == i;
                var icon = new Label { Text = t.Glyph, FontFamily = "FontAwesomeSolid", FontSize = 13, VerticalOptions = LayoutOptions.Center };
                icon.SetDynamicResource(Label.TextColorProperty, on ? "NextColor" : "SecondaryTextColor");
                var text = new Label { Text = t.Label, FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
                text.SetDynamicResource(Label.TextColorProperty, on ? "InkColor" : "SecondaryTextColor");
                var cell = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { icon, text } };
                _icons.Add(icon);
                var hit = new Grid { BackgroundColor = Colors.Transparent, Children = { cell } };
                SemanticProperties.SetDescription(hit, on ? $"{t.Label}, selected" : t.Label);
                var page = t.Page;
                var key = t.Key;
                var tap = new TapGestureRecognizer();
                var index = k;
                tap.Tapped += async (_, _) => await Go(key, page, index);
                hit.GestureRecognizers.Add(tap);
                grid.Add(hit, k);
            }
            _track = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = grid };
            _track.SetDynamicResource(BackgroundColorProperty, "TintColor");
            Children.Add(_track);

            // CHANGE ROUTE, as an icon: it goes back to the Run Login, as the button did.
            var route = new Label { Text = "", FontFamily = "FontAwesomeSolid", FontSize = 17, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
            route.SetDynamicResource(Label.TextColorProperty, "AccentColor");
            var routeBtn = new Border { WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = route };
            routeBtn.SetDynamicResource(BackgroundColorProperty, "TintColor");
            SemanticProperties.SetDescription(routeBtn, "Change route");
            var routeTap = new TapGestureRecognizer();
            routeTap.Tapped += async (_, _) => await Shell.Current.GoToAsync("//SchedulePage");
            routeBtn.GestureRecognizers.Add(routeTap);
            Grid.SetColumn(routeBtn, 1);
            Children.Add(routeBtn);
        }

        /// <summary>The thumb slides to the tapped tab with a little bounce and its icon bumps, as in
        /// the design; then the tab's page takes this one's place.</summary>
        private async Task Go(string key, string page, int index)
        {
            if (_busy || key == Selected || string.IsNullOrWhiteSpace(RunLogin)) return;
            _busy = true;
            try
            {
                var from = Array.FindIndex(Tabs, t => t.Key == Selected);
                var step = _track.Width / 3;
                _ = _thumb.TranslateTo((index - from) * step, 0, 380, Easing.SpringOut);
                var icon = _icons[index];
                await icon.ScaleTo(1.35, 140, Easing.CubicOut);
                await icon.ScaleTo(1, 180, Easing.SpringOut);
                await Task.Delay(80);
                await Shell.Current.GoToAsync($"../{page}", false, new Dictionary<string, object> { { "runLogin", RunLogin } });
            }
            catch (Exception)
            {
                // The page on screen stays; the tabs are still there to try again.
            }
            finally { _busy = false; }
        }
    }
}

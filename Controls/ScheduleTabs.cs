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

        public ScheduleTabs()
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)];
            ColumnSpacing = 8;
            Padding = new Thickness(10, 10, 10, 4);
            Render();
        }

        private void Render()
        {
            Children.Clear();

            // The app's one style of tabs; a tap here swaps the page instead of selecting.
            var tabs = new SegmentedTabs
            {
                AutoSelect = false,
                Items = string.Join(";", Tabs.Select(t => $"{t.Key}|{t.Glyph}|{t.Label}")),
                Selected = Selected
            };
            tabs.TabTapped += async (_, key) => await Go(key);
            Children.Add(tabs);

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

        /// <summary>The tab's page takes this one's place, after the slide of the tabs.</summary>
        private async Task Go(string key)
        {
            if (_busy || key == Selected || string.IsNullOrWhiteSpace(RunLogin)) return;
            _busy = true;
            try
            {
                var page = Tabs.First(t => t.Key == key).Page;
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

using Microsoft.Maui.Controls.Shapes;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// The app's one style of tabs (RE-064 design, «.stabs»): a tinted track, the tab in use on a
    /// card with a soft shadow and a short line under it, its icon in the Next colour. Used by the
    /// schedule (History · Today · Future), My documents (Mine · Vehicle) and Settings, so every
    /// set of tabs in the app reads the same.
    /// </summary>
    /// <remarks>
    /// <see cref="Items"/> is <c>key|glyph|Label;key|glyph|Label</c>. A tap slides the card with a
    /// little bounce, bumps the icon, then sets <see cref="Selected"/> (two-way), or, with
    /// <see cref="AutoSelect"/> off, only raises <see cref="TabTapped"/> (the schedule swaps pages).
    /// No red anywhere: red in this app means an error or something to fix.
    /// </remarks>
    public sealed class SegmentedTabs : ContentView
    {
        public static readonly BindableProperty ItemsProperty =
            BindableProperty.Create(nameof(Items), typeof(string), typeof(SegmentedTabs), string.Empty, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Render());

        public static readonly BindableProperty SelectedProperty =
            BindableProperty.Create(nameof(Selected), typeof(string), typeof(SegmentedTabs), string.Empty, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Render());

        public static readonly BindableProperty Badge0Property =
            BindableProperty.Create(nameof(Badge0), typeof(int), typeof(SegmentedTabs), 0, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Render());

        public static readonly BindableProperty Badge1Property =
            BindableProperty.Create(nameof(Badge1), typeof(int), typeof(SegmentedTabs), 0, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Render());

        public static readonly BindableProperty Badge2Property =
            BindableProperty.Create(nameof(Badge2), typeof(int), typeof(SegmentedTabs), 0, propertyChanged: (b, _, _) => ((SegmentedTabs)b).Render());

        public string Items { get => (string)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
        public string Selected { get => (string)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

        /// <summary>A count on the first, second or third tab: what needs the driver there. Hidden at 0.</summary>
        public int Badge0 { get => (int)GetValue(Badge0Property); set => SetValue(Badge0Property, value); }
        public int Badge1 { get => (int)GetValue(Badge1Property); set => SetValue(Badge1Property, value); }
        public int Badge2 { get => (int)GetValue(Badge2Property); set => SetValue(Badge2Property, value); }

        /// <summary>True: a tap selects the tab. False: it only raises <see cref="TabTapped"/>.</summary>
        public bool AutoSelect { get; set; } = true;

        /// <summary>A tab other than the selected one was tapped, after the slide.</summary>
        public event EventHandler<string>? TabTapped;

        private (string Key, string Glyph, string Label)[] _tabs = [];
        private readonly List<View> _icons = [];
        private Border _track = null!;
        private Grid _thumb = null!;
        private bool _busy;

        public SegmentedTabs()
        {
            Render();
        }

        private void Render()
        {
            _tabs = (Items ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Split('|'))
                .Where(p => p.Length == 3)
                .Select(p => (p[0].Trim(), p[1], p[2]))
                .ToArray();
            _icons.Clear();
            if (_tabs.Length == 0)
            {
                Content = null;
                return;
            }

            var i = Array.FindIndex(_tabs, t => t.Key == Selected);
            var line = new BoxView { HeightRequest = 3, WidthRequest = 22, CornerRadius = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 5) };
            line.SetDynamicResource(BoxView.ColorProperty, "NextColor");
            var card = new Border { StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Shadow = new Shadow { Brush = Colors.Black, Opacity = .12f, Radius = 6, Offset = new Point(0, 2) } };
            card.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            card.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");
            _thumb = new Grid { Margin = new Thickness(3), Children = { card, line }, IsVisible = i >= 0 };

            var grid = new Grid { HeightRequest = 44 };
            foreach (var _ in _tabs) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.Add(_thumb, Math.Max(i, 0));

            for (var k = 0; k < _tabs.Length; k++)
            {
                var t = _tabs[k];
                var on = k == i;
                var icon = new Label { Text = t.Glyph, FontFamily = "FontAwesomeSolid", FontSize = 13, VerticalOptions = LayoutOptions.Center };
                icon.SetDynamicResource(Label.TextColorProperty, on ? "NextColor" : "SecondaryTextColor");
                var text = new Label { Text = t.Label, FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
                text.SetDynamicResource(Label.TextColorProperty, on ? "InkColor" : "SecondaryTextColor");
                var cell = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { icon, text } };

                var count = k switch { 0 => Badge0, 1 => Badge1, 2 => Badge2, _ => 0 };
                if (count > 0)
                {
                    var number = new Label { Text = count.ToString(), FontSize = 10.5, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                    number.SetDynamicResource(Label.TextColorProperty, "WarnColor");
                    var badge = new Border { MinimumWidthRequest = 18, HeightRequest = 18, Padding = new Thickness(5, 0), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 9 }, VerticalOptions = LayoutOptions.Center, Content = number };
                    badge.SetDynamicResource(BackgroundColorProperty, "WarnSoftColor");
                    cell.Children.Add(badge);
                }

                _icons.Add(icon);
                var hit = new Grid { BackgroundColor = Colors.Transparent, Children = { cell } };
                SemanticProperties.SetDescription(hit, on ? $"{t.Label}, selected" : t.Label);
                var index = k;
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await TapAsync(index);
                hit.GestureRecognizers.Add(tap);
                grid.Add(hit, k);
            }

            _track = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = grid };
            _track.SetDynamicResource(BackgroundColorProperty, "TintColor");
            Content = _track;
        }

        private async Task TapAsync(int index)
        {
            var key = _tabs[index].Key;
            if (_busy || key == Selected) return;
            _busy = true;
            try
            {
                var from = Array.FindIndex(_tabs, t => t.Key == Selected);
                if (from >= 0)
                {
                    var step = _track.Width / _tabs.Length;
                    _ = _thumb.TranslateTo((index - from) * step, 0, 320, Easing.SpringOut);
                }
                var icon = _icons[index];
                await icon.ScaleTo(1.35, 120, Easing.CubicOut);
                await icon.ScaleTo(1, 160, Easing.SpringOut);
                if (AutoSelect) Selected = key;
                else TabTapped?.Invoke(this, key);
            }
            catch (Exception)
            {
                // The tabs stay as they were; a second tap tries again.
            }
            finally { _busy = false; }
        }
    }
}

using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.Services;

namespace Raphael.Driver.Controls.Menu
{
    /// <summary>
    /// Settings › MENU STYLE (RE-064): the four styles on offer as cards in a strip, like SCHEDULE
    /// STYLE. Each card shows a picture of the real menu in that style; the one in use has the
    /// accent border and a check. A tap uses the style at once and keeps it on this
    /// phone. Every other style stays in the code (<see cref="MenuStyleManager.All"/>).
    /// </summary>
    public sealed class MenuStylePicker : ScrollView
    {
        private static string Picture(MenuStyle style) => style switch
        {
            MenuStyle.Underline => "menu_preview_underline.png",
            MenuStyle.Metro => "menu_preview_metro.png",
            MenuStyle.Crescent => "menu_preview_crescent.png",
            _ => "menu_preview_angel.png",
        };

        public MenuStylePicker()
        {
            Orientation = ScrollOrientation.Horizontal;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
            Render();
            EventHandler changed = (_, _) => MainThread.BeginInvokeOnMainThread(Render);
            Loaded += (_, _) => MenuStyleManager.StyleChanged += changed;
            Unloaded += (_, _) => MenuStyleManager.StyleChanged -= changed;
        }

        private void Render()
        {
            var strip = new HorizontalStackLayout { Spacing = 10 };
            foreach (var info in MenuStyleManager.Listed) strip.Add(Card(info));
            Content = strip;
        }

        private static View Card(MenuStyleInfo info)
        {
            var on = info.Style == MenuStyleManager.Current;

            // A picture of the real menu, taken from the app (Resources/Images/menu_preview_*.png).
            var frame = new Border
            {
                WidthRequest = 114, HeightRequest = 63, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new Image { Source = Picture(info.Style), Aspect = Aspect.AspectFill },
            };
            frame.SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");

            var name = new Label { Text = info.Name, FontSize = 12.5, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
            name.SetDynamicResource(Label.TextColorProperty, "InkColor");
            var check = new Label { Text = "", FontFamily = "FontAwesomeSolid", FontSize = 12, VerticalOptions = LayoutOptions.Center, IsVisible = on };
            check.SetDynamicResource(Label.TextColorProperty, "AccentColor");
            var head = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            head.Add(name, 0);
            head.Add(check, 1);
            var about = new Label { Text = info.About, FontSize = 10.5, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
            about.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");

            var card = new Border
            {
                WidthRequest = 126, Padding = new Thickness(6, 6, 6, 8), StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = new VerticalStackLayout { Spacing = 5, Children = { frame, head, about } },
            };
            card.SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
            card.SetDynamicResource(Border.StrokeProperty, on ? "AccentColor" : "SeparatorColor");
            SemanticProperties.SetDescription(card, $"{info.Name} menu{(on ? ", selected" : string.Empty)}");
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => MenuStyleManager.Select(info.Style);
            card.GestureRecognizers.Add(tap);
            return card;
        }
    }
}

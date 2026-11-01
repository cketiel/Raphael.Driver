using Microsoft.Maui.Controls.Shapes;

namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>Small builders the schedule styles share, so each style reads as what it draws.</summary>
    internal static class Ui
    {
        public static Label Text(string text, double size, bool bold = false, Color? color = null, string? themeColor = "PrimaryTextColor")
        {
            var label = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, VerticalOptions = LayoutOptions.Center };
            Paint(label, color, themeColor);
            return label;
        }

        public static Label Icon(string glyph, double size, Color? color = null, string? themeColor = "SecondaryTextColor")
        {
            var label = new Label { Text = glyph, FontFamily = Glyph.Family, FontSize = size, VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center };
            Paint(label, color, themeColor);
            return label;
        }

        /// <summary>A fixed colour when there is one, otherwise the theme's, which repaints with the theme.</summary>
        public static void Paint(Label label, Color? color, string? themeColor)
        {
            if (color is not null)
                label.TextColor = color;
            else if (themeColor is not null)
                label.SetDynamicResource(Label.TextColorProperty, themeColor);
        }

        public static HorizontalStackLayout Row(double spacing, params View[] views)
        {
            var row = new HorizontalStackLayout { Spacing = spacing, VerticalOptions = LayoutOptions.Center };
            foreach (var v in views)
                row.Add(v);
            return row;
        }

        public static Border Box(View content, double radius, Thickness padding, Color? fill = null, Color? stroke = null, double strokeWidth = 1.5, bool dashed = false)
        {
            var border = new Border
            {
                Content = content,
                Padding = padding,
                StrokeShape = new RoundRectangle { CornerRadius = radius },
                StrokeThickness = stroke is null ? 0 : strokeWidth,
                Stroke = stroke ?? Colors.Transparent,
                BackgroundColor = fill ?? Colors.Transparent,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
            };
            if (dashed)
                border.StrokeDashArray = [4, 2.5];
            return border;
        }

        /// <summary>A circle of <paramref name="size"/> with a glyph in it, filled or outlined.</summary>
        public static Border Disc(string glyph, double size, Color color, bool filled, Color? glyphColor = null, double glyphSize = 0)
        {
            var icon = Icon(glyph, glyphSize > 0 ? glyphSize : size * 0.5, glyphColor ?? (filled ? Colors.White : color), null);
            icon.HorizontalOptions = icon.VerticalOptions = LayoutOptions.Center;
            return new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeShape = new Ellipse(),
                BackgroundColor = filled ? color : Colors.Transparent,
                Stroke = color,
                StrokeThickness = filled ? 0 : 2,
                Content = icon,
                VerticalOptions = LayoutOptions.Center,
                Padding = 0,
            };
        }

        /// <summary>Text on split-flap tiles, one tile per character, as on an airport board.</summary>
        public static HorizontalStackLayout Flaps(string text, Color tile, Color ink, double size = 12)
        {
            var row = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            foreach (var ch in text)
            {
                if (ch == ' ')
                {
                    row.Add(new BoxView { WidthRequest = 4, Color = Colors.Transparent });
                    continue;
                }
                // M and W are wider than the rest: on a standard tile their sides were cut off.
                var face = new Grid { WidthRequest = size * (ch is 'M' or 'W' ? 1.3 : 0.95), HeightRequest = size * 1.55 };
                face.Add(new Label { Text = ch.ToString(), FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = ink, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center });
                face.Add(new BoxView { HeightRequest = 1, Color = Colors.Black.WithAlpha(0.5f), VerticalOptions = LayoutOptions.Center });
                row.Add(new Border { Content = face, BackgroundColor = tile, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 2 }, Padding = 0 });
            }
            return row;
        }

        public static GraphicsView Drawing(IDrawable drawable, double width, double height) =>
            new() { Drawable = drawable, WidthRequest = width, HeightRequest = height, InputTransparent = true, BackgroundColor = Colors.Transparent };
    }
}

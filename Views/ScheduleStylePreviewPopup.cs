using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.Controls.ScheduleCards;
using Raphael.Driver.Models;
using Raphael.Driver.Services;

namespace Raphael.Driver.Views
{
    /// <summary>
    /// A style seen before it is chosen: its card and the start of its stop screen, with the
    /// sample event. "Use this style" hands the choice back to Settings; Close changes nothing.
    /// </summary>
    public sealed class ScheduleStylePreviewPopup : Popup
    {
        public ScheduleStylePreviewPopup(ScheduleStyleInfo info, Action use)
        {
            BackgroundColor = Colors.Transparent;
            Padding = 0;

            var title = new Label { Text = info.Name, FontSize = 19, FontAttributes = FontAttributes.Bold, Margin = new Thickness(14, 0, 14, 0) };
            title.SetDynamicResource(Label.TextColorProperty, "InkColor");
            var about = new Label { Text = info.About, FontSize = 13, Margin = new Thickness(14, 0, 14, 8) };
            about.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");

            var stack = new VerticalStackLayout { Spacing = 4, Children = { title, about, new EventCardView { StyleOverride = info.Style, IsStill = true, Event = ScheduleStyleSamples.Pickup } } };

            if (info.Style != ScheduleStyle.Legacy)
            {
                var look = new EventLook(ScheduleStyleSamples.Pickup);
                stack.Add(ActionRows.Build(look, info.Style,
                [
                    new EventAction { IsMain = true, Text = "Arrive", IconGlyph = "" },
                    new EventAction { Text = "Call Customer", IconGlyph = "" },
                ]));
            }

            var close = new Button { Text = "Close", Style = (Style)Application.Current!.Resources["SecondaryButtonStyle"] };
            var choose = new Button { Text = "Use this style", Style = (Style)Application.Current!.Resources["PrimaryActionButtonStyle"] };
            close.Clicked += async (_, _) => await CloseAsync();
            choose.Clicked += async (_, _) =>
            {
                use();
                await CloseAsync();
            };
            var buttons = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 10, Margin = new Thickness(12, 10, 12, 0) };
            buttons.Add(close, 0);
            buttons.Add(choose, 1);
            stack.Add(buttons);

            var sheet = new Border
            {
                Content = stack,
                Padding = new Thickness(0, 16, 0, 14),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                WidthRequest = 350,
            };
            sheet.SetDynamicResource(VisualElement.BackgroundColorProperty, "CardBackgroundColor");
            Content = sheet;
        }
    }
}

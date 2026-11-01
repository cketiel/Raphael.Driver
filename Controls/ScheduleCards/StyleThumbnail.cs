using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services;

namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>
    /// A real event card, shrunk, in one schedule style: the picture on Settings' style tiles.
    /// The card is laid out at a phone's width and scaled down, so it is the card the driver
    /// will get, not a drawing of it.
    /// </summary>
    public class StyleThumbnail : ContentView
    {
        public static readonly BindableProperty CardStyleProperty = BindableProperty.Create(
            nameof(CardStyle), typeof(ScheduleStyle), typeof(StyleThumbnail), ScheduleStyle.Legacy, propertyChanged: (b, _, _) => ((StyleThumbnail)b).Build());

        private const double CardWidth = 330, CardScale = 0.35;

        public ScheduleStyle CardStyle
        {
            get => (ScheduleStyle)GetValue(CardStyleProperty);
            set => SetValue(CardStyleProperty, value);
        }

        public StyleThumbnail()
        {
            WidthRequest = 112;
            HeightRequest = 72;
            InputTransparent = true;
            Build();
        }

        private void Build()
        {
            // No InputTransparent on the card itself: on Android it wraps the view in another layer,
            // and the scaled card was laid out but never drawn. The whole thumbnail ignores taps
            // instead (InputTransparent cascades), so the tile under it gets them.
            var card = new EventCardView { StyleOverride = CardStyle, IsStill = true, Event = ScheduleStyleSamples.Pickup, AnchorX = 0, AnchorY = 0, Scale = CardScale };
            var canvas = new AbsoluteLayout();
            AbsoluteLayout.SetLayoutBounds(card, new Rect(0, 0, CardWidth, AbsoluteLayout.AutoSize));
            canvas.Add(card);
            Content = new Border { Content = canvas, Padding = 0, StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }, BackgroundColor = Colors.Transparent };
        }
    }

    /// <summary>
    /// Made-up events for the style previews. Nobody's data: the passenger is "Sample Passenger"
    /// and the address is a placeholder.
    /// </summary>
    public static class ScheduleStyleSamples
    {
        public static ScheduleDto Pickup { get; } = new()
        {
            Name = "Pickup",
            EventType = ScheduleEventType.Pickup,
            TripType = "Appointment",
            ETA = new TimeSpan(7, 5, 0),
            Pickup = new TimeSpan(7, 20, 0),
            Appt = new TimeSpan(8, 0, 0),
            Wait = TimeSpan.FromMinutes(25),
            Patient = "Sample Passenger",
            Address = "100 Main St, Miami, FL",
            SpaceType = "AMB",
            FundingSource = "Medicaid",
        };
    }
}

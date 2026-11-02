namespace Raphael.Driver.Services
{
    /// <summary>
    /// How an event looks in Today's Schedule and on its stop screen. The numbers are the ones
    /// the designs carry (_meta/design/driver-cards/round-1 to round-3), so a driver, the office and
    /// the design pages all call a style by the same number.
    /// </summary>
    public enum ScheduleStyle
    {
        Token = 1,
        FoldedCorner = 2,
        Tint = 3,
        Ticket = 4,
        RoadSigns = 5,
        TransitLine = 6,
        Outline = 7,
        MapPin = 8,
        SplitPill = 9,
        FlapBoard = 10,

        /// <summary>The look the app had before styles existed: a coloured band on top and a stripe on the left.</summary>
        Legacy = 11,
    }

    /// <summary>A style as Settings lists it.</summary>
    public sealed record ScheduleStyleInfo(ScheduleStyle Style, string Name, string About)
    {
        public int Number => (int)Style;
        public string Caption => $"Style {Number}";
    }

    /// <summary>
    /// Which schedule style the app draws with, kept on the phone like the theme. Until a driver
    /// picks one the app draws Map Pin (decision of the user, 2026-11-01).
    /// </summary>
    public static class ScheduleStyleManager
    {
        private const string PreferenceKey = "ScheduleStyle";

        /// <summary>What a phone draws until its driver picks a style.</summary>
        public const ScheduleStyle DefaultStyle = ScheduleStyle.MapPin;

        public static IReadOnlyList<ScheduleStyleInfo> All { get; } =
        [
            new(ScheduleStyle.Token, "Token", "A colour token on each card says the stop: filled for Appt, outlined for Return."),
            new(ScheduleStyle.FoldedCorner, "Folded Corner", "A folded corner carries the stop: plain for Appt, striped for Return."),
            new(ScheduleStyle.Tint, "Tint", "Each card wears its colour, and the promised time is its only solid pill."),
            new(ScheduleStyle.Ticket, "Ticket", "Each stop is a ticket whose stub shows the trip, home to clinic or back."),
            new(ScheduleStyle.RoadSigns, "Road Signs", "A road sign on each card: diamond for Appt, circle for Return, square for the garage."),
            new(ScheduleStyle.TransitLine, "Transit Line", "A transit map of each trip, with this stop's station lit."),
            new(ScheduleStyle.Outline, "Outline", "The border is the code: solid for Appt, dashed for Return, double for the garage."),
            new(ScheduleStyle.MapPin, "Map Pin", "The same pin the map draws: filled for Appt, hollow for Return."),
            new(ScheduleStyle.SplitPill, "Split Pill", "Two halves: pickup or drop-off, and Appt or Return."),
            new(ScheduleStyle.FlapBoard, "Flap Board", "An airport board: the stop and its time on flip tiles."),
            new(ScheduleStyle.Legacy, "Legacy", "The original look: a coloured band on top and a stripe on the left."),
        ];

        private static ScheduleStyle? _current;

        /// <summary>The style on screen. Read from the phone the first time it is asked for.</summary>
        public static ScheduleStyle Current => _current ??= Read();

        public static ScheduleStyleInfo CurrentInfo => Info(Current);

        public static ScheduleStyleInfo Info(ScheduleStyle style) => All.First(s => s.Style == style);

        /// <summary>Raised after the driver picks another style, so open screens redraw.</summary>
        public static event EventHandler? StyleChanged;

        /// <summary>Uses <paramref name="style"/> from now on and remembers it on this phone.</summary>
        public static void Select(ScheduleStyle style)
        {
            if (style == Current)
                return;

            _current = style;
            Preferences.Set(PreferenceKey, (int)style);
            StyleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static ScheduleStyle Read()
        {
            var stored = Preferences.Get(PreferenceKey, (int)DefaultStyle);
            return Enum.IsDefined(typeof(ScheduleStyle), stored) ? (ScheduleStyle)stored : DefaultStyle;
        }
    }
}

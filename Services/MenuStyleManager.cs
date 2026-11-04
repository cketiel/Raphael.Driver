namespace Raphael.Driver.Services
{
    /// <summary>
    /// How the menu at the bottom of the main pages looks (RE-064). Every style opens the same
    /// pages in the same way; only the drawing and the animation change. The numbers are stored
    /// on the phone, so they never change meaning: a new style takes a new number.
    /// Design: <c>_meta/design/driver-navigation</c> and <c>_meta/design/identity</c>.
    /// </summary>
    public enum MenuStyle
    {
        // General: the identity of Raphael, the angel.
        AngelWings = 0,   // K with the wings of round 5
        AngelDove = 1,    // K with the dove wings
        Crescent = 2,     // N, the four on the thumb's arc, right or left hand
        Pulse = 3,        // L, an ECG line beats over the selected item
        Radar = 4,        // M, the GPS dot, and a radar finds the rest

        // Thematic: the world of the driver.
        SteeringWheel = 10, // G
        PinAndVan = 11,     // H
        Tire = 12,          // O
        TrafficLights = 13, // Q
        Speedometer = 14,   // R

        // Neutral.
        Underline = 20,     // C
        Metro = 21,         // S (the default)
        HealthCross = 22,   // T
        Island = 23,        // F
        Jump = 24,          // J

        // Season.
        Christmas = 30,     // U
    }

    /// <summary>The groups the styles are listed under in Settings.</summary>
    public enum MenuStyleGroup { General, Thematic, Neutral, Christmas }

    /// <param name="Ready">False for a style that is listed but not built yet: it shows as
    /// "Coming soon" and cannot be picked.</param>
    /// <param name="Listed">Offered in Settings. Every style stays in the code; only these three are
    /// shown to choose from: Metro, Underline and Crescent.</param>
    public sealed record MenuStyleInfo(MenuStyle Style, string Name, MenuStyleGroup Group, bool Ready, string About, bool Listed = false);

    /// <summary>The menu style on this phone, chosen in Settings. Same pattern as
    /// <see cref="ScheduleStyleManager"/>.</summary>
    public static class MenuStyleManager
    {
        private const string PreferenceKey = "MenuStyle";

        /// <summary>What a phone shows until its driver picks a style.</summary>
        public const MenuStyle DefaultStyle = MenuStyle.Metro;

        public static IReadOnlyList<MenuStyleInfo> All { get; } =
        [
            new(MenuStyle.AngelWings, "Angel", MenuStyleGroup.General, true, "The logo in the middle; its wings open the menu."),
            new(MenuStyle.AngelDove, "Angel Dove", MenuStyleGroup.General, true, "The same angel, with the soft wings of a dove."),
            new(MenuStyle.Crescent, "Crescent", MenuStyleGroup.General, true, "The four on the arc of your thumb, for either hand.", Listed: true),
            new(MenuStyle.Pulse, "Pulse", MenuStyleGroup.General, true, "A heartbeat line marks where you are."),
            new(MenuStyle.Radar, "Radar", MenuStyleGroup.General, true, "You are here; the radar finds the rest."),

            new(MenuStyle.SteeringWheel, "Steering Wheel", MenuStyleGroup.Thematic, true, "Turn the wheel to choose."),
            new(MenuStyle.PinAndVan, "Pin & Van", MenuStyleGroup.Thematic, true, "The van drives to the pin you pick."),
            new(MenuStyle.Tire, "Tire", MenuStyleGroup.Thematic, true, "A small tire rolls to your choice."),
            new(MenuStyle.TrafficLights, "Traffic Lights", MenuStyleGroup.Thematic, true, "Each option is a road sign."),
            new(MenuStyle.Speedometer, "Speedometer", MenuStyleGroup.Thematic, true, "The needle points at the page."),

            new(MenuStyle.Underline, "Underline", MenuStyleGroup.Neutral, true, "The logo by your thumb and a chip that jumps.", Listed: true),
            new(MenuStyle.Metro, "Metro", MenuStyleGroup.Neutral, true, "A train rides from station to station.", Listed: true),
            new(MenuStyle.HealthCross, "Health Cross", MenuStyleGroup.Neutral, true, "A plus button opens a column."),
            new(MenuStyle.Island, "Island", MenuStyleGroup.Neutral, true, "A floating island at the bottom."),
            new(MenuStyle.Jump, "Jump", MenuStyleGroup.Neutral, true, "The icon jumps to its place."),

            new(MenuStyle.Christmas, "Christmas", MenuStyleGroup.Christmas, true, "Snow, lights and the star of Bethlehem."),
        ];

        private static MenuStyle? _current;

        /// <summary>The style on screen. Read from the phone the first time it is asked for.</summary>
        public static MenuStyle Current => _current ??= Read();

        public static MenuStyleInfo Info(MenuStyle style) => All.First(s => s.Style == style);

        /// <summary>The styles Settings offers, in this order.</summary>
        public static IReadOnlyList<MenuStyleInfo> Listed { get; } =
            [Info(MenuStyle.Metro), Info(MenuStyle.Underline), Info(MenuStyle.Crescent)];

        /// <summary>Raised after the driver picks another style, so the menus on screen rebuild.</summary>
        public static event EventHandler? StyleChanged;

        /// <summary>Uses <paramref name="style"/> from now on and remembers it on this phone.</summary>
        public static void Select(MenuStyle style)
        {
            if (style == Current || !Info(style).Listed)
                return;

            _current = style;
            Preferences.Set(PreferenceKey, (int)style);
            StyleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static MenuStyle Read()
        {
            var stored = Preferences.Get(PreferenceKey, (int)DefaultStyle);
            // A stored style that is unknown, or not built in this version, falls back to the default.
            // One no longer offered (a phone that had another style before) goes back to the default.
            return Enum.IsDefined(typeof(MenuStyle), stored) && Info((MenuStyle)stored).Listed
                ? (MenuStyle)stored
                : DefaultStyle;
        }
    }
}

using System.Globalization;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services;

namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>The six things an event on a route can be.</summary>
    public enum EventKind { PickupAppt, DropoffAppt, PickupReturn, DropoffReturn, PullOut, PullIn }

    /// <summary>FontAwesome 6 Solid glyphs the schedule styles draw with.</summary>
    public static class Glyph
    {
        public const string Family = "FontAwesomeSolid";
        public const string Up = "", Down = "", Right = "", Left = "", Chevron = "";
        public const string Home = "", Clinic = "", Garage = "", Van = "", Car = "";
        public const string Flag = "", Calendar = "", Target = "", Clock = "", Hourglass = "";
        public const string User = "", Pin = "";
        public const string Walking = "", Wheelchair = "", Stretcher = "";
    }

    /// <summary>
    /// What an event says, worked out once from its <see cref="ScheduleDto"/>: which of the six
    /// kinds it is, its colour, its words and times. Every schedule style draws from this, so the
    /// eleven of them can differ in looks and never in meaning.
    /// </summary>
    public sealed class EventLook
    {
        public EventLook(ScheduleDto e)
        {
            Event = e;
            Kind = e.Name switch
            {
                "Pull-out" => EventKind.PullOut,
                "Pull-in" => EventKind.PullIn,
                _ => (e.EventType, e.TripType) switch
                {
                    (ScheduleEventType.Dropoff, "Return") => EventKind.DropoffReturn,
                    (ScheduleEventType.Pickup, "Return") => EventKind.PickupReturn,
                    (ScheduleEventType.Dropoff, _) => EventKind.DropoffAppt,
                    _ => EventKind.PickupAppt,
                }
            };
        }

        public ScheduleDto Event { get; }
        public EventKind Kind { get; }

        public bool IsPull => Kind is EventKind.PullOut or EventKind.PullIn;
        public bool IsPickup => Kind is EventKind.PickupAppt or EventKind.PickupReturn;
        public bool IsReturn => Kind is EventKind.PickupReturn or EventKind.DropoffReturn;

        /// <summary>
        /// The convention colour (ScheduleColorConverter): green, red, blue, purple, black. The
        /// Desktop's map pins follow the same convention, so it never changes with the theme.
        /// </summary>
        public Color Color => Kind switch
        {
            EventKind.PickupAppt => Color.FromArgb("#2E7D32"),
            EventKind.DropoffAppt => Color.FromArgb("#C62828"),
            EventKind.PickupReturn => Color.FromArgb("#1565C0"),
            EventKind.DropoffReturn => Color.FromArgb("#6A1B9A"),
            _ => Color.FromArgb("#1C1C1C"),
        };

        /// <summary>
        /// The colour for text and lines drawn on the card. On a dark card the convention colour
        /// is lifted towards white so it stays readable; the hue, and so the meaning, stays.
        /// </summary>
        public Color Ink => CardIsDark ? Mix(Color, Colors.White, IsPull ? 0.85f : 0.4f) : Color;

        public static bool CardIsDark => Luminance(ThemeManager.GetColor("CardBackgroundColor", Colors.White)) < 0.45f;

        public string KindText => Kind switch
        {
            EventKind.PickupAppt => "Pickup Appt",
            EventKind.DropoffAppt => "Dropoff Appt",
            EventKind.PickupReturn => "Pickup Return",
            EventKind.DropoffReturn => "Dropoff Return",
            EventKind.PullOut => "Pull Out",
            _ => "Pull In",
        };

        public string EventWord => IsPull ? (Kind == EventKind.PullOut ? "PULL OUT" : "PULL IN") : IsPickup ? "PICKUP" : "DROPOFF";
        public string TripWord => IsReturn ? "RETURN" : "APPT";
        public string ShortCode => IsPull ? EventWord : $"{(IsPickup ? "PU" : "DO")} · {TripWord}";
        public string BoardCode => IsPull ? (Kind == EventKind.PullOut ? "PULL-OUT" : "PULL-IN") : $"{(IsPickup ? "PU" : "DO")}-{(IsReturn ? "RET" : "APPT")}";

        /// <summary>What the promised time is: the pickup time on a pickup, the appointment on a dropoff.</summary>
        public string PromiseLabel => IsPickup ? "PICKUP" : "APPT";

        public string ArrowGlyph => IsPull ? (Kind == EventKind.PullOut ? Glyph.Right : Glyph.Left) : IsPickup ? Glyph.Up : Glyph.Down;

        /// <summary>Where the trip goes: an Appt trip goes to the clinic, a Return trip goes home.</summary>
        public string DestinationGlyph => IsPull ? Glyph.Van : IsReturn ? Glyph.Home : Glyph.Clinic;
        public string OriginGlyph => IsPull ? Glyph.Garage : IsReturn ? Glyph.Clinic : Glyph.Home;

        public string EtaText => Clock(Event.ETA);

        /// <summary>The time the driver was promised, with AM/PM: the old card left it off.</summary>
        public string PromisedText => Clock(IsPickup ? Event.Pickup : Event.Appt);

        public string LegacyTimeText => (IsPickup ? Event.Pickup : Event.Appt)?.ToString(@"hh\:mm") ?? string.Empty;
        public string LegacyTimeLabel => IsPickup ? "SCH TIME" : "APT TIME";

        public string LineThreeText => IsPull ? (Kind == EventKind.PullOut ? "Leave the garage" : "Back to the garage") : Event.Patient ?? string.Empty;
        public string LineThreeGlyph => IsPull ? Glyph.Van : Glyph.User;

        public string SpaceCode => Event.SpaceType ?? string.Empty;
        public string SpaceGlyph => SpaceCode switch { "WCH" => Glyph.Wheelchair, "STR" => Glyph.Stretcher, _ => Glyph.Walking };
        public string SpaceName => SpaceCode switch { "WCH" => "Wheelchair", "STR" => "Stretcher", "AMB" => "Ambulatory", _ => SpaceCode };

        /// <summary>Whole minutes the driver is planned to wait at this pickup, or null when there is no wait.</summary>
        public int? WaitMinutes => IsPickup && Event.Wait is { TotalMinutes: >= 1 } wait ? (int)Math.Round(wait.TotalMinutes) : null;

        /// <summary>
        /// How early a pickup may be planned (RouteEtaChain.EarlyArrivalMarginFor on the server):
        /// five minutes on a return, a quarter of an hour on anything else.
        /// </summary>
        public int MarginMinutes => IsReturn ? 5 : 15;

        /// <summary>The stamp's explanation, in the words the design was approved with.</summary>
        public string WaitExplanation
        {
            get
            {
                if (WaitMinutes is not { } wait || Event.ETA is not { } eta)
                    return string.Empty;

                var could = Clock(eta - TimeSpan.FromMinutes(wait));
                return $"You can be here by {could}, but a pickup is never planned earlier than {MarginMinutes} min " +
                       $"before its time, so your ETA is {EtaText}. Expect to wait about {wait} min.";
            }
        }

        public static string Clock(TimeSpan? time) =>
            time is { } t ? DateTime.MinValue.Add(t).ToString("hh:mm tt", CultureInfo.InvariantCulture) : string.Empty;

        public static Color Mix(Color a, Color b, float share) =>
            new(a.Red + (b.Red - a.Red) * share, a.Green + (b.Green - a.Green) * share, a.Blue + (b.Blue - a.Blue) * share, a.Alpha);

        private static float Luminance(Color c) => 0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue;
    }
}

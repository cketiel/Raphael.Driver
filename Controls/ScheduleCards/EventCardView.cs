using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using static Raphael.Driver.Controls.ScheduleCards.Ui;

namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>
    /// One event of the route, drawn in the driver's schedule style (Settings → SCHEDULE STYLE).
    /// Legacy is the old card itself (<see cref="LegacyEventCard"/>); the other ten are built here
    /// from <see cref="EventLook"/>, after _meta/design/driver-cards/round-1 to round-3.
    /// <para>
    /// Every style keeps the old card's size: a header and four lines. A pickup with a planned wait
    /// gets a stamp over the empty right of the time and passenger lines. A tap on the stamp
    /// explains it and goes nowhere; a tap anywhere else on the card is the host's, as before.
    /// </para>
    /// </summary>
    public class EventCardView : ContentView
    {
        public static readonly BindableProperty EventProperty = BindableProperty.Create(
            nameof(Event), typeof(ScheduleDto), typeof(EventCardView), propertyChanged: (b, _, _) => ((EventCardView)b).Build());

        public static readonly BindableProperty StyleOverrideProperty = BindableProperty.Create(
            nameof(StyleOverride), typeof(ScheduleStyle?), typeof(EventCardView), propertyChanged: (b, _, _) => ((EventCardView)b).Build());

        public ScheduleDto? Event
        {
            get => (ScheduleDto?)GetValue(EventProperty);
            set => SetValue(EventProperty, value);
        }

        /// <summary>Draws in this style instead of the driver's (Settings' previews).</summary>
        public ScheduleStyle? StyleOverride
        {
            get => (ScheduleStyle?)GetValue(StyleOverrideProperty);
            set => SetValue(StyleOverrideProperty, value);
        }

        /// <summary>A picture only: the stamp cannot be tapped (Settings' thumbnails).</summary>
        public bool IsStill { get; set; }

        /// <summary>
        /// At the top of the stop screen: the explanation of the wait is written under the card
        /// instead of behind the stamp, and Legacy, which never had a card there, shows nothing.
        /// </summary>
        public bool IsHero { get; set; }

        public void Build()
        {
            if (Event is not { } e)
            {
                Content = null;
                return;
            }

            var style = StyleOverride ?? ScheduleStyleManager.Current;
            if (style == ScheduleStyle.Legacy)
            {
                IsVisible = !IsHero;
                Content = IsHero ? null : new LegacyEventCard { BindingContext = e };
                return;
            }

            IsVisible = true;
            var look = new EventLook(e);
            var card = CardFactory.Build(look, style, tappableStamp: !IsStill && !IsHero);

            if (IsHero && look.WaitMinutes is not null)
            {
                var stack = new VerticalStackLayout { Spacing = 0 };
                stack.Add(card);
                stack.Add(CardFactory.WaitNote(look));
                Content = stack;
            }
            else
            {
                Content = card;
            }
        }
    }

    /// <summary>The ten new styles, part by part.</summary>
    internal static class CardFactory
    {
        private static Color CardBg => ThemeManager.GetColor("CardBackgroundColor", Colors.White);
        private static Color PageBg => ThemeManager.GetColor("PageBackgroundColor", Colors.White);
        private static Color Separator => ThemeManager.GetColor("SeparatorColor", Colors.LightGray);

        /// <summary>The ink of the stamps: amber, kept apart from every colour of the convention.</summary>
        public static Color StampInk => EventLook.CardIsDark ? Color.FromArgb("#F0B44C") : Color.FromArgb("#B26A00");

        private const string PinPathData = "M13 31c-1 0-1.6-.6-2.2-1.5C5.6 21.5 1.5 18 1.5 12a11.5 11.5 0 0 1 23 0c0 6-4.1 9.5-9.3 17.5-.6.9-1.2 1.5-2.2 1.5z";

        public static View Build(EventLook look, ScheduleStyle style, bool tappableStamp)
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Add(Header(look, style));
            if (style == ScheduleStyle.Ticket)
                stack.Add(Perforation());
            stack.Add(Body(look, style, tappableStamp));

            var layer = new Grid();
            if (style == ScheduleStyle.Tint && look.IsReturn)
                layer.Add(Drawing(new StripesDrawable(EventLook.Mix(CardBg, look.Color, 0.1f), EventLook.Mix(CardBg, look.Color, 0.04f), 10, 10), -1, -1));
            layer.Add(stack);
            foreach (var decor in Decorations(look, style))
                layer.Add(decor);

            var card = new Border
            {
                Content = layer,
                Padding = 0,
                Margin = new Thickness(10, 0, 10, 10),
                StrokeShape = new RoundRectangle { CornerRadius = style == ScheduleStyle.FoldedCorner ? 10 : 12 },
                StrokeThickness = 1,
            };
            card.SetDynamicResource(VisualElement.BackgroundColorProperty, "CardBackgroundColor");
            card.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");

            switch (style)
            {
                case ScheduleStyle.Tint:
                    card.BackgroundColor = EventLook.Mix(CardBg, look.Color, 0.08f);
                    card.Stroke = look.Ink.WithAlpha(0.45f);
                    card.StrokeThickness = 1.5;
                    break;

                case ScheduleStyle.Outline when look.IsPull:
                    // Double, for the garage: two thin lines with a gap, the outer one carrying the margin.
                    card.Stroke = look.Ink;
                    card.StrokeThickness = 1.5;
                    card.Margin = new Thickness(0);
                    return new Border
                    {
                        Content = card,
                        Padding = 2.5,
                        Margin = new Thickness(10, 0, 10, 10),
                        Stroke = look.Ink,
                        StrokeThickness = 1.5,
                        StrokeShape = new RoundRectangle { CornerRadius = 14 },
                        BackgroundColor = Colors.Transparent,
                    };

                case ScheduleStyle.Outline:
                    card.Stroke = look.Ink;
                    card.StrokeThickness = 2.5;
                    if (look.IsReturn)
                        card.StrokeDashArray = [3, 1.6];
                    break;
            }

            return card;
        }

        // ---------------------------------------------------------------- header

        private static View Header(EventLook look, ScheduleStyle style)
        {
            var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8, Padding = new Thickness(12, 8, 12, 0), MinimumHeightRequest = 26 };
            View left, right = Meta(look);

            switch (style)
            {
                case ScheduleStyle.Token:
                {
                    var filled = !look.IsReturn;
                    var disc = Disc(look.ArrowGlyph, 20, filled ? Colors.White : look.Ink, filled: true, glyphColor: filled ? look.Color : CardBg, glyphSize: 10);
                    left = Box(Row(5, disc, Text(look.ShortCode, 11.5, true, filled ? Colors.White : look.Ink)), 999, new Thickness(3, 2, 9, 2),
                               fill: filled ? look.Color : null, stroke: filled ? null : look.Ink, strokeWidth: 2);
                    break;
                }
                case ScheduleStyle.FoldedCorner:
                    grid.Padding = new Thickness(12, 8, 64, 0);
                    left = look.IsPull ? Text($"Perform {look.KindText}", 12.5, false, null, "SecondaryTextColor") : Space(look, withFunding: true, dot: true);
                    right = new BoxView { Color = Colors.Transparent };
                    break;

                case ScheduleStyle.Tint:
                    left = Row(7, Disc(look.ArrowGlyph, 22, look.Ink, filled: !look.IsReturn, glyphColor: look.IsReturn ? look.Ink : Colors.White, glyphSize: 11),
                               Text(look.ShortCode, 12, true, look.Ink));
                    break;

                case ScheduleStyle.Ticket:
                    left = look.IsPull
                        ? Row(6, Icon(Glyph.Garage, 13, look.Ink), Icon(look.ArrowGlyph, 12, look.Ink), Text(look.Kind == EventKind.PullOut ? "GARAGE → ROUTE" : "ROUTE → GARAGE", 12, true, look.Ink))
                        : Row(5, Icon(look.OriginGlyph, 13, look.Ink), Text(look.IsPickup ? "●" : "○", 11, true, look.Ink), Icon(look.ArrowGlyph, 12, look.Ink),
                              Text(look.IsPickup ? "○" : "●", 11, true, look.Ink), Icon(look.DestinationGlyph, 13, look.Ink), Text(look.IsPickup ? "BOARD" : "ALIGHT", 12, true, look.Ink));
                    break;

                case ScheduleStyle.RoadSigns:
                    grid.Padding = new Thickness(50, 8, 12, 0);
                    left = look.IsPull ? Text($"Perform {look.KindText}", 12.5, false, null, "SecondaryTextColor") : Space(look, withFunding: false);
                    right = look.IsPull ? Text("Garage", 12.5, false, null, "SecondaryTextColor") : Text(look.Event.FundingSource ?? "", 12.5, false, null, "SecondaryTextColor");
                    break;

                case ScheduleStyle.TransitLine:
                    left = Row(0, Icon(look.OriginGlyph, 13, look.Ink), Station(look, here: look.IsPickup || look.Kind == EventKind.PullOut), Track(look),
                               Station(look, here: !look.IsPickup && look.Kind != EventKind.PullOut), Icon(look.DestinationGlyph, 13, look.Ink));
                    break;

                case ScheduleStyle.Outline:
                    left = Row(5, Icon(look.ArrowGlyph, 13, look.Ink), Text(look.IsPull ? look.EventWord : $"{look.EventWord} · {look.TripWord}", 12, true, look.Ink));
                    break;

                case ScheduleStyle.MapPin:
                    left = look.IsPull ? Text($"Perform {look.KindText}", 12.5, false, null, "SecondaryTextColor") : Space(look, withFunding: false);
                    right = Text(look.IsPull ? "Garage" : look.Event.FundingSource ?? "", 12.5, false, null, "SecondaryTextColor");
                    break;

                case ScheduleStyle.SplitPill:
                    left = SplitPill(look);
                    right = look.IsPull ? new BoxView { Color = Colors.Transparent } : Space(look, withFunding: false);
                    break;

                default: // FlapBoard
                    left = Flaps(look.BoardCode, look.Color, Colors.White, 11);
                    break;
            }

            if (right is Label rl)
            {
                rl.HorizontalOptions = LayoutOptions.End;
                rl.LineBreakMode = LineBreakMode.TailTruncation;
                rl.MaxLines = 1;
            }
            right.HorizontalOptions = LayoutOptions.End;
            grid.Add(left, 0);
            grid.Add(right, 1);
            return grid;
        }

        /// <summary>The space type with its icon, and the funding source, in the header's quiet grey.</summary>
        private static View Meta(EventLook look) =>
            look.IsPull ? Row(4, Icon(Glyph.Garage, 12), Text("Garage", 12.5, false, null, "SecondaryTextColor")) : Space(look, withFunding: true);

        private static View Space(EventLook look, bool withFunding, bool dot = false)
        {
            var text = withFunding ? $"{look.SpaceCode}{(dot ? "  •  " : " · ")}{look.Event.FundingSource}" : look.SpaceCode;
            var label = Text(text, 12.5, false, null, "SecondaryTextColor");
            label.LineBreakMode = LineBreakMode.TailTruncation;
            label.MaxLines = 1;
            return Row(4, Icon(look.SpaceGlyph, 13), label);
        }

        private static View Station(EventLook look, bool here) => new Border
        {
            WidthRequest = here ? 18 : 12,
            HeightRequest = here ? 18 : 12,
            StrokeShape = new Ellipse(),
            StrokeThickness = 3,
            Stroke = here ? CardBg : look.Ink,
            BackgroundColor = here ? look.Color : CardBg,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(3, 0),
        };

        private static View Track(EventLook look) => look.IsReturn
            ? new Line { X1 = 0, Y1 = 2, X2 = 44, Y2 = 2, Stroke = look.Ink, StrokeThickness = 4, StrokeDashArray = [1.5, 0.8], WidthRequest = 44, HeightRequest = 4, VerticalOptions = LayoutOptions.Center }
            : new BoxView { WidthRequest = 44, HeightRequest = 4, CornerRadius = 2, Color = look.Ink, VerticalOptions = LayoutOptions.Center };

        private static View SplitPill(EventLook look)
        {
            var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Auto) } };
            var leftHalf = new Border { BackgroundColor = look.Color, StrokeThickness = 0, Padding = new Thickness(8, 2), Content = Row(4, Icon(look.IsPull ? Glyph.Van : look.ArrowGlyph, 10, Colors.White), Text(look.EventWord, 11, true, Colors.White)) };
            var rightHalf = new ContentView { Padding = new Thickness(8, 2), Content = Row(4, Icon(look.IsPull ? Glyph.Garage : look.DestinationGlyph, 10, look.Ink), Text(look.IsPull ? "GARAGE" : look.TripWord, 11, true, look.Ink)) };
            grid.Add(leftHalf, 0);
            grid.Add(rightHalf, 1);
            return new Border { Content = grid, Padding = 0, Stroke = look.Ink, StrokeThickness = 1.5, StrokeShape = new RoundRectangle { CornerRadius = 999 }, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start };
        }

        /// <summary>The ticket's tear line, with a notch cut into each side of the card.</summary>
        private static View Perforation()
        {
            var grid = new Grid { HeightRequest = 2, Margin = new Thickness(0, 6, 0, 0) };
            grid.Add(new Line { X1 = 0, Y1 = 1, X2 = 600, Y2 = 1, StrokeThickness = 2, StrokeDashArray = [3, 2], Stroke = Separator });
            foreach (var start in new[] { true, false })
            {
                grid.Add(new Ellipse
                {
                    WidthRequest = 14,
                    HeightRequest = 14,
                    Fill = PageBg,
                    Stroke = Separator,
                    StrokeThickness = 1,
                    HorizontalOptions = start ? LayoutOptions.Start : LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = start ? new Thickness(-8, -6, 0, -6) : new Thickness(0, -6, -8, -6),
                });
            }
            return grid;
        }

        private static IEnumerable<View> Decorations(EventLook look, ScheduleStyle style)
        {
            if (style == ScheduleStyle.FoldedCorner)
            {
                yield return new Grid
                {
                    WidthRequest = 54,
                    HeightRequest = 54,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    InputTransparent = true,
                    Children =
                    {
                        Drawing(new CornerDrawable(look.Color, look.IsReturn), 54, 54),
                        new Label { Text = look.ArrowGlyph, FontFamily = Glyph.Family, FontSize = 15, TextColor = Colors.White, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 6, 7, 0) },
                    }
                };
            }
            else if (style == ScheduleStyle.RoadSigns)
            {
                yield return Sign(look, 34);
            }
        }

        /// <summary>The road sign: diamond for Appt, circle for Return, square for the garage.</summary>
        public static View Sign(EventLook look, double size)
        {
            var arrow = new Label { Text = look.ArrowGlyph, FontFamily = Glyph.Family, FontSize = size * 0.36, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            return new Grid
            {
                WidthRequest = size,
                HeightRequest = size,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(8, 5, 0, 0),
                InputTransparent = true,
                Children = { Drawing(new SignDrawable(look.Color, look.Kind), size, size), arrow }
            };
        }

        // ---------------------------------------------------------------- body

        private static View Body(EventLook look, ScheduleStyle style, bool tappableStamp)
        {
            var body = new Grid
            {
                Padding = new Thickness(12, 8, 12, 10),
                RowSpacing = 7,
                RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            };

            var first = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8, MinimumHeightRequest = 30 };
            first.Add(style == ScheduleStyle.MapPin ? Row(6, MapPin(look, 26), Eta(look, style)) : Eta(look, style), 0);
            first.Add(Kind(look, style), 1);
            body.Add(first, 0, 0);

            var timeGlyph = !look.IsPull && style is ScheduleStyle.FoldedCorner or ScheduleStyle.MapPin ? Glyph.Calendar : Glyph.Clock;
            var timeIconColor = timeGlyph == Glyph.Calendar ? look.Ink : null;
            var time = Line(Icon(timeGlyph, 13, timeIconColor), look.IsPull ? Text(look.PromisedText, 14) : Promise(look, style));
            var who = Line(Icon(look.LineThreeGlyph, 13), Truncated(Text(look.LineThreeText, 14)));
            var where = Line(Icon(Glyph.Pin, 13), Text(look.Event.Address ?? "", 14));
            ((Label)((Grid)where).Children[1]).LineBreakMode = LineBreakMode.WordWrap;

            body.Add(time, 0, 1);
            body.Add(who, 0, 2);
            body.Add(where, 0, 3);

            if (look.WaitMinutes is not null)
            {
                // The stamp sits over the empty right of the time and passenger lines; they stop short of it.
                time.Margin = who.Margin = new Thickness(0, 0, 84, 0);
                var stamp = Stamp(look, style, tappableStamp);
                body.Add(stamp, 0, 1);
                Grid.SetRowSpan(stamp, 2);
            }

            return body;
        }

        private static View Line(View icon, View content)
        {
            var grid = new Grid { ColumnDefinitions = { new(20), new(GridLength.Star) }, ColumnSpacing = 8 };
            icon.HorizontalOptions = LayoutOptions.Center;
            content.HorizontalOptions = LayoutOptions.Start;
            grid.Add(icon, 0);
            grid.Add(content, 1);
            return grid;
        }

        private static Label Truncated(Label label)
        {
            label.LineBreakMode = LineBreakMode.TailTruncation;
            label.MaxLines = 1;
            return label;
        }

        private static View Eta(EventLook look, ScheduleStyle style)
        {
            var row = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
            if (style is ScheduleStyle.FoldedCorner or ScheduleStyle.RoadSigns)
                row.Add(Text("≈", 18, true, null, "SecondaryTextColor"));
            else if (style is not (ScheduleStyle.MapPin or ScheduleStyle.FlapBoard))
                row.Add(Icon(Glyph.Car, 13));
            row.Add(Text(look.EtaText, 20, true));
            var tag = Text(style == ScheduleStyle.FlapBoard ? "EST." : "ETA", 12, false, null, "SecondaryTextColor");
            tag.VerticalOptions = LayoutOptions.End;
            tag.Margin = new Thickness(0, 0, 0, 3);
            row.Add(tag);
            return row;
        }

        private static View Kind(EventLook look, ScheduleStyle style)
        {
            var coloured = style is ScheduleStyle.Token or ScheduleStyle.FoldedCorner or ScheduleStyle.Ticket
                or ScheduleStyle.RoadSigns or ScheduleStyle.TransitLine or ScheduleStyle.MapPin;
            var label = Text(look.KindText, style is ScheduleStyle.MapPin or ScheduleStyle.FlapBoard ? 16 : 17, true, coloured || look.IsPull ? look.Ink : null);
            label.HorizontalOptions = LayoutOptions.End;
            return label;
        }

        /// <summary>
        /// The promised time with its label, worked so it can never pass for the ETA: same size,
        /// but framed, filled, flagged or on tiles, depending on the style.
        /// </summary>
        private static View Promise(EventLook look, ScheduleStyle style)
        {
            Label Lab(Color color, double size = 10.5)
            {
                var l = Text(look.PromiseLabel, size, true, color);
                l.CharacterSpacing = 1.2;
                return l;
            }
            Label Time(Color? color = null) => Text(look.PromisedText, 14, true, color);

            switch (style)
            {
                case ScheduleStyle.Token:
                    return Box(Row(6, Icon(Glyph.Flag, 11, look.Ink), Lab(look.Ink), Time()), 7, new Thickness(6, 1, 9, 1), stroke: look.Ink);

                case ScheduleStyle.Tint:
                    return Box(Row(6, Icon(Glyph.Flag, 10, Colors.White), Lab(Colors.White), Time(Colors.White)), 999, new Thickness(7, 1, 10, 1), fill: look.Color);

                case ScheduleStyle.FoldedCorner:
                {
                    var t = Time();
                    t.TextDecorations = TextDecorations.Underline;
                    return Row(7, Box(Lab(Colors.White), 4, new Thickness(6, 1), fill: look.Color), t);
                }
                case ScheduleStyle.Ticket:
                case ScheduleStyle.SplitPill:
                {
                    var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Auto) } };
                    grid.Add(new Border { BackgroundColor = look.Color, StrokeThickness = 0, Padding = new Thickness(7, 1), Content = Lab(Colors.White) }, 0);
                    var t = Time();
                    t.Margin = new Thickness(8, 0, 9, 0);
                    grid.Add(t, 1);
                    return Box(grid, style == ScheduleStyle.Ticket ? 6 : 999, new Thickness(0), stroke: look.Ink);
                }
                case ScheduleStyle.RoadSigns:
                {
                    var disc = new Border
                    {
                        WidthRequest = 22,
                        HeightRequest = 22,
                        StrokeShape = new Ellipse(),
                        StrokeThickness = 3,
                        Stroke = look.Ink,
                        BackgroundColor = Colors.White,
                        Content = new Label { Text = Glyph.Clock, FontFamily = Glyph.Family, FontSize = 9, TextColor = Color.FromArgb("#1C1C1C"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                    };
                    return Row(7, disc, Lab(look.Ink, 11), Time());
                }
                case ScheduleStyle.TransitLine:
                    return Row(6, Icon(Glyph.Target, 13, look.Ink), Lab(look.Ink, 11), Time());

                case ScheduleStyle.Outline:
                    return Box(Row(6, Icon(Glyph.Flag, 11, look.Ink), Lab(look.Ink), Time()), 6, new Thickness(5, 0, 8, 0), stroke: look.Ink, dashed: look.IsReturn);

                case ScheduleStyle.MapPin:
                    return Row(6, Box(Lab(Colors.White), 999, new Thickness(7, 1), fill: look.Color), Time());

                default: // FlapBoard
                    return Row(6, Lab(look.Ink), Flaps(look.PromisedText, Color.FromArgb("#1B2329"), Color.FromArgb("#F5F7F8"), 12));
            }
        }

        /// <summary>The map's own pin, filled for Appt and hollow for Return, with the arrow inside.</summary>
        public static View MapPin(EventLook look, double width)
        {
            var hollow = look.IsReturn;
            var pin = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(PinPathData)!,
                Fill = hollow ? CardBg : look.Color,
                Stroke = look.Color,
                StrokeThickness = hollow ? 2.4 : 0,
                Aspect = Stretch.Uniform,
            };
            var ring = new Ellipse { WidthRequest = width * 0.55, HeightRequest = width * 0.55, Fill = hollow ? look.Color : Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, width * 0.19, 0, 0) };
            var arrow = new Label { Text = look.ArrowGlyph, FontFamily = Glyph.Family, FontSize = width * 0.32, TextColor = hollow ? Colors.White : look.Color, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, width * 0.27, 0, 0) };
            return new Grid { WidthRequest = width, HeightRequest = width * 1.23, VerticalOptions = LayoutOptions.Center, InputTransparent = true, Children = { pin, ring, arrow } };
        }

        // ---------------------------------------------------------------- the wait

        /// <summary>
        /// The wait stamp, a different one for each style. Tappable, a transparent button lies over
        /// it: a button takes the tap for itself on Android and iOS, so the card under it does not
        /// open the stop. The touch area is never under 48 dp, whatever the stamp's size.
        /// </summary>
        private static View Stamp(EventLook look, ScheduleStyle style, bool tappable)
        {
            var ink = StampInk;
            var minutes = look.WaitMinutes ?? 0;
            var visual = StampVisual(style, minutes, ink);

            var host = new Grid
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                MinimumWidthRequest = 48,
                MinimumHeightRequest = 48,
                Children = { visual },
            };
            AutomationProperties.SetName(host, $"Wait {minutes} minutes");

            if (tappable)
            {
                var button = new Button { BackgroundColor = Colors.Transparent, BorderWidth = 0, Padding = 0, Text = string.Empty, CornerRadius = 8 };
                SemanticProperties.SetDescription(button, $"Wait {minutes} minutes. Tap for details.");
                button.Clicked += (_, _) => WaitPopup.Show(host, look);
                host.Add(button);
            }
            return host;
        }

        private static View StampVisual(ScheduleStyle style, int minutes, Color ink)
        {
            Label T(string text, double size, bool bold = true, double spacing = 0)
            {
                var l = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, TextColor = ink, HorizontalOptions = LayoutOptions.Center, CharacterSpacing = spacing };
                return l;
            }
            VerticalStackLayout Stack(params View[] v)
            {
                var s = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                foreach (var x in v)
                    s.Add(x);
                return s;
            }

            switch (style)
            {
                case ScheduleStyle.Token: // a rubber stamp
                {
                    var b = Box(Stack(T("WAIT", 9, true, 3), Row(2, T(minutes.ToString(), 20), T("MIN", 10, true, 1.5))), 6, new Thickness(8, 1, 8, 3), stroke: ink, strokeWidth: 2.5);
                    b.Rotation = -7;
                    b.Opacity = 0.92;
                    return b;
                }
                case ScheduleStyle.FoldedCorner: // a postmark
                {
                    var d = Drawing(new PostmarkDrawable(minutes, ink), 56, 56);
                    d.Rotation = -12;
                    return d;
                }
                case ScheduleStyle.Tint: // a dashed tag with an hourglass
                {
                    var b = Box(Stack(Icon(Glyph.Hourglass, 11, ink), T($"{minutes}′", 15), T("WAIT", 7.5, true, 1.5)), 12, new Thickness(0, 3), fill: EventLook.Mix(CardBg, ink, 0.1f), stroke: ink, strokeWidth: 2, dashed: true);
                    b.WidthRequest = 62;
                    b.Rotation = 4;
                    return b;
                }
                case ScheduleStyle.Ticket: // a conductor's punch, with the holes
                {
                    var oval = new Border { WidthRequest = 60, HeightRequest = 50, StrokeShape = new Ellipse(), Stroke = ink, StrokeThickness = 2, Content = Stack(T($"{minutes}′", 15), T("WAIT", 7.5, true, 1.5)) };
                    var grid = new Grid { Rotation = -10, Children = { oval } };
                    foreach (var start in new[] { true, false })
                        grid.Add(new Ellipse { WidthRequest = 7, HeightRequest = 7, Fill = PageBg, Stroke = ink, StrokeThickness = 1.5, HorizontalOptions = start ? LayoutOptions.Start : LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Margin = start ? new Thickness(-3, 0, 0, 0) : new Thickness(0, 0, -3, 0) });
                    return grid;
                }
                case ScheduleStyle.RoadSigns: // a yellow warning sign
                {
                    var diamond = new Border { WidthRequest = 42, HeightRequest = 42, Rotation = 45, BackgroundColor = Color.FromArgb("#F2B705"), Stroke = Color.FromArgb("#1C1C1C"), StrokeThickness = 2.5, StrokeShape = new RoundRectangle { CornerRadius = 5 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                    var dark = Color.FromArgb("#1C1C1C");
                    var words = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { new Label { Text = minutes.ToString(), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = dark, HorizontalOptions = LayoutOptions.Center }, new Label { Text = "MIN WAIT", FontSize = 6.5, FontAttributes = FontAttributes.Bold, TextColor = dark, HorizontalOptions = LayoutOptions.Center } } };
                    return new Grid { WidthRequest = 62, HeightRequest = 62, Children = { diamond, words } };
                }
                case ScheduleStyle.TransitLine: // a clock face with the wait as a slice
                {
                    var face = new Grid { WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Center, Children = { Drawing(new ClockFaceDrawable(minutes, ink), 44, 44), Text($"{minutes}′", 12, true) } };
                    ((Label)face.Children[1]).HorizontalOptions = LayoutOptions.Center;
                    return Stack(face, T("WAIT", 7.5, true, 1.5));
                }
                case ScheduleStyle.Outline: // an ink-pad oval
                {
                    var b = Box(Stack(T("WAIT", 8, true, 2.5), T($"~{minutes} MIN", 16)), 22, new Thickness(9, 3), stroke: ink, strokeWidth: 2);
                    b.Rotation = -9;
                    return b;
                }
                case ScheduleStyle.MapPin: // a timer ring
                    return new Grid { WidthRequest = 56, HeightRequest = 56, Children = { Drawing(new RingDrawable(minutes, ink, Separator), 56, 56), Stack(T($"{minutes}′", 14), T("WAIT", 6.5, true, 1.5)) } };

                case ScheduleStyle.SplitPill: // caution tape
                {
                    var amber = Color.FromArgb("#F2B705");
                    var dark = Color.FromArgb("#1C1C1C");
                    var tape = new Border { WidthRequest = 68, HeightRequest = 46, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = Drawing(new StripesDrawable(dark, amber, 7, 7), 68, 46) };
                    var label = new Border { BackgroundColor = amber, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 4 }, Padding = new Thickness(6, 1), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                        Content = new VerticalStackLayout { Spacing = 0, Children = { new Label { Text = $"{minutes} MIN", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = dark, HorizontalOptions = LayoutOptions.Center }, new Label { Text = "WAIT", FontSize = 6.5, FontAttributes = FontAttributes.Bold, TextColor = dark, HorizontalOptions = LayoutOptions.Center } } } };
                    return new Grid { Rotation = -4, Children = { tape, label } };
                }
                default: // FlapBoard: amber flaps
                    return Stack(Flaps($"{minutes:00}'", Color.FromArgb("#2A2108"), Color.FromArgb("#F2B705"), 12), T("WAIT", 7.5, true, 1.5));
            }
        }

        /// <summary>The explanation of the wait, written out under the card on the stop screen.</summary>
        public static View WaitNote(EventLook look)
        {
            var ink = StampInk;
            var text = new Label { FontSize = 12.5, LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center };
            text.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");
            text.FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = $"Wait {look.WaitMinutes} min. ", FontAttributes = FontAttributes.Bold, TextColor = ink },
                    new Span { Text = look.WaitExplanation },
                }
            };
            var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
            grid.Add(Icon(Glyph.Hourglass, 16, ink), 0);
            grid.Add(text, 1);
            var box = Box(grid, 10, new Thickness(10, 8), fill: EventLook.Mix(CardBg, ink, 0.1f), stroke: ink.WithAlpha(0.6f), strokeWidth: 1, dashed: true);
            box.HorizontalOptions = LayoutOptions.Fill;
            box.Margin = new Thickness(10, 0, 10, 10);
            return box;
        }
    }

    /// <summary>
    /// The stamp's explanation, opened by a tap on the stamp. Closes on a tap anywhere or by
    /// itself after six seconds; a new one replaces any that is open.
    /// </summary>
    public sealed class WaitPopup : Popup
    {
        private static WaitPopup? _open;
        private bool _closed;

        private WaitPopup(EventLook look)
        {
            Color = Colors.Transparent;
            CanBeDismissedByTappingOutsideOfPopup = true;
            var title = new Label { Text = $"Wait {look.WaitMinutes} min", FontSize = 13.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
            var body = new Label { Text = look.WaitExplanation, FontSize = 13, TextColor = Colors.White, LineBreakMode = LineBreakMode.WordWrap };
            Content = new Border
            {
                BackgroundColor = Color.FromArgb("#10293A"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 10),
                WidthRequest = 260,
                Content = new VerticalStackLayout { Spacing = 3, Children = { title, body } },
            };
            Closed += (_, _) =>
            {
                _closed = true;
                if (_open == this)
                    _open = null;
            };
        }

        public static void Show(View anchor, EventLook look)
        {
            var page = Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
            if (page is null)
                return;

            _open?.CloseQuietly();
            var popup = new WaitPopup(look) { Anchor = anchor };
            _open = popup;
            page.ShowPopup(popup);
            anchor.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(6), popup.CloseQuietly);
        }

        private void CloseQuietly()
        {
            if (_closed)
                return;
            _closed = true;
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WaitPopup] already gone: {ex.Message}");
            }
        }
    }
}

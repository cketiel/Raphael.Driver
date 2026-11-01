using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services;
using static Raphael.Driver.Controls.ScheduleCards.Ui;

namespace Raphael.Driver.Controls.ScheduleCards
{
    /// <summary>
    /// The actions of a stop or of the garage, set off in the driver's schedule style the way its
    /// card is (round-3 of _meta/design/driver-cards). Legacy keeps the pages' own rows, which
    /// these replace only for the ten new styles; the actions, their order and what they do are
    /// the page's and do not change.
    /// </summary>
    public class ActionListView : ContentView
    {
        public static readonly BindableProperty ActionsProperty = BindableProperty.Create(
            nameof(Actions), typeof(IEnumerable), typeof(ActionListView), propertyChanged: (b, o, n) => ((ActionListView)b).OnActionsChanged(o, n));

        public static readonly BindableProperty EventProperty = BindableProperty.Create(
            nameof(Event), typeof(ScheduleDto), typeof(ActionListView), propertyChanged: (b, _, _) => ((ActionListView)b).Schedule());

        private bool _pending;

        public IEnumerable? Actions
        {
            get => (IEnumerable?)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public ScheduleDto? Event
        {
            get => (ScheduleDto?)GetValue(EventProperty);
            set => SetValue(EventProperty, value);
        }

        private void OnActionsChanged(object? oldValue, object? newValue)
        {
            if (oldValue is INotifyCollectionChanged o)
                o.CollectionChanged -= OnCollectionChanged;
            if (newValue is INotifyCollectionChanged n)
                n.CollectionChanged += OnCollectionChanged;
            Schedule();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Schedule();

        /// <summary>The pages clear the list and add the rows one by one: draw once, after the last.</summary>
        private void Schedule()
        {
            if (_pending)
                return;
            _pending = true;
            Dispatcher.Dispatch(() =>
            {
                _pending = false;
                Build();
            });
        }

        public void Build()
        {
            var style = ScheduleStyleManager.Current;
            if (Event is not { } e || Actions is null || style == ScheduleStyle.Legacy)
            {
                Content = null;
                return;
            }

            var look = new EventLook(e);
            var actions = Actions.OfType<EventAction>().ToList();
            Content = ActionRows.Build(look, style, actions);
        }
    }

    internal static class ActionRows
    {
        private static Color CardBg => ThemeManager.GetColor("CardBackgroundColor", Colors.White);
        private static Color Separator => ThemeManager.GetColor("SeparatorColor", Colors.LightGray);

        public static View Build(EventLook look, ScheduleStyle style, IReadOnlyList<EventAction> actions)
        {
            var rows = new VerticalStackLayout { Spacing = Grouped(style) ? 0 : 6 };
            for (var i = 0; i < actions.Count; i++)
            {
                if (i > 0 && Grouped(style) && style != ScheduleStyle.Tint)
                    rows.Add(Divider(style));
                rows.Add(Row(look, style, actions[i]));
            }

            View list = rows;
            if (Grouped(style))
                list = Frame(look, style, rows);

            return new ContentView { Content = list, Margin = new Thickness(10, 0, 10, 0) };
        }

        /// <summary>Styles whose actions sit together in one framed block, as their card does.</summary>
        private static bool Grouped(ScheduleStyle style) =>
            style is ScheduleStyle.FoldedCorner or ScheduleStyle.Tint or ScheduleStyle.Ticket or ScheduleStyle.Outline;

        private static View Divider(ScheduleStyle style) => style == ScheduleStyle.Ticket
            ? new Line { X1 = 0, Y1 = 1, X2 = 600, Y2 = 1, StrokeThickness = 2, StrokeDashArray = [3, 2], Stroke = Separator, HeightRequest = 2 }
            : new BoxView { HeightRequest = 1, Color = Separator };

        private static View Frame(EventLook look, ScheduleStyle style, View rows)
        {
            var layer = new Grid { Children = { rows } };
            var frame = new Border { Content = layer, Padding = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, StrokeThickness = 1, Stroke = Separator };
            frame.SetDynamicResource(VisualElement.BackgroundColorProperty, "CardBackgroundColor");

            switch (style)
            {
                case ScheduleStyle.FoldedCorner:
                    layer.Add(new Grid
                    {
                        WidthRequest = 34,
                        HeightRequest = 34,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        InputTransparent = true,
                        Children = { Drawing(new CornerDrawable(look.Color, look.IsReturn), 34, 34) },
                    });
                    break;

                case ScheduleStyle.Tint:
                    frame.BackgroundColor = EventLook.Mix(CardBg, look.Color, 0.08f);
                    frame.Stroke = look.Ink.WithAlpha(0.45f);
                    frame.StrokeThickness = 1.5;
                    frame.Padding = 8;
                    ((VerticalStackLayout)rows).Spacing = 6;
                    break;

                case ScheduleStyle.Outline when look.IsPull:
                    frame.Stroke = look.Ink;
                    frame.StrokeThickness = 1.5;
                    return new Border { Content = frame, Padding = 2.5, Stroke = look.Ink, StrokeThickness = 1.5, StrokeShape = new RoundRectangle { CornerRadius = 14 }, BackgroundColor = Colors.Transparent };

                case ScheduleStyle.Outline:
                    frame.Stroke = look.Ink;
                    frame.StrokeThickness = 2.5;
                    if (look.IsReturn)
                        frame.StrokeDashArray = [3, 1.6];
                    break;
            }
            return frame;
        }

        private static View Row(EventLook look, ScheduleStyle style, EventAction action)
        {
            var main = action.IsMain && action.IsEnabled;
            var filled = main && style is ScheduleStyle.Tint or ScheduleStyle.SplitPill;

            var title = new Label { Text = action.Text, FontSize = 15, FontAttributes = main ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap };
            if (filled)
                title.TextColor = Colors.White;
            else if (main)
                title.TextColor = look.Ink;
            else
                title.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");

            var words = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { title } };
            if (action.HasHint)
                words.Add(Text(action.Hint!, 12.5, false, null, "SecondaryTextColor"));

            var grid = new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
                ColumnSpacing = 12,
                Padding = style == ScheduleStyle.SplitPill || filled ? new Thickness(14, 10) : new Thickness(12),
                Opacity = action.IsEnabled ? 1 : 0.45,
            };
            grid.Add(Lead(look, style, action, main, filled), 0);
            grid.Add(words, 1);
            if (action.IsEnabled)
            {
                var chevron = Icon(Glyph.Chevron, 12, filled ? Colors.White : null);
                if (style == ScheduleStyle.FoldedCorner)
                    chevron.Margin = new Thickness(0, 0, 16, 0);
                grid.Add(chevron, 2);
            }

            if (action.IsEnabled && action.Command is not null)
                grid.GestureRecognizers.Add(new TapGestureRecognizer { Command = action.Command });

            // How each row is set off: on its own card, inside the style's frame, or as a pill.
            switch (style)
            {
                case ScheduleStyle.FoldedCorner:
                case ScheduleStyle.Outline:
                    return grid;

                case ScheduleStyle.Ticket:
                    if (main)
                        grid.BackgroundColor = EventLook.Mix(CardBg, look.Color, 0.1f);
                    return grid;

                case ScheduleStyle.Tint:
                    return new Border { Content = grid, Padding = 0, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = filled ? 999 : 8 }, BackgroundColor = filled ? look.Color : Colors.Transparent };

                case ScheduleStyle.SplitPill:
                    return new Border
                    {
                        Content = grid,
                        Padding = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 999 },
                        StrokeThickness = filled ? 0 : 1.5,
                        Stroke = action.IsEnabled ? EventLook.Mix(look.Ink, Separator, 0.45f) : Separator,
                        BackgroundColor = filled ? look.Color : CardBg,
                    };

                default:
                {
                    var card = new Border { Content = grid, Padding = 0, StrokeShape = new RoundRectangle { CornerRadius = 10 }, StrokeThickness = style == ScheduleStyle.TransitLine ? 0 : 1 };
                    card.SetDynamicResource(VisualElement.BackgroundColorProperty, "CardBackgroundColor");
                    card.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");
                    return card;
                }
            }
        }

        /// <summary>The icon of the action, dressed the way the style dresses its card.</summary>
        private static View Lead(EventLook look, ScheduleStyle style, EventAction action, bool main, bool filled)
        {
            var glyph = action.IconGlyph ?? string.Empty;
            switch (style)
            {
                case ScheduleStyle.Token:
                    return Disc(glyph, 30, look.Ink, filled: !look.IsReturn, glyphColor: look.IsReturn ? look.Ink : Colors.White, glyphSize: 14);

                case ScheduleStyle.RoadSigns:
                {
                    var sign = CardFactory.Sign(look, 30);
                    sign.Margin = new Thickness(0);
                    // The sign carries the stop's arrow; the action's own icon replaces it.
                    if (sign is Grid g && g.Children[1] is Label arrow)
                    {
                        arrow.Text = glyph;
                        arrow.FontSize = 12;
                    }
                    return sign;
                }
                case ScheduleStyle.TransitLine:
                    return new Border
                    {
                        WidthRequest = main ? 22 : 18,
                        HeightRequest = main ? 22 : 18,
                        StrokeShape = new Ellipse(),
                        StrokeThickness = 3,
                        Stroke = main ? CardBg : look.Ink,
                        StrokeDashArray = look.IsReturn && !main ? [1, 0.7] : null,
                        BackgroundColor = main ? look.Color : CardBg,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(4, 0),
                    };

                case ScheduleStyle.MapPin:
                {
                    var hollow = look.IsReturn;
                    var icon = new Label { Text = glyph, FontFamily = Glyph.Family, FontSize = 13, TextColor = hollow ? look.Ink : Colors.White, Rotation = 45, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                    return new Border
                    {
                        WidthRequest = 30,
                        HeightRequest = 30,
                        Rotation = -45,
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(15, 15, 0, 15) },
                        BackgroundColor = hollow ? CardBg : look.Color,
                        Stroke = look.Ink,
                        StrokeThickness = hollow ? 2 : 0,
                        Content = icon,
                        VerticalOptions = LayoutOptions.Center,
                    };
                }
                case ScheduleStyle.FlapBoard:
                {
                    var face = new Grid { WidthRequest = 28, HeightRequest = 28, Children = { Icon(glyph, 13, Colors.White), new BoxView { HeightRequest = 1, Color = Colors.Black.WithAlpha(0.45f), VerticalOptions = LayoutOptions.Center } } };
                    return new Border { Content = face, BackgroundColor = look.Color, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 4 }, VerticalOptions = LayoutOptions.Center };
                }
                default:
                {
                    var icon = Icon(glyph, 17, filled ? Colors.White : look.Ink);
                    icon.WidthRequest = 26;
                    return icon;
                }
            }
        }
    }
}

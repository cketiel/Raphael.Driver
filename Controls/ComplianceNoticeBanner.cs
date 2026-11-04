using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// The office's message about the driver's documents (RE-065, <c>COMPLIANCE_NOTICE</c>), pinned to
    /// the Dashboard until the driver reads it. It comes from the notifications already on the phone
    /// (<see cref="NotificationStore"/>): no request of its own.
    /// </summary>
    public sealed class ComplianceNoticeBanner : ContentView
    {
        private const string EventCode = "COMPLIANCE_NOTICE";

        private readonly Label _title = new() { FontSize = 16, FontAttributes = FontAttributes.Bold, Text = "Message from the office" };
        private readonly Label _line = new() { FontSize = 13.5, MaxLines = 3, LineBreakMode = LineBreakMode.TailTruncation };
        private NotificationStore? _store;
        private NotificationDto? _shown;

        public ComplianceNoticeBanner()
        {
            IsVisible = false;
            var glyph = new Label { FontFamily = "FontAwesomeSolid", FontSize = 17, Text = "", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            glyph.SetDynamicResource(Label.TextColorProperty, "AccentColor");
            var icon = new Border { WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 20 }, Content = glyph, VerticalOptions = LayoutOptions.Start };
            icon.SetDynamicResource(BackgroundColorProperty, "AccentSoftColor");
            _title.SetDynamicResource(Label.TextColorProperty, "InkColor");
            _line.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");
            var chevron = new Label { Text = "", FontFamily = "FontAwesomeSolid", FontSize = 14, VerticalOptions = LayoutOptions.Center };
            chevron.SetDynamicResource(Label.TextColorProperty, "TertiaryTextColor");

            var row = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
            row.Add(icon, 0);
            row.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _line } }, 1);
            row.Add(chevron, 2);

            var frame = new Border { Padding = new Thickness(14, 12), StrokeThickness = 1.5, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = row };
            frame.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            frame.SetDynamicResource(Border.StrokeProperty, "AccentColor");
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OpenAsync();
            frame.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(frame, "Message from the office about your documents");

            Content = frame;

            // Resolved here, not on Loaded: hidden at first, the banner may never be laid out to
            // raise Loaded, and then nothing would ever show it.
            _store = ServiceHelper.GetService<NotificationStore>();
            if (_store != null)
            {
                _store.Items.CollectionChanged += OnItemsChanged;
                Show();
            }
            Unloaded += (_, _) => { if (_store != null) _store.Items.CollectionChanged -= OnItemsChanged; };
            Loaded += (_, _) =>
            {
                if (_store == null) return;
                _store.Items.CollectionChanged -= OnItemsChanged;
                _store.Items.CollectionChanged += OnItemsChanged;
                Show();
            };
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            MainThread.BeginInvokeOnMainThread(Show);

        /// <summary>The newest unread message, if any.</summary>
        private void Show()
        {
            _shown = _store?.Items
                .Where(n => n.BusinessEventCode == EventCode && n.IsUnread)
                .OrderByDescending(n => n.CreatedAtUtc)
                .FirstOrDefault();
            IsVisible = _shown is not null;
            if (_shown is not null) _line.Text = _shown.Message;
        }

        /// <summary>The whole message; reading it takes it off the Dashboard.</summary>
        private async Task OpenAsync()
        {
            if (_shown is null || _store is null) return;
            var notice = _shown;
            var toDocuments = await Shell.Current.DisplayAlert(notice.Title, notice.Message, "My documents", "OK");
            await _store.MarkViewedAsync(notice);
            Show();
            if (toDocuments)
            {
                try { await Shell.Current.GoToAsync(nameof(Views.DocumentsPage)); } catch (Exception) { }
            }
        }
    }
}

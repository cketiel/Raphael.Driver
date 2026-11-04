using Microsoft.Maui.Controls.Shapes;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// The way into MY DOCUMENTS (RE-065), in two forms:
    /// <list type="bullet">
    /// <item>Settings: always there, saying how many need the driver.</item>
    /// <item>Dashboard (<see cref="Banner"/>): only when something expires within a week, has
    /// expired or was rejected — the place the driver is sure to see it.</item>
    /// </list>
    /// It asks the server when it is first shown and after an upload, never on every return.
    /// </summary>
    public sealed class DocumentsStatusCard : ContentView
    {
        public static readonly BindableProperty BannerProperty =
            BindableProperty.Create(nameof(Banner), typeof(bool), typeof(DocumentsStatusCard), false);

        /// <summary>The Dashboard's form: hidden unless something is urgent.</summary>
        public bool Banner
        {
            get => (bool)GetValue(BannerProperty);
            set => SetValue(BannerProperty, value);
        }

        private const int UrgentDays = 7;

        // One answer for both forms and for the life of the session, renewed after an upload.
        private static MyComplianceDto? _last;
        private static bool _stale = true;
        private static Task? _loading;

        private readonly Label _glyph = new() { FontFamily = "FontAwesomeSolid", FontSize = 18, Text = "", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        private readonly Label _title = new() { FontSize = 16, FontAttributes = FontAttributes.Bold };
        private readonly Label _line = new() { FontSize = 13 };
        private readonly Border _frame;
        private IComplianceService? _service;

        public DocumentsStatusCard()
        {
            var icon = new Border { WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 20 }, Content = _glyph };
            icon.SetDynamicResource(BackgroundColorProperty, "TintColor");
            _glyph.SetDynamicResource(Label.TextColorProperty, "AccentColor");
            _title.SetDynamicResource(Label.TextColorProperty, "InkColor");
            _line.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");
            var chevron = new Label { Text = "", FontFamily = "FontAwesomeSolid", FontSize = 14, VerticalOptions = LayoutOptions.Center };
            chevron.SetDynamicResource(Label.TextColorProperty, "TertiaryTextColor");

            var row = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
            row.Add(icon, 0);
            row.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _line } }, 1);
            row.Add(chevron, 2);

            _frame = new Border { Padding = new Thickness(14, 12), StrokeThickness = 1.5, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = row };
            _frame.SetDynamicResource(BackgroundColorProperty, "CardBackgroundColor");
            _frame.SetDynamicResource(Border.StrokeProperty, "SeparatorColor");
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try { await Shell.Current.GoToAsync(nameof(Views.DocumentsPage)); } catch (Exception) { }
            };
            _frame.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(_frame, "My documents");

            Content = _frame;
            Loaded += OnLoaded;
            Unloaded += (_, _) => { if (_service != null) _service.Changed -= OnChanged; };
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            _service ??= Handler?.MauiContext?.Services.GetService<IComplianceService>();
            if (_service is null) return;
            _service.Changed -= OnChanged;
            _service.Changed += OnChanged;
            Show();
            if (!_stale) return;
            _loading ??= LoadAsync(_service);
            await _loading;
            Show();
        }

        private void OnChanged(object? sender, EventArgs e)
        {
            _stale = true;
            _loading = null;
            MainThread.BeginInvokeOnMainThread(() => OnLoaded(this, EventArgs.Empty));
        }

        private static async Task LoadAsync(IComplianceService service)
        {
            var result = await service.GetMineAsync();
            if (result.Ok)
            {
                _last = result.Value;
                _stale = false;
            }
            _loading = null;
        }

        private void Show()
        {
            var items = _last is null ? [] : _last.Driver.Items.Concat(_last.Vehicle?.Items ?? []).ToList();
            var expired = items.Count(i => i.Current is { DaysToExpiry: < 0 });
            var urgent = items.Count(i => i.Current is { DaysToExpiry: >= 0 and <= UrgentDays });
            var soon = items.Count(i => i.Current is { DaysToExpiry: >= 0 } c && c.DaysToExpiry <= ViewModels.DocumentsViewModel.SoonDays);
            var rejected = items.Count(i => i.Latest?.Status == "REJECTED");
            var pending = items.Count(i => i.Latest?.Status == "PENDING");

            if (Banner)
            {
                IsVisible = expired + urgent + rejected > 0;
                _title.Text = expired > 0 ? "Document expired" : rejected > 0 ? "Document rejected" : "Document expiring";
                _line.Text = expired > 0 ? "Upload the renewal: it is out of service until it is approved."
                    : rejected > 0 ? "Open My documents to see why and upload it again."
                    : "Upload the renewal before it expires.";
                _frame.SetDynamicResource(Border.StrokeProperty, expired + rejected > 0 ? "DangerColor" : "WarnColor");
                _glyph.Text = "";
                return;
            }

            _title.Text = "My documents";
            var parts = new List<string>();
            if (expired > 0) parts.Add($"{expired} expired");
            if (soon > 0) parts.Add($"{soon} expiring");
            if (rejected > 0) parts.Add($"{rejected} rejected");
            if (pending > 0) parts.Add($"{pending} in review");
            _line.Text = _last is null ? "License, courses and your vehicle's documents"
                : parts.Count > 0 ? string.Join(" · ", parts) : "Upload, renew and check them";
            _frame.SetDynamicResource(Border.StrokeProperty, expired + rejected > 0 ? "DangerColor" : soon > 0 ? "WarnColor" : "SeparatorColor");
        }
    }
}

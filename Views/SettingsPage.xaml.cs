using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Raphael.Driver.Models;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        // RE-064: the menu at the bottom, in the style chosen in Settings.
        // The password sheet goes above it: out of the page first, back on top after Attach.
        Root.Children.Remove(PasswordSheet);
        Controls.Menu.NavMenuView.Attach(this, "settings");
        ((Grid)Content).Children.Add(PasswordSheet);
        BindingContext = viewModel;

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsPasswordSheetOpen) && viewModel.IsPasswordSheetOpen)
            {
                // Slides up from the bottom.
                SheetPanel.TranslationY = 400;
                _ = SheetPanel.TranslateTo(0, 0, 220, Easing.CubicOut);
            }
            else if (e.PropertyName == nameof(SettingsViewModel.Tab) && viewModel.IsAppearance)
            {
                LoadAppearance();
                ScrollToChosenStyle();
            }
        };
    }

    private CollectionView? _stylesList;

    // Built the first time Appearance opens, and the tiles right after it shows.
    private void LoadAppearance()
    {
        if (AppearanceHost.Content is not null || BindingContext is not SettingsViewModel vm) return;
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var view = (View)((DataTemplate)Resources["AppearanceTemplate"]).CreateContent();
        AppearanceHost.Content = view;
        _stylesList = view.GetVisualTreeDescendants().OfType<CollectionView>().First();
        Console.WriteLine($"RAPHAEL-PERF: appearance {(long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds} ms");
        Dispatcher.Dispatch(() => _stylesList.ItemsSource = vm.ScheduleStyles);
    }

    private async void OnContact(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ContactPage));

    private void OnMessages(object? sender, EventArgs e) =>
        ((AppShellViewModel)Shell.Current.BindingContext).SendSmsCommand.Execute(null);

    private async void OnChangeRoute(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//SchedulePage");

    private async void OnSignOut(object? sender, EventArgs e)
    {
        if (await DisplayAlert("Sign out?", null, "Sign out", "Cancel"))
            ((AppShellViewModel)Shell.Current.BindingContext).SignOutCommand.Execute(null);
    }

    // The driver's style in view, wherever it sits in the strip. After the first layout:
    // asked for any earlier, the list has no items on screen yet and does not move.
    private void ScrollToChosenStyle()
    {
        if (BindingContext is SettingsViewModel vm && vm.ScheduleStyles.FirstOrDefault(o => o.IsSelected) is { } chosen)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => _stylesList?.ScrollTo(chosen, position: ScrollToPosition.Center, animate: false));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel loading)
        {
            loading.LoadMapAppsCommand.Execute(null);
            loading.LoadProfileCommand.Execute(null);
        }
        if (BindingContext is SettingsViewModel { IsAppearance: true }) ScrollToChosenStyle();
    }

    /// <summary>A style tile: its preview, from which the driver can take it.</summary>
    private void OnStyleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ScheduleStyleOption option || BindingContext is not SettingsViewModel vm)
            return;

        this.ShowPopup(new ScheduleStylePreviewPopup(option.Info, () => vm.UseScheduleStyleCommand.Execute(option.Style)), new CommunityToolkit.Maui.PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true, Shape = null, Shadow = null });
    }
}
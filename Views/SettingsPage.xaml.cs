using CommunityToolkit.Maui.Views;
using Raphael.Driver.Models;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // The driver's style in view, wherever it sits in the strip. After the first layout:
        // asked for any earlier, the list has no items on screen yet and does not move.
        if (BindingContext is SettingsViewModel vm && vm.ScheduleStyles.FirstOrDefault(o => o.IsSelected) is { } chosen)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => StylesList.ScrollTo(chosen, position: ScrollToPosition.Center, animate: false));
    }

    /// <summary>A style tile: its preview, from which the driver can take it.</summary>
    private void OnStyleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ScheduleStyleOption option || BindingContext is not SettingsViewModel vm)
            return;

        this.ShowPopup(new ScheduleStylePreviewPopup(option.Info, () => vm.UseScheduleStyleCommand.Execute(option.Style)));
    }
}
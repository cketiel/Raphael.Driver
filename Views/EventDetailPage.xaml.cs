
using Raphael.Driver.Controls;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class EventDetailPage : ContentPage, IRouteSignalHandler
{
    private readonly EventDetailPageViewModel _viewModel;
    private readonly RouteSignalCoordinator _signals;
    private bool _shown;

    public EventDetailPage(EventDetailPageViewModel viewModel, RouteSignalCoordinator signals)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _signals = signals;

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // The rows of the driver's schedule style: the page's own for Legacy, the styled list otherwise.
        var legacy = ScheduleStyleManager.Current == ScheduleStyle.Legacy;
        LegacyActions.IsVisible = legacy;
        StyledActions.IsVisible = !legacy;
        // The first time, the bindings have just drawn both; again only on coming back to the page.
        if (_shown)
        {
            Hero.Build();
            StyledActions.Build();
        }
        else
        {
            Dispatcher.Dispatch(() => DetailsHost.Content ??= (View)((DataTemplate)Resources["DetailsTemplate"]).CreateContent());
        }
        _shown = true;

        _signals.Register(this);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _signals.Unregister(this);
    }

    /// <summary>
    /// The trip on this screen left the driver's route while they were looking at it.
    /// </summary>
    /// <remarks>
    /// Refreshing here would be pointless: the event does not exist any more, so there is
    /// nothing to reload it from. The driver is taken back to the schedule, which loads
    /// correct data on the way in.
    ///
    /// <para>
    /// Only a signal about <b>this</b> trip is acted on. A change elsewhere on the route can
    /// wait until they go back — interrupting a driver reading the detail of the trip they
    /// are actually running, over a different one, is noise at the worst moment.
    /// </para>
    /// </remarks>
    public async Task<RouteSignalOutcome> HandleRouteSignalAsync(NotificationDto signal)
    {
        if (signal.TripId is not { } tripId || _viewModel.Event?.TripId != tripId)
            return RouteSignalOutcome.NotRelevant;

        await RouteSignalPopup.ShowAsync(
            "Route updated",
            $"{signal.Message} Returning to your schedule.",
            "Go now");

        // ".." pops back to the schedule this detail was opened from, which reloads on the
        // way in. An absolute route would land there without the run it needs and show an
        // empty day.
        await Shell.Current.GoToAsync("..");

        return RouteSignalOutcome.Handled;
    }
}

using Raphael.Driver.Controls;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class TodaySchedulePage : ContentPage, IRouteSignalHandler
{
    private readonly TodayScheduleViewModel _viewModel;
    private readonly RouteSignalCoordinator _signals;

    public TodaySchedulePage(TodayScheduleViewModel viewModel, RouteSignalCoordinator signals)
    {
        InitializeComponent();
        // RE-064: the menu at the bottom; this page is part of Schedule.
        Controls.Menu.NavMenuView.Attach(this, "schedule");

        _viewModel = viewModel;
        _signals = signals;

        BindingContext = viewModel;
    }

    // The schedule style and theme the cards on screen were drawn with.
    private (ScheduleStyle, SkinTheme)? _drawnWith;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Back from Settings with another style or theme: the cards are drawn again from the
        // events already in memory. Nothing is asked of the server for it.
        var now = (ScheduleStyleManager.Current, ThemeManager.Effective);
        if (_drawnWith is { } before && before != now)
        {
            var events = EventsList.ItemsSource;
            EventsList.ItemsSource = null;
            EventsList.ItemsSource = events;
        }
        _drawnWith = now;

        // We check if the BindingContext is of the correct type and call the command to reload.
        if (BindingContext is TodayScheduleViewModel vm && vm.LoadEventsCommand.CanExecute(null))
        {
            vm.LoadEventsCommand.Execute(null);
        }

        _signals.Register(this);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _signals.Unregister(this);
    }

    /// <summary>
    /// The route changed while the driver was looking at today's schedule.
    /// </summary>
    /// <remarks>
    /// The two cases are matched differently, and they have to be. A trip that was
    /// <b>removed</b> is still in the list — that is exactly why the list is wrong — so it is
    /// matched by its identifier, and a removal for a trip the driver never had is left alone
    /// rather than interrupting them over somebody else's route.
    ///
    /// <para>
    /// A trip that was <b>added</b> cannot be matched that way: it is not in the list yet, and
    /// its absence is the problem. It is matched by date instead.
    /// </para>
    /// </remarks>
    public async Task<RouteSignalOutcome> HandleRouteSignalAsync(NotificationDto signal)
    {
        var concernsThisScreen = signal.IsTripAddedToRoute
            ? signal.TripDate is null || signal.TripDate == DateTime.Today
            : signal.TripId is { } tripId &&
              _viewModel.Events.Any(e => e.TripId == tripId);

        if (!concernsThisScreen)
            return RouteSignalOutcome.NotRelevant;

        await RouteSignalPopup.ShowAsync(
            "Route updated",
            signal.Message,
            "Refresh now");

        // Through the generated command, which is the public face of the private loader.
        await _viewModel.LoadEventsCommand.ExecuteAsync(null);

        return RouteSignalOutcome.Handled;
    }
}

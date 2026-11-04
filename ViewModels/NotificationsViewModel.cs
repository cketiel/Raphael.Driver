using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using System.Collections.ObjectModel;

namespace Raphael.Driver.ViewModels
{
    public partial class NotificationsViewModel : ObservableObject
    {
        private readonly NotificationStore _store;

        /// <summary>The list the page binds to. Owned by the store, not by this view model.</summary>
        public ObservableCollection<NotificationDto> Notifications => _store.Items;

        [ObservableProperty]
        private bool _isRefreshing;

        public NotificationsViewModel(NotificationStore store)
        {
            _store = store;

            _store.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NotificationStore.UnreadCount))
                    OnPropertyChanged(nameof(HasNotifications));

                if (e.PropertyName == nameof(NotificationStore.LoadFailed))
                {
                    OnPropertyChanged(nameof(EmptyTitle));
                    OnPropertyChanged(nameof(EmptyDetail));
                }
            };

            _store.Items.CollectionChanged += (_, _) =>
                OnPropertyChanged(nameof(HasNotifications));
        }

        public bool HasNotifications => _store.Items.Count > 0;

        /// <summary>
        /// What the empty list says. A refused or failed call must not read as a quiet shift.
        /// </summary>
        public string EmptyTitle => _store.LoadFailed
            ? "Could not load notifications"
            : "No notifications";

        public string EmptyDetail => _store.LoadFailed
            ? "Pull down to try again. If it keeps failing, sign out and back in."
            : "Dispatch will let you know here if a trip you are running gets cancelled.";

        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                await _store.RefreshAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// Opening a notification reads it.
        /// </summary>
        /// <remarks>
        /// It deliberately does not navigate to the trip. The only notification a driver gets
        /// today is <c>TRIP_CANCELLED</c>, and that trip is gone from their schedule — opening
        /// its detail page would land on nothing. When events that point at a live trip get
        /// wired, this is where the tap through belongs; the identifier is already in
        /// <see cref="NotificationDto.TripId"/>.
        /// </remarks>
        [RelayCommand]
        private async Task OpenAsync(NotificationDto notification)
        {
            if (notification is null)
                return;

            await _store.MarkViewedAsync(notification);
        }

        [RelayCommand]
        private async Task MarkUnreadAsync(NotificationDto notification)
        {
            if (notification is null)
                return;

            if (!await _store.MarkUnviewedAsync(notification))
                await Shell.Current.DisplayAlert("Notifications", "Could not mark it as unread.", "OK");
        }

        [RelayCommand]
        private async Task MarkReadAsync(NotificationDto notification)
        {
            if (notification is null)
                return;

            await _store.MarkViewedAsync(notification);
        }

        [RelayCommand]
        private async Task MarkAllReadAsync()
        {
            if (!await _store.MarkAllViewedAsync())
                await Shell.Current.DisplayAlert("Notifications", "Could not mark them as read.", "OK");
        }

        /// <summary>
        /// Takes a notification off this phone. It stays on the server until it expires.
        /// </summary>
        [RelayCommand]
        private void Hide(NotificationDto notification)
        {
            if (notification is null)
                return;

            _store.Hide(notification);
        }

        public async Task OnAppearingAsync()
        {
            IsRefreshing = true;
            try { await _store.RefreshAsync(); }
            finally { IsRefreshing = false; }
        }
    }
}

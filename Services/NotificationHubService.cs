using Microsoft.AspNetCore.SignalR.Client;
using Raphael.Driver.DTOs;
using System.Diagnostics;

namespace Raphael.Driver.Services
{
    /// <summary>
    /// Keeps the app subscribed to <c>/hubs/notifications</c> while there is a session.
    /// </summary>
    /// <remarks>
    /// The hub decides on connect whether this internal user is a driver or a dispatcher,
    /// reading the role from the token — the client cannot ask to be treated as either. Drivers
    /// are addressed by their own connection rather than by a group, so nothing meant for the
    /// dispatch office arrives here.
    ///
    /// <para>
    /// The token travels in the query string because a WebSocket handshake carries no
    /// Authorization header. The API accepts it that way only for this path.
    /// </para>
    /// </remarks>
    public class NotificationHubService : INotificationHubService
    {
        private readonly NotificationStore _store;
        private readonly RouteSignalCoordinator _signals;
        private readonly CallRequestStore _callRequests;

        private HubConnection? _connection;

        public NotificationHubService(
            NotificationStore store,
            RouteSignalCoordinator signals,
            CallRequestStore callRequests)
        {
            _store = store;
            _signals = signals;
            _callRequests = callRequests;
        }

        public bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var token = Preferences.Get("AuthToken", string.Empty);

            if (string.IsNullOrWhiteSpace(token))
                return;

            if (_connection is not null)
            {
                // Already up, or on its way back after a drop.
                if (_connection.State != HubConnectionState.Disconnected)
                    return;

                await DisposeConnectionAsync();
            }

            var hubUrl = $"{Configuration.ApiEnvironment.BaseUrl.TrimEnd('/')}/hubs/notifications";

            try
            {
                // ⚠️ The token used to be appended to the URL as ?access_token=... A query
                // string is written to server logs, proxy logs and anything in between, so the
                // credential that opens a driver's session was being recorded in plain text on
                // every reconnect. AccessTokenProvider sends it as a header instead, which is
                // what the patient app already did, and the backend accepts both
                // (Program.cs OnMessageReceived).
                //
                // It is a callback rather than a captured value on purpose: SignalR calls it
                // again on every automatic reconnect, so a connection that drops after the
                // token was renewed comes back with the new one instead of retrying forever
                // with the old.
                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.AccessTokenProvider = () =>
                            Task.FromResult(Preferences.Get("AuthToken", string.Empty));
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _connection.On<NotificationDto>("ReceiveNotification", async notification =>
                {
                    if (notification is null)
                        return;

                    // Everything comes down the same wire, and a signal goes both ways: into
                    // the bell like any other row, and to the coordinator, which decides
                    // whether the screen the driver is on has to be interrupted over it.
                    _store.Receive(notification);

                    if (notification.IsSignal)
                        await _signals.ReceiveAsync(notification);

                    // The office took the request: the button turns green now, not at the next poll.
                    if (notification.BusinessEventCode == CallRequestStore.ClaimedEventCode)
                        await _callRequests.RefreshAsync();
                });

                // The server sends this after any change it made on the driver's behalf, and
                // after a reconnection there may have been changes nobody told us about.
                _connection.On("RefreshNotifications", async () =>
                {
                    await _store.RefreshAsync();
                });

                _connection.On<Guid>("NotificationViewed", async _ =>
                {
                    await _store.RefreshAsync();
                });

                _connection.On<Guid>("NotificationAcknowledged", async _ =>
                {
                    await _store.RefreshAsync();
                });

                _connection.Reconnected += async _ =>
                {
                    // Whatever arrived while the socket was down is only in the database.
                    await _store.RefreshAsync();
                    await _signals.SyncAsync();
                    await _callRequests.RefreshAsync();
                };

                await _connection.StartAsync(cancellationToken);

                Debug.WriteLine("NotificationHubService: connected.");
            }
            catch (Exception ex)
            {
                // No live channel is a degraded state, not a broken app: the inbox still loads
                // over HTTP and the push still arrives through Firebase.
                Debug.WriteLine($"NotificationHubService: could not connect. {ex.Message}");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is null)
                return;

            try
            {
                await _connection.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotificationHubService: error while stopping. {ex.Message}");
            }
            finally
            {
                await DisposeConnectionAsync();
            }
        }

        private async Task DisposeConnectionAsync()
        {
            if (_connection is null)
                return;

            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotificationHubService: error while disposing. {ex.Message}");
            }

            _connection = null;
        }
    }
}

namespace Raphael.Driver.Helpers
{
    /// <summary>
    /// Builds the page a tap is about to open before the tap, so opening it only has to fill it in.
    /// Each page is used once: a visit never sees what the previous stop left in its view model.
    /// </summary>
    public sealed class ReadyPage<TPage> : RouteFactory where TPage : Page
    {
        private TPage? _ready;

        public override Element GetOrCreate() => Take(IPlatformApplication.Current!.Services);

        public override Element GetOrCreate(IServiceProvider services) => Take(services);

        private TPage Take(IServiceProvider services)
        {
            var page = _ready ?? services.GetRequiredService<TPage>();
            _ready = null;
            return page;
        }

        /// <summary>Prepares the next page once the screen on show has settled.</summary>
        public void PrepareSoon(IDispatcher dispatcher)
        {
            if (_ready is not null) return;
            dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(600), () =>
            {
                if (_ready is not null) return;
                try { _ready = IPlatformApplication.Current!.Services.GetRequiredService<TPage>(); }
                catch (Exception) { /* built when it is opened, as before */ }
            });
        }
    }

    /// <summary>The internal pages kept ready.</summary>
    public static class ReadyPages
    {
        public static readonly ReadyPage<Views.EventDetailPage> EventDetail = new();
        public static readonly ReadyPage<Views.PullOutDetailPage> PullOutDetail = new();
        public static readonly ReadyPage<Views.DocumentsPage> Documents = new();
        public static readonly ReadyPage<Views.DocumentUploadPage> DocumentUpload = new();
    }
}

namespace Raphael.Driver.Services
{
    /// <summary>
    /// The run login the driver is working on, as the Dashboard found it (their active run) or as
    /// it was last opened in Today. The menu's Schedule (RE-064) uses it to open Today directly,
    /// as the Dashboard's SCHEDULE button does; with none known it opens the Run Login page.
    /// Kept in memory only: a new session asks the server again.
    /// </summary>
    public static class ActiveRun
    {
        public static string? RunLogin { get; set; }
    }
}

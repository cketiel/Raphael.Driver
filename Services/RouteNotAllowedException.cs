namespace Raphael.Driver.Services
{
    /// <summary>
    /// The server refused a route typed in CHANGE ROUTE because it belongs to another company
    /// (RE-063, 403 <c>route_other_provider</c>). The phone's copy of it, if any, is gone.
    /// </summary>
    public sealed class RouteNotAllowedException : Exception
    {
        public const string Title = "Route not available";
        public const string Explanation = "This route belongs to another company. You can only open routes of your own company.";

        public RouteNotAllowedException() : base(Explanation) { }
    }
}

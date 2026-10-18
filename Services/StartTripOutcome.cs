namespace Raphael.Driver.Services
{
    /// <summary>What the server said to a Start Trip (RE-057).</summary>
    public enum StartTripOutcome
    {
        /// <summary>Started, or it already was.</summary>
        Started,

        /// <summary>The server answered no; retrying would get the same answer.</summary>
        Refused,

        /// <summary>No answer, or a server error: worth trying again.</summary>
        Failed
    }
}

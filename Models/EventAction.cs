using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Raphael.Driver.Models
{
    public class EventAction
    {
        public string Text { get; set; }
        public string IconGlyph { get; set; } 
        public ICommand Command { get; set; }

        /// <summary>
        /// False keeps the row where the driver is used to finding it, greyed out and inert. A row
        /// that disappears reads as a fault; one that says where the action went does not.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>A second line under the text. Says what to use instead when the row is disabled.</summary>
        public string? Hint { get; set; }

        public bool HasHint => !string.IsNullOrWhiteSpace(Hint);
    }
}

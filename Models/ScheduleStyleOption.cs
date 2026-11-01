using CommunityToolkit.Mvvm.ComponentModel;
using Raphael.Driver.Services;

namespace Raphael.Driver.Models
{
    /// <summary>A schedule style as a tile of Settings' SCHEDULE STYLE strip.</summary>
    public partial class ScheduleStyleOption : ObservableObject
    {
        public ScheduleStyleOption(ScheduleStyleInfo info, bool isSelected)
        {
            Info = info;
            _isSelected = isSelected;
        }

        public ScheduleStyleInfo Info { get; }
        public ScheduleStyle Style => Info.Style;
        public string Name => Info.Name;
        public string Caption => Info.Caption;

        [ObservableProperty]
        private bool _isSelected;
    }
}

using System;

namespace UsageTimerWinUI.Models
{
    public class AppUsageRecord
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public double Seconds { get; set; }

        public double Minutes { get; set; }
        public string Formatted { get; set; } = "";

        public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; set; }
    }
}

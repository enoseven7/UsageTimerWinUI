using System;

namespace UsageTimerWinUI.Models;

public class AppUsageRecord
{
    public string Name { get; set; }
    public string Formatted { get; set; }
    public double Minutes { get; set; }
    public Microsoft.UI.Xaml.Media.ImageSource Icon { get; set; }
}


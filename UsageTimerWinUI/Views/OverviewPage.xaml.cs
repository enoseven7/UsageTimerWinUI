using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace UsageTimerWinUI.Views;

public sealed partial class OverviewPage : Page
{
    private double totalSeconds = 0;
    private readonly DispatcherTimer timer;
    private readonly string saveFile;

    public OverviewPage()
    {
        this.InitializeComponent();

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTimerWinUI");

        Directory.CreateDirectory(folder);
        saveFile = Path.Combine(folder, "time_log.txt");

        if (File.Exists(saveFile))
            double.TryParse(File.ReadAllText(saveFile), out totalSeconds);

        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    private void Timer_Tick(object sender, object e)
    {
        totalSeconds++;

        int days = (int)(totalSeconds / 86400);
        int hours = (int)((totalSeconds % 86400) / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);

        TotalTimeText.Text = $"Total: {days}d {hours}h {minutes}m {seconds}s";
        DayProgress.Value = totalSeconds % 86400;
        HourProgress.Value = totalSeconds % 3600;

        if (seconds == 0)
            File.WriteAllText(saveFile, totalSeconds.ToString());
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        totalSeconds = 0;
        File.WriteAllText(saveFile, "0");

        TotalTimeText.Text = "Total: 0d 0h 0m 0s";
        DayProgress.Value = 0;
        HourProgress.Value = 0;
    }

    private void GithubButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/enoseven7",
            UseShellExecute = true
        });
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using UsageTimerWinUI.Services;

namespace UsageTimerWinUI.Views
{
    public sealed partial class OverviewPage : Page, INotifyPropertyChanged
    {
        private double totalSeconds = 0;
        private DispatcherTimer timer;
        private string saveFile;

        public SolidColorPaint LegendTextPaint { get; set; } = new SolidColorPaint(new SKColor(240, 240, 240));

        private ISeries[] _appUsageSeries = Array.Empty<ISeries>();
        public ISeries[] AppUsageSeries
        {
            get => _appUsageSeries;
            set
            {
                _appUsageSeries = value;
                OnPropertyChanged();
            }
        }

        //public SolidColorPaint LegendTextPaint { get; set; }

        private bool _isLoaded = false;

        public OverviewPage()
        {
            this.InitializeComponent();

            this.Loaded += OverviewPage_Loaded;

            // data binding base
            //this.DataContext = this;


        }

        private void LoadTime()
        {
            // setup save path
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UsageTimerWinUI");

            Directory.CreateDirectory(folder);
            saveFile = Path.Combine(folder, "time_log.txt");

            if (File.Exists(saveFile))
            {
                double.TryParse(File.ReadAllText(saveFile), out totalSeconds);
            }
        }

        private void StartTimer()
        {
            // timer for total use
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
                return;
            _isLoaded = true;

            this.DataContext = this;

            LegendTextPaint = new SolidColorPaint(new SKColor(240, 240, 240));

            LoadTime();
            BuildSeries();
            AppTrackerService.Updated += OnUsageUpdated;

            
            StartTimer();
            UpdateTimeUi();
        }

        private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            timer.Stop();
            AppTrackerService.Updated -= OnUsageUpdated;
        }

        private void OnUsageUpdated()
        {
            // marshal to UI thread just in case
            DispatcherQueue.TryEnqueue(BuildSeries);
        }

        private void BuildSeries()
        {
            try
            {
                var usage = AppTrackerService.Usage;

                if (usage == null || usage.Count == 0)
                {
                    AppUsageSeries = Array.Empty<ISeries>();
                    return;
                }

                var series = new List<ISeries>();

                foreach (var kv in usage.OrderByDescending(x => x.Value))
                {
                    var name = kv.Key;
                    var minutes = Math.Round(kv.Value / 60, 1);

                    if (minutes <= 0)
                        continue;

                    series.Add(new PieSeries<double>
                    {
                        Name = name,
                        Values = new[] { minutes },
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 12,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    });
                }

                AppUsageSeries = series.ToArray();
            }
            catch
            {
                // if LiveCharts or anything else chokes, don't crash the whole app
                AppUsageSeries = Array.Empty<ISeries>();
            }
        }

        private void Timer_Tick(object sender, object e)
        {
            if (!_isLoaded)
                return;

            totalSeconds++;

            UpdateTimeUi();

            int seconds = (int)(totalSeconds % 60);
            if (seconds == 0)
            {
                try
                {
                    File.WriteAllText(saveFile, totalSeconds.ToString());
                }
                catch { }
            }
        }

        private void UpdateTimeUi()
        {
            if (TotalTimeText == null || DayProgress == null || HourProgress == null)
                return;

            int days = (int)(totalSeconds / 86400);
            int hours = (int)((totalSeconds % 86400) / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);

            TotalTimeText.Text = $"Total: {days}d {hours}h {minutes}m {seconds}s";
            DayProgress.Value = totalSeconds % 86400;
            HourProgress.Value = totalSeconds % 3600;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            totalSeconds = 0;

            try
            {
                File.WriteAllText(saveFile, "0");
            }
            catch { }

            UpdateTimeUi();
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/enoseven7",
                UseShellExecute = true
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

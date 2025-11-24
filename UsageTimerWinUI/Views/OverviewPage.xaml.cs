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
using LiveChartsCore.SkiaSharpView.WinUI;

namespace UsageTimerWinUI.Views
{
    public sealed partial class OverviewPage : Page, INotifyPropertyChanged
    {
        private double totalSeconds = 0;
        private DispatcherTimer timer;
        private string saveFile;

        private PieChart? _pieChart;

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

            AppTrackerService.EnsureInitialized();

            LegendTextPaint = new SolidColorPaint(new SKColor(240, 240, 240));

            _pieChart = new PieChart
            {
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                LegendTextPaint = LegendTextPaint,
                LegendTextSize = 14
            };

            // ensure the chart starts with no series to avoid mixing types from other sources
            _pieChart.Series = Array.Empty<ISeries>();
            ChartContainer.Child = _pieChart;

            LoadTime();
            BuildSeries();
            AppTrackerService.Updated += OnUsageUpdated;

            
            StartTimer();
            UpdateTimeUi();
        }

        private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            timer?.Stop();
            AppTrackerService.Updated -= OnUsageUpdated;

            if (_pieChart != null)
            {
                _pieChart.Series = Array.Empty<ISeries>();
                ChartContainer.Child = null; 
                _pieChart = null;
            }
        }

        private void OnUsageUpdated()
        {
            // marshal to UI thread just in case
            DispatcherQueue.TryEnqueue(BuildSeries);
        }

        private void BuildSeries()
        {
            if (_pieChart == null)
                return;

            try
            {
                var usage = AppTrackerService.Usage;

                var series = new List<ISeries>();

                foreach (var kv in usage.OrderByDescending(x => x.Value))
                {
                    var name = kv.Key;
                    // ensure the numeric value is treated as double to avoid boxed-int issues
                    var valueAsDouble = Convert.ToDouble(kv.Value);
                    var minutes = Math.Round(valueAsDouble / 60.0, 1);

                    if (minutes <= 0)
                        continue;

                    // explicitly create a double[] so LiveCharts sees doubles (not boxed ints)
                    series.Add(new PieSeries<double>
                    {
                        Name = name,
                        Values = new double[] { minutes },
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 12,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    });
                }

                // assign on UI thread and defensively clear previous series first
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _pieChart.Series = Array.Empty<ISeries>();
                        _pieChart.Series = series.ToArray();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to assign pie series: {ex}");
                        // dump diagnostic types for debugging
                        foreach (var s in series)
                        {
                            if (s is PieSeries<double> ps && ps.Values != null)
                            {
                                foreach (var v in ps.Values)
                                    Debug.WriteLine($"Value type: {v.GetType().FullName ?? "null"}");
                            }
                        }
                    }
                });
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to build app usage series: {ex}");
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

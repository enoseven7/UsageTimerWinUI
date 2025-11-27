using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UsageTimerWinUI.Services;
using Windows.Foundation.Collections;

namespace UsageTimerWinUI.Views
{
    public sealed partial class OverviewPage : Page, INotifyPropertyChanged
    {
        private double totalSeconds = 0;
        private DispatcherTimer timer;
        private string saveFile;

        private PieChart? _pieChart;

        public SolidColorPaint _legendTextPaint;
        public SolidColorPaint LegendTextPaint
        {
            get => _legendTextPaint;
            set
            {
                _legendTextPaint = value;
                OnPropertyChanged();
            }
        }

        public ISeries[] UsageSeries { get; set;  }
        public Axis[] XAxis { get; set; }
        public Axis[] YAxis { get; set; }

        private ColumnSeries<double> _series;
        private List<double> _values;
        private List<string> _labels;


        //public SolidColorPaint LegendTextPaint { get; set; }

        private bool _isLoaded = false;

        public OverviewPage()
        {
            this.InitializeComponent();

            this.Loaded += OverviewPage_Loaded;

            _labels = new List<string>();
            _values = new List<double>();

            _series = new ColumnSeries<double>
            {
                Values = _values,
                Name = "App Usage",
                Fill = new SolidColorPaint(new SKColor(100, 180, 255)),
                Stroke = null,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
            };

            LegendTextPaint = new SolidColorPaint(
                App.Current.RequestedTheme == ApplicationTheme.Dark
                    ? new SKColor(240, 240, 240)
                    : new SKColor(20, 20, 20)
                    );

            UsageSeries = new ISeries[] { _series };

            XAxis = new[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(new SKColor(200,200,200)),
                    Labels = _labels
                }
            };

            YAxis = new[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(new SKColor(200,200,200))
                }
            };

            DataContext = this;

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
            SessionTimerService.Updated += OnGlobalTimerTick;
        }

        private void OnGlobalTimerTick()
        {
            DispatcherQueue.TryEnqueue(UpdateTimeUi);
        }

        private void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
                return;
            _isLoaded = true;

            this.DataContext = this;

            AppTrackerService.EnsureInitialized();

            //LegendTextPaint = new SolidColorPaint(new SKColor(240, 240, 240));

            /*
            _pieChart = new PieChart
            {
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                LegendTextPaint = LegendTextPaint,
                LegendTextSize = 14
            };

            // ensure the chart starts with no series to avoid mixing types from other sources
            _pieChart.Series = Array.Empty<ISeries>();
            ChartContainer.Child = _pieChart;
            */

            LoadTime();
            //BuildSeries();
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
            var usage = AppTrackerService.Usage;

            _labels.Clear();
            _values.Clear();

            foreach (var kv in usage.OrderByDescending(x => x.Value))
            {
                _labels.Add(kv.Key);
                _values.Add(Math.Round(kv.Value / 60.0, 2));
            }

            _series.Values = _values.ToArray();

            LegendTextPaint = new SolidColorPaint(
                SettingsService.Theme == "Dark"
                    ? new SKColor(240, 240, 240)
                    : new SKColor(20, 20, 20)
                    );

            DispatcherQueue.TryEnqueue(() =>
            {
                this.DataContext = null;
                this.DataContext = this;
                if (UsageChart != null)
                {
                    //XAxis[0].Invalidate(UsageChart.CoreChart);
                    UsageChart.CoreChart.Update();
                }
            });

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

            double total = SessionTimerService.TotalSeconds;

            int days = (int)(total / 86400);
            int hours = (int)((total % 86400) / 3600);
            int minutes = (int)((total % 3600) / 60);
            int seconds = (int)(total % 60);

            TotalTimeText.Text = $"Total: {days}d {hours}h {minutes}m {seconds}s";
            DayProgress.Value = total % 86400;
            HourProgress.Value = total % 3600;
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

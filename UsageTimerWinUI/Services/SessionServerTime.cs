using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace UsageTimerWinUI.Services
{
    public static class SessionTimerService
    {
        public static double TotalSeconds { get; private set; }

        public static event Action? Updated;

        private static DispatcherTimer? _timer;
        private static readonly string folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsageTimerWinUI");

        private static readonly string saveFile = Path.Combine(folder, "time_log.txt");

        static SessionTimerService()
        {
            Directory.CreateDirectory(folder);

            if (File.Exists(saveFile))
            {
                double temp;

                if(double.TryParse(File.ReadAllText(saveFile), out temp))
                {
                    TotalSeconds = temp;
                }
            }
                
        }

        public static void Start()
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                TotalSeconds++;

                if (TotalSeconds % 60 == 0)
                    File.WriteAllText(saveFile, TotalSeconds.ToString());

                Updated?.Invoke();
            };

            _timer.Start();
        }
    }
}

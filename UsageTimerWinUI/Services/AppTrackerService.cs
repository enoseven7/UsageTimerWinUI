using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UsageTimerWinUI.Services
{
    public static class AppTrackerService
    {
        public static Dictionary<string, double> Usage { get; private set; } = new();
        public static List<string> TrackedApps { get; private set; } = new();

        public static event Action? Updated;

        private static readonly string folder = 
            Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTimerWinUI");

        private static readonly string file = Path.Combine(folder, "trackedApps.json");

        static AppTrackerService()
        {
            Directory.CreateDirectory(folder);
            Load();
        }

        public static void AddApp(string name)
        {
            if (!TrackedApps.Contains(name))
            {
                TrackedApps.Add(name);
                Save();
            }
        }

        public static void RemoveApp(string name)
        {
            if (TrackedApps.Contains(name))
            {
                TrackedApps.Remove(name);
                Save();
            }
        }

        public static List<string> GetRunningProcessNames()
        {
            return Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero)
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        public static void Tick()
        {
            foreach (var name in TrackedApps)
            {
                if (!Usage.ContainsKey(name))
                    Usage[name] = 0;

                Usage[name] += 1;
            }

            Updated?.Invoke();
        }

        private static void Save()
        {
            var wrapper = new AppSaveWrapper
            {
                TrackedApps = TrackedApps,
                Usage = Usage
            };

            File.WriteAllText(file,
                JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void Load()
        {
            if (!File.Exists(file)) return;

            try
            {
                var wrapper = JsonSerializer.Deserialize<AppSaveWrapper>(File.ReadAllText(file));
                if (wrapper == null) return;

                TrackedApps = wrapper.TrackedApps ?? new();
                Usage = wrapper.Usage ?? new();
            }
            catch { }
        }

        private class AppSaveWrapper
        {
            public List<string>? TrackedApps { get; set; }
            public Dictionary<string, double>? Usage { get; set; }
        }
    }
}

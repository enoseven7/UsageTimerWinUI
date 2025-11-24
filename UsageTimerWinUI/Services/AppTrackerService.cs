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

        public static Dictionary<string, string> DisplayNames { get; private set; } = new();
        public static List<string> TrackedApps { get; private set; } = new();

        public static event Action? Updated;

        private static readonly string folder = 
            Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTimerWinUI");

        private static readonly string file = Path.Combine(folder, "trackedApps.json");
        private static bool _initialized = false;
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Directory.CreateDirectory(folder);
            Load();

            // Force everything to double
            var fixedUsage = new Dictionary<string, double>();
            foreach (var kv in Usage)
                fixedUsage[kv.Key] = Convert.ToDouble(kv.Value);

            Usage = fixedUsage;

            // Ensure all tracked apps exist in Usage
            foreach (var app in TrackedApps)
                if (!Usage.ContainsKey(app))
                    Usage[app] = 0;
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

        // Safely enumerate processes and skip protected/system processes.
        public static List<string> GetRunningProcessNames()
        {
            var list = new List<string>();

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProcesses failed: {ex}");
                return list;
            }

            foreach (var p in processes)
            {
                try
                {
                    // Accessing MainWindowHandle or ProcessName can throw for protected processes.
                    if (p.MainWindowHandle == IntPtr.Zero)
                        continue;

                    string name = p.ProcessName;

                    if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name))
                        list.Add(name);
                }
                catch (Exception ex)
                {
                    // Skip protected/system processes — log for diagnostics but do not crash.
                    Debug.WriteLine($"Skipping process (id={p.Id}): {ex.Message}");
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            list.Sort();
            return list;
        }


        public static void Tick()
        {
            foreach (var name in TrackedApps)
            {
                if (!Usage.ContainsKey(name))
                    Usage[name] = 0;

                Usage[name] += 1;
            }
            if (DateTime.Now.Second % 10 == 0)
                Save();

            Updated?.Invoke();
        }

        private static void Save()
        {
            var wrapper = new AppSaveWrapper
            {
                TrackedApps = TrackedApps,
                Usage = Usage,
                DisplayNames = DisplayNames
            };

            File.WriteAllText(file,
                JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void Load()
        {
            if (!File.Exists(file)) return;

            try
            {
                var json = File.ReadAllText(file);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Read tracked apps array safely
                if (root.TryGetProperty("TrackedApps", out var ta) && ta.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    TrackedApps = ta.EnumerateArray()
                                    .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                                    .Select(e => e.GetString()!)
                                    .ToList();
                }


                // Read usage object and coerce any numeric type to double
                Usage = new Dictionary<string, double>();
                if (root.TryGetProperty("Usage", out var usageElem) && usageElem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in usageElem.EnumerateObject())
                    {
                        double val = 0;
                        var v = prop.Value;
                        if (v.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            // Try GetDouble first, fall back to integer conversions
                            if (!v.TryGetDouble(out val))
                            {
                                if (v.TryGetInt64(out var i64))
                                    val = Convert.ToDouble(i64);
                                else if (v.TryGetInt32(out var i32))
                                    val = Convert.ToDouble(i32);
                            }
                        }
                        else if (v.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            double.TryParse(v.GetString(), out val);
                        }

                        Usage[prop.Name] = val;
                    }
                }

                // ensure all tracked apps exist in Usage
                foreach (var app in TrackedApps)
                {
                    if (!Usage.ContainsKey(app))
                        Usage[app] = 0;
                }

                if (root.TryGetProperty("DisplayNames", out var dnElem) &&
                    dnElem.ValueKind == JsonValueKind.Object)
                {
                    DisplayNames = dnElem.EnumerateObject()
                                         .Where(p => p.Value.ValueKind == JsonValueKind.String)
                                         .ToDictionary(p => p.Name, p => p.Value.GetString() ?? p.Name);
                }
                else
                {
                    DisplayNames = new();
                }
            }
            catch
            {
                // ignore corrupt file but don't leave Usage null
                Usage ??= new Dictionary<string, double>();
                TrackedApps ??= new List<string>();
                DisplayNames ??= new();
            }
        }

        public static void SetDisplayName(string processName, string displayName)
        {
            DisplayNames[processName] = displayName;
            Save();
        }

        private class AppSaveWrapper
        {
            public List<string>? TrackedApps { get; set; }
            public Dictionary<string, double>? Usage { get; set; }

            public Dictionary<string, string>? DisplayNames { get; set; }
        }
    }
}

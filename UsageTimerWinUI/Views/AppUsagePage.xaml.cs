using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using UsageTimerWinUI.Models;
using System.Linq;

namespace UsageTimerWinUI.Views;

public sealed partial class AppUsagePage : Page
{
    private readonly DispatcherTimer timer;
    private readonly Dictionary<string, double> usage = new();
    private readonly string savePath;
    public ObservableCollection<AppUsageRecord> AppListItems { get; set; } = new();


    public AppUsagePage()
    {
        this.InitializeComponent();

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTimerWinUI");

        Directory.CreateDirectory(folder);
        savePath = Path.Combine(folder, "apps.json");

        Load();

        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    // Win32 imports
    private static class Win32
    {
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    }

    private void Timer_Tick(object sender, object e)
    {
        var activeProcesses = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.ProcessName));

 

        foreach(var proc in activeProcesses)
        {
            
            try
            {
                string name = proc.ProcessName.ToLower();
                if (!usage.ContainsKey(name))
                    usage[name] = 0;
                usage[name] += 1;
                Refresh(name);
                if (DateTime.Now.Second % 10 == 0)
                    Save();
            }
            catch { }
        }
    }

    private ImageSource GetAppIcon(string exePath)
    {
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var img = new BitmapImage();
            img.SetSource(ms.AsRandomAccessStream());
            return img;
        }
        catch
        {
            return null;
        }
    }

    private void Refresh(string name)
    {
        var record = AppListItems.FirstOrDefault(x => x.Name == name);
        if (record == null)
        {
            string exePath = Process.GetProcessesByName(name).FirstOrDefault()?.MainModule?.FileName;
            record = new AppUsageRecord
            {
                Name = name,
                Icon = exePath != null ? GetAppIcon(exePath) : null
            };
            AppListItems.Add(record);
        }

        record.Minutes = Math.Round(usage[name] / 60.0);
        record.Formatted = TimeSpan.FromSeconds(usage[name]).ToString(@"hh\:mm\:ss");
    }

    private void Save()
    {
        File.WriteAllText(savePath,
            JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Load()
    {
        if (!File.Exists(savePath)) return;

        var json = File.ReadAllText(savePath);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, double>>(json);

        if (loaded != null)
        {
            usage.Clear();
            foreach (var kv in loaded)
                usage[kv.Key] = kv.Value;
        }
    }
}

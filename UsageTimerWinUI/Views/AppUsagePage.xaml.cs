using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UsageTimerWinUI.Models;
using UsageTimerWinUI.Services;

namespace UsageTimerWinUI.Views;

public sealed partial class AppUsagePage : Page
{
    public ObservableCollection<AppUsageRecord> AppListItems { get; set; } = new();

    public AppUsagePage()
    {
        this.InitializeComponent();

        // Defer initialization to Loaded to avoid doing work during construction
        this.Loaded += AppUsagePage_Loaded;
        this.Unloaded += AppUsagePage_Unloaded;
    }

    private void AppUsagePage_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            RefreshDropdown();
            RefreshList();
            AppTrackerService.Updated += OnTrackerTick;
        }
        catch (Exception ex)
        {
            // Don't crash the whole app; log the error for diagnosis
            Debug.WriteLine($"AppUsagePage load error: {ex}");
        }
    }

    private void AppUsagePage_Unloaded(object? sender, RoutedEventArgs e)
    {
        // Unsubscribe to avoid keeping references / firing after page disposal
        AppTrackerService.Updated -= OnTrackerTick;
        this.Loaded -= AppUsagePage_Loaded;
        this.Unloaded -= AppUsagePage_Unloaded;
    }

    private void OnTrackerTick()
    {
        // Keep UI updates safe - marshal to UI thread if needed by the service/event
        _ = DispatcherQueue.TryEnqueue(() => RefreshList());
    }

    private void RefreshDropdown()
    {
        try
        {
            ProcessDropdown.ItemsSource = AppTrackerService.GetRunningProcessNames();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RefreshDropdown error: {ex}");
            ProcessDropdown.ItemsSource = Array.Empty<string>();
        }
    }

    private async void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog()
        {
            Title = "Add App (Process Name)",
            Content = new TextBox { PlaceholderText = "e.g. discord, chrome, obs64" },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string app = (dialog.Content as TextBox)?.Text.Trim().ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(app))
            {
                AppTrackerService.AddApp(app);
                RefreshList();
            }
        }
    }

    private void RefreshList()
    {
        AppListItems.Clear();

        foreach (var app in AppTrackerService.TrackedApps ?? Enumerable.Empty<string>())
        {
            try
            {
                AppTrackerService.Usage.TryGetValue(app, out var seconds);
                var ts = TimeSpan.FromSeconds(seconds);

                AppListItems.Add(new AppUsageRecord
                {
                    Name = app,
                    Minutes = Math.Round(ts.TotalMinutes, 1),
                    Formatted = ts.ToString(@"hh\:mm\:ss"),
                    Icon = GetIcon(app)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to build record for '{app}': {ex}");
            }
        }
    }

    private void ProcessDropdown_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        string app = args.Text.Trim();

        if (!string.IsNullOrWhiteSpace(app))
        {
            AppTrackerService.AddApp(app);
            RefreshList();
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AppUsageRecord record)
        {
            AppTrackerService.RemoveApp(record.Name);
            RefreshList();
        }
    }

    private ImageSource? GetIcon(string processName)
    {
        try
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            string? exe = null;
            try
            {
                exe = proc?.MainModule?.FileName;
            }
            catch (Exception ex)
            {
                // Accessing MainModule can throw (access denied / platform issues). Log and continue.
                Debug.WriteLine($"Unable to read MainModule for {processName}: {ex}");
                return null;
            }

            if (exe == null) return null;

            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            using var bmp = icon?.ToBitmap();
            if (bmp == null) return null;

            using MemoryStream ms = new();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            BitmapImage img = new();
            img.SetSource(ms.AsRandomAccessStream());
            return img;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetIcon({processName}) failed: {ex}");
            return null;
        }
    }
}

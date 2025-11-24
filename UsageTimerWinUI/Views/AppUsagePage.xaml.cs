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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UsageTimerWinUI.Models;
using UsageTimerWinUI.Services;
using Windows.Storage.Streams; // for AsRandomAccessStream extension


namespace UsageTimerWinUI.Views;

public sealed partial class AppUsagePage : Page
{
    public ObservableCollection<AppUsageRecord> AppListItems { get; set; } = new();

    public ObservableCollection<ProcessOption> AvailableProcesses { get; set; } = new();

    public AppUsagePage()
    {
        this.InitializeComponent();

        // Defer initialization to Loaded to avoid doing work during construction
        this.Loaded += AppUsagePage_Loaded;
        this.Unloaded += AppUsagePage_Unloaded;
    }

    private void AppUsagePage_Loaded(object? sender, RoutedEventArgs e)
    {
        AppTrackerService.EnsureInitialized();
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
            AvailableProcesses.Clear();

            var names = AppTrackerService.GetRunningProcessNames();

            // Add items first (fast), load icons asynchronously to avoid touching process handles on UI thread
            foreach (var name in names)
            {
                var placeholder = new ProcessOption
                {
                    Name = name,
                    Icon = null
                };

                AvailableProcesses.Add(placeholder);

                // Load icon bytes in background; create BitmapImage on UI thread
                _ = Task.Run(() =>
                {
                    try
                    {
                        var png = GetIconPngBytes(name);
                        if (png != null)
                        {
                            _ = DispatcherQueue.TryEnqueue(() =>
                            {
                                var idx = AvailableProcesses.IndexOf(placeholder);
                                if (idx >= 0)
                                {
                                    // create BitmapImage on UI thread from bytes
                                    var img = new BitmapImage();
                                    using var ms = new MemoryStream(png);
                                    img.SetSource(ms.AsRandomAccessStream());

                                    // replace item so UI updates (works without INotifyPropertyChanged)
                                    AvailableProcesses[idx] = new ProcessOption { Name = placeholder.Name, Icon = img };
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Background icon load failed for {name}: {ex}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RefreshDropdown error: {ex}");
        }
    }

    /// <summary>
    /// Extracts the associated icon and returns PNG bytes (runs on background thread).
    /// Returns null when icon can't be obtained.
    /// </summary>
    private static byte[]? GetIconPngBytes(string processName)
    {
        try
        {
            Process[] procs;
            try
            {
                procs = Process.GetProcessesByName(processName);
            }
            catch
            {
                return null;
            }

            string? exe = null;
            foreach (var p in procs)
            {
                try
                {
                    if (TryGetProcessExecutablePath(p.Id, out var path) && !string.IsNullOrEmpty(path))
                    {
                        exe = path;
                        break;
                    }
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                return null;

            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                using var bmp = icon?.ToBitmap();
                if (bmp == null) return null;

                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            return null;
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
        // Build a lookup for fast updates
        var existing = AppListItems.ToDictionary(x => x.Name, x => x);

        foreach (var app in AppTrackerService.TrackedApps)
        {
            AppTrackerService.Usage.TryGetValue(app, out var seconds);
            var ts = TimeSpan.FromSeconds(seconds);

            if (existing.TryGetValue(app, out var record))
            {
                // Update the existing item (DO NOT REPLACE IT)
                record.Minutes = Math.Round(ts.TotalMinutes, 1);
                record.Formatted = ts.ToString(@"hh\:mm\:ss");

                //var file = FileVersionInfo.GetVersionInfo(app);
                //record.DisplayName = file.ProductName ?? app;
            }
            else
            {
                var niceName = GetNiceDisplayName(app);
                // Add new app
                AppListItems.Add(new AppUsageRecord
                {
                    Name = app,
                    Minutes = Math.Round(ts.TotalMinutes, 1),
                    DisplayName = AppTrackerService.DisplayNames.ContainsKey(app) ? AppTrackerService.DisplayNames[app] : niceName,
                    Formatted = ts.ToString(@"hh\:mm\:ss"),
                    Icon = GetIcon(app)
                });
            }
        }

        // OPTIONAL: Remove apps no longer tracked
        // (Usually unnecessary)
        foreach (var existingApp in existing.Keys)
        {
            if (!AppTrackerService.TrackedApps.Contains(existingApp))
            {
                var remove = existing[existingApp];
                AppListItems.Remove(remove);
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
        if (AppList.SelectedItem is AppUsageRecord record)
        {
            AppTrackerService.RemoveApp(record.Name);
            RefreshList();
        }
    }

    private ImageSource? GetIcon(string processName)
    {
        try
        {
            Process[] procs;
            try
            {
                procs = Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProcessesByName failed for {processName}: {ex}");
                return null;
            }

            string? exe = null;

            foreach (var p in procs)
            {
                try
                {
                    // Try a safe approach to get the executable path; skip instances we cannot access.
                    if (TryGetProcessExecutablePath(p.Id, out var path) && !string.IsNullOrEmpty(path))
                    {
                        exe = path;
                        break;
                    }
                }
                catch
                {
                    // Access denied / protected process; try next instance
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                return null;

            try
            {
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
                Debug.WriteLine($"Icon extraction failed for {exe}: {ex}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetIcon({processName}) failed: {ex}");
            return null;
        }
    }

    // Use QueryFullProcessImageName via a handle opened with PROCESS_QUERY_LIMITED_INFORMATION
    private static bool TryGetProcessExecutablePath(int pid, out string? path)
    {
        path = null;
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle == IntPtr.Zero)
                return false;

            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(handle, 0, sb, ref size))
            {
                path = sb.ToString();
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);


    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if(AppList.SelectedItem is not AppUsageRecord record)
            return;

        var dialog = new ContentDialog()
        {
            Title = $"Rename {record.Name}",
            Content = new TextBox { Text = record.DisplayName },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string newName = (dialog.Content as TextBox)?.Text.Trim() ?? record.DisplayName;
            if (!string.IsNullOrWhiteSpace(newName))
            {
                AppTrackerService.SetDisplayName(record.Name, newName);
                RefreshList();
            }
        }
    }

    private void ProcessDropdown_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProcessDropdown.SelectedValue is string name && !string.IsNullOrWhiteSpace(name))
        {
            AppTrackerService.AddApp(name);
            RefreshList();

            // Reset text/selection so repeated adds work predictably
            ProcessDropdown.SelectedItem = null;
            ProcessDropdown.Text = "";
        }
    }

    private string GetNiceDisplayName(string processName)
    {
        try
        {
            // Try get a running process by that name
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            string? exe = null;

            try
            {
                exe = proc?.MainModule?.FileName;
            }
            catch
            {
                // Access to MainModule can fail, ignore
            }

            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var info = FileVersionInfo.GetVersionInfo(exe);

                // ProductName or FileDescription often look nice ("Discord", "Google Chrome", etc.)
                if (!string.IsNullOrWhiteSpace(info.ProductName))
                    return info.ProductName;

                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    return info.FileDescription;
            }
        }
        catch
        {
            // swallow and fall back
        }

        // Fallback: capitalised process name
        return char.ToUpper(processName[0]) + processName.Substring(1);
    }

}

using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Diagnostics;
using UsageTimerWinUI.Helpers;
using UsageTimerWinUI.Services;
using UsageTimerWinUI.Views;
using Windows.UI.WindowManagement;
using WinRT;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using WindowId = Microsoft.UI.WindowId;

namespace UsageTimerWinUI;

public sealed partial class MainWindow : Window
{
    private DispatcherTimer _trackerTimer;
    private bool minimizeToTrayEnabled = true;
    private AppWindow appWindow;
    private TrayIcon? trayIcon;
    public MainWindow()
    {
        this.InitializeComponent();
        SettingsService.Load();

        AppTrackerService.EnsureInitialized();
        SessionTimerService.Start();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // WinUI 3 API to get AppWindow
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        appWindow = AppWindow.GetFromWindowId(windowId);

        // Attach closing event
        appWindow.Closing += AppWindow_Closing;

        ThemeHelper.ApplyTheme(this);
        TrySetMica();

        trayIcon = new TrayIcon(this, new System.Drawing.Icon("Assets/timerIcon.ico"));

        Nav.SelectionChanged += Nav_SelectionChanged;
        ContentFrame.Navigate(typeof(OverviewPage));

        StartGlobalAppTimer();
    }

    private System.Drawing.Icon GetAppIconHandle()
    {
        // Try common asset locations (case-insensitive variants)
        string[] candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "assets", "timerIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "timerIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "timerIcon.ico")
        };

        string iconPath = null;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                iconPath = c;
                break;
            }
        }

        // For packaged apps, try the installed location as a fallback
        if (iconPath == null)
        {
            try
            {
                var pkgPath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                var pkgCandidate = Path.Combine(pkgPath, "assets", "timerIcon.ico");
                if (File.Exists(pkgCandidate)) iconPath = pkgCandidate;
            }
            catch { /* not packaged or API unavailable */ }
        }

        if (iconPath == null)
            throw new FileNotFoundException("Icon not found. Place 'timerIcon.ico' in an 'assets' (or 'Assets') folder and set its Build Action to 'Content' and 'Copy to Output Directory' to 'Always' or 'Copy if newer'.");

        using var icon = new System.Drawing.Icon(iconPath);
        return icon;
    }

    private void StartGlobalAppTimer()
    {
        _trackerTimer = new DispatcherTimer();
        _trackerTimer.Interval = TimeSpan.FromSeconds(1);
        _trackerTimer.Tick += (s, e) => AppTrackerService.Tick();
        _trackerTimer.Start();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        Debug.WriteLine("MainWindow: Closing event triggered.");
        if (SettingsService.MinimizeToTray)
        {
            Debug.WriteLine("MainWindow: Minimize to tray is enabled, cancelling close and hiding window.");
            args.Cancel = true;
            appWindow.Hide();
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        try
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItem is not NavigationViewItem item) return;

            switch (item.Tag)
            {
                case "overview":
                    ContentFrame.Navigate(typeof(OverviewPage));
                    break;

                case "apps":
                    ContentFrame.Navigate(typeof(AppUsagePage));
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log to app folder for post-mortem
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsageTimerWinUI");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "navigation_error.txt"), $"{DateTime.Now}: {ex}\n\n");
            }
            catch { }
        }
    }

    private MicaController? _micaController;
    private SystemBackdropConfiguration? _config;

    private void TrySetMica()
    {
        if (!SettingsService.UseMica || !MicaController.IsSupported())
            return;

        _config = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        _micaController = new MicaController();
        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_config);
    }
}

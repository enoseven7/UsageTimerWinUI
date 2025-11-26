using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using UsageTimerWinUI.Helpers;
using UsageTimerWinUI.Services;
using UsageTimerWinUI.Views;
using Windows.UI.WindowManagement;
using WinRT;
using static System.Runtime.InteropServices.JavaScript.JSType;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using WindowId = Microsoft.UI.WindowId;
using System.Runtime.InteropServices;

namespace UsageTimerWinUI;

public sealed partial class MainWindow : Window
{
    private DispatcherTimer _trackerTimer;
    private bool minimizeToTrayEnabled = true;
    private AppWindow appWindow;
    private TrayIcon? trayIcon;

    private bool trayInitialized = false;

    private bool allowClose = false;
    public MainWindow()
    {
        this.InitializeComponent();
        this.Activated += MainWindow_Activated;
        SettingsService.Load();

        AppTrackerService.EnsureInitialized();
        SessionTimerService.Start();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        InitAppWindow();

        ThemeHelper.ApplyTheme(this);
        TrySetMica();



        

        Nav.SelectionChanged += Nav_SelectionChanged;
        ContentFrame.Navigate(typeof(OverviewPage));

        StartGlobalAppTimer();
    }

    private void StartGlobalAppTimer()
    {
        _trackerTimer = new DispatcherTimer();
        _trackerTimer.Interval = TimeSpan.FromSeconds(1);
        _trackerTimer.Tick += (s, e) => AppTrackerService.Tick();
        _trackerTimer.Start();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        if (trayInitialized)
            return;

        trayInitialized = true;

        InitTrayIcon();
    }

    private void InitTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "timerIcon.ico");
        var icon = System.IO.File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        trayIcon = new TrayIcon(
            icon,
            "UsageTimer",
            onOpen: () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    appWindow.Show();
                    this.Activate();
                });
            },
            onExit: () =>
            {
                Debug.WriteLine("TRAY EXIT CLICKED");

                DispatcherQueue.TryEnqueue(() =>
                {
                    Debug.WriteLine("TRAY EXIT CALLBACK INVOKED");
                    this.RequestTrueClose();
                });
            });
    }
    private void InitAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += MainWindow_Closing;
    }
    private void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        Debug.Write(allowClose);
        if (allowClose)
        {
            trayIcon?.Dispose();
            return;
        }

        if (SettingsService.MinimizeToTray)
        {
            // cancel real close, hide to tray
            args.Cancel = true;
            sender.Hide();
            return;
        }
        // really exit
        trayIcon?.Dispose();
        
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

    public void RequestTrueClose()
    {
        Debug.WriteLine("RequestTrueClose HIT");
        allowClose = true;
        this.Close();
    }


}

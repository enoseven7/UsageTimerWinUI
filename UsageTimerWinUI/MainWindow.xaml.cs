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

    private static readonly string folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsageTimerWinUI");

    private static readonly string saveFile = Path.Combine(folder, "time_log.txt");
    public MainWindow()
    {
        this.InitializeComponent();
        this.Activated += MainWindow_Activated;
        SettingsService.Load();
        ThemeHelper.ApplyTheme(this);

        AppTrackerService.EnsureInitialized();
        SessionTimerService.Start();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        InitAppWindow();


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
        /*
        if (trayInitialized)
            return;

        trayInitialized = true;

        InitTrayIcon();
        */
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
        if (File.Exists(saveFile))
        {
            SessionTimerService.Save();
        }

        Debug.Write(allowClose);
        if (allowClose)
        {
            trayIcon?.Dispose();
            return;
        }

        if (SettingsService.MinimizeToTray)
        {
            // cancel real close, hide to tray
            InitTrayIcon();
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
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _config;

    private void TrySetMica()
    {

        var theme = SettingsService.Theme switch
        {
            "Light" => SystemBackdropTheme.Light,
            "Dark" => SystemBackdropTheme.Dark,
            _ => SystemBackdropTheme.Default // System
        };

        _config = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = theme
        };
        ThemeHelper.ApplyTheme(this);

        if (!SettingsService.UseMica || !MicaController.IsSupported())
        {
            if(_acrylicController == null)
            {
                //_acrylicController?.Dispose();
                //_acrylicController = null;
                _acrylicController = new DesktopAcrylicController();
                _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                _acrylicController.SetSystemBackdropConfiguration(_config);    
            }
            else
            {
                _acrylicController.SetSystemBackdropConfiguration(_config);
            }
            return;
        }

        if(_micaController == null)
        {
            //_micaController?.Dispose();
            //_micaController = null;
            _micaController = new MicaController();
            _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
            _micaController.SetSystemBackdropConfiguration(_config);
        }
        else
        {
            _micaController.SetSystemBackdropConfiguration(_config);
        }
        
    }

    public void RequestTrueClose()
    {
        Debug.WriteLine("RequestTrueClose HIT");
        allowClose = true;
        this.Close();
    }

    public void RefreshBackdrop()
    {
        TrySetMica();
    }
}
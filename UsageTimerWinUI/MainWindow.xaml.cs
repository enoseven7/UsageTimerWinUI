using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using UsageTimerWinUI.Services;
using UsageTimerWinUI.Views;
using WinRT;

namespace UsageTimerWinUI;

public sealed partial class MainWindow : Window
{
    private DispatcherTimer _trackerTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        SettingsService.Load();

        AppTrackerService.EnsureInitialized();
        SessionTimerService.Start();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

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

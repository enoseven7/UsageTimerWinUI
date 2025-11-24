using System;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        AppTrackerService.EnsureInitialized();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

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

    private MicaController? _micaController;
    private SystemBackdropConfiguration? _config;

    private void TrySetMica()
    {
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

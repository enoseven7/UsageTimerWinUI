using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UsageTimerWinUI.Views;
using Microsoft.UI.Composition;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT;
using Microsoft.UI.Xaml.Media; // Add this using
using Microsoft.UI.Composition.SystemBackdrops; // Add this using

namespace UsageTimerWinUI;

public sealed partial class MainWindow : Window
{
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfiguration;

    public MainWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set up Mica as the system backdrop
        TrySetMicaBackdrop();

        Nav.SelectionChanged += Nav_SelectionChanged;
        ContentFrame.Navigate(typeof(OverviewPage));
    }

    private void TrySetMicaBackdrop()
    {
        _micaController = new MicaController();
        _backdropConfiguration = new SystemBackdropConfiguration();
        _backdropConfiguration.IsInputActive = true;
        _backdropConfiguration.Theme = SystemBackdropTheme.Default;

        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
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
    }
}
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using UsageTimerWinUI.Services;

namespace UsageTimerWinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // Theme
            foreach (var item in ThemeCombo.Items)
            {
                if (item is ComboBoxItem cbi &&
                    (string)cbi.Tag == SettingsService.Theme)
                {
                    ThemeCombo.SelectedItem = cbi;
                    break;
                }
            }

            // Mica
            // Temporarily unsubscribe to avoid firing the Toggled handler
            // while the Page may not yet be part of the visual tree (XamlRoot == null).
            MicaToggle.Toggled -= MicaToggle_Toggled;
            MicaToggle.IsOn = SettingsService.UseMica;
            MicaToggle.Toggled += MicaToggle_Toggled;
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is not ComboBoxItem cbi)
                return;

            string theme = (string)cbi.Tag;
            SettingsService.SetTheme(theme);

            // Apply immediately
            if (Window.Current is Window window)
            {
                ThemeHelper.ApplyTheme(window);
            }
        }

        private void MicaToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool useMica = MicaToggle.IsOn;
            SettingsService.SetUseMica(useMica);

            // Only show the dialog if the Page has a XamlRoot (is in the visual tree).
            // If XamlRoot is null we are likely still constructing/navigating — avoid calling ShowAsync.
            if (this.XamlRoot == null)
                return;

            // Inform user they need restart for Mica change
            var dialog = new ContentDialog
            {
                Title = "Restart needed",
                Content = "Mica changes will apply next time you start the app.",
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SettingsService.GetDataFolder();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private async void ResetData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Reset all data?",
                Content = "This will reset total time and all app usage tracking.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            SessionTimerService.Reset();
            AppTrackerService.Reset();
        }
    }
}
using Microsoft.UI.Xaml;

namespace UsageTimerWinUI.Services
{
    public static class ThemeHelper
    {
        public static void ApplyTheme(Window window)
        {
            if (window.Content is not FrameworkElement root)
                return;

            root.RequestedTheme = SettingsService.Theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default // System
            };
        }
    }
}
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UsageTimerWinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            GlobalCrashLogger.Init();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "UsageTimerWinUI", "crash.txt"),
                    e.ExceptionObject.ToString());
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                File.WriteAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "UsageTimerWinUI", "crash_async.txt"),
                    e.Exception.ToString());
            };

        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }

    public static class GlobalCrashLogger
    {
        static GlobalCrashLogger()
        {
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                try
                {
                    string folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "UsageTimerWinUI");

                    Directory.CreateDirectory(folder);
                    Debug.WriteLine($"SAVING TO: {folder}");


                    File.AppendAllText(
                        System.IO.Path.Combine(folder, "firstchance_log.txt"),
                        $"{DateTime.Now}: {e.Exception}\n\n");
                }
                catch
                {
                    
                }
            };
        }

        public static void Init()
        {
            
        }
    }


}

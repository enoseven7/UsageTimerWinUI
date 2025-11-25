using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;

namespace UsageTimerWinUI.Services
{
    public static class StartupManager
    {
        private const string TaskId = "AppStartup";

        public static async Task<bool> IsEnabledAsync()
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            return startupTask.State == StartupTaskState.Enabled;
        }

        public static async Task<StartupTaskState> RequestEnableAsync()
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            return await startupTask.RequestEnableAsync();
        }

        public static async Task DisableAsync()
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            startupTask.Disable();
        }
    }
}

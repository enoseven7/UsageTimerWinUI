using System;
using System.Runtime.InteropServices;

namespace UsageTimerWinUI.Helpers
{
    internal static class Win32Helpers
    {
        private const uint WM_CLOSE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

        public static void SendClose(nint hwnd)
        {
            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }
}

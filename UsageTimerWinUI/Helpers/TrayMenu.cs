using System;
using System.Runtime.InteropServices;

namespace UsageTimerWinUI.Helpers
{
    public static class TrayMenu
    {
        private const int MF_STRING = 0x0000;
        private const int TPM_RIGHTBUTTON = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(nint hWnd);


        public static void Show(nint hwnd)
        {
            IntPtr menu = CreatePopupMenu();

            AppendMenu(menu, MF_STRING, 1, "Open UsageTimer");
            AppendMenu(menu, MF_STRING, 2, "Exit");

            // Get screen cursor position (physical pixels)
            GetCursorPos(out POINT p);

            double scale = GetScaleForWindow(hwnd);

            // Convert to scaled coordinates
            int x = (int)(p.X / scale);
            int y = (int)(p.Y / scale);

            uint selected = TrackPopupMenu(
                menu,
                TPM_RIGHTBUTTON,
                x,
                y,
                0,
                hwnd,
                IntPtr.Zero);

            switch (selected)
            {
                case 1:
                    WinUIOpen(hwnd);
                    break;

                case 2:
                    Environment.Exit(0);
                    break;
            }
        }

        private static double GetScaleForWindow(nint hwnd)
        {
            uint dpi = GetDpiForWindow(hwnd);
            return dpi / 96.0;
        }

        private static void WinUIOpen(nint hwnd)
        {
            // clicking tray restores window anyway
        }
    }
}

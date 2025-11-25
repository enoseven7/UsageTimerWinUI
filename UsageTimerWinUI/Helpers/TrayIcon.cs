using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace UsageTimerWinUI.Helpers
{
    public sealed class TrayIcon : IDisposable
    {
        private const uint WM_APP = 0x8000;
        private const uint WM_TRAY = WM_APP + 1;

        private readonly nint _hwnd;
        private readonly uint _id = 1;
        private readonly WndProc _newWndProc;
        private readonly nint _oldWndProc;

        [DllImport("user32.dll")]
        private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, WndProc newProc);

        [DllImport("user32.dll")]
        private static extern nint CallWindowProc(nint prev, nint hWnd, uint msg, nint wParam, nint lParam);

        private const int GWLP_WNDPROC = -4;

        public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public nint hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public nint hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

        private const uint NIM_ADD = 0x0;
        private const uint NIM_DELETE = 0x2;
        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;

        public TrayIcon(Window window, Icon icon)
        {
            _hwnd = WindowNative.GetWindowHandle(window);

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = _id,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAY,
                hIcon = icon.Handle,
                szTip = "UsageTimer"
            };

            Shell_NotifyIcon(NIM_ADD, ref data);

            _newWndProc = WndProcInternal;
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _newWndProc);
        }

        private nint WndProcInternal(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == WM_TRAY)
            {
                int code = (int)lParam;

                if (code == 0x0202) // left click
                {
                    var mainWindow = Application.Current as App;
                    mainWindow?._window?.Activate();
                }
                else if (code == 0x0205) // right click
                {
                    TrayMenu.Show(_hwnd);
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = _id
            };

            Shell_NotifyIcon(NIM_DELETE, ref data);
        }
    }
}

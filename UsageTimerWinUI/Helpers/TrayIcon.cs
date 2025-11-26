using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;

namespace UsageTimerWinUI.Helpers
{
    /// <summary>
    /// Safe tray icon that uses its own hidden native window, 
    /// does NOT touch WinUI's WndProc or use WinForms.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private const uint WM_APP = 0x8000;
        private const uint WM_TRAY = WM_APP + 1;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        private const uint NIM_ADD = 0x0;
        private const uint NIM_DELETE = 0x2;
        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;

        private const int MF_STRING = 0x0000;
        private const int TPM_RIGHTBUTTON = 0x0002;

        private readonly IntPtr _hwnd;
        private readonly uint _id = 1;
        private readonly Action _onOpen;
        private readonly Action _onExit;

        // Static stuff for the hidden window class
        private static readonly Dictionary<IntPtr, TrayIcon> s_instances = new();
        private static bool s_classRegistered;
        private static readonly string s_className = "UsageTimerTrayWindowClass";

        private static WndProcDelegate? s_wndProcDelegate;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public TrayIcon(Icon icon, string tooltip, Action onOpen, Action onExit)
        {
            _onOpen = onOpen;
            _onExit = onExit;

            EnsureWindowClassRegistered();

            var hInstance = GetModuleHandle(null);

            _hwnd = CreateWindowEx(
                0,
                s_className,
                string.Empty,
                0, // no style, invisible helper window
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create tray helper window.");

            s_instances[_hwnd] = this;

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = _id,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAY,
                hIcon = icon.Handle,
                szTip = tooltip ?? "UsageTimer"
            };

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private static void EnsureWindowClassRegistered()
        {
            if (s_classRegistered) return;

            s_wndProcDelegate = TrayWndProc;

            var wcex = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = string.Empty,
                lpszClassName = s_className,
                hIconSm = IntPtr.Zero
            };

            ushort atom = RegisterClassEx(ref wcex);
            if (atom == 0)
            {
                throw new InvalidOperationException("Failed to register tray window class.");
            }

            s_classRegistered = true;
        }

        private static IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (s_instances.TryGetValue(hWnd, out var instance))
            {
                return instance.InstanceWndProc(msg, wParam, lParam);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private IntPtr InstanceWndProc(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAY && wParam == (IntPtr)_id)
            {
                int ev = lParam.ToInt32();

                if (ev == WM_LBUTTONUP)
                {
                    _onOpen?.Invoke();
                    return 1;
                }
                else if (ev == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                    return 1;
                }

                return IntPtr.Zero;
            }

            return DefWindowProc(_hwnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            IntPtr menu = CreatePopupMenu();

            AppendMenu(menu, MF_STRING, 1, "Open UsageTimer");
            AppendMenu(menu, MF_STRING, 2, "Exit");

            GetCursorPos(out POINT p);

            uint cmd = TrackPopupMenu(
                menu,
                TPM_RIGHTBUTTON,
                p.X,
                p.Y,
                0,
                _hwnd,
                IntPtr.Zero);

            switch (cmd)
            {
                case 1:
                    _onOpen?.Invoke();
                    break;
                case 2:
                    _onExit?.Invoke();
                    break;
            }
        }

        public void Dispose()
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = _id
            };

            Shell_NotifyIcon(NIM_DELETE, ref data);

            s_instances.Remove(_hwnd);
            DestroyWindow(_hwnd);
        }
    }
}

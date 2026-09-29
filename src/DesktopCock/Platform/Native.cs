using System;
using System.Runtime.InteropServices;

namespace DesktopCock;
internal static partial class Native
{
    internal static long ExtendedStyle(IntPtr window) => GetWindowLongPtr(window,-20).ToInt64();
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Time; }
    [StructLayout(LayoutKind.Sequential)] private struct AppBar { public uint Size; public IntPtr Window; public uint Message, Edge; public Rect Bounds; public IntPtr Param; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref AppBar data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr SetCapture(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);

    internal static void Configure(IntPtr window)
    {
        long style = GetWindowLongPtr(window, -20).ToInt64();
        SetWindowLongPtr(window, -20, new IntPtr((style | 0x08000000L | 0x80L) & ~0x40000L));
    }
    internal static double IdleSeconds()
    {
        var info = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.Time)/1000.0 : 0;
    }
    internal static (Rect Screen, int Perch) Desktop()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if(!GetMonitorInfo(MonitorFromPoint(new Point(), 1), ref info))
        {
            var fallback=new Rect { Right=Math.Max(64,GetSystemMetrics(0)), Bottom=Math.Max(64,GetSystemMetrics(1)) };
            return (fallback,fallback.Bottom);
        }
        int perch = info.Work.Bottom;
        var bar = new AppBar { Size = (uint)Marshal.SizeOf<AppBar>() };
        if (SHAppBarMessage(5, ref bar) != UIntPtr.Zero && bar.Edge == 3)
        {
            perch = bar.Bounds.Top;
            if ((SHAppBarMessage(4, ref bar).ToUInt64() & 1) != 0)
            {
                var taskbar = FindWindow("Shell_TrayWnd", null);
                if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var live) || live.Top >= info.Monitor.Bottom-3)
                    perch = info.Monitor.Bottom;
                else perch = live.Top;
            }
        }
        return (info.Monitor, Math.Clamp(perch, info.Monitor.Top+64, info.Monitor.Bottom));
    }
    internal static bool FullScreen(IntPtr own, Rect screen)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == own || fg == GetShellWindow() || fg == FindWindow("Progman", null) || fg == FindWindow("WorkerW", null) || !IsWindowVisible(fg) || IsIconic(fg)) return false;
        return GetWindowRect(fg, out var r) && r.Left <= screen.Left && r.Top <= screen.Top && r.Right >= screen.Right && r.Bottom >= screen.Bottom;
    }
}

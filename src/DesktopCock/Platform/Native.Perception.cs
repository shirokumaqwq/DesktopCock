using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DesktopCock.Core;

namespace DesktopCock;

internal sealed record DesktopWindow(long Id, Native.Rect Bounds, uint ProcessId);
internal static partial class Native
{
    private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);
    [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int DwmGetWindowFlag(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll")] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int size, out int needed);
    internal static bool DesktopAvailable()
    {
        var desktop = OpenInputDesktop(0, false, 0x0001);
        if (desktop == IntPtr.Zero) return false;
        try
        {
            var name=new StringBuilder(256);
            return GetUserObjectInformation(desktop,2,name,512,out _) && name.ToString().Equals("Default",StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseDesktop(desktop); }
    }
    internal static void ClickThrough(IntPtr hwnd, bool value)
    {
        var style = ExtendedStyle(hwnd);
        var desired = value ? style | 0x20L : style & ~0x20L;
        if (desired != style) SetWindowLongPtr(hwnd, -20, new IntPtr(desired));
    }
    internal static IReadOnlyList<DesktopWindow> Windows()
    {
        var result = new List<DesktopWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
            // Desktop overlays (capture controls, HUDs, the pet) can have a full-
            // screen bounding box while all or most of their pixels are transparent.
            if ((ExtendedStyle(hwnd) & 0x20L) != 0) return true;
            if (DwmGetWindowFlag(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            GetWindowThreadProcessId(hwnd, out uint process);
            if (DwmGetWindowAttribute(hwnd, 9, out var bounds, Marshal.SizeOf<Rect>()) != 0 &&
                !GetWindowRect(hwnd, out bounds)) return true;
            if (bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
                result.Add(new(hwnd.ToInt64(), bounds, process));
            return true;
        }, IntPtr.Zero);
        return result;
    }
    internal static bool Contains(Rect r, double x, double y) => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
    internal static DesktopWindow? OwnerAt(IReadOnlyList<DesktopWindow> windows, double x, double y)
    {
        foreach (var window in windows)
            if (window.ProcessId != Environment.ProcessId && Contains(window.Bounds,x,y)) return window;
        return null;
    }
    internal static bool VisibleSupport(PerchTarget target, IReadOnlyList<DesktopWindow> windows)
    {
        foreach (double x in new[] { target.MinX, (target.MinX+target.MaxX)/2, target.MaxX })
            if (OwnerAt(windows, x, target.Y+3)?.Id != target.WindowId) return false;
        return true;
    }
}

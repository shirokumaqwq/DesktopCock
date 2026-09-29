using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopCock.Core;

namespace DesktopCock;

// Observe mode keys without consuming typing. Never save cursor-theme settings.
internal sealed class HandInput : IDisposable
{
    private static readonly uint[] CursorIds = [32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650];
    private readonly Dictionary<uint, IntPtr> originals = new();
    private readonly HashSet<int> down = [];
    private readonly Hook callback;
    private readonly Dispatcher dispatcher;
    private readonly Action<HandMode> changed;
    private readonly object gate = new();
    private IntPtr hook;
    private HandMode mode;
    private int scale;
    private bool disposed;

    internal HandInput(Dispatcher dispatcher, Action<HandMode> changed)
    {
        this.dispatcher = dispatcher; this.changed = changed;
        callback = OnKeyboard;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
    }
    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !disposed)
        {
            int key = Marshal.ReadInt32(data);
            if (key is 0x31 or 0x32 or 0x61 or 0x62 or 0xC0)
            {
                if (message.ToInt64() is 0x101 or 0x105) down.Remove(key);
                else if (message.ToInt64() is 0x100 or 0x104 && down.Add(key) && AltOnly())
                {
                    var next = key is 0x31 or 0x61 ? HandMode.Perch : key is 0x32 or 0x62 ? HandMode.Pet : HandMode.None;
                    dispatcher.BeginInvoke(new Action(() => { if (!disposed) changed(next); }));
                }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    private static bool AltOnly() => (Native.GetAsyncKeyState(0x12) & 0x8000) != 0 &&
        (Native.GetAsyncKeyState(0x10) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x11) & 0x8000) == 0 &&
        (Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0;

    internal void Apply(HandMode next, int pixelScale)
    {
        lock (gate)
        {
            if (disposed || (mode == next && scale == pixelScale)) return;
            if (next == HandMode.None) { Restore(); return; }
            IntPtr cursor = CreateHandCursor(next, pixelScale);
            try
            {
                if (originals.Count == 0)
                    foreach (uint id in CursorIds)
                    {
                        IntPtr copy = CopyImage(LoadCursor(IntPtr.Zero, new IntPtr(id)), 2, 0, 0, 0);
                        if (copy == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                        originals.Add(id, copy);
                    }
                foreach (uint id in CursorIds)
                {
                    IntPtr copy = CopyImage(cursor, 2, 0, 0, 0);
                    if (copy == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (!SetSystemCursor(copy, id))
                    { DestroyCursor(copy); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                }
                mode = next; scale = pixelScale;
            }
            catch { Restore(); throw; }
            finally { DestroyCursor(cursor); }
        }
    }
    private static IntPtr CreateHandCursor(HandMode mode, int scale)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Hands",
            mode == HandMode.Perch ? "glove-perch-pixel-v2.png" : "glove-pet-pixel-v2.png"));
        bitmap.EndInit(); bitmap.Freeze();
        var source = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
        int width = source.PixelWidth * scale, height = source.PixelHeight * scale;
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        var enlarged = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Buffer.BlockCopy(pixels, ((y / scale) * source.PixelWidth + x / scale) * 4, enlarged, (y * width + x) * 4, 4);
        var header = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
        IntPtr color = CreateDIBSection(IntPtr.Zero, ref header, 0, out var bits, IntPtr.Zero, 0);
        IntPtr mask = IntPtr.Zero;
        try
        {
            if (color == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            Marshal.Copy(enlarged, 0, bits, enlarged.Length);
            mask = CreateBitmap(width, height, 1, 1, new byte[((width + 15) / 16) * 2 * height]);
            if (mask == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            // Hotspots: flat finger top / underside of the downward fingertip.
            var info = new IconInfo { X = (uint)((mode == HandMode.Perch ? 18 : 7) * scale),
                Y = (uint)((mode == HandMode.Perch ? 4 : 34) * scale), Color = color, Mask = mask };
            var result = CreateIconIndirect(ref info);
            if (result == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            return result;
        }
        finally { if (color != IntPtr.Zero) DeleteObject(color); if (mask != IntPtr.Zero) DeleteObject(mask); }
    }
    private void Restore()
    {
        bool failed = false;
        foreach (var (id, original) in originals)
            if (!SetSystemCursor(original, id)) { DestroyCursor(original); failed = true; }
        originals.Clear(); mode = HandMode.None; scale = 0;
        if (failed) SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);
    }
    private void OnExit(object? sender, EventArgs e) { lock (gate) Restore(); }
    private void OnUnhandled(object sender, UnhandledExceptionEventArgs e) { lock (gate) Restore(); }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            Restore();
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
        }
    }
    private delegate IntPtr Hook(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo { public int Icon; public uint X, Y; public IntPtr Mask, Color; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int X, Y; public uint Used, Important; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, Hook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CopyImage(IntPtr image, uint type, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetSystemCursor(IntPtr cursor, uint id);
    [DllImport("user32.dll")] private static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconIndirect(ref IconInfo info);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bits, byte[] pixels);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
}

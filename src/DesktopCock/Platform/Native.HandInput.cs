using System;
using System.Runtime.InteropServices;

namespace DesktopCock;

internal static partial class Native
{
    [StructLayout(LayoutKind.Sequential)] private struct RawInputDevice
    { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInputHeader
    { public uint Type, Size; public IntPtr Device, Parameter; }
    [StructLayout(LayoutKind.Sequential)] private struct RawMouse
    { public ushort Flags; public uint Buttons, RawButtons; public int X, Y; public uint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct RawMouseInput
    { public RawInputHeader Header; public RawMouse Mouse; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(
        ref RawInputDevice devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(
        IntPtr input, uint command, out RawMouseInput data, ref uint size, uint headerSize);

    // Background mouse notifications only; do not suppress normal app input.
    internal static bool TrackHandMouse(IntPtr target)
    {
        var device = new RawInputDevice { Page = 1, Usage = 2,
            Flags = target == IntPtr.Zero ? 1u : 0x100u, Target = target };
        return RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }
    internal static bool ReadHandMouse(IntPtr input)
    {
        uint size = (uint)Marshal.SizeOf<RawMouseInput>();
        return GetRawInputData(input, 0x10000003, out var data, ref size,
            (uint)Marshal.SizeOf<RawInputHeader>()) == size && data.Header.Type == 0;
    }
}

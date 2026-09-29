using System;
using System.Windows;

namespace DesktopCock;
public sealed partial class PetWindow
{
    private static bool Inside(int[] box, double px, double py) => px >= box[0] && px < box[0]+box[2] && py >= box[1] && py < box[1]+box[3];
    private (double X,double Y) Local(Native.Point p, int direction)
    {
        double px = (p.X-left+.5)/Settings.Scale, py = (p.Y-top+.5)/Settings.Scale;
        return (direction == 1 ? px : sprite.Width-px,py);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0xFF && rawHandMouse && Native.ReadHandMouse(lParam))
        {
            FollowHandPosition();
            // Leave WM_INPUT unhandled so the default window procedure cleans it up.
        }
        if (message == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (message == 0x84)
        {
            var packed = lParam.ToInt64();
            var cursor = new Native.Point { X=unchecked((short)(packed & 0xffff)), Y=unchecked((short)((packed >> 16) & 0xffff)) };
            var (px,py) = Local(cursor,RenderDirection);
            if (controller.Interactions.Active || flight.Airborne || !sprite.Opaque((int)Math.Floor(px),(int)Math.Floor(py))) { handled = true; return new IntPtr(-1); }
            handled = true; return new IntPtr(1); // Explicit HTCLIENT for the visible sprite.
        }
        if (message == 0x201 && !paused && !flight.Airborne)
        {
            clicked = true; held = true; OnDirectInteraction(); controller.Interactions.BeginGesture();
            gestureDirection = Bird.Direction;
            var box = sprite.Spec.Head; gestureHead = new Rect(box[0]-2,box[1]-2,box[2]+4,box[3]+4);
            Native.SetCapture(handle); handled = true;
        }
        if (message == 0x202) { held = false; controller.Interactions.EndGesture(); Native.ReleaseCapture(); handled = true; }
        if (message == 0x215) { held = false; controller.Interactions.EndGesture(); }
        if (message is 0x7E or 0x2E0 or 0x218 or 0x1A) { nextDesktopCheck = 0; hadMouse = false; }
        return IntPtr.Zero;
    }
}

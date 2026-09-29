using System;
using System.Windows.Media;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;

public sealed partial class PetWindow
{
    private HandInput? handInput;
    private bool followingHand, rawHandMouse;
    private void InitializeHands()
    {
        try { handInput = new HandInput(Dispatcher, SetHandMode); }
        catch (System.ComponentModel.Win32Exception e)
        { tray.ShowBalloonTip(3000, "手型快捷键未启用", e.Message, Forms.ToolTipIcon.Warning); }
    }
    private void SetHandMode(HandMode mode)
    {
        UpdateRuntimeScene(); controller.SetHandMode(mode); SyncHandCursor();
        timer.Interval = TimeSpan.FromMilliseconds(16);
    }
    private void SyncHandCursor()
    {
        SyncHandTracking();
        try { handInput?.Apply(controller.Interactions.Mode, Settings.Scale); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or System.IO.IOException or NotSupportedException)
        {
            controller.SetHandMode(HandMode.None); handInput?.Apply(HandMode.None, Settings.Scale);
            tray.ShowBalloonTip(3000, "手型未加载", e.Message, Forms.ToolTipIcon.Warning);
        }
    }
    private void SyncHandTracking()
    {
        bool follow = controller.OnHand && !paused && FlightVisible && IsVisible;
        if (follow == followingHand) return;
        followingHand = follow;
        if (follow)
        {
            rawHandMouse = Native.TrackHandMouse(handle);
            CompositionTarget.Rendering += FollowHandBeforeRender;
        }
        else
        {
            CompositionTarget.Rendering -= FollowHandBeforeRender;
            if (rawHandMouse) Native.TrackHandMouse(IntPtr.Zero);
            rawHandMouse = false;
        }
    }
    private void FollowHandBeforeRender(object? sender, EventArgs e) => FollowHandPosition();
    private void FollowHandPosition()
    {
        if (!followingHand || paused || !FlightVisible || !IsVisible) return;
        if (!Native.GetCursorPos(out var cursor)) return;
        // No behavior time, animation, audio, or perception work in the input path.
        // Always sample the latest OS position instead of replaying queued deltas.
        controller.UpdateHand(0, cursor.X, cursor.Y);
        if (!controller.OnHand) { SyncHandTracking(); return; }
        x = flight.Position.X;
        Position();
    }
    private void CloseHands()
    {
        CompositionTarget.Rendering -= FollowHandBeforeRender;
        if (rawHandMouse) Native.TrackHandMouse(IntPtr.Zero);
        followingHand = rawHandMouse = false;
        handInput?.Dispose();
    }
    private bool HandAtHead(Native.Point cursor)
    {
        // Fixed to the standing pose so lowering the head cannot lose contact.
        var standing = animations.Get(Mood.Idle, 0).Spec;
        double localX = (cursor.X - flight.Position.X) / Settings.Scale * Bird.Direction + standing.Foot[0];
        double localY = (cursor.Y - flight.Position.Y) / Settings.Scale + standing.Foot[1];
        var head = standing.Head;
        return localX >= head[0] - 9 && localX <= head[0] + head[2] + 9 &&
            localY >= head[1] - 10 && localY <= head[1] + head[3] + 3;
    }
}

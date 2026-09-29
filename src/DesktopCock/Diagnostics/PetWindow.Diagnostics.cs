using System;
using System.Collections.Generic;
using System.Windows.Interop;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;

public sealed partial class PetWindow
{
    private PreviewWindow? preview;
    private PreviewSession? previewSession;
    private FlightDebugWindow? flightOverlay;
    private Forms.ToolStripMenuItem? debugItem;
    private double nextDiagnosticRefresh;
    internal bool SimulateNight { get; set; }
    internal bool SimulateIdle { get; set; }
    internal bool FlightDebugEnabled => flightOverlay != null;
    internal Native.Rect FlightScreen => screen;
    internal long StandingOn => flight.PlatformId;
    internal long FlyingTo => flight.Airborne ? flight.TargetId : -1;
    internal GroundSnapshot DisplayedGround => PresentationSnapshot.Ground;
    private double nextStatus;
    private string cachedStatus = "";
    internal string FlightStatus
    {
        get
        {
            if (MonotonicNow < nextStatus) return cachedStatus;
            nextStatus = MonotonicNow+.25;
            cachedStatus = $"{flight.State} · {Scene.Status} · {Scene.Platforms.Count} 个平台\n"+
                $"GPU 检测 {Metric(PerformanceMetric.GpuDetection)} / 追踪 {Metric(PerformanceMetric.GpuTracking)}\n"+
                $"CPU 主循环 {Metric(PerformanceMetric.MainLoopCpu)} / 采集 {Metric(PerformanceMetric.CaptureCpu)}\n"+
                $"调度延迟 {Metric(PerformanceMetric.SchedulerDelay)} / 感知年龄 {Metric(PerformanceMetric.SnapshotAge)}\n"+
                controller.LastMovementMessage;
            return cachedStatus;
        }
    }
    private static string Metric(PerformanceMetric metric)
    {
        var sample = PerformanceTrace.Read(metric);
        return sample.Count == 0 ? "—" : $"{sample.Latest:0.00} ms (P95 {sample.P95:0.00})";
    }
    private void SyncExternalControl() => controller.SetExternalControl(FlightDebugEnabled || previewSession != null, previewSession != null);
    partial void AddDiagnosticsMenu(Forms.ContextMenuStrip menu)
    {
        menu.Items.Add("动作预览 / 调试", null, (_,_) => OpenPreview());
        debugItem = new("飞行 Debug", null, (_,_) => SetFlightDebug(!FlightDebugEnabled));
        menu.Items.Add(debugItem);
    }
    public void OpenPreview()
    {
        if (preview == null)
        {
            preview = new PreviewWindow(this);
            preview.Closed += (_,_) => { preview = null; SimulateNight = SimulateIdle = false; ResumeNaturalActivity(); };
        }
        preview.Show(); preview.Activate();
    }
    internal void PreviewAction(Mood mood, Gaze gaze = Gaze.Level, HeadYaw head = HeadYaw.Forward, bool turn = false)
    {
        controller.Interactions.CancelGesture();
        previewSession = new(Settings.Behavior, mood, RenderDirection, gaze, head, turn);
        SyncExternalControl(); nextDiagnosticRefresh = 0;
    }
    internal void ResumeNaturalActivity()
    {
        previewSession = null; controller.Interactions.CancelGesture(); Bird.Resume(); SyncExternalControl();
        nextDiagnosticRefresh = 0;
    }
    internal void SetFlightDebug(bool enabled)
    {
        if (enabled == FlightDebugEnabled) return;
        if (enabled)
        {
            var overlay = new FlightDebugWindow(this); overlay.Show();
            if (!overlay.CaptureExcluded)
            {
                overlay.Close();
                tray.ShowBalloonTip(4000, "飞行 Debug 不可用", "无法排除覆盖层捕获或安装选点监听。", Forms.ToolTipIcon.Warning); return;
            }
            flightOverlay = overlay;
            if (!FlightVisible) overlay.Hide();
        }
        else { flightOverlay?.Close(); flightOverlay = null; }
        if (debugItem != null) debugItem.Checked = enabled;
        SyncExternalControl(); nextDiagnosticRefresh = 0; RefreshDiagnostics();
    }
    partial void CloseDiagnostics()
    { flightOverlay?.Close(); flightOverlay = null; preview?.Close(); }
    partial void RefreshDiagnostics()
    {
        if (MonotonicNow < nextDiagnosticRefresh) return;
        nextDiagnosticRefresh = MonotonicNow+.25; preview?.Refresh();
    }
    partial void OnDirectInteraction() => SimulateIdle = false;
    partial void AdjustInput(ref DateTime localTime, ref double idle)
    {
        if (SimulateNight) localTime = DateTime.Today.AddHours(23);
        if (SimulateIdle) idle = (SimulateNight ? Settings.Behavior.NightSleepSeconds : Settings.Behavior.DaySleepSeconds)+10;
    }
    partial void AdvanceOverride(double dt, ref bool handled)
    {
        if (previewSession == null) return;
        double movement = previewSession.Tick(dt)*Settings.Scale;
        double proposed = flight.Position.X+movement;
        controller.WalkOnPlatform(movement);
        if (controller.ActivePlatform is { } p && (proposed < p.MinX || proposed > p.MaxX)) previewSession.TurnAtEdge();
        handled = true;
    }
    partial void OverridePresentation(ref PetSnapshot snapshot)
    {
        if (previewSession != null && !snapshot.Airborne)
            snapshot = snapshot with { Ground = previewSession.Snapshot, Direction = previewSession.Snapshot.Direction };
    }
    partial void ShowDiagnosticsSprite(Sprite value, int direction, bool singing) => preview?.ShowSprite(value, direction, singing);
    partial void AdjustTickInterval(ref double interval) { if (FlightDebugEnabled) interval = 16; }
    partial void OnManualFlight() { SimulateNight = SimulateIdle = false; ResumeNaturalActivity(); }
    partial void AddPerceptionExclusions(List<Native.Rect> rectangles)
    {
        if (preview is { IsVisible: true } && Native.GetWindowRect(new WindowInteropHelper(preview).Handle, out var bounds)) rectangles.Add(bounds);
    }
    partial void UpdateDiagnosticsVisibility()
    {
        if (flightOverlay == null) return;
        if (FlightVisible && !flightOverlay.IsVisible) flightOverlay.Show();
        if (!FlightVisible && flightOverlay.IsVisible) flightOverlay.Hide();
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;
public sealed partial class PetWindow
{
    private PerceptionService? perception;
    private Forms.ToolStripMenuItem? exploreItem;
    private bool sessionLocked;
    private FlightController flight => controller.Flight;
    private bool flightInitialized => controller.Initialized;
    internal bool FlightVisible => !userHidden && !fullScreen && !sessionLocked;
    private static double MonotonicNow => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private PerceptionSnapshot Scene => perception?.Snapshot ?? PerceptionSnapshot.Empty;
    internal IReadOnlyList<PerchTarget> LandingCandidates => controller.LandingCandidates;
    private int RenderDirection => PresentationSnapshot.Direction;
    private void UpdateRuntimeScene()
    {
        var s = Scene;
        controller.UpdateEnvironment(new(screen.Left, screen.Top, screen.Right, screen.Bottom), perch, MonotonicNow,
            new(s.Timestamp, s.Platforms, s.Tracked, s.TrackingLost, s.TrackRevision));
    }
    private void AddFlightMenu(Forms.ContextMenuStrip menu)
    {
        exploreItem = new("自主飞行", null, (_,_) => SetAutoExplore(!Settings.AutoExploreEnabled)) { Checked = Settings.AutoExploreEnabled };
        menu.Items.Add(exploreItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("探索间隔…", null, (_,_) => ShowExploreSettings()));
    }
    private void InitializeFlight()
    {
        UpdateRuntimeScene(); controller.Initialize(x); x = flight.Position.X; perception = new();
    }
    private void CloseFlight() { perception?.Dispose(); perception = null; }
    private void SetPetScale(int value)
    {
        Settings.Scale = value; SaveSettings(); nextDesktopCheck = 0;
        controller.Configure(Settings.RuntimeOptions);
    }
    internal void SetAutoExplore(bool value)
    {
        Settings.AutoExploreEnabled = value;
        if (exploreItem != null) exploreItem.Checked = value;
        controller.Configure(Settings.RuntimeOptions); SaveSettings();
    }
    internal FlightRequestResult RequestFlight(long targetId, double anchorX, FlightSource source)
    {
        UpdateRuntimeScene(); controller.SetLifecycle(FlightVisible, paused);
        var result = controller.RequestFlight(targetId, anchorX, source);
        if (result.Accepted && source == FlightSource.Manual) OnManualFlight();
        return result;
    }
    private void UpdatePerception()
    {
        if (!flightInitialized || perception == null) return;
        UpdateRuntimeScene(); controller.SetLifecycle(FlightVisible, paused);
        var excluded = new List<Native.Rect>(); AddPerceptionExclusions(excluded);
        var mask = new BirdMask(left, top, sprite.Width, sprite.Height, Settings.Scale, RenderDirection, sprite.Pixels);
        perception.Update(new(controller.ShouldPerceive, screen, Settings.Scale, mask, excluded.ToArray(),
            controller.ActivePlatform?.Id > 0 ? controller.ActivePlatform : null, controller.TrackAnchorX, controller.TrackRevision));
        UpdateDiagnosticsVisibility();
    }
    partial void OnManualFlight();
    partial void AddPerceptionExclusions(List<Native.Rect> rectangles);
    partial void UpdateDiagnosticsVisibility();
    internal void ShowExploreSettings()
    {
        var dialog=new Window { Title="探索间隔",Width=340,Height=190,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen };
        var panel=new StackPanel { Margin=new Thickness(20) };
        panel.Children.Add(new TextBlock { Text="每隔多少秒考虑换个地方（5–3600）" });
        var value=new TextBox { Text=Settings.ExploreIntervalSeconds.ToString("0",CultureInfo.InvariantCulture),Margin=new Thickness(0,12,0,8) };
        var error=new TextBlock { Foreground=System.Windows.Media.Brushes.Firebrick };
        var save=new Button { Content="保存",Padding=new Thickness(12,5,12,5),HorizontalAlignment=HorizontalAlignment.Right };
        save.Click+=(_,_) =>
        {
            if(!double.TryParse(value.Text,out double seconds) || !double.IsFinite(seconds) || seconds<5 || seconds>3600)
            { error.Text="请输入 5–3600 之间的秒数";return; }
            Settings.ExploreIntervalSeconds=seconds;controller.Configure(Settings.RuntimeOptions);SaveSettings();dialog.Close();
        };
        panel.Children.Add(value);panel.Children.Add(error);panel.Children.Add(save);dialog.Content=panel;dialog.ShowDialog();
    }
}

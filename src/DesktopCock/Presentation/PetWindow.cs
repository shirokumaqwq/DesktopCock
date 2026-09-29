using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCock.Core;
using Forms = System.Windows.Forms;

namespace DesktopCock;
public sealed partial class PetWindow : Window
{
    public static string DataFolder => SettingsStore.DataFolder;
    internal readonly Settings Settings;
    internal readonly Behavior Bird;
    private readonly PetController controller;
    private readonly AnimationBank animations = new();
    private readonly Image image = new() { Stretch = Stretch.Fill, RenderTransformOrigin = new Point(.5,.5), IsHitTestVisible = false };
    private readonly TextBlock notes = new() { Text = "♪", Foreground = new SolidColorBrush(Color.FromRgb(244,194,77)), FontSize = 9, FontWeight = FontWeights.Bold, IsHitTestVisible = false };
    private readonly Canvas canvas = new() { Width = 64, Height = 64, Background = null };
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem visibilityItem, pauseItem;
    private readonly Dictionary<string, Forms.ToolStripMenuItem> skinItems = [];
    private readonly DispatcherTimer timer = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Sprite sprite;
    private IntPtr handle;
    private HwndSource? source;
    private double previousTime, nextDesktopCheck, x = double.NaN;
    private Native.Rect screen;
    private int perch, left, top;
    private int positionedLeft = int.MinValue, positionedTop = int.MinValue, positionedScale;
    private uint positionedDpi;
    private int positionedWidth, positionedHeight;
    private bool userHidden, fullScreen, paused, held, clicked;
    private double previousMouseX, previousMouseY;
    private bool hadMouse;
    private Rect gestureHead;
    private int gestureDirection;
    internal bool IsPaused => paused;
    internal IReadOnlyList<SkinOption> Skins => animations.Skins;
    internal string CurrentSkin => animations.CurrentSkin;
    internal string EnvironmentStatus => $"{Settings.Scale} 倍像素 · {(sessionLocked ? "锁屏隐藏" : fullScreen ? "全屏隐藏" : userHidden ? "手动隐藏" : flight.Airborne ? "飞行中" : flight.PlatformId==0 ? "主屏任务栏" : "平台停栖")}";

    public PetWindow()
    {
        Settings = SettingsStore.Load();
        Bird = new Behavior(Settings.Behavior, wakeStretchSeconds: animations.DurationSeconds(Mood.Stretch));
        controller = new PetController(Bird, Settings.RuntimeOptions, support: p => Native.VisibleSupport(p, Native.Windows()));
        controller.Interactions.GestureCancelled += ReleaseGesture;
        animations.SelectSkin(Settings.Skin); Settings.Skin = animations.CurrentSkin;
        sprite = animations.Get(Mood.Idle,0);
        Title = "玄凤 · DesktopCock"; Width = Height = 192; WindowStyle = WindowStyle.None;
        AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        image.Width = image.Height = 64;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        canvas.Children.Add(image); canvas.Children.Add(notes); Canvas.SetLeft(notes,49); Canvas.SetTop(notes,12);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = canvas };
        tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory,"Assets","pet.ico")), Text = "玄凤 · DesktopCock", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        visibilityItem = new Forms.ToolStripMenuItem("隐藏玄凤", null, (_,_) => { userHidden = !userHidden; visibilityItem!.Text = userHidden ? "显示玄凤" : "隐藏玄凤"; SyncVisibility(); });
        pauseItem = new Forms.ToolStripMenuItem("暂停活动", null, (_,_) => SetPaused(!paused));
        menu.Items.Add(visibilityItem); menu.Items.Add(pauseItem);
        var size = new Forms.ToolStripMenuItem("大小");
        foreach (int value in new[] { 2,3,4 })
        {
            var item = new Forms.ToolStripMenuItem($"{value} 倍", null, (_,_) => SetPetScale(value));
            size.DropDownItems.Add(item);
        }
        menu.Items.Add(size);
        var skinMenu = new Forms.ToolStripMenuItem("羽色");
        foreach (var skin in animations.Skins)
        {
            var item = new Forms.ToolStripMenuItem(skin.Name, null, (_,_) => SetSkin(skin.Id)) { Checked = skin.Id == CurrentSkin };
            skinItems[skin.Id] = item; skinMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(skinMenu); AddAudioMenu(menu); AddFlightMenu(menu); AddDiagnosticsMenu(menu);
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_,_) => Application.Current.Shutdown());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_,_) => { userHidden = false; SyncVisibility(); };
        SourceInitialized += (_,_) =>
        {
            handle = new WindowInteropHelper(this).Handle; Native.Configure(handle);
            source = HwndSource.FromHwnd(handle); source.AddHook(WindowMessage);
            RefreshDesktop(); InitializeFlight(); InitializeHands(); Render(); Position();
        };
        timer.Interval = TimeSpan.FromMilliseconds(33); timer.Tick += Tick; timer.Start();
        Closed += (_,_) => { timer.Stop(); CloseHands(); CloseAudio(); CloseFlight(); source?.RemoveHook(WindowMessage); Native.ReleaseCapture(); CloseDiagnostics(); tray.Visible = false; tray.Dispose(); };
    }
    private void SaveSettings()
    {
        try { SettingsStore.Save(Settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { tray.ShowBalloonTip(3000, "设置未保存", e.Message, Forms.ToolTipIcon.Warning); }
    }
    private void ReleaseGesture() { held = clicked = false; hadMouse = false; Native.ReleaseCapture(); }
    internal void SetPaused(bool value)
    {
        paused = value; controller.SetLifecycle(FlightVisible, value); pauseItem.Text = paused ? "继续活动" : "暂停活动";
        controller.Interactions.Exit();
        SyncHandCursor();
        SyncAudioLifecycle();
    }
    internal void SetSkin(string id)
    {
        animations.SelectSkin(id);
        Settings.Skin = CurrentSkin;
        foreach (var (key, item) in skinItems) item.Checked = key == CurrentSkin;
        Render(); RefreshDiagnostics(); SaveSettings();
    }
    private void SyncVisibility()
    {
        if (userHidden || fullScreen || sessionLocked)
        { controller.Interactions.Exit(); Hide(); }
        else if (!IsVisible) Show();
        controller.SetLifecycle(FlightVisible, paused);
        SyncHandCursor();
        visibilityItem.Text = userHidden ? "显示玄凤" : "隐藏玄凤";
        SyncAudioLifecycle();
    }
    private void RefreshDesktop()
    {
        (screen,perch) = Native.Desktop();
        if (double.IsNaN(x)) x = screen.Left + (screen.Right-screen.Left)*.7;
        var locked=!Native.DesktopAvailable();
        var nowFullScreen = Native.FullScreen(handle,screen);
        if (fullScreen != nowFullScreen || locked!=sessionLocked) { fullScreen = nowFullScreen;sessionLocked=locked; SyncVisibility(); }
        UpdateRuntimeScene();
        if (flightInitialized) x = flight.Position.X;
    }

    private void Position()
    {
        if (handle == IntPtr.Zero) return;
        var scale = Settings.Scale;
        double anchorY=flightInitialized?flight.Position.Y:perch;
        double footX=RenderDirection==1?sprite.Spec.Foot[0]:sprite.Width-sprite.Spec.Foot[0];
        left = (int)Math.Round(x-footX*scale); top = (int)Math.Round(anchorY-sprite.Spec.Foot[1]*scale);
        uint dpi = Native.GetDpiForWindow(handle);
        if (left == positionedLeft && top == positionedTop && Settings.Scale == positionedScale && dpi == positionedDpi &&
            positionedWidth==sprite.Width && positionedHeight==sprite.Height) return;
        bool resize = scale != positionedScale || dpi != positionedDpi ||
            positionedWidth != sprite.Width || positionedHeight != sprite.Height;
        if (resize)
        {
            Width=sprite.Width*scale*96.0/Math.Max(96,dpi);Height=sprite.Height*scale*96.0/Math.Max(96,dpi);
            Native.SetWindowPos(handle,new IntPtr(-1),left,top,sprite.Width*scale,sprite.Height*scale,0x0010 | 0x0040);
        }
        else Native.SetWindowPos(handle,IntPtr.Zero,left,top,0,0,0x0001 | 0x0004 | 0x0010);
        positionedLeft = left; positionedTop = top; positionedScale = scale; positionedDpi = dpi;
        positionedWidth=sprite.Width;positionedHeight=sprite.Height;
    }
    private void Tick(object? sender, EventArgs args)
    {
#if DEBUG
        long started = Stopwatch.GetTimestamp();
        var delay = clock.Elapsed.TotalSeconds-previousTime-timer.Interval.TotalSeconds;
        PerformanceTrace.Record(PerformanceMetric.SchedulerDelay, Math.Max(0, delay*1000));
        if (Scene.Timestamp > 0) PerformanceTrace.Record(PerformanceMetric.SnapshotAge, Math.Max(0, MonotonicNow-Scene.Timestamp)*1000);
        try { TickCore(sender, args); }
        finally { PerformanceTrace.Elapsed(PerformanceMetric.MainLoopCpu, started); }
#else
        TickCore(sender, args);
#endif
    }
    private void TickCore(object? sender, EventArgs args)
    {
        double now = clock.Elapsed.TotalSeconds, dt = now-previousTime; previousTime = now;
        if (handle == IntPtr.Zero) return;
        if (now >= nextDesktopCheck) { RefreshDesktop(); nextDesktopCheck = now+.5; }
        UpdatePerception();
        controller.SetLifecycle(FlightVisible, paused);
        SyncHandCursor();
        UpdateVoice();
        if (paused || !FlightVisible)
        { timer.Interval = TimeSpan.FromMilliseconds(250); hadMouse = false; RefreshDiagnostics(); return; }
        Native.GetCursorPos(out var cursor);
        controller.UpdateHand(dt, cursor.X, cursor.Y);
        SyncHandTracking();
        if(controller.AdvanceMovement(dt, Native.IdleSeconds()))
        {
            UpdateVoice();
            x = flight.Position.X; Render();Position();UpdatePerception();RefreshDiagnostics();hadMouse=false;
            timer.Interval=TimeSpan.FromMilliseconds(16);return;
        }
        x = flight.Position.X; Position();
        var (mx,my) = Local(cursor,Bird.Direction);
        double speed = hadMouse && dt < .5 ? Math.Sqrt(Math.Pow(cursor.X-previousMouseX,2)+Math.Pow(cursor.Y-previousMouseY,2))/Settings.Scale/Math.Max(dt,.001) : 0;
        previousMouseX = cursor.X; previousMouseY = cursor.Y; hadMouse = true;
        if (held && (Native.GetAsyncKeyState(1) & 0x8000) == 0) { held = false; controller.Interactions.EndGesture(); Native.ReleaseCapture(); }
        bool atHead = Inside(sprite.Spec.Head,mx,my);
        if (controller.Interactions.Mode == HandMode.Pet) atHead = HandAtHead(cursor);
        if (held)
        {
            var gp = Local(cursor,gestureDirection);
            atHead |= gestureHead.Contains(new Point(gp.X,gp.Y));
        }
        bool near = mx >= -24 && mx <= 88 && my >= -48 && my <= 74;
        var localTime = DateTime.Now;
        double idle = Native.IdleSeconds(); AdjustInput(ref localTime, ref idle);
        // Freeze facing during a head gesture so the interaction region cannot jump away.
        int direction = held && atHead ? gestureDirection : cursor.X >= x ? 1 : -1;
        var interaction = InteractionMapper.Map(near, atHead, Inside(sprite.Spec.Feet,mx,my), held, clicked,
            speed, direction, (cursor.X-x)/Settings.Scale, my-28, Settings.Behavior, controller.Interactions);
        bool overridden = false; AdvanceOverride(dt, ref overridden);
        if (!overridden) controller.AdvanceGround(dt, new Input(localTime, idle, interaction));
        UpdateVoice(interaction.Direct || (Bird.State == Mood.Sing && near));
        TickLearning();
        clicked = false; x = flight.Position.X;
        Render();
        if (controller.OnHand) FollowHandPosition(); else Position();
        UpdatePerception();RefreshDiagnostics();
        double interval = flight.PlatformId != 0 ? 16 : Bird.State == Mood.Sleep ? 100 : Bird.State == Mood.Idle && !near ? 50 : 33;
        AdjustTickInterval(ref interval); timer.Interval = TimeSpan.FromMilliseconds(interval);
        if (audio.HasSession) timer.Interval = TimeSpan.FromMilliseconds(16);
        if (controller.Interactions.Active) timer.Interval = TimeSpan.FromMilliseconds(16);
    }
    private void Render()
    {
        var snapshot = PresentationSnapshot;
        sprite = animations.Get(snapshot, voicePlayback);
        canvas.Width=image.Width=sprite.Width;canvas.Height=image.Height=sprite.Height;
        Native.ClickThrough(handle,flight.Airborne || controller.Interactions.Active);
        if (!ReferenceEquals(image.Source,sprite.Bitmap)) image.Source = sprite.Bitmap;
        if (image.RenderTransform is not ScaleTransform transform || transform.ScaleX != RenderDirection)
            image.RenderTransform = new ScaleTransform(RenderDirection,1);
        notes.Visibility = !snapshot.Airborne && snapshot.Ground.State == Mood.Sing && voicePlayback.Playing ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(notes,snapshot.Direction == 1 ? 51 : 5);
        Canvas.SetTop(notes,11-(int)(snapshot.Ground.StateAge*4)%5);
        ShowDiagnosticsSprite(sprite, RenderDirection, !snapshot.Airborne && snapshot.Ground.State == Mood.Sing);
    }
    private PetSnapshot PresentationSnapshot
    {
        get { var snapshot = controller.Snapshot; OverridePresentation(ref snapshot); return snapshot; }
    }
    partial void AddDiagnosticsMenu(Forms.ContextMenuStrip menu);
    partial void CloseDiagnostics();
    partial void RefreshDiagnostics();
    partial void OnDirectInteraction();
    partial void AdjustInput(ref DateTime localTime, ref double idle);
    partial void AdvanceOverride(double dt, ref bool handled);
    partial void OverridePresentation(ref PetSnapshot snapshot);
    partial void ShowDiagnosticsSprite(Sprite value, int direction, bool singing);
    partial void AdjustTickInterval(ref double interval);
}

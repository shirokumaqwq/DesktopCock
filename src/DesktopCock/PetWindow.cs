using System;
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
    public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCock");
    internal readonly Settings Settings;
    internal readonly Behavior Bird;
    private readonly AnimationBank animations = new();
    private readonly Image image = new() { Stretch = Stretch.Fill, RenderTransformOrigin = new Point(.5,.5), IsHitTestVisible = false };
    private readonly TextBlock notes = new() { Text = "♪", Foreground = new SolidColorBrush(Color.FromRgb(244,194,77)), FontSize = 9, FontWeight = FontWeights.Bold, IsHitTestVisible = false };
    private readonly Canvas canvas = new() { Width = 64, Height = 64, Background = null };
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem visibilityItem, pauseItem;
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
    private bool userHidden, fullScreen, paused, held, clicked;
    private double previousMouseX, previousMouseY;
    private bool hadMouse;
    private Rect gestureHead;
    private int gestureDirection;
    private PreviewWindow? preview;
    internal bool SimulateNight { get; set; }
    internal bool SimulateIdle { get; set; }
    internal bool IsPaused => paused;
    internal string EnvironmentStatus => $"{Settings.Scale} 倍像素 · {(fullScreen ? "全屏隐藏" : userHidden ? "手动隐藏" : "主屏任务栏")}";

    public PetWindow()
    {
        Settings = LoadSettings(); Bird = new Behavior(Settings);
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
            var item = new Forms.ToolStripMenuItem($"{value} 倍", null, (_,_) => { Settings.Scale = value; SaveSettings(); nextDesktopCheck = 0; });
            size.DropDownItems.Add(item);
        }
        menu.Items.Add(size); menu.Items.Add("动作预览 / 调试", null, (_,_) => OpenPreview());
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_,_) => Application.Current.Shutdown());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_,_) => { userHidden = false; SyncVisibility(); OpenPreview(); };
        SourceInitialized += (_,_) =>
        {
            handle = new WindowInteropHelper(this).Handle; Native.Configure(handle);
            source = HwndSource.FromHwnd(handle); source.AddHook(WindowMessage);
            RefreshDesktop(); Render(); Position();
        };
        timer.Interval = TimeSpan.FromMilliseconds(33); timer.Tick += Tick; timer.Start();
        Closed += (_,_) => { timer.Stop(); source?.RemoveHook(WindowMessage); Native.ReleaseCapture(); preview?.Close(); tray.Visible = false; tray.Dispose(); };
    }
    private Settings LoadSettings()
    {
        try
        {
            var path = Path.Combine(DataFolder,"settings.json");
            var settings = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings() : new Settings();
            settings.Validate(); return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(Path.Combine(DataFolder,"error.log"), $"{DateTime.Now:O} Settings reset: {e.Message}\n");
            return new Settings();
        }
    }
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var path = Path.Combine(DataFolder,"settings.json");
            File.WriteAllText(path+".tmp", JsonSerializer.Serialize(Settings,new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path+".tmp",path,true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { tray.ShowBalloonTip(3000,"设置未保存",e.Message,Forms.ToolTipIcon.Warning); }
    }
    internal void SetPaused(bool value)
    {
        paused = value; pauseItem.Text = paused ? "继续活动" : "暂停活动";
        held = clicked = false; Native.ReleaseCapture();
    }
    public void OpenPreview()
    {
        if (preview == null)
        {
            preview = new PreviewWindow(this);
            preview.Closed += (_,_) => { preview = null; SimulateNight = SimulateIdle = false; Bird.Resume(); };
        }
        preview.Show(); preview.Activate();
    }
    private void SyncVisibility()
    {
        if (userHidden || fullScreen)
        { held = clicked = false; Native.ReleaseCapture(); Hide(); }
        else if (!IsVisible) Show();
        visibilityItem.Text = userHidden ? "显示玄凤" : "隐藏玄凤";
    }
    private void RefreshDesktop()
    {
        (screen,perch) = Native.Desktop();
        if (double.IsNaN(x)) x = screen.Left + (screen.Right-screen.Left)*.7;
        var nowFullScreen = Native.FullScreen(handle,screen);
        if (fullScreen != nowFullScreen) { fullScreen = nowFullScreen; SyncVisibility(); }
    }
    private void Position()
    {
        if (handle == IntPtr.Zero) return;
        var scale = Settings.Scale;
        x = Math.Clamp(x,screen.Left+32*scale,Math.Max(screen.Left+32*scale,screen.Right-32*scale));
        left = (int)Math.Round(x-32*scale); top = perch-sprite.Spec.Foot[1]*scale;
        uint dpi = Native.GetDpiForWindow(handle);
        if (left == positionedLeft && top == positionedTop && Settings.Scale == positionedScale && dpi == positionedDpi) return;
        Width = Height = 64*scale*96.0/Math.Max(96,dpi);
        Native.SetWindowPos(handle,new IntPtr(-1),left,top,64*scale,64*scale,0x0010 | 0x0040);
        positionedLeft = left; positionedTop = top; positionedScale = scale; positionedDpi = dpi;
    }
    private static bool Inside(int[] box, double px, double py) => px >= box[0] && px < box[0]+box[2] && py >= box[1] && py < box[1]+box[3];
    private (double X,double Y) Local(Native.Point p, int direction)
    {
        double px = (p.X-left+.5)/Settings.Scale, py = (p.Y-top+.5)/Settings.Scale;
        return (direction == 1 ? px : 64-px,py);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (message == 0x84)
        {
            var packed = lParam.ToInt64();
            var cursor = new Native.Point { X=unchecked((short)(packed & 0xffff)), Y=unchecked((short)((packed >> 16) & 0xffff)) };
            var (px,py) = Local(cursor,Bird.Direction);
            if (!sprite.Opaque((int)Math.Floor(px),(int)Math.Floor(py))) { handled = true; return new IntPtr(-1); }
            handled = true; return new IntPtr(1); // Explicit HTCLIENT for the visible sprite.
        }
        if (message == 0x201 && !paused)
        {
            clicked = true; held = true; SimulateIdle = false;
            gestureDirection = Bird.Direction;
            var box = sprite.Spec.Head; gestureHead = new Rect(box[0]-2,box[1]-2,box[2]+4,box[3]+4);
            Native.SetCapture(handle); handled = true;
        }
        if (message == 0x202) { held = false; Native.ReleaseCapture(); handled = true; }
        if (message == 0x215) held = false;
        if (message is 0x7E or 0x2E0 or 0x218 or 0x1A) { nextDesktopCheck = 0; hadMouse = false; }
        return IntPtr.Zero;
    }
    private void Tick(object? sender, EventArgs args)
    {
        double now = clock.Elapsed.TotalSeconds, dt = now-previousTime; previousTime = now;
        if (handle == IntPtr.Zero) return;
        if (now >= nextDesktopCheck) { RefreshDesktop(); nextDesktopCheck = now+.5; }
        if (!userHidden && !fullScreen) Position();
        if (paused || userHidden || fullScreen)
        { timer.Interval = TimeSpan.FromMilliseconds(250); hadMouse = false; preview?.Refresh(); return; }
        Native.GetCursorPos(out var cursor);
        var (mx,my) = Local(cursor,Bird.Direction);
        double speed = hadMouse && dt < .5 ? Math.Sqrt(Math.Pow(cursor.X-previousMouseX,2)+Math.Pow(cursor.Y-previousMouseY,2))/Settings.Scale/Math.Max(dt,.001) : 0;
        previousMouseX = cursor.X; previousMouseY = cursor.Y; hadMouse = true;
        if (held && (Native.GetAsyncKeyState(1) & 0x8000) == 0) { held = false; Native.ReleaseCapture(); }
        bool atHead = Inside(sprite.Spec.Head,mx,my);
        if (held)
        {
            var gp = Local(cursor,gestureDirection);
            atHead |= gestureHead.Contains(new Point(gp.X,gp.Y));
        }
        bool near = mx >= -24 && mx <= 88 && my >= -16 && my <= 74;
        var localTime = SimulateNight ? DateTime.Today.AddHours(23) : DateTime.Now;
        double idle = SimulateIdle ? (SimulateNight ? Settings.NightSleepSeconds : Settings.DaySleepSeconds)+10 : Native.IdleSeconds();
        // Freeze facing during a head gesture so the interaction region cannot jump away.
        int direction = held && atHead ? gestureDirection : cursor.X >= x ? 1 : -1;
        Bird.Tick(dt,new Input(localTime,idle,near,atHead,Inside(sprite.Spec.Feet,mx,my),held,clicked,speed,direction));
        clicked = false;
        x += Bird.Movement*Settings.Scale;
        if (x < screen.Left+32*Settings.Scale || x > screen.Right-32*Settings.Scale) Bird.TurnAtEdge();
        Render(); Position(); preview?.Refresh();
        timer.Interval = TimeSpan.FromMilliseconds(Bird.State == Mood.Sleep ? 250 : Bird.State == Mood.Idle && !near ? 100 : 33);
    }
    private void Render()
    {
        sprite = animations.Get(Bird.State,Bird.StateAge);
        if (!ReferenceEquals(image.Source,sprite.Bitmap)) image.Source = sprite.Bitmap;
        if (image.RenderTransform is not ScaleTransform transform || transform.ScaleX != Bird.Direction)
            image.RenderTransform = new ScaleTransform(Bird.Direction,1);
        notes.Visibility = Bird.State == Mood.Sing ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(notes,Bird.Direction == 1 ? 51 : 5);
        Canvas.SetTop(notes,11-(int)(Bird.StateAge*4)%5);
        preview?.ShowSprite(sprite.Bitmap,Bird.Direction,Bird.State == Mood.Sing);
    }
}

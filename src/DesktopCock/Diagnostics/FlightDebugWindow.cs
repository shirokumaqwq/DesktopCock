using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCock.Core;

namespace DesktopCock;

// The window is always click-through. A short-lived low-level mouse hook consumes
// ONLY an Alt+left click in a legal target strip, including its matching button-up.
// This also works across process boundaries, unlike HTTRANSPARENT alone.
internal sealed class FlightDebugWindow : Window
{
    private readonly PetWindow pet;
    private readonly OverlayVisual visual;
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(33) };
    private readonly MouseHook callback;
    private IntPtr hook,handle;
    private Native.Rect positioned;
    private uint positionedDpi;
    private bool consuming,disposed;
    internal bool CaptureExcluded { get; private set; }
    private delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Native.Point Point; public uint Mouse,Flags,Time; public UIntPtr Extra; }
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int type,MouseHook callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    internal FlightDebugWindow(PetWindow pet)
    {
        this.pet=pet;visual=new(pet);Content=visual;callback=OnMouse;
        Title="玄凤 · 飞行 Debug";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
        Topmost=true;Focusable=false;
        SourceInitialized+=(_,_) =>
        {
            handle=new WindowInteropHelper(this).Handle;Native.Configure(handle);Native.ClickThrough(handle,true);
            CaptureExcluded=OperatingSystem.IsWindowsVersionAtLeast(10,0,19041) && Native.SetWindowDisplayAffinity(handle,0x11);
            if(CaptureExcluded) hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) CaptureExcluded=false;
        };
        timer.Tick+=(_,_) =>
        {
            if(!IsVisible || !pet.FlightVisible) return;
            var r=pet.FlightScreen;
            uint dpi=Native.GetDpiForWindow(handle);
            if(!positioned.Equals(r)||positionedDpi!=dpi)
            {
                double factor=96.0/Math.Max(96,dpi);
                Width=(r.Right-r.Left)*factor;Height=(r.Bottom-r.Top)*factor;
                Native.SetWindowPos(handle,new IntPtr(-1),r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top,0x0010);
                positioned=r;positionedDpi=dpi;
            }
            visual.InvalidateVisual();
        };
        Loaded+=(_,_)=>timer.Start();
        Closed+=(_,_)=> { disposed=true;timer.Stop();if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook);hook=IntPtr.Zero; };
    }
    private IntPtr OnMouse(int code,IntPtr message,IntPtr data)
    {
        if(code>=0 && !disposed)
        {
            int kind=message.ToInt32();
            if(kind==0x202 && consuming) { consuming=false;return new IntPtr(1); }
            if(kind==0x201 && (Native.GetAsyncKeyState(0x12)&0x8000)!=0 && IsVisible && pet.FlightVisible)
            {
                var mouse=Marshal.PtrToStructure<MouseData>(data);
                // Hit-test what was actually painted. The shared request then
                // revalidates the latest snapshot; a stale highlight is consumed
                // and rejected, never leaked as a click into the app underneath.
                var target=visual.TargetAt(mouse.Point.X,mouse.Point.Y);
                if(target!=null)
                {
                    consuming=true;
                    long id=target.Id;double x=mouse.Point.X;
                    Dispatcher.BeginInvoke(new Action(()=>pet.RequestFlight(id,x,FlightSource.Manual)));
                    return new IntPtr(1);
                }
            }
        }
        return CallNextHookEx(hook,code,message,data);
    }
    private sealed class OverlayVisual(PetWindow pet) : FrameworkElement
    {
        private PerchTarget[] drawnTargets=[];
        internal PerchTarget? TargetAt(double x,double y) => drawnTargets
            .Where(p=>x>=p.MinX-5 && x<=p.MaxX+5 && Math.Abs(y-p.Y)<=9)
            .OrderBy(p=>Math.Abs(y-p.Y)).ThenByDescending(p=>p.Confidence).FirstOrDefault();
        protected override void OnRender(DrawingContext dc)
        {
            var r=pet.FlightScreen;double dip=VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double sx=ActualWidth/Math.Max(1,r.Right-r.Left),sy=ActualHeight/Math.Max(1,r.Bottom-r.Top);
            if(sx<=0||sy<=0) return;
            dc.PushTransform(new ScaleTransform(sx,sy));
            dc.PushTransform(new TranslateTransform(-r.Left,-r.Top));
            Native.GetCursorPos(out var cursor);
            bool alt=(Native.GetAsyncKeyState(0x12)&0x8000)!=0;
            drawnTargets=pet.LandingCandidates.ToArray();
            var hovered=alt?TargetAt(cursor.X,cursor.Y):null;
            foreach(var p in drawnTargets)
            {
                Color color=p.Id==pet.FlyingTo?Colors.Orange:p.Id==pet.StandingOn?Colors.DeepSkyBlue:Colors.LimeGreen;
                var brush=new SolidColorBrush(color);
                var fill=new SolidColorBrush(Color.FromArgb(75,color.R,color.G,color.B));
                dc.DrawRoundedRectangle(fill,null,new Rect(p.MinX,p.Y-5,Math.Max(1,p.MaxX-p.MinX),10),3,3);
                dc.DrawLine(new Pen(brush,2),new Point(p.MinX,p.Y),new Point(p.MaxX,p.Y));
                dc.DrawEllipse(brush,null,new Point(p.MinX,p.Y),3,3);
                dc.DrawEllipse(brush,null,new Point(p.MaxX,p.Y),3,3);
                var label=new FormattedText(p.Id==0?"任务栏":$"#{p.Id}",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"),12/sx,brush,dip);
                dc.DrawText(label,new Point(p.MinX,Math.Max(r.Top,p.Y-label.Height-8)));
            }
            if(hovered!=null)
            {
                double x=hovered.ClampX(cursor.X),s=pet.Settings.Scale;
                dc.DrawRectangle(null,new Pen(Brushes.Orange,2),new Rect(x-32*s,hovered.Y-56*s,64*s,56*s));
                dc.DrawEllipse(Brushes.Orange,null,new Point(x,hovered.Y),5,5);
            }
            var text=new FormattedText("飞行 Debug · 按住 Alt 点击绿色区间试飞"+Environment.NewLine+pet.FlightStatus,
                CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12/sx,Brushes.White,dip);
            text.MaxTextWidth=Math.Min(680/sx,(r.Right-r.Left)-40);
            var panel=new Rect(r.Left+16,r.Top+16,text.Width+20,text.Height+16);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(215,23,33,30)),null,panel,6,6);
            dc.DrawText(text,new Point(panel.Left+10,panel.Top+8));
            dc.Pop();dc.Pop();
        }
    }
}

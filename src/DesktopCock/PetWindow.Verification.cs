using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DesktopCock.Core;

namespace DesktopCock;
public sealed partial class PetWindow
{
    // A local integration harness: no input injection, OS settings changes or other app controls.
    internal async Task VerifyDesktop()
    {
        timer.Stop();
        var checks = new List<object>();
        int failures = 0, originalScale = Settings.Scale;
        var foreground = Native.Foreground;
        void Check(string name, bool pass, object? detail = null)
        { checks.Add(new { name,pass,detail }); if (!pass) failures++; }
        try
        {
            long styles = Native.ExtendedStyle(handle);
            Check("NoActivate style",(styles & 0x08000000) != 0);
            Check("Tool window, excluded from taskbar",(styles & 0x80) != 0 && (styles & 0x40000) == 0);
            Check("Mouse activation is declined",Native.SendMessage(handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt64() == 3);
            foreach(int scale in new[] {2,3,4})
            {
                Settings.Scale = scale;
                foreach(int direction in new[] { 1,-1 })
                {
                if (Bird.Direction != direction) Bird.TurnAtEdge();
                foreach(var mood in Enum.GetValues<Mood>())
                {
                    Bird.Force(mood); Bird.Tick(.05, new Input(DateTime.Now,0,false,false,false,false,false,0,1));
                    Render(); Position(); await Task.Delay(180);
                    Native.GetWindowRect(handle,out var bounds);
                    Check($"{scale}x {mood}: physical dimensions",bounds.Right-bounds.Left == 64*scale && bounds.Bottom-bounds.Top == 64*scale,new { bounds.Left,bounds.Top,bounds.Right,bounds.Bottom });
                    Check($"{scale}x {mood}: perch anchor",bounds.Top+sprite.Spec.Foot[1]*scale == perch);
                    Check($"{scale}x {mood}: transparent corner passes through",Native.WindowFromPoint(new Native.Point { X=bounds.Left+scale,Y=bounds.Top+scale }) != handle);
                    var opaque = (from py in Enumerable.Range(35,16) from px in Enumerable.Range(23,23) where sprite.Opaque(px,py) select (px,py)).First();
                    var target = Native.WindowFromPoint(new Native.Point { X=bounds.Left+(direction==1 ? opaque.px : 63-opaque.px)*scale+scale/2,Y=bounds.Top+opaque.py*scale+scale/2 });
                    Check($"{scale}x {mood} direction {direction}: visible bird accepts input",target==handle, new { target=target.ToInt64(),expected=handle.ToInt64() });
                }
                }
            }
            Check("Rendering and resizing preserve foreground",Native.Foreground == foreground);
        }
        catch(Exception e) { Check("Harness completed",false,e.ToString()); }
        finally
        {
            Settings.Scale = originalScale;
            var report = new { created=DateTime.Now, dpi=Native.GetDpiForWindow(handle), failures, checks };
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"verification"));
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"verification","desktop.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
            Application.Current.Shutdown(failures==0 ? 0 : 1);
        }
    }
}

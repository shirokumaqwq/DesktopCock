using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using DesktopCock.Core;

namespace DesktopCock;

internal sealed class PerceptionService : IDisposable
{
    private readonly Thread worker;
    private readonly ManualResetEvent stop=new(false);
    private PerceptionInput input=new(false,default,3,null,[],null,0,0);
    private PerceptionSnapshot snapshot=PerceptionSnapshot.Empty;
    internal PerceptionSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal void Update(PerceptionInput value) => Volatile.Write(ref input,value);
    internal PerceptionService()
    {
        worker=new Thread(Run) { IsBackground=true, Name="DesktopCock GPU perception" }; worker.Start();
    }
    private static double Now => Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    private void Run()
    {
        GpuVision? gpu=null;
        var catalog=new PlatformCatalog();
        Native.Rect captureBounds=default;
        PerchTarget? tracked=null;
        DesktopWindow? owner=null;
        long sequence=0, revision=-1;
        double nextDetect=0, nextTrack=0, lastMatch=0, retryAt=0, nextDesktopCheck=0, detectionMs=0,trackingMs=0;
        double referenceX=0, referenceY=0, pendingX=0,pendingY=0;
        bool referenceReady=false, trackingLost=false, desktopAvailable=true, matchHealthy=true, trackDirty=false;
        int failures=0;
        IReadOnlyList<PerchTarget> candidates=Array.Empty<PerchTarget>();
        var submittedWindows=(IReadOnlyList<DesktopWindow>)Array.Empty<DesktopWindow>();
        try
        {
            while(!stop.WaitOne(tracked==null?50:8))
            {
                var current=Volatile.Read(ref input);
                double now=Now;
                if(now>=nextDesktopCheck) { desktopAvailable=Native.DesktopAvailable();nextDesktopCheck=now+.5; }
                if(!current.Enabled || !desktopAvailable)
                {
                    if(gpu!=null) { gpu.Dispose();gpu=null; catalog.Clear(); }
                    tracked=null;referenceReady=false;revision=-1;candidates=Array.Empty<PerchTarget>();
                    Publish(new(++sequence,now,candidates,null,false,desktopAvailable?"感知已暂停":"锁屏：感知已暂停",0,0));
                    continue;
                }
                if(now<retryAt) continue;
                try
                {
                    if(gpu==null || !captureBounds.Equals(current.Screen))
                    {
                        gpu?.Dispose();gpu=null;catalog.Clear();candidates=Array.Empty<PerchTarget>();
                        gpu=new(current.Screen);captureBounds=current.Screen;
                        tracked=null;referenceReady=false;revision=-1;nextDetect=0;
                    }
                    gpu.Configure(current);
#if DEBUG
                    long enumerationStarted = Stopwatch.GetTimestamp();
#endif
                    var windows=Native.Windows();
#if DEBUG
                    PerformanceTrace.Elapsed(PerformanceMetric.WindowEnumerationCpu, enumerationStarted);
                    long captureStarted = Stopwatch.GetTimestamp();
#endif
                    bool changed=(tracked!=null || !gpu.HasFrame || now>=nextDetect) && gpu.Acquire();
#if DEBUG
                    PerformanceTrace.Elapsed(PerformanceMetric.CaptureCpu, captureStarted);
#endif
                    trackDirty|=changed;
                    if(current.Revision!=revision)
                    {
                        // Drain any result belonging to the previous target before replacing its template.
                        if(gpu.TrackingPending) { gpu.PollTracking(out _); if(gpu.TrackingPending) continue; }
                        revision=current.Revision;tracked=current.Track;trackingLost=false;referenceReady=false;matchHealthy=true;
                        owner=tracked==null?null:windows.FirstOrDefault(w=>w.Id==tracked.WindowId);
                        lastMatch=now;
                        if(tracked!=null && gpu.HasFrame)
                        {
                            float patchWidth=(float)Math.Min(400,tracked.Right-tracked.Left-4);
                            referenceX=Math.Clamp(current.AnchorX,tracked.Left+patchWidth/2,tracked.Right-patchWidth/2);referenceY=tracked.Y;
                            gpu.SaveReference((float)(referenceX-current.Screen.Left),(float)(referenceY-current.Screen.Top),current.Scale,patchWidth);
                            referenceReady=true;
                        }
                    }
                    if(tracked!=null && !trackingLost)
                    {
                        var nextOwner=windows.FirstOrDefault(w=>w.Id==tracked.WindowId);
                        if(nextOwner==null || owner==null || nextOwner.ProcessId!=owner.ProcessId)
                            trackingLost=true;
                        else
                        {
                            double dx=nextOwner.Bounds.Left-owner.Bounds.Left,dy=nextOwner.Bounds.Top-owner.Bounds.Top;
                            bool resized=nextOwner.Bounds.Right-nextOwner.Bounds.Left!=owner.Bounds.Right-owner.Bounds.Left ||
                                nextOwner.Bounds.Bottom-nextOwner.Bounds.Top!=owner.Bounds.Bottom-owner.Bounds.Top;
                            tracked=Shift(tracked,dx,dy,tracked.ObservedAt);referenceX+=dx;referenceY+=dy;
                            owner=nextOwner;
                            if(resized || !InsideScreen(tracked,current.Screen,current.Scale) || !Native.VisibleSupport(tracked,windows))
                                trackingLost=true;
                        }
                        var match=gpu.PollTracking(out double ms);
                        if(match.HasValue && !trackingLost)
                        {
                            trackingMs=ms;
#if DEBUG
                            if (ms > 0) PerformanceTrace.Record(PerformanceMetric.GpuTracking, ms);
#endif
                            var m=match.Value;
                            // Flat horizontal surfaces have ambiguous X, but must still agree in Y.
                            bool good=m.Z<.065f && (m.W>.0003f || Math.Abs(m.Y-pendingY)<3);
                            matchHealthy=good;
                            if(good)
                            {
                                double dx=m.X-pendingX,dy=m.Y-pendingY;
                                tracked=Shift(tracked,dx,dy,now);referenceX+=dx;referenceY+=dy;lastMatch=now;
                                if(!InsideScreen(tracked,current.Screen,current.Scale) || !Native.VisibleSupport(tracked,windows))
                                    trackingLost=true;
                            }
                        }
                        if(!changed && !gpu.TrackingPending && !trackingLost && matchHealthy)
                            lastMatch=now; // Unchanged desktop is positive evidence, not a missing observation.
                        if(now-lastMatch>.25) trackingLost=true;
                        if(trackDirty && now>=nextTrack && referenceReady && !trackingLost && !gpu.TrackingPending)
                        {
                            pendingX=referenceX-current.Screen.Left;pendingY=referenceY-current.Screen.Top;
                            gpu.Track((float)pendingX,(float)pendingY);
                            nextTrack=now+1.0/60;trackDirty=false;
                        }
                    }
                    var detected=gpu.PollDetection(out double detectionTime);
                    if(detected!=null)
                    {
                        detectionMs=detectionTime;
#if DEBUG
                        if (detectionTime > 0) PerformanceTrace.Record(PerformanceMetric.GpuDetection, detectionTime);
                        long postprocessStarted = Stopwatch.GetTimestamp();
#endif
                        var observations=Extract(detected,gpu,current,submittedWindows);
                        catalog.Observe(observations,now);
                        candidates=catalog.Get(now,28*current.Scale,58*current.Scale,current.Screen.Top);
#if DEBUG
                        PerformanceTrace.Elapsed(PerformanceMetric.PostprocessCpu, postprocessStarted);
#endif
                    }
                    // Cached GPU frame is also sampled on a static desktop to establish stability.
                    if(now>=nextDetect && !gpu.DetectionPending && gpu.HasFrame)
                    { submittedWindows=windows;gpu.Detect();nextDetect=now+.5; }
                    // Prevent stale visible debug targets between the slower global scans.
                    var visible=candidates.Where(p=>now-p.ObservedAt<=.8 && Native.VisibleSupport(p,windows)).ToArray();
                    if(tracked!=null && !trackingLost)
                    {
                        visible=visible.Where(p=>p.Id!=tracked.Id).Append(tracked with { ObservedAt=now }).ToArray();
                    }
                    Publish(new(++sequence,now,Array.AsReadOnly(visible),trackingLost?null:tracked,trackingLost,
                        trackingLost?"目标已失效":tracked!=null?"局部追踪":gpu.HasFrame?"GPU 搜索平台":"等待桌面帧",detectionMs,trackingMs,revision));
                    failures=0;
                }
                catch(Exception e)
                {
                    gpu?.Dispose();gpu=null;catalog.Clear();tracked=null;referenceReady=false;revision=-1;
                    candidates=Array.Empty<PerchTarget>();
                    retryAt=now+Math.Min(30,Math.Pow(2,Math.Min(++failures,5)));
                    Publish(new(++sequence,now,candidates,null,true,"感知不可用："+e.Message,0,0,current.Revision));
                }
            }
        }
        finally { gpu?.Dispose(); }
    }
    private static PerchTarget Shift(PerchTarget p,double dx,double dy,double now) => p with
    { Left=p.Left+dx,Right=p.Right+dx,MinX=p.MinX+dx,MaxX=p.MaxX+dx,Y=p.Y+dy,ObservedAt=now };
    private static bool InsideScreen(PerchTarget p,Native.Rect r,int scale) =>
        p.MinX>=r.Left+28*scale && p.MaxX<=r.Right-28*scale && p.Y>=r.Top+58*scale && p.Y<=r.Bottom;
    private static IEnumerable<PlatformObservation> Extract(Vector4[] lines,GpuVision gpu,PerceptionInput input,IReadOnlyList<DesktopWindow> windows)
    {
        var found=new List<PlatformObservation>();
        var distinct=new List<Vector4>();
        foreach(var line in lines.Where(l=>l.Y>l.X).OrderByDescending(l=>Math.Abs(l.W)))
            if(!distinct.Any(p=>Math.Abs(p.Z-line.Z)<4 && Math.Min(p.Y,line.Y)-Math.Max(p.X,line.X)>.6*Math.Min(p.Y-p.X,line.Y-line.X)))
                distinct.Add(line);
        // Opposite transitions with the same endpoints bound one rectangular strip:
        // keep its upper edge, instead of mistaking the strip's bottom for support.
        var paired=new HashSet<int>();
        distinct=distinct.OrderBy(p=>p.Z).ToList();
        for(int i=0;i<distinct.Count;i++)
        {
            if(paired.Contains(i)) continue;
            for(int j=i+1;j<distinct.Count;j++)
                if(!paired.Contains(j) && distinct[i].W*distinct[j].W<0 &&
                    Math.Abs(distinct[i].X-distinct[j].X)<5 && Math.Abs(distinct[i].Y-distinct[j].Y)<5)
                { paired.Add(j);break; }
        }
        foreach(var line in distinct.Where((_,i)=>!paired.Contains(i)).OrderByDescending(l=>Math.Abs(l.W)))
        {
            double left=input.Screen.Left+line.X*gpu.FactorX,right=input.Screen.Left+line.Y*gpu.FactorX;
            double y=input.Screen.Top+line.Z*gpu.FactorY;
            if(y<input.Screen.Top+58*input.Scale || right-left<56*input.Scale) continue;
            var window=Native.OwnerAt(windows,(left+right)/2,y+3);
            if(window==null) continue;
            left=Math.Max(left,window.Bounds.Left);right=Math.Min(right,window.Bounds.Right);
            if(right-left<56*input.Scale) continue;
            var p=new PerchTarget(0,left,right,y,left+28*input.Scale,right-28*input.Scale,Math.Abs(line.W),window.Id,0);
            if(!Native.VisibleSupport(p,windows)) continue;
            if(found.Any(f=>Math.Abs(f.Y-y)<5*gpu.FactorY && Math.Min(f.Right,right)-Math.Max(f.Left,left)>.6*Math.Min(f.Right-f.Left,right-left))) continue;
            found.Add(new(left,right,y,Math.Abs(line.W),window.Id,window.Bounds.Left,window.Bounds.Top));
            if(found.Count>=128) break;
        }
        return found;
    }
    private void Publish(PerceptionSnapshot value) => Volatile.Write(ref snapshot,value);
    public void Dispose() { stop.Set();worker.Join(2000);if(!worker.IsAlive) stop.Dispose(); }
}

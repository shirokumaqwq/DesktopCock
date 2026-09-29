using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace DesktopCock;

// Owned exclusively by the perception worker. No screen pixels are mapped to the CPU.
internal sealed class GpuVision : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Parameters
    {
        public Vector4 Size, Geometry, BirdRect, BirdInfo, CursorRect;
        public Vector4 Excluded0, Excluded1, Excluded2, Excluded3, Excluded4, Excluded5, Excluded6, Excluded7;
        public Vector4 Tracking, Prediction;
        public Vector4 CaptureInfo;
    }
    private sealed class Texture : IDisposable
    {
        public readonly ID3D11Texture2D Value;
        public readonly ID3D11ShaderResourceView View;
        public readonly ID3D11UnorderedAccessView? Output;
        public Texture(ID3D11Device device, int width, int height, Format format, bool output)
        {
            Value = device.CreateTexture2D(new Texture2DDescription {
                Width=(uint)width, Height=(uint)height, MipLevels=1, ArraySize=1,
                Format=format, SampleDescription=new(1,0), Usage=ResourceUsage.Default,
                BindFlags=BindFlags.ShaderResource | (output ? BindFlags.UnorderedAccess : BindFlags.None)
            });
            View = device.CreateShaderResourceView(Value);
            if(output) Output = device.CreateUnorderedAccessView(Value);
        }
        public void Dispose() { Output?.Dispose(); View.Dispose(); Value.Dispose(); }
    }
    private sealed class Readback : IDisposable
    {
        public readonly ID3D11Buffer Output, Staging;
        public readonly ID3D11UnorderedAccessView View;
#if DEBUG
        public readonly ID3D11Query Start, End, Disjoint;
#endif
        public readonly int Count;
        public bool Pending;
        public Readback(ID3D11Device device, int count)
        {
            Count=count;
            Output=device.CreateBuffer((uint)(count*16), BindFlags.UnorderedAccess, ResourceUsage.Default,
                CpuAccessFlags.None,ResourceOptionFlags.BufferStructured,16);
            View=device.CreateUnorderedAccessView(Output);
            Staging=device.CreateBuffer((uint)(count*16),BindFlags.None,ResourceUsage.Staging,CpuAccessFlags.Read);
#if DEBUG
            Start=device.CreateQuery(new(QueryType.Timestamp)); End=device.CreateQuery(new(QueryType.Timestamp));
            Disjoint=device.CreateQuery(new(QueryType.TimestampDisjoint));
#endif
        }
        public void Begin(ID3D11DeviceContext c)
        {
#if DEBUG
            c.Begin(Disjoint); c.End(Start);
#endif
        }
        public void Submit(ID3D11DeviceContext c)
        {
#if DEBUG
            c.End(End); c.End(Disjoint);
#endif
            c.CopyResource(Staging,Output); c.Flush(); Pending=true;
        }
        public unsafe Vector4[]? Poll(ID3D11DeviceContext c, out double milliseconds)
        {
            milliseconds=0;
            if(!Pending) return null;
            var result=c.Map(Staging,0,MapMode.Read,MapFlags.DoNotWait,out var mapped);
            if(result.Code==unchecked((int)0x887A000A)) return null;
            result.CheckError();
            var data=new Vector4[Count];
            try { new ReadOnlySpan<Vector4>((void*)mapped.DataPointer,Count).CopyTo(data); }
            finally { c.Unmap(Staging,0); }
#if DEBUG
            ulong start=0,end=0;
            TimestampDisjoint timing=default;
            if(c.GetData(Start,(IntPtr)(&start),8,AsyncGetDataFlags.DoNotFlush).Code==0 &&
               c.GetData(End,(IntPtr)(&end),8,AsyncGetDataFlags.DoNotFlush).Code==0 &&
               c.GetData(Disjoint,(IntPtr)(&timing),(uint)sizeof(TimestampDisjoint),AsyncGetDataFlags.DoNotFlush).Code==0 &&
               timing.Disjoint==0 && timing.Frequency>0)
                milliseconds=(end-start)*1000.0/timing.Frequency;
#endif
            Pending=false; return data;
        }
        [StructLayout(LayoutKind.Sequential)] private struct TimestampDisjoint { public ulong Frequency; public int Disjoint; }
        public void Dispose()
        {
            View.Dispose(); Output.Dispose(); Staging.Dispose();
#if DEBUG
            Start.Dispose(); End.Dispose(); Disjoint.Dispose();
#endif
        }
    }
    private ID3D11Device device=null!;
    private ID3D11DeviceContext context=null!;
    private IDXGIOutputDuplication duplication=null!;
    private Texture desktop=null!, small=null!, box=null!, edges=null!, reference=null!, bird=null!;
    private ID3D11Buffer constants=null!;
    private readonly Dictionary<string, ID3D11ComputeShader> shaders=[];
    private Readback detection=null!, tracking=null!;
    private byte[]? lastBirdPixels;
    private Parameters parameters;
    private readonly int width, height, smallWidth, smallHeight;
    private int captureWidth,captureHeight;
    private ModeRotation rotation;
    public bool HasFrame { get; private set; }
    public bool DetectionPending => detection.Pending;
    public bool TrackingPending => tracking.Pending;
    public double FactorX => (double)width/smallWidth;
    public double FactorY => (double)height/smallHeight;
    public GpuVision(Native.Rect screen)
    {
        width=screen.Right-screen.Left; height=screen.Bottom-screen.Top;
        double reduction=Math.Min(1,1280.0/Math.Max(width,height));
        smallWidth=(int)Math.Ceiling(width*reduction); smallHeight=(int)Math.Ceiling(height*reduction);
        try
        {
            using var factory=CreateDXGIFactory1<IDXGIFactory1>();
            for(uint a=0;factory.EnumAdapters1(a,out var adapter).Success;a++)
            {
                using(adapter)
                {
                    for(uint o=0;adapter.EnumOutputs(o,out var output).Success;o++)
                    {
                        using(output)
                        {
                            var description=output.Description;
                            if(description.DesktopCoordinates.Left!=screen.Left || description.DesktopCoordinates.Top!=screen.Top) continue;
                            rotation=description.Rotation;
                            D3D11CreateDevice(adapter,DriverType.Unknown,DeviceCreationFlags.BgraSupport,
                                new[]{FeatureLevel.Level_11_0},out device,out context).CheckError();
                            using var output1=output.QueryInterface<IDXGIOutput1>();
                            duplication=output1.DuplicateOutput(device); break;
                        }
                    }
                }
                if(duplication!=null) break;
            }
            if(duplication==null) throw new NotSupportedException("主屏没有可用的 GPU 桌面捕获输出");
            captureWidth=(int)duplication.Description.ModeDescription.Width;
            captureHeight=(int)duplication.Description.ModeDescription.Height;
            desktop=new(device,captureWidth,captureHeight,Format.B8G8R8A8_UNorm,false);
            small=new(device,smallWidth,smallHeight,Format.R32G32B32A32_Float,true);
            box=new(device,smallWidth,smallHeight,Format.R32G32B32A32_Float,true);
            edges=new(device,smallWidth,smallHeight,Format.R32G32B32A32_Float,true);
            reference=new(device,32,16,Format.R32G32B32A32_Float,true);
            bird=new(device,96,96,Format.B8G8R8A8_UNorm,false);
            constants=device.CreateBuffer((uint)Marshal.SizeOf<Parameters>(),BindFlags.ConstantBuffer);
            detection=new(device,smallHeight*8); tracking=new(device,1);
            foreach(var entry in new[]{"Downsample","HorizontalBox","Edges","ExtractRows"})
                shaders[entry]=Compile("Detection",entry);
            foreach(var entry in new[]{"SaveReference","Match"}) shaders[entry]=Compile("Tracking",entry);
        }
        catch { Dispose(); throw; }
    }
    private ID3D11ComputeShader Compile(string file,string entry)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopCock.Perception.Shaders."+file+".hlsl")
            ?? throw new InvalidDataException("缺少 GPU shader: "+file);
        using var reader=new StreamReader(stream);
        var code=Compiler.Compile(reader.ReadToEnd(),entry,file+".hlsl","cs_5_0",ShaderFlags.OptimizationLevel3);
        return device.CreateComputeShader(code.Span);
    }
    public bool Acquire()
    {
        var result=duplication.AcquireNextFrame(0,out var info,out var resource);
        if(result.Code==unchecked((int)0x887A0027)) return false; // WAIT_TIMEOUT is not lost tracking.
        result.CheckError();
        try
        {
            using(resource)
            {
                if(info.LastPresentTime==0 && HasFrame) return false; // pointer-only update
                using var frame=resource.QueryInterface<ID3D11Texture2D>();
                if(frame.Description.Width!=captureWidth || frame.Description.Height!=captureHeight)
                    throw new InvalidOperationException("显示尺寸已变化");
                context.CopyResource(desktop.Value,frame); HasFrame=true; return true;
            }
        }
        finally { duplication.ReleaseFrame(); }
    }
    public void Configure(PerceptionInput input)
    {
        parameters.Size=new(width,height,smallWidth,smallHeight);
        parameters.CaptureInfo=new(captureWidth,captureHeight,(int)rotation,0);
        parameters.Geometry=new((float)FactorX,(float)FactorY,(float)(56*input.Scale/FactorX),(float)(58*input.Scale/FactorY));
        parameters.BirdRect=Vector4.Zero; parameters.BirdInfo=Vector4.Zero;
        if(input.Bird is { } mask)
        {
            parameters.BirdRect=new(mask.Left-input.Screen.Left,mask.Top-input.Screen.Top,mask.Width*mask.Scale,mask.Height*mask.Scale);
            parameters.BirdInfo=new(mask.Width,mask.Height,mask.Scale,mask.Direction);
            if(!ReferenceEquals(lastBirdPixels,mask.Pixels))
            {
                var padded=new byte[96*96*4];
                for(int y=0;y<mask.Height;y++) Array.Copy(mask.Pixels,y*mask.Width*4,padded,y*96*4,mask.Width*4);
                context.UpdateSubresource(padded,bird.Value,0,96*4);
                lastBirdPixels=mask.Pixels;
            }
        }
        Native.GetCursorPos(out var cursor);
        parameters.CursorRect=new(cursor.X-input.Screen.Left-12,cursor.Y-input.Screen.Top-12,48,48);
        var excluded=new Vector4[8];
        for(int i=0;i<Math.Min(8,input.Excluded.Length);i++)
        {
            var r=input.Excluded[i];excluded[i]=new(r.Left-input.Screen.Left,r.Top-input.Screen.Top,r.Right-r.Left,r.Bottom-r.Top);
        }
        parameters.Excluded0=excluded[0];parameters.Excluded1=excluded[1];parameters.Excluded2=excluded[2];parameters.Excluded3=excluded[3];
        parameters.Excluded4=excluded[4];parameters.Excluded5=excluded[5];parameters.Excluded6=excluded[6];parameters.Excluded7=excluded[7];
        context.CSSetConstantBuffer(0,constants);context.CSSetShaderResource(0,desktop.View);context.CSSetShaderResource(2,bird.View);
    }
    private void DispatchImage(string entry,Texture? input,Texture output,int w,int h)
    {
        context.CSSetShaderResource(1,input?.View);context.CSSetUnorderedAccessView(0,output.Output);
        context.CSSetShader(shaders[entry]);context.Dispatch((uint)((w+7)/8),(uint)((h+7)/8),1);
        context.CSSetUnorderedAccessView(0,null);context.CSSetShaderResource(1,null);
    }
    public void Detect()
    {
        if(detection.Pending || !HasFrame) return;
        context.UpdateSubresource(in parameters,constants); detection.Begin(context);
        DispatchImage("Downsample",null,small,smallWidth,smallHeight);
        DispatchImage("HorizontalBox",small,box,smallWidth,smallHeight);
        DispatchImage("Edges",box,edges,smallWidth,smallHeight);
        context.CSSetShaderResource(1,edges.View);context.CSSetUnorderedAccessView(1,detection.View);
        context.CSSetShader(shaders["ExtractRows"]);context.Dispatch((uint)((smallHeight+31)/32),1,1);
        context.CSSetUnorderedAccessView(1,null);context.CSSetShaderResource(1,null);detection.Submit(context);
    }
    public Vector4[]? PollDetection(out double milliseconds) => detection.Poll(context,out milliseconds);
    public void SaveReference(float x,float y,int scale,float patchWidth=400)
    {
        parameters.Tracking=new(x,y,Math.Min(160*scale,patchWidth),Math.Min(24*scale,80));
        context.UpdateSubresource(in parameters,constants); DispatchImage("SaveReference",null,reference,32,16);
    }
    public void Track(float expectedX,float expectedY)
    {
        if(tracking.Pending || !HasFrame) return;
        parameters.Prediction=new(expectedX,expectedY,64,0);
        context.UpdateSubresource(in parameters,constants);tracking.Begin(context);
        context.CSSetShaderResource(1,reference.View);context.CSSetUnorderedAccessView(1,tracking.View);
        context.CSSetShader(shaders["Match"]);context.Dispatch(1,1,1);
        context.CSSetUnorderedAccessView(1,null);context.CSSetShaderResource(1,null);tracking.Submit(context);
    }
    public Vector4? PollTracking(out double milliseconds) => tracking.Poll(context,out milliseconds)?[0];
    public void Dispose()
    {
        context?.ClearState();
        foreach(var shader in shaders.Values) shader.Dispose();
        tracking?.Dispose();detection?.Dispose();constants?.Dispose();
        bird?.Dispose();reference?.Dispose();edges?.Dispose();box?.Dispose();small?.Dispose();desktop?.Dispose();
        duplication?.Dispose();context?.Dispose();device?.Dispose();
    }
}

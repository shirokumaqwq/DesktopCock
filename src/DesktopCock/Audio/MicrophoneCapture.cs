using System;
using System.IO;
using NAudio.Wave;

namespace DesktopCock;

// Created only during an explicit teaching/automatic-learning session. Never loopback.
internal sealed class MicrophoneCapture : IDisposable
{
    private readonly WaveInEvent input;
    private readonly MemoryStream buffer = new();
    private readonly object sync = new();
    private readonly bool automatic;
    private bool stopped, suppressed;
    internal event Action<byte[]>? Segment;
    internal event Action<string>? Failed;
    internal MicrophoneCapture(bool automatic)
    {
        this.automatic = automatic;
        input = new() { WaveFormat = new WaveFormat(16000,16,1), BufferMilliseconds = 100, NumberOfBuffers = 3 };
        input.DataAvailable += OnData;
        input.RecordingStopped += (_,e) => { if (e.Exception != null) Failed?.Invoke("麦克风不可用："+e.Exception.Message); };
    }
    internal void Start() => input.StartRecording();
    internal void Suppress(bool value)
    {
        lock (sync) { suppressed = value; if (value) buffer.SetLength(0); }
    }
    private void OnData(object? sender, WaveInEventArgs e)
    {
        byte[]? segment = null;
        lock (sync)
        {
            if (stopped || suppressed) return;
            int maximum = 16000*2*(automatic ? 6 : 10);
            buffer.Write(e.Buffer,0,Math.Min(e.BytesRecorded,maximum-(int)buffer.Length));
            if (buffer.Length >= maximum)
            {
                segment = buffer.ToArray(); buffer.SetLength(0);
                if (!automatic) suppressed = true;
            }
        }
        if (segment != null) Segment?.Invoke(segment);
    }
    internal byte[] Finish()
    {
        lock (sync) { stopped = true; }
        input.StopRecording();
        lock (sync) { var data = buffer.ToArray(); buffer.SetLength(0); return data; }
    }
    public void Dispose()
    {
        lock (sync) { stopped = true; }
        input.Dispose(); lock (sync) buffer.Dispose();
    }
}

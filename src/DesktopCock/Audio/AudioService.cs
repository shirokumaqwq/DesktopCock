using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DesktopCock.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DesktopCock;

// One voice, WASAPI shared output. UI polling reads the device clock; decoding
// ahead of playback is deliberately irrelevant to animation and completion.
internal sealed class AudioService : IDisposable
{
    private WasapiOut? output;
    private WaveFileReader? reader;
    private VolumeSampleProvider? gain;
    private AudioClip? clip;
    private long requestId;
    private double started;
    private int visualDelayMs;
    internal string LastError { get; private set; } = "";
    internal bool HasSession => output != null;
    internal string? ClipId => clip?.Id;
    internal long RequestId => requestId;
    private static double Now => Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;

    internal bool Play(long id, AudioClip value, double volume, int delayMs)
    {
        Stop(); LastError = "";
        try
        {
            reader = new WaveFileReader(value.Path);
            gain = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = (float)Math.Clamp(volume, 0, 1) };
            output = new WasapiOut(AudioClientShareMode.Shared, true, 30);
            output.Init(gain);
            requestId = id; clip = value; visualDelayMs = delayMs; started = Now;
            output.Play(); return true;
        }
        catch (Exception e) when (IsAudioFailure(e))
        { LastError = "声音播放不可用："+e.Message; Stop(); return false; }
    }
    internal VocalizationPlayback Read()
    {
        if (output == null || clip == null) return default;
        try
        {
            // Bound a wedged device without using elapsed wall time as a lip clock.
            if (output.PlaybackState == PlaybackState.Stopped || Now-started > clip.Timeline.Duration+3)
                return new(requestId, false, clip.Timeline.Duration, clip.Timeline.Duration, MouthPose.Closed);
            var position = Math.Clamp(output.GetPosition()/(double)output.OutputWaveFormat.AverageBytesPerSecond, 0, clip.Timeline.Duration);
            var playing = position < clip.Timeline.Duration;
            return new(requestId, playing, position, clip.Timeline.Duration,
                playing ? clip.Timeline.At(position-visualDelayMs/1000.0) : MouthPose.Closed);
        }
        catch (Exception e) when (IsAudioFailure(e))
        { LastError = "声音设备已断开："+e.Message; return new(requestId, false, 0, clip.Timeline.Duration, MouthPose.Closed); }
    }
    internal void SetVolume(double value) { if (gain != null) gain.Volume = (float)Math.Clamp(value, 0, 1); }
    internal void Stop()
    {
        var old = output; output = null;
        try { old?.Stop(); old?.Dispose(); }
        catch (Exception e) when (IsAudioFailure(e)) { LastError = e.Message; }
        reader?.Dispose(); reader = null; gain = null; clip = null;
    }
    internal static bool IsAudioFailure(Exception e) => e is COMException or InvalidOperationException or IOException or InvalidDataException or
        ArgumentException or NotSupportedException or NAudio.MmException or UnauthorizedAccessException;
    public void Dispose() => Stop();
}

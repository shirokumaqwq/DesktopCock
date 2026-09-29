using System;
using System.Diagnostics;

namespace DesktopCock;

internal enum PerformanceMetric
{
    CaptureCpu, WindowEnumerationCpu, PostprocessCpu, MainLoopCpu,
    SchedulerDelay, SnapshotAge, GpuDetection, GpuTracking
}
internal readonly record struct PerformanceSummary(int Count, double Latest, double Average, double P95);

// Fixed storage, no logging per frame. Consumers take snapshots at most 4 Hz.
internal static class PerformanceTrace
{
    private sealed class Buffer
    {
        internal readonly double[] Values = new double[256];
        internal int Count, Next;
    }
    private static readonly Buffer[] Buffers = CreateBuffers();
    private static Buffer[] CreateBuffers()
    {
        var result = new Buffer[Enum.GetValues<PerformanceMetric>().Length];
        for (int i = 0; i < result.Length; i++) result[i] = new();
        return result;
    }
    internal static void Record(PerformanceMetric metric, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0) return;
        var buffer = Buffers[(int)metric];
        lock (buffer)
        {
            buffer.Values[buffer.Next] = milliseconds;
            buffer.Next = (buffer.Next+1)%buffer.Values.Length;
            buffer.Count = Math.Min(buffer.Count+1, buffer.Values.Length);
        }
    }
    internal static void Elapsed(PerformanceMetric metric, long started) =>
        Record(metric, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    internal static PerformanceSummary Read(PerformanceMetric metric)
    {
        var buffer = Buffers[(int)metric];
        lock (buffer)
        {
            if (buffer.Count == 0) return default;
            var sorted = new double[buffer.Count]; Array.Copy(buffer.Values, sorted, buffer.Count);
            double sum = 0; foreach (var value in sorted) sum += value;
            Array.Sort(sorted);
            return new(buffer.Count, buffer.Values[(buffer.Next+buffer.Values.Length-1)%buffer.Values.Length],
                sum/buffer.Count, sorted[(int)Math.Ceiling(buffer.Count*.95)-1]);
        }
    }
}

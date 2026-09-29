using System;
using System.Collections.Generic;
using DesktopCock.Core;

namespace DesktopCock;

internal sealed record PerceptionSnapshot(long Sequence, double Timestamp, IReadOnlyList<PerchTarget> Platforms,
    PerchTarget? Tracked, bool TrackingLost, string Status, double DetectionMilliseconds, double TrackingMilliseconds,
    long TrackRevision = 0)
{
    internal static readonly PerceptionSnapshot Empty = new(0,0,Array.Empty<PerchTarget>(),null,false,"未启动",0,0);
}
internal sealed record BirdMask(int Left, int Top, int Width, int Height, int Scale, int Direction, byte[] Pixels);
internal sealed record PerceptionInput(bool Enabled, Native.Rect Screen, int Scale, BirdMask? Bird,
    Native.Rect[] Excluded, PerchTarget? Track, double AnchorX, long Revision);

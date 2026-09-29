namespace DesktopCock.Core;

public enum VocalizationKind { None, Chirp, Song }
public enum MouthPose { Closed, Small, Open }
public readonly record struct VocalizationRequest(long Id, VocalizationKind Kind, Mood Owner);
public readonly record struct MouthCue(double Seconds, MouthPose Pose);
public readonly record struct VocalizationPlayback(long RequestId, bool Playing, double Position,
    double Duration, MouthPose Mouth);

// A timeline is shared by recorded and generated voices. Silence always closes the beak.
public sealed class MouthTimeline
{
    private readonly MouthCue[] cues;
    public double Duration { get; }
    public MouthTimeline(double duration, IEnumerable<MouthCue> values)
    {
        if (!double.IsFinite(duration) || duration <= 0 || duration > 30)
            throw new ArgumentException("Invalid voice duration");
        Duration = duration; cues = values.ToArray();
        if (cues.Length == 0 || cues[0].Seconds != 0 || cues[^1].Pose != MouthPose.Closed)
            throw new ArgumentException("Mouth timeline must start at zero and end closed");
        double previous = -1;
        foreach (var cue in cues)
        {
            if (!double.IsFinite(cue.Seconds) || cue.Seconds <= previous || cue.Seconds > duration ||
                !Enum.IsDefined(cue.Pose)) throw new ArgumentException("Invalid mouth cue");
            previous = cue.Seconds;
        }
    }
    public MouthPose At(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds >= Duration) return MouthPose.Closed;
        int left = 0, right = cues.Length;
        while (left < right) { int mid = (left+right)/2; if (cues[mid].Seconds <= seconds) left = mid+1; else right = mid; }
        return cues[Math.Max(0, left-1)].Pose;
    }
}

// Consumes requests even when inaudible: enabling audio never replays an old action.
public sealed class VocalizationGate
{
    private long seen;
    private double nextChirp;
    public bool Accept(VocalizationRequest request, double now, bool audible, bool busy)
    {
        if (request.Id <= seen) return false;
        seen = request.Id;
        if (!audible || busy || request.Kind == VocalizationKind.None) return false;
        if (request.Kind == VocalizationKind.Chirp)
        {
            if (now < nextChirp) return false;
            nextChirp = now+20;
        }
        return true;
    }
}

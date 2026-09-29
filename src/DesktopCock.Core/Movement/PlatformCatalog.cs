namespace DesktopCock.Core;

public readonly record struct PlatformObservation(double Left, double Right, double Y, double Confidence,
    long WindowId, double WindowX, double WindowY);

// Stable identities are matched in window-local coordinates so a dragged window
// does not reset the one-second observation period.
public sealed class PlatformCatalog
{
    private sealed class Entry(PlatformObservation value, long id, double now)
    {
        public PlatformObservation Value = value;
        public readonly long Id = id;
        public double FirstSeen = now, LastSeen = now;
        public int Samples = 1;
    }
    private readonly List<Entry> entries = [];
    private long nextId = 1;
    public void Clear() => entries.Clear();
    public void Observe(IEnumerable<PlatformObservation> observations, double now)
    {
        var used = new HashSet<long>();
        foreach (var value in observations.OrderByDescending(v => v.Confidence).Take(128))
        {
            var entry = entries.Where(e => !used.Contains(e.Id) && e.Value.WindowId == value.WindowId &&
                Math.Abs((e.Value.Y-e.Value.WindowY)-(value.Y-value.WindowY)) <= 8 &&
                Math.Min(e.Value.Right-e.Value.WindowX, value.Right-value.WindowX) -
                Math.Max(e.Value.Left-e.Value.WindowX, value.Left-value.WindowX) >=
                Math.Min(e.Value.Right-e.Value.Left, value.Right-value.Left)*.6)
                .OrderBy(e => Math.Abs((e.Value.Y-e.Value.WindowY)-(value.Y-value.WindowY))).FirstOrDefault();
            if (entry == null) { entry = new(value, nextId++, now); entries.Add(entry); }
            else
            {
                if (now-entry.LastSeen > .8) { entry.FirstSeen = now; entry.Samples = 0; }
                entry.Value = value; entry.LastSeen = now; entry.Samples++;
            }
            used.Add(entry.Id);
        }
        entries.RemoveAll(e => now-e.LastSeen > .8);
    }
    public IReadOnlyList<PerchTarget> Get(double now, double halfWidth, double headroom, double screenTop) =>
        entries.Where(e => now-e.LastSeen <= .8 && e.Samples >= 3 && now-e.FirstSeen >= 1 &&
            e.Value.Right-e.Value.Left >= halfWidth*2 && e.Value.Y-screenTop >= headroom)
        .Select(e => new PerchTarget(e.Id, e.Value.Left, e.Value.Right, e.Value.Y,
            e.Value.Left+halfWidth, e.Value.Right-halfWidth, e.Value.Confidence, e.Value.WindowId, e.LastSeen))
        .OrderByDescending(e => e.Confidence).Take(64).ToArray();
}

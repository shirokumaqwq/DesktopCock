using System.Text;

namespace DesktopCock.Core;

public sealed class LearnedPhrase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public int Repetitions { get; set; }
    public double Familiarity { get; set; }
    public double Confidence { get; set; }
    public DateTimeOffset LastHeard { get; set; }
    public bool Ready => Familiarity >= 2.5;
}

// Learning new content is a memory update, not a neural-network training step.
// Exact normalized matching intentionally avoids merging different short phrases.
public sealed class PhraseMemory
{
    private readonly List<LearnedPhrase> phrases = [];
    private readonly Dictionary<string, string> lastSessions = [];
    public IReadOnlyList<LearnedPhrase> Phrases => phrases;
    public PhraseMemory(IEnumerable<LearnedPhrase>? saved = null)
    {
        foreach (var item in saved ?? [])
            if (item != null && Guid.TryParseExact(item.Id,"N",out _) && Normalize(item.Text).Length is >= 2 and <= 32 &&
                double.IsFinite(item.Familiarity) && double.IsFinite(item.Confidence) &&
                item.Familiarity >= 0 && item.Repetitions >= 0 && item.Confidence is >= 0 and <= 1 &&
                !phrases.Any(p => p.Id == item.Id || Normalize(p.Text) == Normalize(item.Text))) phrases.Add(item);
    }
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var result = new StringBuilder();
        foreach (char c in value.Normalize(NormalizationForm.FormKC))
            if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }
    public LearnedPhrase? Observe(string text, double confidence, DateTimeOffset now, string session, bool automatic)
    {
        var key = Normalize(text);
        if (key.Length is < 2 or > 32 || !double.IsFinite(confidence) || confidence < .55 || confidence > 1 ||
            string.IsNullOrWhiteSpace(session)) return null;
        var phrase = phrases.FirstOrDefault(p => Normalize(p.Text) == key);
        if (lastSessions.TryGetValue(key,out var previous) && previous == session) return null;
        if (phrase != null && (now-phrase.LastHeard).TotalSeconds < (automatic ? 15 : 1)) return null;
        if (phrase == null)
        {
            if (phrases.Count >= 100) return null;
            phrase = new() { Text = text.Trim() }; phrases.Add(phrase);
        }
        var days = Math.Max(0,(now-phrase.LastHeard).TotalDays);
        phrase.Familiarity = Math.Min(10, phrase.Familiarity*Math.Pow(.5,days/30)+(automatic ? .55 : 1));
        phrase.Repetitions++; phrase.Confidence = Math.Max(phrase.Confidence,confidence); phrase.LastHeard = now;
        lastSessions[key] = session; return phrase;
    }
    public double FamiliarityAt(LearnedPhrase phrase, DateTimeOffset now) =>
        phrase.Familiarity*Math.Pow(.5,Math.Max(0,(now-phrase.LastHeard).TotalDays)/30);
    public bool Remove(string id)
    {
        var phrase = phrases.FirstOrDefault(p => p.Id == id);
        if (phrase == null) return false;
        lastSessions.Remove(Normalize(phrase.Text)); return phrases.Remove(phrase);
    }
}

namespace DesktopCock.Core;

public sealed record PetPersonality(double Curiosity = .5, double Sociability = .5, double Patience = .5)
{
    public void Validate()
    {
        if (new[] { Curiosity, Sociability, Patience }.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new ArgumentOutOfRangeException(nameof(PetPersonality), "Personality values must be in [0, 1].");
    }
}

public sealed record PetProfile(string Id, PetPersonality Personality)
{
    public static PetProfile Default { get; } = new("cockatiel", new());
}

public enum AttentionTargetKind { None, Cursor, Hand, Environment }
// Position is relative to the pet in logical sprite pixels, not desktop pixels.
public readonly record struct AttentionState(AttentionTargetKind Kind, string? TargetId, double X, double Y, double Strength)
{
    public static AttentionState None => default;
}

public sealed class PetState
{
    private double annoyance;
    public double Annoyance { get => annoyance; internal set => annoyance = Math.Clamp(value, 0, 100); }
    public AttentionState Attention { get; private set; }
    public void ObserveAttention(AttentionState attention) => Attention =
        attention.Kind == AttentionTargetKind.None || !double.IsFinite(attention.Strength) ||
        !double.IsFinite(attention.X) || !double.IsFinite(attention.Y) || attention.Strength <= 0
            ? AttentionState.None : attention with { Strength = Math.Min(attention.Strength, 1) };
    public void ClearAttention() => Attention = AttentionState.None;
}

public interface IBehaviorParameterResolver
{
    BehaviorParameters Resolve(BehaviorParameters baseline, PetProfile profile);
}

// Personality mappings are a later gameplay feature. Neutral resolution retains
// the supplied parameters, including live edits, without applying hidden bonuses.
public sealed class NeutralParameterResolver : IBehaviorParameterResolver
{
    public BehaviorParameters Resolve(BehaviorParameters baseline, PetProfile profile)
    {
        profile.Personality.Validate();
        baseline.Validate();
        return baseline;
    }
}

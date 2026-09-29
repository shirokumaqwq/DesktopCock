namespace DesktopCock.Core;

public enum Mood { Idle, Walk, Beg, Pet, Sing, Sleep, Stretch, Look, Lift, Bite }
public enum Gaze { Level, UpDiagonal, UpFront }
public enum HeadYaw { Forward, Front, Back }

public interface IRandomSource
{
    double NextDouble();
    int Next(int exclusiveMaximum);
}
public sealed class SeededRandom(int? seed = null) : IRandomSource
{
    private readonly Random random = seed.HasValue ? new(seed.Value) : new();
    public double NextDouble() => random.NextDouble();
    public int Next(int exclusiveMaximum) => random.Next(exclusiveMaximum);
}

public readonly record struct ActionIntent(Mood Action, double Duration = 0, int? Direction = null,
    bool Continue = false, bool TrackTarget = false, AttentionState Target = default,
    VocalizationKind Vocalization = VocalizationKind.None);

public readonly record struct GroundSnapshot(Mood State, int Direction, double StateAge, double Movement,
    double WalkDistance, Gaze LookPose, HeadYaw HeadPose, double LookAge, bool IsTurning, double TurnProgress);

public readonly record struct DecisionEnvironment(SceneSnapshot Scene, long PlatformId, LocomotionState Locomotion)
{
    public static DecisionEnvironment Empty => new(SceneSnapshot.Empty, 0, LocomotionState.Perched);
}

public readonly record struct DecisionContext(double Elapsed, double Delta, Input Input,
    GroundSnapshot Action, double RemainingSeconds, double LastInteraction, PetProfile Profile,
    PetState Pet, BehaviorParameters Parameters, bool CanBite, bool CanSing, DecisionEnvironment Environment);

public interface IDecisionPolicy
{
    ActionIntent Choose(in DecisionContext context);
    void Reset(double elapsed, BehaviorParameters parameters);
}

public sealed class DefaultDecisionPolicy : IDecisionPolicy
{
    private readonly IRandomSource random;
    private double nextActivity, nextSong;
    private double gentle, petDuration, leaveDelay;
    public DefaultDecisionPolicy(BehaviorParameters p, IRandomSource random)
    {
        this.random = random;
        nextActivity = p.ActivityMinSeconds; nextSong = p.SingCheckSeconds;
    }
    public void Reset(double elapsed, BehaviorParameters parameters)
    { gentle = petDuration = 0; nextActivity = elapsed + parameters.ActivityMinSeconds; }
    private double Between(double min, double max) => min + random.NextDouble() * (max - min);
    public ActionIntent Choose(in DecisionContext c)
    {
        var p = c.Parameters; var input = c.Input.Interaction; var now = c.Elapsed; var state = c.Action.State;
        var hour = c.Input.LocalTime.Hour;
        var night = p.NightStartHour > p.NightEndHour
            ? hour >= p.NightStartHour || hour < p.NightEndHour
            : hour >= p.NightStartHour && hour < p.NightEndHour;
        if (input.Hand == HandMode.None && !input.Direct && c.Input.IdleSeconds >= (night ? p.NightSleepSeconds : p.DaySleepSeconds))
        { gentle = petDuration = 0; return new(Mood.Sleep); }

        if (input.Hand != HandMode.None)
        {
            leaveDelay = now + .7;
            c.Pet.Annoyance -= p.RecoveryPerSecond * c.Delta;
            if (input.Lifting) { gentle = petDuration = 0; return new(Mood.Lift); }
            if (input.Hand == HandMode.Pet && input.AtHead)
            {
                gentle += c.Delta;
                return new(gentle < p.PetConfirmSeconds ? Mood.Beg : Mood.Pet);
            }
            gentle = petDuration = 0;
            return new(Mood.Look, TrackTarget: true, Target: c.Pet.Attention);
        }

        if (input.HeadContact && input.AtHead)
        {
            if (input.Stroke == StrokeQuality.Gentle) { gentle += c.Delta; petDuration += c.Delta; }
            else { gentle = 0; if (input.Stroke == StrokeQuality.Rough) c.Pet.Annoyance += p.RoughAnnoyancePerSecond*c.Delta; }
            if (petDuration > p.PetToleranceSeconds) c.Pet.Annoyance += p.LongPetAnnoyancePerSecond*c.Delta;
        }
        else gentle = petDuration = 0;
        if (c.Pet.Annoyance >= p.BiteThreshold && input.Near && c.CanBite)
            return new(Mood.Bite, .4, input.CursorDirection, Target: c.Pet.Attention);
        if (!input.Direct) c.Pet.Annoyance -= p.RecoveryPerSecond*c.Delta;
        if (gentle >= p.PetConfirmSeconds) return new(Mood.Pet);
        if (input.Near)
        {
            leaveDelay = now + .7;
            if (state == Mood.Beg && c.RemainingSeconds > 0) return new(state, Continue: true);
            return new(Mood.Look, TrackTarget: true, Target: c.Pet.Attention);
        }
        if (state is Mood.Look or Mood.Lift or Mood.Pet)
            nextActivity = Math.Max(nextActivity, leaveDelay);
        if (state is Mood.Walk or Mood.Beg or Mood.Sing && c.RemainingSeconds > 0)
            return new(state, Continue: true);
        if (now >= nextSong)
        {
            nextSong = now + p.SingCheckSeconds;
            if (now-c.LastInteraction >= p.SingIdleSeconds && c.CanSing && random.NextDouble() < p.SingChance)
                return new(Mood.Sing, Between(3, 5), Vocalization: VocalizationKind.Song);
        }
        if (now >= nextActivity)
        {
            nextActivity = now + Between(p.ActivityMinSeconds, p.ActivityMaxSeconds);
            var choice = random.NextDouble();
            if (choice >= .85) return new(Mood.Beg, p.BegSeconds);
            if (choice >= .5) return new(Mood.Walk, BetweenWalk(out int direction), direction);
            return new(Mood.Idle, Vocalization: VocalizationKind.Chirp);
        }
        return new(Mood.Idle);

        // Preserve the original random draw order: direction before duration.
        double BetweenWalk(out int direction)
        { direction = random.Next(2) == 0 ? -1 : 1; return Between(p.WalkMinSeconds, p.WalkMaxSeconds); }
    }
}

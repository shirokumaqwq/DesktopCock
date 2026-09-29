namespace DesktopCock.Core;

// Ground runtime: injected time/randomness, semantic interaction and no artwork.
public sealed class Behavior
{
    private readonly IDecisionPolicy policy;
    private readonly double wakeStretchSeconds;
    private double previousIdle, lastInteraction;
    private bool hasPreviousIdle;
    private long voiceSequence;
    public VocalizationRequest? VoiceRequest { get; private set; }
    public BehaviorParameters Parameters { get; }
    public PetProfile Profile { get; }
    public PetState Pet { get; } = new();
    public ActionStateMachine Actions { get; }
    public GroundSnapshot Snapshot => Actions.Snapshot;
    public Mood State => Snapshot.State;
    public int Direction => Snapshot.Direction;
    public double Annoyance => Pet.Annoyance;
    public double StateAge => Snapshot.StateAge;
    public double Movement => Snapshot.Movement;
    public double WalkDistance => Snapshot.WalkDistance;
    public Gaze LookPose => Snapshot.LookPose;
    public HeadYaw HeadPose => Snapshot.HeadPose;
    public bool IsTurning => Snapshot.IsTurning;
    public double TurnProgress => Snapshot.TurnProgress;
    public Behavior(BehaviorParameters parameters, int? seed = null, IDecisionPolicy? policy = null,
        IRandomSource? random = null, PetProfile? profile = null, IBehaviorParameterResolver? resolver = null,
        double wakeStretchSeconds = 1)
    {
        if (!double.IsFinite(wakeStretchSeconds) || wakeStretchSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(wakeStretchSeconds));
        // The host supplies the authored clip length; core stays independent of assets.
        this.wakeStretchSeconds = wakeStretchSeconds;
        Profile = profile ?? PetProfile.Default;
        Parameters = (resolver ?? new NeutralParameterResolver()).Resolve(parameters, Profile);
        Actions = new(Parameters);
        this.policy = policy ?? new DefaultDecisionPolicy(Parameters, random ?? new SeededRandom(seed));
    }
    public void TurnAtEdge() => Actions.TurnAtEdge();
    public void Resume()
    {
        VoiceRequest = null;
        Actions.Reset(); hasPreviousIdle = false; Pet.ClearAttention();
        policy.Reset(Actions.Elapsed, Parameters);
    }
    public void Tick(double seconds, Input input, DecisionEnvironment? environment = null)
    {
        var dt = Actions.BeginTick(seconds);
        var interaction = input.Interaction;
        Pet.ObserveAttention(interaction.Near || interaction.Hand != HandMode.None
            ? new(interaction.Source, null, interaction.CursorOffsetX, interaction.CursorOffsetY, 1)
            : AttentionState.None);
        var inputResumed = hasPreviousIdle && input.IdleSeconds + .2 < previousIdle;
        previousIdle = input.IdleSeconds; hasPreviousIdle = true;
        if (interaction.Direct) lastInteraction = Actions.Elapsed;
        if (interaction.Tap) Pet.Annoyance += State == Mood.Sleep ? Parameters.WakeAnnoyance : Parameters.ClickAnnoyance;
        if (State == Mood.Sleep)
        {
            if (inputResumed || interaction.Tap || interaction.Hand != HandMode.None) Actions.Apply(new(Mood.Stretch, wakeStretchSeconds), dt, interaction);
            return;
        }
        if (Actions.IsProtected) return;
        if (State == Mood.Bite) { Pet.Annoyance = 20; Actions.Finish(); }
        if (State == Mood.Stretch) Actions.Finish();
        var context = new DecisionContext(Actions.Elapsed, dt, input, Snapshot, Actions.RemainingSeconds,
            lastInteraction, Profile, Pet, Parameters, Actions.CanBite, Actions.CanSing, environment ?? DecisionEnvironment.Empty);
        var intent = policy.Choose(context);
        if (intent.Target.Kind != AttentionTargetKind.None) Pet.ObserveAttention(intent.Target);
        Actions.Apply(intent, dt, interaction);
        if (intent.Vocalization != VocalizationKind.None && State == intent.Action && !intent.Continue)
            VoiceRequest = new(++voiceSequence, intent.Vocalization, State);
    }
    // The host feeds back device playback progress, never its decoder/read cursor.
    public void UpdateVoice(VocalizationPlayback playback)
    {
        if (VoiceRequest is not { } request || request.Id != playback.RequestId) return;
        if (request.Kind == VocalizationKind.Song && State == request.Owner)
        {
            if (playback.Playing) Actions.SetVoiceRemaining(playback.Duration-playback.Position+.5);
            else Actions.Finish();
        }
        if (!playback.Playing) VoiceRequest = null;
    }
}

namespace DesktopCock.Core;

public sealed class ActionStateMachine(BehaviorParameters parameters)
{
    private Gaze pendingGaze;
    private HeadYaw pendingHead;
    private double gazeDwell, headDwell, turnDwell, turnAge, until;
    private int pendingDirection = 1;
    private double lastBite = -3, lastSong = -parameters.SingCooldownSeconds;
    public Mood State { get; private set; } = Mood.Idle;
    public int Direction { get; private set; } = 1;
    public double StateAge { get; private set; }
    public double Elapsed { get; private set; }
    public double Movement { get; private set; }
    public double WalkDistance { get; private set; }
    public Gaze LookPose { get; private set; }
    public HeadYaw HeadPose { get; private set; }
    public double LookAge { get; private set; }
    public bool IsTurning { get; private set; }
    public double TurnProgress { get; private set; }
    public double RemainingSeconds => until - Elapsed;
    public bool IsProtected => (State is Mood.Stretch or Mood.Bite) && RemainingSeconds > 0;
    public bool CanBite => Elapsed-lastBite >= parameters.BiteCooldownSeconds;
    public bool CanSing => Elapsed-lastSong >= parameters.SingCooldownSeconds;
    public GroundSnapshot Snapshot => new(State, Direction, StateAge, Movement, WalkDistance,
        LookPose, HeadPose, LookAge, IsTurning, TurnProgress);
    public double BeginTick(double seconds)
    {
        var dt = !double.IsFinite(seconds) || seconds < 0 ? 0 : seconds > .5 ? .1 : seconds;
        Elapsed += dt; StateAge += dt; LookAge += dt; Movement = 0;
        return dt;
    }
    private void Set(Mood state, double duration = 0)
    {
        if (State != state)
        {
            StateAge = 0; LookPose = pendingGaze = Gaze.Level;
            HeadPose = pendingHead = HeadYaw.Forward;
            IsTurning = false; TurnProgress = 0;
            LookAge = gazeDwell = headDwell = turnDwell = turnAge = 0;
            if (state == Mood.Walk) WalkDistance = 0;
        }
        State = state; until = Elapsed + duration;
    }
    public void Reset() { StateAge = 0; Movement = 0; Set(Mood.Idle); }
    internal void Finish() => Set(Mood.Idle);
    internal void SetVoiceRemaining(double seconds)
    {
        if (State == Mood.Sing && double.IsFinite(seconds)) until = Elapsed+Math.Clamp(seconds, 0, 31);
    }
    public void TurnAtEdge() => Direction *= -1;
    public void Apply(ActionIntent intent, double dt, InteractionFrame input)
    {
        if (IsProtected) return;
        if (!intent.Continue)
        {
            if ((intent.Action == Mood.Bite && !CanBite) || (intent.Action == Mood.Sing && !CanSing)) return;
            if ((State is Mood.Walk or Mood.Beg or Mood.Sing) && RemainingSeconds <= 0) Set(Mood.Idle);
            Set(intent.Action, intent.Duration);
            if (intent.Action == Mood.Bite) lastBite = Elapsed;
            if (intent.Action == Mood.Sing) lastSong = Elapsed;
            if (intent.Direction is { } direction) Direction = direction >= 0 ? 1 : -1;
        }
        else if (State == Mood.Walk)
        { Movement = Direction * parameters.WalkSpeed * dt; WalkDistance += parameters.WalkSpeed * dt; }
        if (intent.TrackTarget)
        {
            if (intent.Target.Kind != AttentionTargetKind.None)
                input = input with { CursorOffsetX = intent.Target.X, CursorOffsetY = intent.Target.Y };
            TrackCursor(dt, input);
        }
    }
    private void TrackCursor(double dt, InteractionFrame input)
    {
        // Held head gestures must not rotate their interaction region.
        if (State != Mood.Look || (input.HeadContact && input.AtHead)) return;
        if (IsTurning)
        {
            AdvanceTurn(dt);
            return;
        }

        // Head leads. A nearby cursor can be watched over the shoulder indefinitely.
        // Only a sustained, farther rearward target prompts a small stepping pivot.
        var localX = input.CursorOffsetX * Direction;
        var desiredHead = localX < (HeadPose == HeadYaw.Back ? -6 : -12) ? HeadYaw.Back
            : localX > (HeadPose == HeadYaw.Forward ? 6 : 12) ? HeadYaw.Forward : HeadYaw.Front;
        if (desiredHead != pendingHead) { pendingHead = desiredHead; headDwell = 0; }
        headDwell += dt;
        if (HeadPose != desiredHead && headDwell >= .10)
        {
            HeadPose = (HeadYaw)((int)HeadPose + Math.Sign((int)desiredHead - (int)HeadPose));
            headDwell = LookAge = 0;
        }
        if (localX < -28 && HeadPose == HeadYaw.Back)
        {
            turnDwell += dt;
            if (turnDwell >= .45)
            {
                pendingDirection = -Direction;
                IsTurning = true; TurnProgress = 0;
                turnAge = turnDwell = 0;
            }
        }
        else turnDwell = 0;

        var above = input.CursorOffsetY < (LookPose == Gaze.Level ? -10 : -6);
        var frontWidth = LookPose == Gaze.UpFront ? 16 : 11;
        var desired = !above ? Gaze.Level : Math.Abs(input.CursorOffsetX) <= frontWidth ? Gaze.UpFront : Gaze.UpDiagonal;
        if (desired != pendingGaze) { pendingGaze = desired; gazeDwell = 0; }
        gazeDwell += dt;
        if (desired != LookPose && gazeDwell >= .12)
        {
            // Always pass through the diagonal pose when raising/lowering the head.
            LookPose = (Gaze)((int)LookPose + Math.Sign((int)desired - (int)LookPose));
            LookAge = gazeDwell = 0;
        }
    }

    private void AdvanceTurn(double dt)
    {
        turnAge += dt;
        // Change facing only while the torso is frontal, never mirror a profile.
        if (turnAge >= .22) Direction = pendingDirection;
        TurnProgress = Math.Clamp(turnAge / .44, 0, 1);
        IsTurning = turnAge < .44;
        if (!IsTurning)
        {
            HeadPose = pendingHead = HeadYaw.Forward;
            headDwell = turnDwell = 0;
        }
    }
}

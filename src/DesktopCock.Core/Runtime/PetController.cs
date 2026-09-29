namespace DesktopCock.Core;

public readonly record struct DesktopBounds(double Left, double Top, double Right, double Bottom);
public sealed record SceneSnapshot(double Timestamp, IReadOnlyList<PerchTarget> Platforms,
    PerchTarget? Tracked, bool TrackingLost, long TrackRevision)
{
    public static SceneSnapshot Empty { get; } = new(0, Array.Empty<PerchTarget>(), null, false, 0);
}
public readonly record struct RuntimeOptions(int Scale = 3, bool AutoExplore = true, double ExploreInterval = 90);

// Owns all ground/flight arbitration. The host supplies desktop facts and optional
// fresh support validation; neither native handles nor GPU resources enter here.
public sealed partial class PetController
{
    private readonly IRandomSource random;
    private readonly Func<PerchTarget, bool> support;
    private DesktopBounds screen;
    private double perch, now, nextExplore, trackRequestedAt;
    private bool previousVisible = true, externalControl, groundSuspended;
    private RuntimeOptions options;
    private SceneSnapshot scene = SceneSnapshot.Empty;
    public Behavior Ground { get; }
    public FlightController Flight { get; } = new();
    public InteractionSession Interactions { get; } = new();
    public bool Initialized { get; private set; }
    public bool Visible { get; private set; } = true;
    public bool Paused { get; private set; }
    public PerchTarget? ActivePlatform { get; private set; }
    public long TrackRevision { get; private set; }
    public double TrackAnchorX { get; private set; }
    public string LastMovementMessage { get; private set; } = "";
    public bool ShouldPerceive => Visible && (Interactions.Active || releaseHandPending || ActivePlatform?.Id > 0 || options.AutoExplore || externalControl || (Flight.Airborne && Flight.TargetId != 0));
    public PerchTarget TaskbarTarget => new(0, screen.Left, screen.Right, perch, screen.Left+32*options.Scale,
        Math.Max(screen.Left+32*options.Scale, screen.Right-32*options.Scale), 1, 0, now);
    public PetSnapshot Snapshot => new(Ground.Snapshot, Flight.State, Flight.StateAge, Flight.Position,
        Flight.Airborne ? (Flight.FrontFacing ? 1 : Flight.Direction) : Ground.Direction, Ground.Annoyance, Ground.Pet.Attention)
        { FlightFrontFacing = Flight.Airborne && Flight.FrontFacing };
    public PetController(Behavior ground, RuntimeOptions options, IRandomSource? random = null,
        Func<PerchTarget, bool>? support = null)
    {
        Ground = ground; this.options = options; this.random = random ?? new SeededRandom(); this.support = support ?? (_ => true);
        Interactions.Changed += HandChanged;
    }
    public void UpdateEnvironment(DesktopBounds bounds, double taskbarY, double timestamp, SceneSnapshot observation)
    {
        bool changed = screen != bounds;
        screen = bounds; perch = taskbarY; now = timestamp; scene = observation;
        if (Initialized && changed) ResetPosition(Flight.Position.X);
    }
    public void Initialize(double x)
    { ResetPosition(x); Initialized = true; nextExplore = now+options.ExploreInterval; }
    private void ResetPosition(double x)
    {
        OnHand = releaseHandPending = handReleaseFlight = false; handLiftAge = 0;
        x = TaskbarTarget.ClampX(x); Flight.Reset(x, perch); Flight.SetGroundPosition(TaskbarTarget, x);
        ActivePlatform = TaskbarTarget; TrackRevision++;
    }
    public void SetLifecycle(bool visible, bool paused)
    {
        bool becomingInactive = (!visible || paused) && (Visible && !Paused);
        Visible = visible; Paused = paused;
        if (!visible) previousVisible = false;
        if (becomingInactive) { Interactions.Exit(); Ground.Pet.ClearAttention(); }
    }
    public void Configure(RuntimeOptions value)
    {
        var old = options; options = value;
        if (old == value) return;
        nextExplore = now+options.ExploreInterval;
        if (!Initialized) return;
        if (old.Scale != value.Scale && ActivePlatform is { Id: > 0 } p)
        {
            ActivePlatform = FitPlatform(p);
            if (!ActivePlatform.CanLand || p.Y < screen.Top+58*value.Scale) RequestFlight(0, Flight.Position.X, FlightSource.Escape);
            else RefreshTracking(ActivePlatform, Flight.Position.X);
        }
        if (!OnHand && !value.AutoExplore && old.AutoExplore && (Flight.PlatformId != 0 || Flight.Airborne))
            RequestFlight(0, Flight.Position.X, FlightSource.Escape);
    }
    // Used by any host-controlled session (e.g. a developer preview); normal
    // policy code has no preview flag or forced-state path.
    public void SetExternalControl(bool enabled, bool suspendGround = false)
    {
        groundSuspended = suspendGround;
        if (externalControl == enabled) return;
        externalControl = enabled; nextExplore = now+options.ExploreInterval;
        if (!enabled && !options.AutoExplore && !Flight.Airborne && Flight.PlatformId != 0)
            RequestFlight(0, Flight.Position.X, FlightSource.Escape);
    }
    private PerchTarget FitPlatform(PerchTarget p) => p with
    {
        MinX = Math.Max(screen.Left+32*options.Scale, p.Left+28*options.Scale),
        MaxX = Math.Min(screen.Right-32*options.Scale, p.Right-28*options.Scale)
    };
    public IReadOnlyList<PerchTarget> LandingCandidates => (now-scene.Timestamp < 1 ? scene.Platforms : [])
        .Select(FitPlatform).Where(p => p.CanLand && p.Y >= screen.Top+58*options.Scale && Math.Abs(p.Y-perch)>3)
        .Append(TaskbarTarget).ToArray();
    private void RefreshTracking(PerchTarget target, double anchorX)
    { TrackRevision++; trackRequestedAt = now; TrackAnchorX = target.ClampX(anchorX); }
    public FlightRequestResult RequestFlight(long targetId, double anchorX, FlightSource source)
    {
        if (!Initialized) return Reject("小鸟尚未就绪");
        if (Paused) return Reject("请先恢复活动，再试飞");
        if (!Visible) return Reject("隐藏、锁屏或全屏期间不能试飞");
        var target = targetId == 0 ? TaskbarTarget : LandingCandidates.FirstOrDefault(p => p.Id == targetId);
        if (target == null || !target.CanLand) return Reject("目标已失效，请重新选择停留位置");
        if (targetId != 0 && !support(target)) return Reject("目标已被遮挡");
        if (source == FlightSource.Manual) Ground.Resume();
        OnHand = releaseHandPending = handReleaseFlight = false; handLiftAge = 0;
        Interactions.CancelGesture(); Ground.Pet.ClearAttention();
        ActivePlatform = target; RefreshTracking(target, anchorX); Flight.FlyTo(target, anchorX);
        LastMovementMessage = $"{(source == FlightSource.Manual ? "手动" : "自动")}飞往 " + (targetId == 0 ? "任务栏" : $"#{targetId}");
        return new(true, LastMovementMessage);
    }
    private FlightRequestResult Reject(string message) { LastMovementMessage = message; return new(false, message); }
    private void EscapePlatform()
    {
        var current = ActivePlatform?.Id ?? -1;
        var alternative = LandingCandidates.Where(p => p.Id != current && p.Id != 0)
            .OrderByDescending(p => p.Confidence).FirstOrDefault();
        var result = RequestFlight(alternative?.Id ?? 0, alternative == null ? Flight.Position.X : (alternative.MinX+alternative.MaxX)/2, FlightSource.Escape);
        if (!result.Accepted && alternative != null) RequestFlight(0, Flight.Position.X, FlightSource.Escape);
    }
    // Returns true while movement owns the pose; ground decision time is frozen.
    public bool AdvanceMovement(double dt, double idleSeconds)
    {
        if (!Initialized || Paused || !Visible) return Flight.Airborne;
        if (!previousVisible && !releaseHandPending) ResetPosition(Flight.Position.X);
        previousVisible = true;
        if (releaseHandPending) ReleaseHand();
        if (OnHand) return false;
        if (ActivePlatform?.Id > 0)
        {
            if (scene.TrackRevision == TrackRevision && scene.Tracked is { } tracked && tracked.Id == ActivePlatform.Id)
            {
                ActivePlatform = FitPlatform(tracked);
                if (!ActivePlatform.CanLand) EscapePlatform();
            }
            else if ((scene.TrackRevision == TrackRevision && scene.TrackingLost) ||
                (now-trackRequestedAt > 1.5 && (now-scene.Timestamp > 1.5 || scene.TrackRevision != TrackRevision))) EscapePlatform();
        }
        else ActivePlatform = TaskbarTarget;
        if (Flight.Airborne)
        {
            if (Flight.FlightAge > 6 && Flight.TargetId != 0) RequestFlight(0, Flight.Position.X, FlightSource.Escape);
            ActivePlatform ??= TaskbarTarget;
            if (Flight.Tick(dt, ActivePlatform, options.Scale))
            {
                if (!groundSuspended) Ground.Resume();
                nextExplore = now+options.ExploreInterval;
                if (!options.AutoExplore && !externalControl && !handReleaseFlight && Flight.PlatformId != 0)
                    RequestFlight(0, Flight.Position.X, FlightSource.Escape);
                handReleaseFlight = false;
            }
            Flight.Constrain(TaskbarTarget.MinX, screen.Top+58*options.Scale, TaskbarTarget.MaxX, screen.Bottom);
            return Flight.Airborne;
        }
        if (ActivePlatform != null) Flight.Follow(ActivePlatform, 0);
        if (options.AutoExplore && !externalControl && now >= nextExplore && !Interactions.Active && !Interactions.GestureActive &&
            Ground.State is Mood.Idle or Mood.Walk && idleSeconds > 2)
        {
            nextExplore = now+options.ExploreInterval;
            var available = LandingCandidates.Where(p => p.Id != Flight.PlatformId &&
                Math.Abs((p.MinX+p.MaxX)/2-Flight.Position.X)+Math.Abs(p.Y-Flight.Position.Y) > 40*options.Scale)
                .OrderByDescending(p => p.Id == 0 ? -1 : p.Confidence).Take(5).ToArray();
            if (available.Length > 0)
            {
                var target = available[random.Next(available.Length)];
                RequestFlight(target.Id, target.MinX+random.NextDouble()*(target.MaxX-target.MinX), FlightSource.Autonomous);
            }
        }
        return Flight.Airborne;
    }
    public void AdvanceGround(double dt, Input input)
    {
        if (Paused || !Visible || Flight.Airborne || groundSuspended) return;
        Ground.Tick(dt, input with { Interaction = input.Interaction with { Lifting = LiftingHand } },
            new DecisionEnvironment(scene, Flight.PlatformId, Flight.State));
        if (!OnHand) WalkOnPlatform(Ground.Movement*options.Scale);
    }
    public void WalkOnPlatform(double physicalPixels)
    {
        if (!Initialized || ActivePlatform == null || Flight.Airborne) return;
        double proposed = Flight.Position.X+physicalPixels;
        Flight.Follow(ActivePlatform, physicalPixels);
        if (proposed < ActivePlatform.MinX || proposed > ActivePlatform.MaxX) Ground.TurnAtEdge();
    }
}

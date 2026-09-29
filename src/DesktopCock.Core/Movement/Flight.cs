using System.Numerics;

namespace DesktopCock.Core;

public enum LocomotionState { Perched, TakingOff, Flying, Landing }
public enum FlightSource { Autonomous, Manual, Escape }
public sealed record PerchTarget(long Id, double Left, double Right, double Y, double MinX, double MaxX,
    double Confidence, long WindowId, double ObservedAt, bool Stable = true)
{
    public bool CanLand => Stable && MaxX >= MinX;
    public double ClampX(double x) => Math.Clamp(x, MinX, Math.Max(MinX, MaxX));
}
public sealed record FlightRequestResult(bool Accepted, string Message);

// All positions and velocities are physical desktop pixels. No Windows/GPU dependency.
public sealed class FlightController
{
    public LocomotionState State { get; private set; } = LocomotionState.Perched;
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public long PlatformId { get; private set; }
    public long TargetId { get; private set; }
    public double StateAge { get; private set; }
    public double FlightAge { get; private set; }
    public int Direction { get; private set; } = 1;
    public bool FrontFacing { get; private set; }
    public bool Airborne => State != LocomotionState.Perched;
    private double offsetX;
    private Vector2 lastTarget;
    private bool targetKnown;

    public void Reset(double x, double y)
    {
        Position = new((float)x, (float)y); Velocity = Vector2.Zero;
        State = LocomotionState.Perched; PlatformId = TargetId = 0;
        StateAge = FlightAge = 0; targetKnown = false;
        FrontFacing = false;
    }
    public void FlyTo(PerchTarget target, double x)
    {
        offsetX = target.ClampX(x)-target.Left; TargetId = target.Id;
        lastTarget = new((float)target.ClampX(x), (float)target.Y); targetKnown = true;
        var travel = lastTarget-Position;
        FrontFacing = Math.Abs(travel.Y) > 1 && Math.Abs(travel.X) <= Math.Abs(travel.Y)*.30;
        FlightAge = 0;
        if (State == LocomotionState.Perched) { State = LocomotionState.TakingOff; StateAge = 0; }
        else if (State == LocomotionState.Landing) { State = LocomotionState.Flying; StateAge = 0; }
    }
    public void Follow(PerchTarget platform, double movement)
    {
        if (Airborne) return;
        if (platform.Id != PlatformId) return;
        offsetX = platform.ClampX(platform.Left + offsetX + movement)-platform.Left;
        Position = new((float)(platform.Left+offsetX), (float)platform.Y);
    }
    public void SetGroundPosition(PerchTarget platform, double x)
    {
        PlatformId = TargetId = platform.Id; offsetX = platform.ClampX(x)-platform.Left;
        Position = new((float)(platform.Left+offsetX), (float)platform.Y);
    }
    public void Constrain(double left,double top,double right,double bottom)
    {
        var clamped=new Vector2((float)Math.Clamp(Position.X,left,Math.Max(left,right)),
            (float)Math.Clamp(Position.Y,top,Math.Max(top,bottom)));
        if(clamped.X!=Position.X) Velocity=new(0,Velocity.Y);
        if(clamped.Y!=Position.Y) Velocity=new(Velocity.X,0);
        Position=clamped;
    }
    public bool Tick(double seconds, PerchTarget target, int scale)
    {
        if (!Airborne) return false;
        float dt = (float)Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, .05);
        if(dt<=0) return false;
        StateAge += dt; FlightAge += dt;
        Vector2 destination = new((float)target.ClampX(target.Left+offsetX), (float)target.Y);
        var targetVelocity = targetKnown && dt > 0 ? (destination-lastTarget)/dt : Vector2.Zero;
        targetVelocity = Limit(targetVelocity, 300*scale);
        lastTarget = destination; targetKnown = true;
        if (State == LocomotionState.Landing)
        {
            if (Vector2.Distance(Position, destination) > 24*scale)
            { State = LocomotionState.Flying; StateAge = 0; }
            else
            {
                Position = destination; Velocity = targetVelocity;
                if (StateAge >= .24)
                {
                    State = LocomotionState.Perched; StateAge = 0; PlatformId = target.Id;
                    offsetX = destination.X-target.Left; Velocity = Vector2.Zero; return true;
                }
                return false;
            }
        }
        float distance = Vector2.Distance(Position, destination);
        Vector2 desired;
        if (State == LocomotionState.TakingOff)
        {
            desired = new(Math.Clamp((destination.X-Position.X)*5, -70*scale, 70*scale), -100*scale);
            if (StateAge >= .22) { State = LocomotionState.Flying; StateAge = 0; }
        }
        else
        {
            // Lift above the direct line, then reduce that offset continuously on approach.
            var aim = destination - new Vector2(0, Math.Min(50*scale, distance*.24f));
            var delta = aim-Position;
            desired = delta.LengthSquared() < .01f ? targetVelocity :
                Vector2.Normalize(delta)*Math.Min(240*scale, distance*5) + targetVelocity;
        }
        Velocity += Limit(desired-Velocity, 1000*scale*dt);
        Velocity = Limit(Velocity, 300*scale);
        Position += Velocity*dt;
        // Keep the current view while slowing into a perch. Different entry/exit
        // angles prevent tiny tracking corrections from flickering the wing view.
        if (State == LocomotionState.Flying && Velocity.Length() > 12*scale)
        {
            var horizontal = Math.Abs(Velocity.X);
            var vertical = Math.Abs(Velocity.Y);
            FrontFacing = FrontFacing ? horizontal <= vertical*.55 : horizontal <= vertical*.30;
        }
        if (Math.Abs(Velocity.X) > 8*scale) Direction = Velocity.X >= 0 ? 1 : -1;
        if (State == LocomotionState.Flying && distance < 1.5*scale &&
            (Velocity-targetVelocity).Length() < 12*scale)
        { State = LocomotionState.Landing; StateAge = 0; Position = destination; }
        return false;
    }
    private static Vector2 Limit(Vector2 value, float maximum) =>
        value.LengthSquared() > maximum*maximum && maximum > 0 ? Vector2.Normalize(value)*maximum : value;
}

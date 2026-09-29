namespace DesktopCock.Core;

public sealed partial class PetController
{
    private const long HandPlatformId = -2;
    private double handLiftAge;
    private bool releaseHandPending;
    private bool handReleaseFlight;
    public bool OnHand { get; private set; }
    public bool LiftingHand => handLiftAge > 0 && !OnHand;

    public void SetHandMode(HandMode mode)
    {
        if (mode == Interactions.Mode) return;
        if (mode == HandMode.None) Interactions.Exit();
        else if (Visible && !Paused) Interactions.Enter(mode == HandMode.Perch ? "perch" : "pet");
    }

    private void HandChanged()
    {
        releaseHandPending |= OnHand;
        OnHand = false; handLiftAge = 0;
        Ground.Resume();
        nextExplore = now + options.ExploreInterval;
    }

    // Cursor hotspot is the contact point on the finger, in physical screen pixels.
    public void UpdateHand(double seconds, double contactX, double contactY)
    {
        if (!Initialized || Paused || !Visible || Interactions.Mode != HandMode.Perch ||
            Flight.Airborne || releaseHandPending || groundSuspended) return;
        if (!double.IsFinite(contactX) || !double.IsFinite(contactY)) return;
        // Moving outside the supported monitor releases the bird onto a real platform.
        bool inBounds = contactX >= TaskbarTarget.MinX && contactX <= TaskbarTarget.MaxX &&
            contactY >= screen.Top + 58 * options.Scale && contactY <= screen.Bottom;
        if (!inBounds)
        {
            handLiftAge = 0;
            if (OnHand) { OnHand = false; releaseHandPending = true; }
            return;
        }
        if (!OnHand)
        {
            bool close = Math.Abs(contactX - Flight.Position.X) <= 22 * options.Scale &&
                Math.Abs(contactY - Flight.Position.Y) <= 16 * options.Scale;
            if (!close || Ground.Actions.IsProtected || Ground.State == Mood.Sleep)
            { handLiftAge = 0; return; }
            handLiftAge += Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, .05);
            if (handLiftAge < .4) return;
            OnHand = true; TrackRevision++;
        }
        ActivePlatform = new(HandPlatformId, contactX, contactX, contactY, contactX, contactX, 1, 0, now);
        Flight.SetGroundPosition(ActivePlatform, contactX);
    }

    private void ReleaseHand()
    {
        // Distance to the nearest usable point, not to the platform's center.
        var position = Flight.Position;
        foreach (var candidate in LandingCandidates.OrderBy(p =>
            Math.Pow(p.ClampX(position.X) - position.X, 2) + Math.Pow(p.Y - position.Y, 2)))
        {
            if (RequestFlight(candidate.Id, position.X, FlightSource.Escape).Accepted)
            { handReleaseFlight = true; return; }
        }
    }
}

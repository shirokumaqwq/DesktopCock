using System.Numerics;

namespace DesktopCock.Core;

// Position is in physical desktop pixels; ground motion and attention use
// logical sprite pixels. Scale is applied only at the movement boundary.
public readonly record struct PetSnapshot(GroundSnapshot Ground, LocomotionState Locomotion,
    double LocomotionAge, Vector2 Position, int Direction, double Annoyance, AttentionState Attention)
{
    public bool FlightFrontFacing { get; init; }
    public bool Airborne => Locomotion != LocomotionState.Perched;
}

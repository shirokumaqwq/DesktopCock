using DesktopCock.Core;

namespace DesktopCock;

internal static class InteractionMapper
{
    internal static InteractionFrame Map(bool near, bool head, bool feet, bool held, bool clicked,
        double speed, int direction, double x, double y, BehaviorParameters parameters, InteractionSession session)
    {
        var stroke = speed >= parameters.PetMinSpeed && speed <= parameters.PetMaxSpeed ? StrokeQuality.Gentle
            : speed > parameters.PetMaxSpeed ? StrokeQuality.Rough : StrokeQuality.None;
        var mode = session.Mode;
        bool hand = mode != HandMode.None;
        return new(hand || (near && (held || clicked)), head, feet, mode == HandMode.Pet ? head : held,
            hand ? false : clicked, stroke, direction, x, y,
            hand ? AttentionTargetKind.Hand : AttentionTargetKind.Cursor, mode);
    }
}

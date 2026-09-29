using System;
using DesktopCock.Core;

namespace DesktopCock;

// A presentation session owns its executor. It never forces the natural policy.
internal sealed class PreviewSession
{
    private readonly ActionStateMachine actions;
    private readonly Gaze gaze;
    private readonly HeadYaw head;
    private readonly bool turn;
    private bool turned;
    internal PreviewSession(BehaviorParameters parameters, Mood mood, int direction, Gaze gaze, HeadYaw head, bool turn)
    {
        actions = new(parameters); this.gaze = gaze; this.head = gaze == Gaze.UpFront ? HeadYaw.Front : head; this.turn = turn;
        if (direction < 0) actions.TurnAtEdge();
        actions.Apply(new(mood), 0, default);
    }
    internal GroundSnapshot Snapshot
    {
        get
        {
            var s = actions.Snapshot;
            return s with { LookPose = gaze, HeadPose = head, LookAge = s.StateAge,
                IsTurning = turn && s.StateAge < .44, TurnProgress = Math.Min(1, s.StateAge/.44) };
        }
    }
    internal double Tick(double seconds)
    {
        var dt = actions.BeginTick(seconds);
        actions.Apply(new(actions.State, Continue: true), dt, default);
        if (turn && !turned && actions.StateAge >= .22) { actions.TurnAtEdge(); turned = true; }
        return actions.Movement;
    }
    internal void TurnAtEdge() => actions.TurnAtEdge();
}

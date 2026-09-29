namespace DesktopCock.Core;

public enum StrokeQuality { None, Gentle, Rough }
public enum HandMode { None, Perch, Pet }
public readonly record struct InteractionFrame(bool Near, bool AtHead, bool AtFeet, bool HeadContact,
    bool Tap, StrokeQuality Stroke, int CursorDirection, double CursorOffsetX = 32, double CursorOffsetY = 0,
    AttentionTargetKind Source = AttentionTargetKind.Cursor, HandMode Hand = HandMode.None, bool Lifting = false)
{
    public bool Direct => Tap || (HeadContact && AtHead);
}

public readonly record struct Input(DateTime LocalTime, double IdleSeconds, InteractionFrame Interaction);

// The host owns bindings and artwork. The core only owns the session lifecycle.
public sealed class InteractionSession
{
    public bool Active { get; private set; }
    public string HandType { get; private set; } = "default";
    public bool GestureActive { get; private set; }
    public HandMode Mode => !Active ? HandMode.None : HandType switch { "perch" => HandMode.Perch, "pet" => HandMode.Pet, _ => HandMode.None };
    public event Action? GestureCancelled;
    public event Action? Changed;
    public void Enter(string handType = "default")
    {
        bool wasActive = Active;
        SetHand(handType); Active = true;
        if (!wasActive) Changed?.Invoke();
    }
    public void Exit() { CancelGesture(); bool changed = Active; Active = false; if (changed) Changed?.Invoke(); }
    public void SetHand(string handType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handType);
        if (HandType == handType) return;
        CancelGesture(); HandType = handType; if (Active) Changed?.Invoke();
    }
    // Ordinary mouse gestures use the same cancellation path outside hand mode.
    public void BeginGesture() => GestureActive = true;
    public void EndGesture() => GestureActive = false;
    public void CancelGesture() { GestureActive = false; GestureCancelled?.Invoke(); }
}

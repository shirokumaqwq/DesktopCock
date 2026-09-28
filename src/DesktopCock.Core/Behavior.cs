namespace DesktopCock.Core;

public enum Mood { Idle, Walk, Beg, Pet, Sing, Sleep, Stretch, Look, Lift, Bite }
public sealed class Settings
{
    public int Scale { get; set; } = 3;
    public double DaySleepSeconds { get; set; } = 600;
    public double NightSleepSeconds { get; set; } = 120;
    public int NightStartHour { get; set; } = 22;
    public int NightEndHour { get; set; } = 7;
    public double ActivityMinSeconds { get; set; } = 8;
    public double ActivityMaxSeconds { get; set; } = 20;
    public double WalkMinSeconds { get; set; } = 2;
    public double WalkMaxSeconds { get; set; } = 5;
    public double WalkSpeed { get; set; } = 8;
    public double BegSeconds { get; set; } = 6;
    public double SingIdleSeconds { get; set; } = 60;
    public double SingCheckSeconds { get; set; } = 30;
    public double SingChance { get; set; } = .2;
    public double SingCooldownSeconds { get; set; } = 120;
    public double PetMinSpeed { get; set; } = 2;
    public double PetMaxSpeed { get; set; } = 40;
    public double PetConfirmSeconds { get; set; } = .3;
    public double PetToleranceSeconds { get; set; } = 8;
    public double ClickAnnoyance { get; set; } = 15;
    public double WakeAnnoyance { get; set; } = 60;
    public double RoughAnnoyancePerSecond { get; set; } = 20;
    public double LongPetAnnoyancePerSecond { get; set; } = 10;
    public double RecoveryPerSecond { get; set; } = 10;
    public double BiteThreshold { get; set; } = 60;
    public double BiteCooldownSeconds { get; set; } = 3;
    public void Validate()
    {
        Scale = Math.Clamp(Scale, 2, 4);
        foreach (var p in GetType().GetProperties().Where(p => p.PropertyType == typeof(double)))
        {
            var value = (double)p.GetValue(this)!;
            if (!double.IsFinite(value) || value < 0) throw new InvalidDataException($"Invalid setting: {p.Name}");
        }
        if (DaySleepSeconds < 1 || NightSleepSeconds < 1 || ActivityMinSeconds < 1 ||
            ActivityMaxSeconds < ActivityMinSeconds || WalkMaxSeconds < WalkMinSeconds ||
            SingCheckSeconds < 1 || SingChance > 1 || PetMaxSpeed < PetMinSpeed ||
            NightStartHour is < 0 or > 23 || NightEndHour is < 0 or > 23)
            throw new InvalidDataException("Invalid behavior settings");
    }
}

public readonly record struct Input(DateTime LocalTime, double IdleSeconds, bool Near, bool AtHead,
    bool AtFeet, bool Held, bool Clicked, double Speed, int CursorDirection);

// Pure behavior model. Elapsed time and local time are supplied by the host/tests.
public sealed class Behavior(Settings settings, int? seed = null)
{
    private readonly Random random = seed.HasValue ? new(seed.Value) : new();
    public Mood State { get; private set; } = Mood.Idle;
    public int Direction { get; private set; } = 1;
    public double Annoyance { get; private set; }
    public double StateAge { get; private set; }
    public double Movement { get; private set; }
    private double elapsed, until, nextActivity = settings.ActivityMinSeconds, nextSong = settings.SingCheckSeconds, lastSong = -settings.SingCooldownSeconds, lastInteraction;
    private double gentle, petDuration, lastBite = -3, previousIdle, leaveDelay;
    private bool hasPreviousIdle, preview;

    private double Between(double min, double max) => min + random.NextDouble() * (max - min);
    private void Set(Mood state, double duration = 0)
    {
        if (State != state) StateAge = 0;
        State = state; until = elapsed + duration;
    }
    public void TurnAtEdge() => Direction *= -1;
    public void Force(Mood state) { preview = true; StateAge = 0; Set(state, 4); }
    public void Resume() { preview = false; StateAge = 0; Set(Mood.Idle); gentle = petDuration = 0; hasPreviousIdle = false; nextActivity = elapsed + settings.ActivityMinSeconds; }
    public void Tick(double seconds, Input input)
    {
        var dt = Math.Clamp(seconds, 0, .1); // Never replay suspended time.
        elapsed += dt; StateAge += dt; Movement = 0;
        var inputResumed = hasPreviousIdle && input.IdleSeconds + .2 < previousIdle;
        previousIdle = input.IdleSeconds; hasPreviousIdle = true;
        if (preview) { if (State == Mood.Walk) Movement = Direction * settings.WalkSpeed * dt; return; }
        bool direct = input.Clicked || (input.Held && input.AtHead);
        if (direct) lastInteraction = elapsed;
        if (input.Clicked) Annoyance = Math.Clamp(Annoyance + (State == Mood.Sleep ? settings.WakeAnnoyance : settings.ClickAnnoyance),0,100);
        if (State == Mood.Sleep)
        {
            if (inputResumed || input.Clicked) Set(Mood.Stretch, 1);
            return;
        }
        // Preserve wake irritation during the stretch so a wake-click can lead to a bite.
        if (State == Mood.Stretch && elapsed < until) return;
        if (State == Mood.Bite && elapsed < until) return;
        if (State == Mood.Bite) { Annoyance = 20; Set(Mood.Idle); }
        if (State == Mood.Stretch) Set(Mood.Idle);
        var night = settings.NightStartHour > settings.NightEndHour
            ? input.LocalTime.Hour >= settings.NightStartHour || input.LocalTime.Hour < settings.NightEndHour
            : input.LocalTime.Hour >= settings.NightStartHour && input.LocalTime.Hour < settings.NightEndHour;
        if (!direct && input.IdleSeconds >= (night ? settings.NightSleepSeconds : settings.DaySleepSeconds))
        { gentle = petDuration = 0; Set(Mood.Sleep); return; }

        if (input.Held && input.AtHead)
        {
            if (input.Speed >= settings.PetMinSpeed && input.Speed <= settings.PetMaxSpeed)
            { gentle += dt; petDuration += dt; }
            else { gentle = 0; if (input.Speed > settings.PetMaxSpeed) Annoyance += settings.RoughAnnoyancePerSecond * dt; }
            if (petDuration > settings.PetToleranceSeconds) Annoyance += settings.LongPetAnnoyancePerSecond * dt;
        }
        else { gentle = petDuration = 0; }
        Annoyance = Math.Clamp(Annoyance, 0, 100);
        if (Annoyance >= settings.BiteThreshold && input.Near && elapsed - lastBite >= settings.BiteCooldownSeconds)
        { Direction = input.CursorDirection; lastBite = elapsed; Set(Mood.Bite, .4); return; }
        if (!direct) Annoyance = Math.Max(0, Annoyance-settings.RecoveryPerSecond*dt);
        if (gentle >= settings.PetConfirmSeconds) { Set(Mood.Pet); return; }
        if (input.Near)
        {
            leaveDelay = elapsed + .7;
            // Keep the invitation pose while the cursor approaches the head.
            if (State == Mood.Beg && elapsed < until && !input.AtFeet) return;
            Direction = input.CursorDirection;
            Set(input.AtFeet ? Mood.Lift : Mood.Look); return;
        }
        if (State is Mood.Look or Mood.Lift or Mood.Pet)
        { Set(Mood.Idle); nextActivity = Math.Max(nextActivity, leaveDelay); }
        if (State is Mood.Walk or Mood.Beg or Mood.Sing)
        {
            if (elapsed < until) { if (State == Mood.Walk) Movement = Direction * settings.WalkSpeed * dt; return; }
            Set(Mood.Idle);
        }
        if (elapsed >= nextSong)
        {
            nextSong = elapsed + settings.SingCheckSeconds;
            if (elapsed - lastInteraction >= settings.SingIdleSeconds && elapsed - lastSong >= settings.SingCooldownSeconds && random.NextDouble() < settings.SingChance)
            { lastSong = elapsed; Set(Mood.Sing, Between(3, 5)); return; }
        }
        if (elapsed >= nextActivity)
        {
            nextActivity = elapsed + Between(settings.ActivityMinSeconds, settings.ActivityMaxSeconds);
            var choice = random.NextDouble();
            if (choice >= .85) Set(Mood.Beg, settings.BegSeconds);
            else if (choice >= .5) { Direction = random.Next(2) == 0 ? -1 : 1; Set(Mood.Walk, Between(settings.WalkMinSeconds, settings.WalkMaxSeconds)); }
        }
    }
}

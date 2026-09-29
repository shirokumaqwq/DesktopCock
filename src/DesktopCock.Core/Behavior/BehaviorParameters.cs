namespace DesktopCock.Core;

public sealed class BehaviorParameters
{
    public double DaySleepSeconds { get; set; } = 600;
    public double NightSleepSeconds { get; set; } = 120;
    public int NightStartHour { get; set; } = 22;
    public int NightEndHour { get; set; } = 7;
    public double ActivityMinSeconds { get; set; } = 8;
    public double ActivityMaxSeconds { get; set; } = 20;
    public double WalkMinSeconds { get; set; } = 2;
    public double WalkMaxSeconds { get; set; } = 5;
    public double WalkSpeed { get; set; } = 16;
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

using System;

/// <summary>
/// Holds session configuration passed from the Main Menu into a newly launched galaxy.
/// </summary>
public static class GameConfig
{
    public const int MinStars = 10;
    public const int MaxStars = 100;
    public const int StarIncrement = 10;
    public const int MinEmpires = 1;
    public const int MaxEmpires = 12;

    public const int DefaultQuickStartStars = 30;
    public const int DefaultQuickStartEmpires = 4;

    public static int NumberOfStars { get; set; } = DefaultQuickStartStars;
    public static int NumberOfEmpires { get; set; } = DefaultQuickStartEmpires;
    public static bool HasPendingConfig { get; set; } = false;

    public static int NormalizeStarCount(int stars)
    {
        int clamped = Math.Clamp(stars, MinStars, MaxStars);
        int snapped = ((clamped + StarIncrement / 2) / StarIncrement) * StarIncrement;
        return Math.Clamp(snapped, MinStars, MaxStars);
    }

    public static int ClampEmpiresForStars(int empires, int stars)
    {
        int maxAllowed = Math.Min(MaxEmpires, Math.Max(MinEmpires, stars));
        return Math.Clamp(empires, MinEmpires, maxAllowed);
    }

    public static void ConfigureQuickStart()
    {
        NumberOfStars = DefaultQuickStartStars;
        NumberOfEmpires = ClampEmpiresForStars(DefaultQuickStartEmpires, DefaultQuickStartStars);
        HasPendingConfig = true;
    }

    public static void ConfigureCustomGame(int stars, int empires)
    {
        NumberOfStars = NormalizeStarCount(stars);
        NumberOfEmpires = ClampEmpiresForStars(empires, NumberOfStars);
        HasPendingConfig = true;
    }
}

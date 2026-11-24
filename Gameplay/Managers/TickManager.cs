using System.Collections.Generic;
using System.Linq; // Required for AsReadOnly() if not already present
using Godot;

public partial class TickManager : Node
{
    private static TickManager _instance;
    public static bool HasInstance => _instance != null;
    public static TickManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushError(
                    "TickManager.Instance was accessed before it was ready or TickManager is not in the scene tree. Ensure it's added to the scene, possibly as an Autoload."
                );
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public const float DefaultTickInterval = 1.0f;
    public const float MinSpeedMultiplier = 0.25f;
    public const float MaxSpeedMultiplier = 4.0f;
    private static readonly float[] SpeedSteps = { 0.25f, 0.5f, 1.0f, 2.0f, 4.0f };

    public int tickCount = 0;
    public float TickUpdateInterval { get; set; } = DefaultTickInterval; // 1 tick (1 month) per second at 1x speed
    public bool IsPaused { get; set; } = false;
    private float _tickUpdateTimer = 0.0f;

    public float SpeedMultiplier => TickUpdateInterval > 0f ? DefaultTickInterval / TickUpdateInterval : 0f;

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (multiplier <= 0f)
        {
            IsPaused = true;
            return;
        }

        IsPaused = false;
        float clamped = Mathf.Clamp(multiplier, MinSpeedMultiplier, MaxSpeedMultiplier);
        TickUpdateInterval = DefaultTickInterval / clamped;
    }

    public void DoubleSpeed()
    {
        int idx = GetNearestSpeedStepIndex();
        if (idx < SpeedSteps.Length - 1)
        {
            TickUpdateInterval = DefaultTickInterval / SpeedSteps[idx + 1];
        }
    }

    public void HalveSpeed()
    {
        int idx = GetNearestSpeedStepIndex();
        if (idx > 0)
        {
            TickUpdateInterval = DefaultTickInterval / SpeedSteps[idx - 1];
        }
    }

    private int GetNearestSpeedStepIndex()
    {
        float current = SpeedMultiplier;
        int bestIdx = 2; // 1.0x default
        float bestDiff = float.MaxValue;
        for (int i = 0; i < SpeedSteps.Length; i++)
        {
            float diff = Mathf.Abs(SpeedSteps[i] - current);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    /// <summary>
    /// Converts a 0-based total-months tick counter into a 1-based calendar year.
    /// </summary>
    public static int GetYear(int totalMonths) => (totalMonths < 0 ? 0 : totalMonths / 12) + 1;

    /// <summary>
    /// Converts a 0-based total-months tick counter into a 1-based calendar month (1..12).
    /// </summary>
    public static int GetMonth(int totalMonths) => (totalMonths < 0 ? 0 : totalMonths % 12) + 1;

    /// <summary>
    /// Formats a tick count as "Year X, Month Y" (each tick represents 1 month).
    /// </summary>
    public static string FormatDate(int totalMonths) =>
        $"Year {GetYear(totalMonths)}, Month {GetMonth(totalMonths)}";

    /// <summary>
    /// Formats a tick count as a compact "YX MY" date tag for Chronicle entries.
    /// </summary>
    public static string FormatShortDate(int totalMonths) =>
        $"Y{GetYear(totalMonths)} M{GetMonth(totalMonths)}";

    [Signal]
    public delegate void TickUpdateSignalEventHandler(int value);

    public override void _EnterTree()
    {
        if (_instance == null)
        {
            Instance = this;
        }
        ChronicleManager.TickProvider = () => HasInstance ? _instance.tickCount : 0;
    }

    public override void _Process(double delta)
    {
        if (IsPaused)
        {
            return;
        }

        float dt = (float)delta;

        // If the interval is not positive, population updates are effectively paused.
        // This check prevents issues if the interval is misconfigured.
        if (TickUpdateInterval <= 0f)
        {
            // Optionally, log a warning once if this state is undesirable.
            GD.PrintErr(
                "StarManager: PopulationUpdateInterval is not positive. Population updates paused."
            );
            return;
        }

        _tickUpdateTimer += dt;

        // Check if enough time has passed to perform the population update.
        if (_tickUpdateTimer >= TickUpdateInterval)
        {
            tickCount++;

            // Update starmanager tick if present in the scene tree
            if (StarManager.HasInstance)
            {
                StarManager.Instance.UpdateTick();
            }

            // Subtract the processed interval from the timer to carry over any remainder.
            _tickUpdateTimer -= TickUpdateInterval;
            EmitSignal(SignalName.TickUpdateSignal, tickCount);
        }
    }
}

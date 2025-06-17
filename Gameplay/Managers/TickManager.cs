using System.Collections.Generic;
using System.Linq; // Required for AsReadOnly() if not already present
using Godot;

public partial class TickManager : Node
{
    private static TickManager _instance;
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

    public int tickCount = 0;
    public float TickUpdateInterval { get; set; } = 10.0f; // Time in seconds between population updates
    private float _tickUpdateTimer = 0.0f;

    [Signal]
    public delegate void TickUpdateSignalEventHandler(int value);

    public override void _EnterTree()
    {
        if (_instance == null)
        {
            Instance = this;
        }
    }

    public override void _Process(double delta)
    {
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

            // Update starmanager tick
            StarManager.Instance.UpdateTick();

            // Subtract the processed interval from the timer to carry over any remainder.
            _tickUpdateTimer -= TickUpdateInterval;
            EmitSignal(SignalName.TickUpdateSignal, tickCount);
        }
    }
}

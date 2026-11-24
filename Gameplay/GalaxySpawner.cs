using System;
using System.Collections.Generic;
using Godot;

public partial class GalaxySpawner : Node2D
{
    public override void _Ready()
    {
        // Reset the Chronicle DB and simulation clock only when starting a new game
        ChronicleManager.Instance?.ResetWorldState();
        if (TickManager.HasInstance)
        {
            TickManager.Instance.tickCount = 0;
            TickManager.Instance.SetSpeedMultiplier(1.0f);
            TickManager.Instance.IsPaused = false;
        }
        // Star spawning is handled by StarManager.Instance._Ready()
        // Empire spawning is handled by EmpireManager in its own _Ready method or via direct call.
    }
}

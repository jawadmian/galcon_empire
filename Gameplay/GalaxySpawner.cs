using System;
using System.Collections.Generic;
using Godot;

public partial class GalaxySpawner : Node2D
{
    public override void _Ready()
    {
        // Star spawning is handled by StarManager.Instance._Ready()
        // Empire spawning is handled by EmpireManager in its own _Ready method or via direct call.
    }
}

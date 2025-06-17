using System;
using Godot;

public partial class Empire : Node // Or whatever base class you use
{
    public Star HomeStar { get; set; }
    public string EmpireName { get; set; }
    public Color EmpireColor { get; set; }

    public override void _Ready()
    {
        base._Ready();
        if (HomeStar == null)
        {
            GD.PrintErr($"Empire '{EmpireName ?? Name}' initialized WITHOUT a Home Star!");
        }
    }
}

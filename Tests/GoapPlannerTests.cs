using Godot;
using System;
using System.Collections.Generic;

public partial class GoapPlannerTests : Node
{
    public override void _Ready()
    {
        GD.Print("--- Running Goap Planner Tests ---");
        RunTest("Test Simple Expansion Plan", TestSimpleExpansionPlan);
        GD.Print("--- Goap Planner Tests Completed ---");
    }

    private void RunTest(string name, Action testFunc)
    {
        try
        {
            testFunc();
            GD.Print($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[FAIL] {name}: {ex.Message}");
        }
    }

    private void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private void TestSimpleExpansionPlan()
    {
        var planner = new GoapPlanner();
        var startState = new Blackboard();
        startState.SetValue("Stockpile_Ore", 0L);
        startState.SetValue("Production_Ore", 10L);
        startState.SetValue("HasShipyard", false);
        startState.SetValue("HasColonyShip", false);
        startState.SetValue("OwnedStarsCount", 1);

        // We use GrowResourceGoal now
        var goal = new GrowResourceGoal(ResourceType.Ore, 15L);

        // Mock an improvement for testing
        var mockImprovement = new ImprovementResource
        {
            ImprovementName = "Test Mine",
            BuildTime = 5
        };
        mockImprovement.BuildCost[ResourceType.Ore] = 50;
        mockImprovement.ResourceOutput[ResourceType.Ore] = 5;

        var actions = new List<GoapAction>
        {
            new WaitAction(),
            new BuildImprovementAction(mockImprovement),
            new BuildShipyardAction(),
            new BuildColonyShipAction(),
            new ColonizeAction()
        };

        var plan = planner.Plan(startState, goal, actions);

        Assert(plan != null, "Planner should find a valid plan.");
        Assert(plan.Count > 0, "Plan should have steps.");

        // We expect it to Wait -> BuildImprovementAction
        var actionsList = new List<GoapAction>(plan);
        Assert(actionsList[actionsList.Count - 1] is BuildImprovementAction, "Last action should be BuildImprovementAction to achieve the Ore goal.");
    }
}

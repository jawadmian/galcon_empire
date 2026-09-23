using Godot;
using System;
using System.Collections.Generic;

public partial class GoapPlannerTests : Node
{
    public override void _Ready()
    {
        GD.Print("--- Running Goap Planner Tests ---");
        RunTest("Test Simple Expansion Plan", TestSimpleExpansionPlan);
        RunTest("Test Build Shipyard Plan", TestBuildShipyardPlan);
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

    private void TestBuildShipyardPlan()
    {
        var planner = new GoapPlanner();
        var startState = new Blackboard();
        startState.SetValue($"Stockpile_{ResourceType.Ore}", 1500L);
        startState.SetValue($"Stockpile_{ResourceType.Money}", 1000L);
        startState.SetValue($"Production_{ResourceType.Ore}", 50L);
        startState.SetValue($"Production_{ResourceType.Money}", 50L);
        startState.SetValue("HasShipyard", false);
        startState.SetValue("OwnedStarsCount", 1);

        var goal = new BuildShipyardGoal();

        var shipyardImprovement = new ImprovementResource
        {
            ImprovementName = "Test Shipyard",
            BuildTime = 3,
        };
        shipyardImprovement.ResourceOutput[ResourceType.ShipyardProduction] = 25;
        shipyardImprovement.BuildCost[ResourceType.Ore] = 1000;
        shipyardImprovement.BuildCost[ResourceType.Money] = 500;

        var actions = new List<GoapAction>
        {
            new BuildImprovementAction(shipyardImprovement),
            new BuildColonyShipAction(),
            new ColonizeAction()
        };

        var plan = planner.Plan(startState, goal, actions);

        Assert(plan != null, "Planner should find a plan for BuildShipyardGoal.");
        Assert(plan.Count == 1, "Plan should consist of 1 action.");
        var action = plan.Peek();
        Assert(action is BuildImprovementAction, "Action should be BuildImprovementAction.");

        // Simulate plan execution
        var testEmpire = new Empire { EmpireName = "Test Empire" };
        var testStar = new Star { StarName = "Test Star" };
        testEmpire.AddStar(testStar);

        bool performed = action.Perform(testEmpire, startState);
        Assert(performed, "Perform should succeed and queue shipyard.");
        Assert(testEmpire.HasShipyardPending(), "Empire should have a shipyard pending construction.");
    }
}

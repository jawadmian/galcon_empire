using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class GoapPlannerTests
{
    [TestCase]
    public void TestSimpleExpansionPlan()
    {
        var planner = new GoapPlanner();
        var startState = new Blackboard();
        startState.SetValue("Stockpile_Ore", 0L);
        startState.SetValue("Production_Ore", 10L);
        startState.SetValue("HasShipyard", false);
        startState.SetValue("HasColonyShip", false);
        startState.SetValue("OwnedStarsCount", 1);

        var goal = new GrowResourceGoal(ResourceType.Ore, 15L);

        var mockImprovement = new ImprovementResource
        {
            ImprovementName = "Test Mine",
            BuildTime = 5,
        };
        mockImprovement.BuildCost[ResourceType.Ore] = 50;
        mockImprovement.ResourceOutput[ResourceType.Ore] = 5;

        var actions = new List<GoapAction>
        {
            new WaitAction(),
            new BuildImprovementAction(mockImprovement),
            new BuildColonyShipAction(),
            new ColonizeAction(),
        };

        var plan = planner.Plan(startState, goal, actions);

        AssertThat(plan).IsNotNull();
        AssertThat(plan.Count).IsGreater(0);

        var actionsList = new List<GoapAction>(plan);
        AssertThat(actionsList[actionsList.Count - 1] is BuildImprovementAction).IsTrue();
    }

    [TestCase]
    public void TestBuildShipyardPlan()
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
            new ColonizeAction(),
        };

        var plan = planner.Plan(startState, goal, actions);

        AssertThat(plan).IsNotNull();
        AssertThat(plan.Count).IsEqual(1);
        var action = plan.Peek();
        AssertThat(action is BuildImprovementAction).IsTrue();

        var testEmpire = new Empire { EmpireName = "Test Empire" };
        var testStar = new Star { StarName = "Test Star" };
        testEmpire.AddStar(testStar);

        bool performed = action.Perform(testEmpire, startState);
        AssertThat(performed).IsTrue();
        AssertThat(testEmpire.HasShipyardPending()).IsTrue();

        testEmpire.Free();
        testStar.Free();
    }
}

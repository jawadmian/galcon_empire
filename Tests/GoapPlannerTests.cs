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

    [TestCase]
    public void TestScrapShipAndImprovementPlanReducesDeficit()
    {
        var planner = new GoapPlanner();
        var goal = new ReduceDeficitGoal();

        // 1. Deficit from expensive ship upkeep -> planner selects ScrapShipAction
        var shipDeficitState = new Blackboard();
        shipDeficitState.SetValue("HasScrappableShip", true);
        shipDeficitState.SetValue("ShipCount", 1);
        shipDeficitState.SetValue("HasScrappableImprovement", false);
        shipDeficitState.SetValue("InstalledImprovementsCount", 0);
        shipDeficitState.SetValue("Production_Ore", -50L);
        shipDeficitState.SetValue("TotalUpkeep_Ore", 50L);
        shipDeficitState.SetValue("ScrappableShipUpkeep_Ore", 50L);
        shipDeficitState.SetValue("Stockpile_Ore", 0L);

        goal.UpdatePriority(shipDeficitState);
        AssertThat(goal.IsValid(shipDeficitState)).IsTrue();
        AssertThat(goal.Priority).IsEqual(3.2f);

        var actions = new List<GoapAction>
        {
            new ScrapShipAction(),
            new ScrapImprovementAction(),
        };

        var shipPlan = planner.Plan(shipDeficitState, goal, actions);
        AssertThat(shipPlan).IsNotNull();
        AssertThat(shipPlan.Count).IsEqual(1);
        AssertThat(shipPlan.Peek() is ScrapShipAction).IsTrue();

        // 2. Deficit from expensive facility upkeep -> planner selects ScrapImprovementAction
        var facilityDeficitState = new Blackboard();
        facilityDeficitState.SetValue("HasScrappableShip", false);
        facilityDeficitState.SetValue("ShipCount", 0);
        facilityDeficitState.SetValue("HasScrappableImprovement", true);
        facilityDeficitState.SetValue("InstalledImprovementsCount", 1);
        facilityDeficitState.SetValue("Production_Money", -40L);
        facilityDeficitState.SetValue("TotalUpkeep_Money", 40L);
        facilityDeficitState.SetValue("ScrappableImprovementUpkeep_Money", 40L);
        facilityDeficitState.SetValue("ScrappableImprovementOutput_Money", 0L);
        facilityDeficitState.SetValue("Stockpile_Money", 0L);

        var facilityPlan = planner.Plan(facilityDeficitState, goal, actions);
        AssertThat(facilityPlan).IsNotNull();
        AssertThat(facilityPlan.Count).IsEqual(1);
        AssertThat(facilityPlan.Peek() is ScrapImprovementAction).IsTrue();
    }

    [TestCase]
    public void TestScrapImprovementFreesSlotForHighPriorityShipyardAndPreventsFlipFlop()
    {
        var planner = new GoapPlanner();
        var shipyardGoal = new BuildShipyardGoal();

        var shipyardImprovement = new ImprovementResource
        {
            ImprovementName = "Test Shipyard",
            BuildTime = 3,
        };
        shipyardImprovement.ResourceOutput[ResourceType.ShipyardProduction] = 25;
        shipyardImprovement.BuildCost[ResourceType.Ore] = 500;
        shipyardImprovement.BuildCost[ResourceType.Money] = 500;

        var actions = new List<GoapAction>
        {
            new BuildImprovementAction(shipyardImprovement),
            new ScrapImprovementAction(),
        };

        // 1. When slots are full (0 available), planner chains ScrapImprovementAction -> BuildImprovementAction
        var fullSlotsState = new Blackboard();
        fullSlotsState.SetValue("OwnedStarsCount", 1);
        fullSlotsState.SetValue("AvailableBuildingSlots", 0);
        fullSlotsState.SetValue("HasScrappableImprovement", true);
        fullSlotsState.SetValue("InstalledImprovementsCount", 3);
        fullSlotsState.SetValue("HasShipyard", false);
        fullSlotsState.SetValue("Stockpile_Ore", 1000L);
        fullSlotsState.SetValue("Stockpile_Money", 1000L);
        fullSlotsState.SetValue("Production_Ore", 100L);
        fullSlotsState.SetValue("Production_Money", 100L);

        var fullPlan = planner.Plan(fullSlotsState, shipyardGoal, actions);
        AssertThat(fullPlan).IsNotNull();
        AssertThat(fullPlan.Count).IsEqual(2);
        AssertThat(fullPlan.Dequeue() is ScrapImprovementAction).IsTrue();
        AssertThat(fullPlan.Dequeue() is BuildImprovementAction).IsTrue();

        // 2. When a slot IS open, high ScrapImprovementAction cost ensures direct build (1 action)
        var openSlotState = fullSlotsState.Clone();
        openSlotState.SetValue("AvailableBuildingSlots", 1);
        var openPlan = planner.Plan(openSlotState, shipyardGoal, actions);
        AssertThat(openPlan).IsNotNull();
        AssertThat(openPlan.Count).IsEqual(1);
        AssertThat(openPlan.Peek() is BuildImprovementAction).IsTrue();

        // 3. Non-critical Ore/Money growth cannot trigger slot-freeing scrap (prevents flip-flopping)
        var oreGoal = new GrowResourceGoal(ResourceType.Ore, 500);
        AssertThat(oreGoal.IsValid(fullSlotsState)).IsFalse();
    }
}

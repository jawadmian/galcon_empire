using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class EmpireTests
{
    private readonly List<Node> _createdNodes = new List<Node>();

    [AfterTest]
    public void TearDown()
    {
        foreach (var node in _createdNodes)
        {
            if (node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            {
                node.Free();
            }
        }
        _createdNodes.Clear();
    }

    private Empire CreateTestEmpire(string name = "TestEmpire")
    {
        var empire = new Empire { EmpireName = name };
        var star = new Star { StarName = name + "_Home" };
        _createdNodes.Add(empire);
        _createdNodes.Add(star);

        empire.HomeStar = star;
        empire._Ready();
        return empire;
    }

    [TestCase]
    public void TestStockpileInitialization()
    {
        var empire = CreateTestEmpire();

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            AssertThat(empire.ResourceStockpiles[type]).IsEqual(0L);
        }
    }

    [TestCase]
    public void TestHomeStarAssignment()
    {
        var empire = new Empire();
        var star = new Star { StarName = "TestStar" };
        _createdNodes.Add(empire);
        _createdNodes.Add(star);

        empire.HomeStar = star;
        AssertThat(empire.OwnedStars.Count).IsEqual(1);
        AssertThat(empire.OwnedStars[0]).IsEqual(star);
    }

    [TestCase]
    public void TestResourceProduction()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;

        // Disable base food to isolate improvement summation
        star.BaseFoodProduction = 0;
        star.ProductionPerTick[ResourceType.Food] = 100;
        star.ProductionPerTick[ResourceType.Ore] = 50;

        empire.UpdateTick(1);
        empire.UpdateTick(2);

        AssertThat(empire.ResourceStockpiles[ResourceType.Food]).IsEqual(200L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(100L);
    }

    [TestCase]
    public void TestQueueRequestAffordability()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Expensive Mine", BuildTime = 5 };
        improvement.BuildCost[ResourceType.Money] = 1000;

        empire.QueueImprovement(improvement, star);
        empire.UpdateTick(1);

        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(0L);
    }

    [TestCase]
    public void TestConstructionResourceDeduction()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Affordable Farm", BuildTime = 1 };
        improvement.BuildCost[ResourceType.Money] = 100;

        empire.ResourceStockpiles[ResourceType.Money] = 500;

        empire.QueueImprovement(improvement, star);
        empire.UpdateTick(1);

        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(400L);
    }

    [TestCase]
    public void TestConstructionTiming()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Slow Building", BuildTime = 3 };

        empire.QueueImprovement(improvement, star);

        empire.UpdateTick(1); // Tick 1: Starts construction (Remaining: 3 -> 2)
        empire.UpdateTick(2); // Tick 2: Remaining: 2 -> 1
        AssertThat(star.Improvements.Count).IsEqual(0);

        empire.UpdateTick(3); // Tick 3: Remaining: 1 -> 0 -> Complete
        AssertThat(star.Improvements.Count).IsEqual(1);
        AssertThat(star.Improvements[0]).IsEqual(improvement);
    }

    [TestCase]
    public void TestPopulationResourceProduction()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        star.BaseFoodProduction = 100;

        // 1. 250 pop -> +2 Ore/Money, Food = 100 base - 2 consumed = +98, ShipyardProduction = 0
        star.StarPopulation = 250;
        empire.UpdateTick(1);

        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(2L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(2L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Food]).IsEqual(98L);
        AssertThat(empire.ResourceStockpiles[ResourceType.ShipyardProduction]).IsEqual(0L);

        // 2. 15,000 pop -> +150 Ore/Money, Food = 100 base - 150 consumed = -50 net (stockpile 98 -> 48)
        star.StarPopulation = 15000;
        empire.UpdateTick(2);

        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(152L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(152L);
        AssertThat(empire.ProductionPerTick[ResourceType.Food]).IsEqual(-50L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Food]).IsEqual(48L);
    }

    [TestCase]
    public void TestImprovementPendingTracking()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;

        var imp1 = new ImprovementResource { ImprovementName = "Mine I", BuildTime = 3 };
        var imp2 = new ImprovementResource { ImprovementName = "Farm I", BuildTime = 2 };

        AssertThat(empire.IsImprovementPending(null, null)).IsFalse();
        AssertThat(empire.IsImprovementPending(imp1, star)).IsFalse();

        empire.QueueImprovement(imp1, star);
        AssertThat(empire.IsImprovementPending(imp1, star)).IsTrue();
        AssertThat(empire.IsImprovementPending(imp2, star)).IsFalse();
    }
}

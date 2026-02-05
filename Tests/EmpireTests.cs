using Godot;
using System;
using System.Collections.Generic;

public partial class EmpireTests : Node
{
    public override void _Ready()
    {
        GD.Print("--- Running Empire Tests ---");
        
        RunTest("Test Resource Stockpile Initialization", TestStockpileInitialization);
        RunTest("Test Home Star Assignment", TestHomeStarAssignment);
        RunTest("Test Resource Production Summation", TestResourceProduction);
        RunTest("Test Build Queue Affordability", TestQueueRequestAffordability);
        RunTest("Test Construction Resource Deduction", TestConstructionResourceDeduction);
        RunTest("Test Construction Timing", TestConstructionTiming);

        GD.Print("--- Empire Tests Completed ---");
        // Exit the test runner if needed, though for now we'll just let it finish.
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

    private Empire CreateTestEmpire(string name = "TestEmpire")
    {
        var empire = new Empire { EmpireName = name };
        var star = new Star { StarName = name + "_Home" };
        // We set home star BEFORE calling _Ready to avoid the error message
        empire.HomeStar = star;
        empire._Ready();
        return empire;
    }

    private void TestStockpileInitialization()
    {
        var empire = CreateTestEmpire();

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            Assert(empire.ResourceStockpiles[type] == 0, $"Resource {type} should initialize to 0.");
        }
    }

    private void TestHomeStarAssignment()
    {
        var empire = new Empire();
        var star = new Star { StarName = "TestStar" };
        
        empire.HomeStar = star;
        Assert(empire.OwnedStars.Count == 1, "Setting HomeStar should add it to OwnedStars.");
        Assert(empire.OwnedStars[0] == star, "HomeStar should be the first entry in OwnedStars.");
    }

    private void TestResourceProduction()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        
        // Mock production on star
        star.ProductionPerTick[ResourceType.Food] = 100;
        star.ProductionPerTick[ResourceType.Ore] = 50;
        
        empire.UpdateTick(1);

        Assert(empire.ResourceStockpiles[ResourceType.Food] == 100, "Food stockpile should increase by production.");
        Assert(empire.ResourceStockpiles[ResourceType.Ore] == 50, "Ore stockpile should increase by production.");
    }

    private void TestQueueRequestAffordability()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Expensive Mine", BuildTime = 5 };
        improvement.BuildCost[ResourceType.Money] = 1000;

        empire.QueueImprovement(improvement, star);
        
        // Tick while unaffordable
        empire.UpdateTick(1);
        
        Assert(empire.ResourceStockpiles[ResourceType.Money] == 0, "No resources should have been deducted yet.");
    }

    private void TestConstructionResourceDeduction()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Affordable Farm", BuildTime = 1 };
        improvement.BuildCost[ResourceType.Money] = 100;

        empire.ResourceStockpiles[ResourceType.Money] = 500;
        
        empire.QueueImprovement(improvement, star);
        empire.UpdateTick(1); // Should start construction

        Assert(empire.ResourceStockpiles[ResourceType.Money] == 400, "Money should be deducted when construction starts.");
    }

    private void TestConstructionTiming()
    {
        var empire = CreateTestEmpire();
        var star = empire.HomeStar;
        var improvement = new ImprovementResource { ImprovementName = "Slow Building", BuildTime = 3 };
        
        empire.QueueImprovement(improvement, star);
        
        empire.UpdateTick(1); // Tick 1: Starts construction (Remaining: 3 -> 2 in same tick loop)
        
        empire.UpdateTick(2); // Tick 2: Remaining: 2 -> 1
        Assert(star.Improvements.Count == 0, "Building should not be complete after 2 ticks.");

        empire.UpdateTick(3); // Tick 3: Remaining: 1 -> 0 -> Complete
        Assert(star.Improvements.Count == 1, "Building should be complete after 3 ticks.");
        Assert(star.Improvements[0] == improvement, "The correct improvement should be added.");
    }
}

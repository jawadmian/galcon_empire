using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class ShipSystemTests
{
    private ImprovementResource CreateShipyardImprovement(int spp = 25)
    {
        var shipyard = new ImprovementResource
        {
            ImprovementName = "Test Orbital Shipyard",
            BuildTime = 3,
        };
        shipyard.ResourceOutput[ResourceType.ShipyardProduction] = spp;
        shipyard.BuildCost[ResourceType.Ore] = 1000;
        shipyard.BuildCost[ResourceType.Money] = 500;
        return shipyard;
    }

    private ShipDesignResource CreateColonyShipDesign(
        int sppCost = 150,
        float speed = 40f,
        int oreCost = 1500,
        int moneyCost = 500
    )
    {
        var design = new ShipDesignResource
        {
            DesignId = "test_colony_ship",
            DesignName = "Test Colony Ship",
            Speed = speed,
            MaxHitPoints = 100,
            AttackDamage = 0,
            DefenseRating = 5,
            CargoCapacity = 5000,
            SensorRange = 150f,
            ProductionCost = sppCost,
        };
        design.ResourceCosts[ResourceType.Ore] = oreCost;
        design.ResourceCosts[ResourceType.Money] = moneyCost;
        design.SpecialAbilities.Add(ShipAbility.Colonize);
        return design;
    }

    [TestCase]
    public void TestShipyardProductionIsNonStockpileable()
    {
        var empire = new Empire { EmpireName = "TestEmpire" };
        var star = new Star { StarName = "Sol", StarPopulation = 5000 };
        empire.HomeStar = star;
        star.AddImprovement(CreateShipyardImprovement(25));

        empire.UpdateTick(1);
        empire.UpdateTick(2);
        empire.UpdateTick(3);

        // Idle shipyard must NOT accumulate ShipyardProduction in global stockpiles
        AssertThat(empire.ResourceStockpiles[ResourceType.ShipyardProduction]).IsEqual(0L);
        // ProductionPerTick reflects only the shipyard's 25 SPP, not raw population
        AssertThat(empire.ProductionPerTick[ResourceType.ShipyardProduction]).IsEqual(25L);
        // Stockpileable resources still accumulate from population (50/tick * 3 = 150)
        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(150L);

        empire.Free();
        star.Free();
    }

    [TestCase]
    public void TestPerStarShipConstructionQueue()
    {
        var empire = new Empire { EmpireName = "Builders" };
        var star = new Star { StarName = "Dockyard", Position = new Vector2(100, 100) };
        empire.HomeStar = star;
        star.AddImprovement(CreateShipyardImprovement(25));

        empire.ResourceStockpiles[ResourceType.Ore] = 2000;
        empire.ResourceStockpiles[ResourceType.Money] = 1000;

        var colonyDesign = CreateColonyShipDesign(sppCost: 150, oreCost: 1500, moneyCost: 500);
        bool queued = empire.QueueShip(colonyDesign, star);
        AssertThat(queued).IsTrue();
        AssertThat(empire.HasShipPending(ShipAbility.Colonize)).IsTrue();

        // Tick 1: starts construction, deducts resources, applies first 25 SPP (25/150)
        empire.UpdateTick(1);
        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(500L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(500L);
        AssertThat(empire.ShipBuildQueue.Count).IsEqual(1);
        AssertThat(empire.OwnedFleets.Count).IsEqual(0);

        // Ticks 2..5: 50, 75, 100, 125 SPP
        for (int t = 2; t <= 5; t++)
        {
            empire.UpdateTick(t);
            AssertThat(empire.OwnedFleets.Count).IsEqual(0);
        }

        // Tick 6: 150/150 SPP -> ship completes and spawns fleet
        empire.UpdateTick(6);
        AssertThat(empire.ShipBuildQueue.Count).IsEqual(0);
        AssertThat(empire.OwnedFleets.Count).IsEqual(1);
        AssertThat(empire.HasFleetWithAbility(ShipAbility.Colonize)).IsTrue();

        foreach (var fleet in new List<Fleet>(empire.OwnedFleets))
        {
            fleet.Disband();
            if (!fleet.IsInsideTree() && !fleet.IsQueuedForDeletion())
            {
                fleet.Free();
            }
        }
        empire.Free();
        star.Free();
    }

    [TestCase]
    public void TestFleetStraightLineMovementTickDriven()
    {
        var empire = new Empire { EmpireName = "Voyagers" };
        var origin = new Star { StarName = "Origin", Position = new Vector2(0, 0) };
        var target = new Star { StarName = "Destination", Position = new Vector2(100, 0) };
        empire.HomeStar = origin;

        var fastDesign = new ShipDesignResource { DesignId = "scout", DesignName = "Scout", Speed = 80f };
        var slowDesign = CreateColonyShipDesign(speed: 40f);

        var fleet = new Fleet();
        fleet.Initialize(empire, origin, new Ship(fastDesign));
        fleet.AddShip(new Ship(slowDesign));

        // Fleet speed must match slowest ship (40)
        AssertThat(fleet.FleetSpeed).IsEqual(40f);

        bool arrived = false;
        fleet.FleetArrivedSignal += (f, dest) => arrived = dest == target;

        fleet.OrderMoveTo(target);
        AssertThat(fleet.State).IsEqual(FleetState.InTransit);

        // Tick 1: (0,0) -> (40,0)
        fleet.UpdateTick();
        AssertThat(fleet.Position.X).IsEqualApprox(40f, 0.01f);
        AssertThat(arrived).IsFalse();

        // Tick 2: (40,0) -> (80,0)
        fleet.UpdateTick();
        AssertThat(fleet.Position.X).IsEqualApprox(80f, 0.01f);
        AssertThat(arrived).IsFalse();

        // Tick 3: remaining distance 20 <= speed 40 -> snaps to (100,0) and arrives
        fleet.UpdateTick();
        AssertThat(fleet.Position.X).IsEqualApprox(100f, 0.01f);
        AssertThat(fleet.State).IsEqual(FleetState.InOrbit);
        AssertThat(fleet.OrbitingStar).IsEqual(target);
        AssertThat(arrived).IsTrue();

        fleet.Free();
        empire.Free();
        origin.Free();
        target.Free();
    }

    [TestCase]
    public void TestColonyShipColonizationSuccess()
    {
        var empire = new Empire { EmpireName = "Pioneers", EmpireColor = Colors.Cyan };
        var home = new Star { StarName = "Home", Position = new Vector2(0, 0) };
        var unowned = new Star { StarName = "Frontier", Position = new Vector2(60, 0) };
        empire.HomeStar = home;

        var fleet = new Fleet();
        fleet.Initialize(empire, home, new Ship(CreateColonyShipDesign(speed: 40f)));
        empire.RegisterFleet(fleet);

        fleet.OrderColonize(unowned);

        // Tick 1: (40, 0)
        fleet.UpdateTick();
        AssertThat(unowned.OwningEmpire).IsNull();

        // Tick 2: arrives at (60, 0) and colonizes
        fleet.UpdateTick();
        AssertThat(unowned.OwningEmpire).IsEqual(empire);
        AssertThat(empire.OwnedStars.Count).IsEqual(2);
        AssertThat(unowned.StarPopulation).IsEqual(5000);
        AssertThat(fleet.IsEmpty).IsTrue();
        AssertThat(empire.OwnedFleets.Count).IsEqual(0);

        fleet.Free();
        empire.Free();
        home.Free();
        unowned.Free();
    }

    [TestCase]
    public void TestColonyShipAbortsIfAlreadyClaimed()
    {
        var empireA = new Empire { EmpireName = "Empire A" };
        var empireB = new Empire { EmpireName = "Empire B" };
        var homeA = new Star { StarName = "Home A", Position = new Vector2(0, 0) };
        var contested = new Star { StarName = "Contested", Position = new Vector2(40, 0) };
        empireA.HomeStar = homeA;

        var fleet = new Fleet();
        fleet.Initialize(empireA, homeA, new Ship(CreateColonyShipDesign(speed: 40f)));
        empireA.RegisterFleet(fleet);

        fleet.OrderColonize(contested);

        // Empire B claims the star before Empire A's fleet arrives
        empireB.AddStar(contested);

        fleet.UpdateTick();
        AssertThat(contested.OwningEmpire).IsEqual(empireB);
        // Fleet aborts gracefully without consuming the colony ship
        AssertThat(fleet.IsEmpty).IsFalse();
        AssertThat(fleet.IsIdle).IsTrue();
        AssertThat(fleet.HasAbility(ShipAbility.Colonize)).IsTrue();

        fleet.Free();
        empireA.Free();
        empireB.Free();
        homeA.Free();
        contested.Free();
    }

    [TestCase]
    public void TestGoapColonizationPlanChaining()
    {
        var planner = new GoapPlanner();
        var startState = new Blackboard();
        startState.SetValue($"Stockpile_{ResourceType.Ore}", 2000L);
        startState.SetValue($"Stockpile_{ResourceType.Money}", 1000L);
        startState.SetValue($"Production_{ResourceType.ShipyardProduction}", 25L);
        startState.SetValue("HasShipyard", true);
        startState.SetValue("HasColonyShip", false);
        startState.SetValue("HasIdleColonyShip", false);
        startState.SetValue("HasUnownedStars", true);
        startState.SetValue("OwnedStarsCount", 1);

        var goal = new ColonizeStarGoal();
        goal.UpdatePriority(startState);

        var colonyDesign = CreateColonyShipDesign();
        var actions = new List<GoapAction>
        {
            new BuildColonyShipAction(colonyDesign),
            new ColonizeAction(),
        };

        var plan = planner.Plan(startState, goal, actions);
        AssertThat(plan).IsNotNull();
        AssertThat(plan.Count).IsEqual(2);

        var step1 = plan.Dequeue();
        var step2 = plan.Dequeue();
        AssertThat(step1 is BuildColonyShipAction).IsTrue();
        AssertThat(step2 is ColonizeAction).IsTrue();
    }

    [TestCase]
    public void TestGoapColonizationPlanWithAllImprovementsInPool()
    {
        var planner = new GoapPlanner();
        var startState = new Blackboard();
        startState.SetValue($"Stockpile_{ResourceType.Food}", 5000L);
        startState.SetValue($"Stockpile_{ResourceType.Ore}", 5000L);
        startState.SetValue($"Stockpile_{ResourceType.Money}", 5000L);
        startState.SetValue($"Production_{ResourceType.Food}", 500L);
        startState.SetValue($"Production_{ResourceType.Ore}", 2000L);
        startState.SetValue($"Production_{ResourceType.Money}", 2000L);
        startState.SetValue($"Production_{ResourceType.ShipyardProduction}", 25L);
        startState.SetValue("HasShipyard", true);
        startState.SetValue("HasColonyShip", false);
        startState.SetValue("HasIdleColonyShip", false);
        startState.SetValue("HasUnownedStars", true);
        startState.SetValue("OwnedStarsCount", 1);

        var mine = new ImprovementResource { ImprovementName = "Mine", BuildTime = 3 };
        mine.BuildCost[ResourceType.Money] = 1000;
        mine.ResourceOutput[ResourceType.Ore] = 2000;

        var farm = new ImprovementResource { ImprovementName = "Farm", BuildTime = 2 };
        farm.BuildCost[ResourceType.Ore] = 2000;
        farm.ResourceOutput[ResourceType.Food] = 500;

        var market = new ImprovementResource { ImprovementName = "Market", BuildTime = 2 };
        market.BuildCost[ResourceType.Food] = 2000;
        market.ResourceOutput[ResourceType.Money] = 2000;

        var shipyard = CreateShipyardImprovement(25);
        var colonyDesign = CreateColonyShipDesign();

        var actions = new List<GoapAction>
        {
            new BuildImprovementAction(mine),
            new BuildImprovementAction(farm),
            new BuildImprovementAction(market),
            new BuildImprovementAction(shipyard),
            new BuildColonyShipAction(colonyDesign),
            new ColonizeAction(),
        };

        var goal = new ColonizeStarGoal();
        goal.UpdatePriority(startState);

        var plan = planner.Plan(startState, goal, actions);
        AssertThat(plan).IsNotNull();
        AssertThat(plan.Count).IsEqual(2);
        AssertThat(plan.Dequeue() is BuildColonyShipAction).IsTrue();
        AssertThat(plan.Dequeue() is ColonizeAction).IsTrue();
    }
}

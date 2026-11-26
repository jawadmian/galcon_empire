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
        // Stockpileable resources accumulate from population minus shipyard upkeep: (50 - 20)/tick * 3 = 90
        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(90L);

        empire.Free();
        star.Free();
    }

    [TestCase]
    public void TestPerStarShipConstructionQueue()
    {
        var empire = new Empire { EmpireName = "Builders" };
        var star = new Star { StarName = "Dockyard", Position = new Vector2(100, 100), StarPopulation = 15000 };
        empire.HomeStar = star;
        star.AddImprovement(CreateShipyardImprovement(25));

        empire.ResourceStockpiles[ResourceType.Ore] = 2000;
        empire.ResourceStockpiles[ResourceType.Money] = 1000;

        var colonyDesign = CreateColonyShipDesign(sppCost: 150, oreCost: 1500, moneyCost: 500);
        bool queued = empire.QueueShip(colonyDesign, star);
        AssertThat(queued).IsTrue();
        AssertThat(empire.HasShipPending(ShipAbility.Colonize)).IsTrue();

        // Tick 1: starts construction, deducts 1500 Ore / 500 Money, +150 pop - 20/10 shipyard upkeep = 630 Ore, 640 Money
        empire.UpdateTick(1);
        AssertThat(empire.ResourceStockpiles[ResourceType.Ore]).IsEqual(630L);
        AssertThat(empire.ResourceStockpiles[ResourceType.Money]).IsEqual(640L);
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

        // Fleet starts offset in orbit beside origin, not directly on top of the star
        AssertThat(fleet.Position.DistanceTo(origin.Position)).IsEqualApprox(Fleet.OrbitRadius, 0.1f);

        bool arrived = false;
        fleet.FleetArrivedSignal += (f, dest) => arrived = dest == target;

        fleet.OrderMoveTo(target);
        AssertThat(fleet.State).IsEqual(FleetState.InTransit);

        // Tick 1: moves 40u toward target from orbit offset
        float dist0 = fleet.Position.DistanceTo(target.Position);
        fleet.UpdateTick();
        float dist1 = fleet.Position.DistanceTo(target.Position);
        AssertThat(dist1).IsLess(dist0);
        AssertThat(arrived).IsFalse();

        // Tick 2: remaining distance <= 40 -> arrives in orbit beside target
        fleet.UpdateTick();
        AssertThat(fleet.Position.DistanceTo(target.Position)).IsEqualApprox(Fleet.OrbitRadius, 0.1f);
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
        var unowned = new Star { StarName = "Frontier", Position = new Vector2(100, 0) };
        empire.HomeStar = home;

        var fleet = new Fleet();
        fleet.Initialize(empire, home, new Ship(CreateColonyShipDesign(speed: 40f)));
        empire.RegisterFleet(fleet);

        fleet.OrderColonize(unowned);

        // Tick 1: in transit
        fleet.UpdateTick();
        AssertThat(unowned.OwningEmpire).IsNull();

        // Tick 2: arrives and colonizes
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

    [TestCase]
    public void TestStarBuildingCapAndSpillover()
    {
        var empire = new Empire { EmpireName = "Builders" };
        var starA = new Star { StarName = "Alpha", MaxImprovements = 2 };
        var starB = new Star { StarName = "Beta", MaxImprovements = 2 };
        empire.HomeStar = starA;
        empire.AddStar(starB);

        var mine = new ImprovementResource { ImprovementName = "Mine", BuildTime = 1 };
        mine.ResourceOutput[ResourceType.Ore] = 100;

        // Fill starA to its cap of 2
        empire.QueueImprovement(mine, starA);
        empire.QueueImprovement(mine, starA);
        AssertThat(empire.HasAvailableBuildingSlot(starA)).IsFalse();

        // Attempting a 3rd improvement on starA is rejected
        empire.QueueImprovement(mine, starA);
        AssertThat(empire.GetPendingImprovementsCount(starA)).IsEqual(2);

        // GetStarWithAvailableBuildingSlot spills over to starB
        AssertThat(empire.GetStarWithAvailableBuildingSlot()).IsEqual(starB);

        empire.Free();
        starA.Free();
        starB.Free();
    }

    [TestCase]
    public void TestStarBaseFoodAndPopulationConsumption()
    {
        var star = new Star
        {
            StarName = "Gaia",
            BaseFoodProduction = 100, // Supports 10,000 baseline pop without farms
            GrowthRate = 0.1f,
            StarPopulation = 5000,
        };

        // At 5,000 pop: consumes 50, base 100 -> net +50 -> grows
        AssertThat(star.FoodConsumptionPerTick).IsEqual(50);
        AssertThat(star.NetFoodProductionPerTick).IsEqual(50);
        star.UpdateTick();
        AssertThat(star.StarPopulation).IsEqual(5500);

        // At 10,000 pop (baseline): consumes 100, base 100 -> net 0 -> holds steady without farms
        star.StarPopulation = 10000;
        AssertThat(star.NetFoodProductionPerTick).IsEqual(0);
        star.UpdateTick();
        AssertThat(star.StarPopulation).IsEqual(10000);

        // Overpopulated without farms (12,000 pop): shrinks toward baseline, never starving below 10,000
        star.StarPopulation = 12000;
        AssertThat(star.NetFoodProductionPerTick).IsEqual(-20);
        star.UpdateTick();
        AssertThat(star.StarPopulation).IsEqual(10800);
        star.UpdateTick();
        AssertThat(star.StarPopulation).IsEqual(10000);

        // Adding a Farm (+500 Food) restores surplus and unlocks growth above baseline
        var farm = new ImprovementResource { ImprovementName = "Farm", BuildTime = 2 };
        farm.ResourceOutput[ResourceType.Food] = 500;
        star.AddImprovement(farm);

        AssertThat(star.NetFoodProductionPerTick).IsEqual(500);
        star.UpdateTick();
        AssertThat(star.StarPopulation).IsEqual(11000);

        star.Free();
    }

    [TestCase]
    public void TestSystemSizeScalesSlotsMaxPopAndColonizationPriority()
    {
        var smallStar = new Star { StarName = "Dwarf", Position = new Vector2(200, 0) };
        smallStar.SetSystemSize(3);
        AssertThat(smallStar.SystemSize).IsEqual(3);
        AssertThat(smallStar.MaxImprovements).IsEqual(3);
        AssertThat(smallStar.MaxPopulation).IsEqual(60000);

        var homeStar = new Star { StarName = "Home", Position = new Vector2(0, 0) };
        homeStar.SetSystemSize(Star.HomeSystemSize);
        AssertThat(homeStar.SystemSize).IsEqual(10);
        AssertThat(homeStar.MaxImprovements).IsEqual(10);
        AssertThat(homeStar.MaxPopulation).IsEqual(200000);

        var giantStar = new Star { StarName = "Titan", Position = new Vector2(500, 0) };
        giantStar.SetSystemSize(12);
        AssertThat(giantStar.SystemSize).IsEqual(12);
        AssertThat(giantStar.MaxImprovements).IsEqual(12);
        AssertThat(giantStar.MaxPopulation).IsEqual(240000);

        // Clamping outside 3..12
        var clamped = new Star();
        clamped.SetSystemSize(99);
        AssertThat(clamped.MaxImprovements).IsEqual(12);
        clamped.SetSystemSize(1);
        AssertThat(clamped.MaxImprovements).IsEqual(3);

        // Larger system at 500u outscores small system at 200u for colonization
        float smallScore = StarManager.ScoreStarForColonization(smallStar, 200f);
        float giantScore = StarManager.ScoreStarForColonization(giantStar, 500f);
        AssertThat(giantScore).IsGreater(smallScore);

        // Proximity still outweighs distant large systems across the galaxy
        var nearbyMedium = new Star { StarName = "Nearby", Position = new Vector2(350, 0) };
        nearbyMedium.SetSystemSize(6);
        float nearbyScore = StarManager.ScoreStarForColonization(nearbyMedium, 350f);
        float farGiantScore = StarManager.ScoreStarForColonization(giantStar, 2000f);
        AssertThat(nearbyScore).IsGreater(farGiantScore);
        nearbyMedium.Free();

        // StarManager default spacing is spread out (>= 300u apart)
        var manager = new StarManager();
        AssertThat(manager.MinStarDistanceSquared).IsGreaterEqual(90000.0f);

        smallStar.Free();
        homeStar.Free();
        giantStar.Free();
        clamped.Free();
        manager.Free();
    }

    [TestCase]
    public void TestShipNameCollectionAndLeadShipClassNaming()
    {
        var nautical = GD.Load<ShipNameCollectionResource>("res://Data/ShipNames/NauticalNames.tres");
        AssertThat(nautical).IsNotNull();
        AssertThat(nautical.NonCombatSmallShipNames.Contains("Minnow")).IsTrue();
        AssertThat(nautical.NonCombatLargeShipNames.Contains("Beluga")).IsTrue();
        AssertThat(nautical.SmallShipNames.Contains("Piranha")).IsTrue();
        AssertThat(nautical.LargeShipNames.Contains("Orca")).IsTrue();

        // Verify all 5 themes load cleanly
        var themes = new[]
        {
            "res://Data/ShipNames/NauticalNames.tres",
            "res://Data/ShipNames/MythologyNames.tres",
            "res://Data/ShipNames/CelestialNames.tres",
            "res://Data/ShipNames/ImperialVirtuesNames.tres",
            "res://Data/ShipNames/AvianNames.tres",
        };
        foreach (string path in themes)
        {
            var theme = GD.Load<ShipNameCollectionResource>(path);
            AssertThat(theme).IsNotNull();
            AssertThat(theme.SmallShipNames.Count).IsGreater(0);
            AssertThat(theme.NonCombatSmallShipNames.Count).IsGreater(0);
        }

        var empire = new Empire
        {
            EmpireName = "Obscurus Combine",
            ShipNameCollection = nautical,
        };
        var colonyDesign = new ShipDesignResource
        {
            DesignId = "colony_ship_v1",
            DesignName = "Ark-Class Colony Ship",
            SizeCategory = ShipSizeCategory.Large,
            RoleCategory = ShipRoleCategory.NonCombat,
        };
        var warshipDesign = new ShipDesignResource
        {
            DesignId = "battleship_v1",
            DesignName = "Dreadnought",
            SizeCategory = ShipSizeCategory.Large,
            RoleCategory = ShipRoleCategory.Combat,
        };

        // Non-combat vessels draw from NonCombat pool
        var firstColony = new Ship(colonyDesign);
        empire.AssignShipAndClassName(firstColony);
        AssertThat(firstColony.ShipName).IsEqual("Beluga");
        AssertThat(firstColony.ClassName).IsEqual("Beluga-Class");
        AssertThat(firstColony.ChronicleName).IsEqual("Beluga");

        var secondColony = new Ship(colonyDesign);
        empire.AssignShipAndClassName(secondColony);
        AssertThat(secondColony.ShipName).IsEqual("Manatee");
        AssertThat(secondColony.ClassName).IsEqual("Beluga-Class");

        // Combat vessels draw separately from Combat pool
        var firstWarship = new Ship(warshipDesign);
        empire.AssignShipAndClassName(firstWarship);
        AssertThat(firstWarship.ShipName).IsEqual("Orca");
        AssertThat(firstWarship.ClassName).IsEqual("Orca-Class");

        // Exhaust remaining 6 names in NonCombatLargeShipNames (total 8) and verify numbered suffix
        for (int i = 0; i < 6; i++)
        {
            var extra = new Ship(colonyDesign);
            empire.AssignShipAndClassName(extra);
        }

        var ninthColony = new Ship(colonyDesign);
        empire.AssignShipAndClassName(ninthColony);
        AssertThat(ninthColony.ShipName).IsEqual("Beluga 2");
        AssertThat(ninthColony.ClassName).IsEqual("Beluga-Class");

        // Verify ordinal fleet naming
        AssertThat(empire.GetNextFleetName()).IsEqual("1st Fleet");
        AssertThat(empire.GetNextFleetName()).IsEqual("2nd Fleet");
        AssertThat(empire.GetNextFleetName()).IsEqual("3rd Fleet");

        empire.Free();
    }

    [TestCase]
    public void TestMultiTickColonizationDivertsWhenTargetClaimed()
    {
        var previousStarManager = StarManager.HasInstance ? StarManager.Instance : null;
        var starManager = new StarManager();
        StarManager.SetInstanceForTesting(starManager);

        var empireA = new Empire { EmpireName = "Sovereign Concordat", EmpireColor = Colors.Gold };
        var empireB = new Empire { EmpireName = "Rival Syndicate", EmpireColor = Colors.Red };
        var homeA = new Star { StarName = "Home", Position = new Vector2(0, 0) };
        var primaryTarget = new Star { StarName = "Alpha", Position = new Vector2(160, 0) };
        primaryTarget.SetSystemSize(12);
        var backupTarget = new Star { StarName = "Beta", Position = new Vector2(160, 120) };
        backupTarget.SetSystemSize(8);

        try
        {
            empireA.HomeStar = homeA;
            starManager.RegisterStar(homeA);
            starManager.RegisterStar(primaryTarget);
            starManager.RegisterStar(backupTarget);

            var advisor = new GoapAdvisor(empireA);

            var fleet = new Fleet();
            fleet.Initialize(empireA, homeA, new Ship(CreateColonyShipDesign(speed: 40f)));
            empireA.RegisterFleet(fleet);

            // Tick 1: Sensor update + ColonizeAction picks highest-priority target (Alpha)
            advisor.Recommend(empireA);
            var colonizeAction = new ColonizeAction();
            AssertThat(colonizeAction.CheckPreconditions(advisor.State)).IsTrue();
            AssertThat(colonizeAction.Perform(empireA, advisor.State)).IsTrue();
            AssertThat(fleet.DestinationStar).IsEqual(primaryTarget);
            AssertThat(fleet.State).IsEqual(FleetState.InTransit);

            // Tick 2: Fleet moves 40u toward Alpha
            fleet.UpdateTick();
            AssertThat(fleet.Position.DistanceTo(primaryTarget.Position))
                .IsLess(homeA.Position.DistanceTo(primaryTarget.Position));
            AssertThat(fleet.State).IsEqual(FleetState.InTransit);

            // Empire B claims Alpha mid-flight!
            empireB.AddStar(primaryTarget);

            // Tick 3: Fleet detects Alpha was claimed and diverts to Beta, moving toward it
            fleet.UpdateTick();
            AssertThat(fleet.DestinationStar).IsEqual(backupTarget);
            AssertThat(fleet.CurrentOrder).IsEqual(FleetOrderType.Colonize);
            AssertThat(fleet.State).IsEqual(FleetState.InTransit);

            // Subsequent ticks: Fleet reaches Beta and colonizes it
            for (int t = 0; t < 10 && fleet.State == FleetState.InTransit; t++)
            {
                fleet.UpdateTick();
            }

            AssertThat(backupTarget.OwningEmpire).IsEqual(empireA);
            AssertThat(fleet.IsEmpty).IsTrue();

            fleet.Free();
        }
        finally
        {
            StarManager.SetInstanceForTesting(previousStarManager);
            starManager.Free();
            empireA.Free();
            empireB.Free();
            homeA.Free();
            primaryTarget.Free();
            backupTarget.Free();
        }
    }

    [TestCase]
    public void TestMultiTickColonizationReturnsHomeWhenNoTargetsRemain()
    {
        var previousStarManager = StarManager.HasInstance ? StarManager.Instance : null;
        var starManager = new StarManager();
        StarManager.SetInstanceForTesting(starManager);

        var empireA = new Empire { EmpireName = "Sovereign Concordat", EmpireColor = Colors.Gold };
        var empireB = new Empire { EmpireName = "Rival Syndicate", EmpireColor = Colors.Red };
        var homeA = new Star { StarName = "Home", Position = new Vector2(0, 0) };
        var onlyTarget = new Star { StarName = "Frontier", Position = new Vector2(160, 0) };

        try
        {
            empireA.HomeStar = homeA;
            starManager.RegisterStar(homeA);
            starManager.RegisterStar(onlyTarget);

            var fleet = new Fleet();
            fleet.Initialize(empireA, homeA, new Ship(CreateColonyShipDesign(speed: 40f)));
            empireA.RegisterFleet(fleet);

            fleet.OrderColonize(onlyTarget);
            AssertThat(fleet.State).IsEqual(FleetState.InTransit);

            // Advance 2 ticks mid-flight
            fleet.UpdateTick();
            fleet.UpdateTick();
            float distMid = fleet.Position.DistanceTo(homeA.Position);
            AssertThat(distMid).IsGreater(Fleet.OrbitRadius);

            // Rival claims the only remaining unowned star
            empireB.AddStar(onlyTarget);

            // Next tick: No unowned stars remain -> fleet orders return home (not stuck in space!)
            fleet.UpdateTick();
            AssertThat(fleet.DestinationStar).IsEqual(homeA);
            AssertThat(fleet.CurrentOrder).IsEqual(FleetOrderType.MoveTo);
            AssertThat(fleet.State).IsEqual(FleetState.InTransit);
            AssertThat(fleet.Position.DistanceTo(homeA.Position)).IsLess(distMid);

            // Subsequent ticks: Fleet arrives safely back in orbit beside Home
            for (int t = 0; t < 5 && fleet.State == FleetState.InTransit; t++)
            {
                fleet.UpdateTick();
            }
            AssertThat(fleet.Position.DistanceTo(homeA.Position)).IsEqualApprox(Fleet.OrbitRadius, 0.1f);
            AssertThat(fleet.State).IsEqual(FleetState.InOrbit);
            AssertThat(fleet.OrbitingStar).IsEqual(homeA);
            AssertThat(fleet.IsIdle).IsTrue();
            AssertThat(fleet.HasAbility(ShipAbility.Colonize)).IsTrue();

            fleet.Free();
        }
        finally
        {
            StarManager.SetInstanceForTesting(previousStarManager);
            starManager.Free();
            empireA.Free();
            empireB.Free();
            homeA.Free();
            onlyTarget.Free();
        }
    }

    [TestCase]
    public void TestFleetManagerColonizationDoesNotEvictSiblingFleets()
    {
        var fm = new FleetManager();
        var empireA = new Empire { EmpireName = "Obscurus Combine" };
        var empireB = new Empire { EmpireName = "Stellar Dominion" };
        var homeA = new Star { StarName = "Muhlifain", Position = new Vector2(0, 0) };
        var starA = new Star { StarName = "Nashira", Position = new Vector2(40, 0) };
        var homeB = new Star { StarName = "Ascella", Position = new Vector2(0, 500) };
        var starB = new Star { StarName = "Sheratan", Position = new Vector2(120, 500) };

        try
        {
            var fleetA = fm.SpawnFleet(empireA, homeA, new Ship(CreateColonyShipDesign(speed: 40f)));
            var fleetB = fm.SpawnFleet(empireB, homeB, new Ship(CreateColonyShipDesign(speed: 40f)));
            empireA.RegisterFleet(fleetA);
            empireB.RegisterFleet(fleetB);

            fleetA.OrderColonize(starA); // Arrives & disbands on tick 1
            fleetB.OrderColonize(starB); // Needs 3 ticks to arrive

            // Tick 1: fleetA colonizes starA and disbands; fleetB must remain tracked and advance
            fm.UpdateTick();
            AssertThat(starA.OwningEmpire).IsEqual(empireA);
            AssertThat(fm.ActiveFleets.Count).IsEqual(1);
            AssertThat(fm.ActiveFleets[0]).IsEqual(fleetB);
            AssertThat(fleetB.Position.DistanceTo(starB.Position))
                .IsLess(homeB.Position.DistanceTo(starB.Position));

            // Tick 2 & 3: fleetB finishes its journey and colonizes starB
            fm.UpdateTick();
            fm.UpdateTick();
            AssertThat(starB.OwningEmpire).IsEqual(empireB);

            if (GodotObject.IsInstanceValid(fleetA)) fleetA.Free();
            if (GodotObject.IsInstanceValid(fleetB)) fleetB.Free();
        }
        finally
        {
            fm.Free();
            empireA.Free();
            empireB.Free();
            homeA.Free();
            starA.Free();
            homeB.Free();
            starB.Free();
        }
    }

    [TestCase]
    public void TestColonyShipPopulationCostAndMinimumRemaining()
    {
        var empire = new Empire { EmpireName = "Colonizers", EmpireColor = Colors.Cyan };
        var shipyardStar = new Star { StarName = "Core", Position = new Vector2(0, 0), StarPopulation = 8000 };
        var frontier = new Star { StarName = "NewWorld", Position = new Vector2(40, 0) };
        empire.HomeStar = shipyardStar;
        shipyardStar.AddImprovement(CreateShipyardImprovement(50));

        var colonyDesign = CreateColonyShipDesign(sppCost: 50, speed: 40f);

        // 8,000 pop is insufficient because 5,000 colonists would leave only 3,000 (< 5,000 min)
        AssertThat(empire.QueueShip(colonyDesign, shipyardStar)).IsFalse();

        // 12,000 pop is sufficient (12,000 >= 5,000 + 5,000)
        shipyardStar.StarPopulation = 12000;
        AssertThat(empire.QueueShip(colonyDesign, shipyardStar)).IsTrue();

        // Dispatching a colony ship deducts 5,000 colonists from the departure star, leaving 7,000
        var ship = new Ship(colonyDesign);
        var fleet = new Fleet();
        fleet.Initialize(empire, shipyardStar, ship);
        empire.RegisterFleet(fleet);

        fleet.OrderColonize(frontier);
        AssertThat(shipyardStar.StarPopulation).IsEqual(7000);
        AssertThat(ship.LoadedColonists).IsEqual(5000);

        // Even if TakeColonists is called on a low-pop star, population never drops below 5,000
        int partial = shipyardStar.TakeColonists(5000);
        AssertThat(partial).IsEqual(2000);
        AssertThat(shipyardStar.StarPopulation).IsEqual(Star.MinPopulationAfterColonistDeparture);

        // Colonizing transfers the loaded colonists to the new colony
        fleet.UpdateTick();
        AssertThat(frontier.OwningEmpire).IsEqual(empire);
        AssertThat(frontier.StarPopulation).IsEqual(5000);

        if (GodotObject.IsInstanceValid(fleet)) fleet.Free();
        empire.Free();
        shipyardStar.Free();
        frontier.Free();
    }

    [TestCase]
    public void TestFleetShipCountLabelUpdatesWithShips()
    {
        var empire = new Empire { EmpireName = "Armada", EmpireColor = Colors.Cyan };
        var home = new Star { StarName = "Dock", Position = new Vector2(0, 0) };
        var design = CreateColonyShipDesign();
        var ship1 = new Ship(design);
        var ship2 = new Ship(design);

        var fleet = new Fleet();
        fleet.Initialize(empire, home, ship1);
        fleet._Ready();

        var shipSprite = fleet.GetNodeOrNull<Sprite2D>("ShipSprite");
        AssertThat(shipSprite).IsNotNull();
        AssertThat(shipSprite.Texture).IsNotNull();
        AssertThat(shipSprite.Texture.ResourcePath).IsEqual("res://Images/Ships/fleet_ship_icon.png");

        var countLabel = fleet.GetNodeOrNull<Label>("ShipCountLabel");
        AssertThat(countLabel).IsNotNull();
        AssertThat(countLabel.GetThemeFontSize("font_size")).IsEqual(26);
        AssertThat(countLabel.Text).IsEqual("1");

        fleet.AddShip(ship2);
        AssertThat(countLabel.Text).IsEqual("2");

        fleet.RemoveShip(ship1);
        AssertThat(countLabel.Text).IsEqual("1");

        fleet.Free();
        empire.Free();
        home.Free();
    }
}

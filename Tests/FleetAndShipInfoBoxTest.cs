using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FleetAndShipInfoBoxTest
{
    private readonly List<Node> _createdNodes = new List<Node>();
    private ChronicleManager _chronicleManager;
    private ChronicleManager _previousChronicleManager;

    [BeforeTest]
    public void Setup()
    {
        _previousChronicleManager = ChronicleManager.Instance;
        _chronicleManager = new ChronicleManager();
        _chronicleManager.InitializeDatabase("Data Source=FleetShipUITest;Mode=Memory;Cache=Shared", resetDatabase: true);
        ChronicleManager.SetInstanceForTesting(_chronicleManager);
    }

    [AfterTest]
    public void TearDown()
    {
        foreach (var node in _createdNodes)
        {
            if (node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            {
                if (node.IsInsideTree())
                    node.QueueFree();
                else
                    node.Free();
            }
        }
        _createdNodes.Clear();

        _chronicleManager.CloseDatabase();
        _chronicleManager.Free();
        ChronicleManager.SetInstanceForTesting(_previousChronicleManager);
    }

    private ShipDesignResource CreateColonyDesign()
    {
        var design = new ShipDesignResource
        {
            DesignId = "colony_ship_v1",
            DesignName = "Ark-Class Colony Ship",
            Speed = 40f,
            MaxHitPoints = 100,
            AttackDamage = 0,
            DefenseRating = 5,
            CargoCapacity = 5000,
            SensorRange = 150f,
            ProductionCost = 50,
            RequiredTechLevel = 1,
        };
        design.SpecialAbilities.Add(ShipAbility.Colonize);
        return design;
    }

    [TestCase]
    public void TestFleetAndShipChronicleLifecycleEvents()
    {
        var empire = new Empire { EmpireName = "Vanguard", EmpireColor = Colors.Cyan };
        var home = new Star { StarName = "Sol", Position = new Vector2(0, 0) };
        var frontier = new Star { StarName = "Proxima", Position = new Vector2(40, 0) };
        _createdNodes.Add(empire);
        _createdNodes.Add(home);
        _createdNodes.Add(frontier);

        empire.HomeStar = home;
        var shipyard = new ImprovementResource { ImprovementName = "Shipyard", BuildTime = 1 };
        shipyard.ResourceOutput[ResourceType.ShipyardProduction] = 50;
        home.AddImprovement(shipyard);

        var design = CreateColonyDesign();
        empire.QueueShip(design, home);
        empire.UpdateTick(1); // Starts and completes in 1 tick (50/50 SPP)

        AssertThat(empire.OwnedFleets.Count).IsEqual(1);
        Fleet fleet = empire.OwnedFleets[0];
        Ship ship = fleet.Ships[0];
        _createdNodes.Add(fleet);

        AssertThat(ship.BuiltAtStarName).IsEqual("Sol");
        AssertThat(ship.CurrentFleet).IsEqual(fleet);
        string fleetChronicleId = fleet.ChronicleId;

        // Dispatch and arrive to colonize
        fleet.OrderColonize(frontier);
        fleet.UpdateTick();

        // Both fleet and ship should have Commissioned, Dispatched, Arrived, and New Colony Established events
        var shipEvents = _chronicleManager.QueryEvents(new ChronicleFilter { EntityId = ship.ChronicleId });
        var fleetEvents = _chronicleManager.QueryEvents(new ChronicleFilter { EntityId = fleetChronicleId });

        AssertThat(shipEvents.Count).IsEqual(4);
        AssertThat(fleetEvents.Count).IsEqual(4);

        var titles = new HashSet<string>();
        foreach (var ev in shipEvents)
            titles.Add(ev.Title);

        AssertThat(titles.Contains("Ship Commissioned")).IsTrue();
        AssertThat(titles.Contains("Fleet Dispatched")).IsTrue();
        AssertThat(titles.Contains("Fleet Arrived")).IsTrue();
        AssertThat(titles.Contains("New Colony Established")).IsTrue();
    }

    [TestCase]
    public void TestFleetInfoBoxLoadsDisplaysShipsAndChronicle()
    {
        var scene = GD.Load<PackedScene>("res://UI/fleet_info_box.tscn");
        AssertThat(scene).IsNotNull();

        var box = scene.Instantiate<FleetInfoBox>();
        _createdNodes.Add(box);
        box._Ready();

        // Verify NinePatchRect margins
        var bg = box.GetNodeOrNull<NinePatchRect>("Background");
        var margin = box.GetNodeOrNull<MarginContainer>("Background/ContentMargin");
        AssertThat(bg).IsNotNull();
        AssertThat(margin).IsNotNull();
        AssertThat(margin.GetThemeConstant("margin_top")).IsGreaterEqual(44);

        var empire = new Empire { EmpireName = "Starborn", EmpireColor = Colors.Gold };
        var origin = new Star { StarName = "Vega", Position = new Vector2(0, 0) };
        var target = new Star { StarName = "Altair", Position = new Vector2(200, 0) };
        _createdNodes.Add(empire);
        _createdNodes.Add(origin);
        _createdNodes.Add(target);

        var ship = new Ship(CreateColonyDesign()) { CommissionedTick = 5, BuiltAtStarName = "Vega" };
        var fleet = new Fleet();
        _createdNodes.Add(fleet);
        fleet.Initialize(empire, origin, ship);
        fleet.OrderMoveTo(target); // Records "Fleet Dispatched"

        box.DisplayFleet(fleet);

        var nameLabel = box.GetNodeOrNull<Label>("%NameLabel");
        var statusLabel = box.GetNodeOrNull<Label>("%StatusLabel");
        var shipsList = box.GetNodeOrNull<VBoxContainer>("%ShipsList");
        var chronicleList = box.GetNodeOrNull<VBoxContainer>("%ChronicleList");

        AssertThat(nameLabel.Text).Contains("ARK-CLASS COLONY SHIP");
        AssertThat(statusLabel.Text).Contains("Altair");
        AssertThat(shipsList.GetChildCount()).IsEqual(1);
        AssertThat(shipsList.GetChild(0) is Button).IsTrue();
        AssertThat(chronicleList.GetChildCount()).IsEqual(1);
    }

    [TestCase]
    public void TestShipInfoBoxLoadsDisplaysDetailsAndChronicle()
    {
        var scene = GD.Load<PackedScene>("res://UI/ship_info_box.tscn");
        AssertThat(scene).IsNotNull();

        var box = scene.Instantiate<ShipInfoBox>();
        _createdNodes.Add(box);
        box._Ready();

        var empire = new Empire { EmpireName = "Navigators" };
        var origin = new Star { StarName = "Deneb", Position = new Vector2(0, 0) };
        var dest = new Star { StarName = "Rigel", Position = new Vector2(100, 0) };
        _createdNodes.Add(empire);
        _createdNodes.Add(origin);
        _createdNodes.Add(dest);

        var ship = new Ship(CreateColonyDesign()) { CommissionedTick = 12, BuiltAtStarName = "Deneb" };
        var fleet = new Fleet();
        _createdNodes.Add(fleet);
        fleet.Initialize(empire, origin, ship);
        fleet.OrderMoveTo(dest); // Records "Fleet Dispatched" for ship

        box.DisplayShip(ship);

        var nameLabel = box.GetNodeOrNull<Label>("%NameLabel");
        var originLabel = box.GetNodeOrNull<Label>("%OriginLabel");
        var statsLabel = box.GetNodeOrNull<Label>("%StatsLabel");
        var abilitiesLabel = box.GetNodeOrNull<Label>("%AbilitiesLabel");
        var chronicleList = box.GetNodeOrNull<VBoxContainer>("%ChronicleList");

        AssertThat(nameLabel.Text).Contains("ARK-CLASS COLONY SHIP");
        AssertThat(originLabel.Text).Contains("Day 12");
        AssertThat(originLabel.Text).Contains("Deneb");
        AssertThat(statsLabel.Text).Contains("100/100");
        AssertThat(statsLabel.Text).Contains("40");
        AssertThat(abilitiesLabel.Text).Contains("Colonize");
        AssertThat(chronicleList.GetChildCount()).IsEqual(1);
    }
}

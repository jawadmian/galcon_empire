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
        var home = new Star { StarName = "Sol", Position = new Vector2(0, 0), StarPopulation = 15000 };
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

        var entityTypeLabel = box.GetNodeOrNull<Label>("%EntityTypeLabel");
        AssertThat(entityTypeLabel).IsNotNull();
        AssertThat(entityTypeLabel.Text).IsEqual("FLEET");

        var closeBtn = box.GetNodeOrNull<Button>("%CloseButton");
        AssertThat(closeBtn).IsNotNull();
        AssertThat(closeBtn.OffsetLeft).IsEqual(-36.0f);
        AssertThat(closeBtn.OffsetTop).IsEqual(10.0f);
        AssertThat(closeBtn.OffsetRight).IsEqual(-12.0f);
        AssertThat(closeBtn.OffsetBottom).IsEqual(34.0f);

        var chronicleScroll = box.GetNodeOrNull<ScrollContainer>("Background/ContentMargin/MainVBox/ChronicleScroll");
        AssertThat(chronicleScroll).IsNotNull();
        AssertThat(chronicleScroll.HorizontalScrollMode).IsEqual(ScrollContainer.ScrollMode.Disabled);

        var nameLabel = box.GetNodeOrNull<Label>("%NameLabel");
        var empireLabel = box.GetNodeOrNull<Label>("%EmpireLabel");
        var statusLabel = box.GetNodeOrNull<Label>("%StatusLabel");
        var speedLabel = box.GetNodeOrNull<Label>("%SpeedLabel");
        var shipsList = box.GetNodeOrNull<VBoxContainer>("%ShipsList");
        var chronicleList = box.GetNodeOrNull<VBoxContainer>("%ChronicleList");

        AssertThat(nameLabel.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(empireLabel.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(statusLabel.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(speedLabel.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);

        AssertThat(nameLabel.Text).Contains("1ST FLEET");
        AssertThat(statusLabel.Text).Contains("Altair");
        AssertThat(shipsList.GetChildCount()).IsEqual(1);
        var shipButton = shipsList.GetChild(0) as Button;
        AssertThat(shipButton).IsNotNull();
        AssertThat(shipButton.Text.Contains("#")).IsFalse();
        AssertThat(shipButton.ClipText).IsTrue();

        // Condensed view defaults to High priority; toggling to Detailed shows Normal "Fleet Dispatched"
        var filterBtn = box.GetNodeOrNull<Button>("%ChronicleFilterButton");
        AssertThat(filterBtn).IsNotNull();
        AssertThat(filterBtn.Text).IsEqual("VIEW: HIGH");
        AssertThat(chronicleList.GetChildCount()).IsEqual(1);
        AssertThat((chronicleList.GetChild(0) as Label)?.Text).Contains("No recorded fleet events");

        filterBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(filterBtn.Text).IsEqual("DETAILED: ALL");
        AssertThat(chronicleList.GetChildCount()).IsEqual(1);
        AssertThat((chronicleList.GetChild(0) as Label)?.Text).Contains("Fleet Dispatched");
    }

    [TestCase]
    public void TestFleetInfoBoxLongTextDoesNotOverflowPanelWidth()
    {
        var scene = GD.Load<PackedScene>("res://UI/fleet_info_box.tscn");
        var box = scene.Instantiate<FleetInfoBox>();
        _createdNodes.Add(box);

        var viewport = new SubViewport { Size = new Vector2I(600, 600) };
        _createdNodes.Add(viewport);
        viewport.AddChild(box);
        box._Ready();

        var empire = new Empire { EmpireName = "The Interstellar Sovereignty of Extremely Verbose Names" };
        var origin = new Star { StarName = "Vega", Position = new Vector2(0, 0) };
        var target = new Star { StarName = "Alpha Centauri Megastructure Nexus Prime Sector IV", Position = new Vector2(200, 0) };
        _createdNodes.Add(empire);
        _createdNodes.Add(origin);
        _createdNodes.Add(target);

        var ship = new Ship(CreateColonyDesign()) { CommissionedTick = 5, BuiltAtStarName = "Vega" };
        var fleet = new Fleet();
        _createdNodes.Add(fleet);
        fleet.Initialize(empire, origin, ship);
        fleet.FleetName = "First Expeditionary Colonization Armada of the Outer Rim";
        fleet.OrderColonize(target);

        box.DisplayFleet(fleet);

        var mainVBox = box.GetNodeOrNull<VBoxContainer>("Background/ContentMargin/MainVBox");
        AssertThat(mainVBox).IsNotNull();
        AssertThat(mainVBox.GetCombinedMinimumSize().X).IsLessEqual(324.0f);
    }

    [TestCase]
    public void TestDebugUiSpeedControlsAndPause()
    {
        TickManager tm;
        if (TickManager.HasInstance)
        {
            tm = TickManager.Instance;
            tm.SetSpeedMultiplier(1.0f);
        }
        else
        {
            tm = new TickManager();
            _createdNodes.Add(tm);
            tm._EnterTree();
        }

        AssertThat(tm.TickUpdateInterval).IsEqual(1.0f);
        AssertThat(tm.IsPaused).IsFalse();
        AssertThat(tm.SpeedMultiplier).IsEqual(1.0f);

        var scene = GD.Load<PackedScene>("res://Data/Improvements/debug_ui.tscn");
        AssertThat(scene).IsNotNull();
        var debugUi = scene.Instantiate<DebugUi>();
        _createdNodes.Add(debugUi);
        debugUi._Ready();

        var statusLabel = debugUi.GetNodeOrNull<Label>("%SpeedStatusLabel");
        var slowDownBtn = debugUi.GetNodeOrNull<Button>("%SlowDownButton");
        var pauseBtn = debugUi.GetNodeOrNull<Button>("%PauseButton");
        var speedUpBtn = debugUi.GetNodeOrNull<Button>("%SpeedUpButton");

        AssertThat(statusLabel).IsNotNull();
        AssertThat(slowDownBtn).IsNotNull();
        AssertThat(pauseBtn).IsNotNull();
        AssertThat(speedUpBtn).IsNotNull();
        AssertThat(statusLabel.Text).IsEqual("SPEED: 1x");
        AssertThat(pauseBtn.Text).IsEqual("||");

        // Double speed: 1x -> 2x -> 4x (capped at 4x)
        speedUpBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(2.0f);
        AssertThat(statusLabel.Text).IsEqual("SPEED: 2x");

        speedUpBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(4.0f);
        AssertThat(statusLabel.Text).IsEqual("SPEED: 4x");
        AssertThat(speedUpBtn.Disabled).IsTrue();

        speedUpBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(4.0f);

        // Halve speed: 4x -> 2x -> 1x -> 0.5x -> 0.25x (capped at 0.25x)
        slowDownBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(2.0f);
        slowDownBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(1.0f);
        slowDownBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(0.5f);
        AssertThat(statusLabel.Text).IsEqual("SPEED: 0.5x");
        slowDownBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.SpeedMultiplier).IsEqual(0.25f);
        AssertThat(statusLabel.Text).IsEqual("SPEED: 0.25x");
        AssertThat(slowDownBtn.Disabled).IsTrue();

        // Pause toggle
        pauseBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.IsPaused).IsTrue();
        AssertThat(pauseBtn.Text).IsEqual("▶");
        AssertThat(statusLabel.Text).Contains("PAUSED");

        int ticksBefore = tm.tickCount;
        tm._Process(5.0);
        AssertThat(tm.tickCount).IsEqual(ticksBefore);

        pauseBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(tm.IsPaused).IsFalse();
        AssertThat(pauseBtn.Text).IsEqual("||");

        // Reset to 1x
        tm.SetSpeedMultiplier(1.0f);
        debugUi.RefreshSpeedDisplay();
        AssertThat(statusLabel.Text).IsEqual("SPEED: 1x");
    }

    [TestCase]
    public void TestShipInfoBoxLoadsDisplaysDetailsAndChronicle()
    {
        var scene = GD.Load<PackedScene>("res://UI/ship_info_box.tscn");
        AssertThat(scene).IsNotNull();

        var box = scene.Instantiate<ShipInfoBox>();
        _createdNodes.Add(box);
        box._Ready();

        var entityTypeLabel = box.GetNodeOrNull<Label>("%EntityTypeLabel");
        AssertThat(entityTypeLabel).IsNotNull();
        AssertThat(entityTypeLabel.Text).IsEqual("SHIP");

        var closeBtn = box.GetNodeOrNull<Button>("%CloseButton");
        AssertThat(closeBtn).IsNotNull();
        AssertThat(closeBtn.OffsetLeft).IsEqual(-36.0f);
        AssertThat(closeBtn.OffsetTop).IsEqual(10.0f);
        AssertThat(closeBtn.OffsetRight).IsEqual(-12.0f);
        AssertThat(closeBtn.OffsetBottom).IsEqual(34.0f);

        var chronicleScroll = box.GetNodeOrNull<ScrollContainer>("Background/ContentMargin/MainVBox/ChronicleScroll");
        AssertThat(chronicleScroll).IsNotNull();
        AssertThat(chronicleScroll.HorizontalScrollMode).IsEqual(ScrollContainer.ScrollMode.Disabled);

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
        AssertThat(nameLabel.Text.Contains("#")).IsFalse();
        AssertThat(ship.ChronicleName.Contains("#")).IsFalse();
        AssertThat(originLabel.Text).Contains("Year 2, Month 1");
        AssertThat(originLabel.Text).Contains("Deneb");
        AssertThat(statsLabel.Text).Contains("100/100");
        AssertThat(statsLabel.Text).Contains("40");
        AssertThat(abilitiesLabel.Text).Contains("Colonize");

        var filterBtn = box.GetNodeOrNull<Button>("%ChronicleFilterButton");
        AssertThat(filterBtn).IsNotNull();
        AssertThat(filterBtn.Text).IsEqual("VIEW: HIGH");

        filterBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(filterBtn.Text).IsEqual("DETAILED: ALL");
        AssertThat(chronicleList.GetChildCount()).IsEqual(1);
        AssertThat((chronicleList.GetChild(0) as Label)?.Text).Contains("Fleet Dispatched");
    }

    [TestCase]
    public void TestTickManagerYearMonthFormatting()
    {
        AssertThat(TickManager.GetYear(0)).IsEqual(1);
        AssertThat(TickManager.GetMonth(0)).IsEqual(1);
        AssertThat(TickManager.FormatDate(0)).IsEqual("Year 1, Month 1");
        AssertThat(TickManager.FormatShortDate(0)).IsEqual("Y1 M1");

        AssertThat(TickManager.GetYear(11)).IsEqual(1);
        AssertThat(TickManager.GetMonth(11)).IsEqual(12);
        AssertThat(TickManager.FormatDate(11)).IsEqual("Year 1, Month 12");
        AssertThat(TickManager.FormatShortDate(11)).IsEqual("Y1 M12");

        AssertThat(TickManager.GetYear(12)).IsEqual(2);
        AssertThat(TickManager.GetMonth(12)).IsEqual(1);
        AssertThat(TickManager.FormatDate(12)).IsEqual("Year 2, Month 1");
        AssertThat(TickManager.FormatShortDate(12)).IsEqual("Y2 M1");

        AssertThat(TickManager.FormatDate(25)).IsEqual("Year 3, Month 2");
        AssertThat(TickManager.FormatShortDate(25)).IsEqual("Y3 M2");
    }
}

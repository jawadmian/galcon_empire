using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class EmpireInfoBoxTest
{
    private PackedScene _scene;
    private EmpireInfoBox _infoBox;
    private Empire _empire;
    private Star _capitalStar;
    private Star _colonyStar;

    [BeforeTest]
    public void Setup()
    {
        _scene = GD.Load<PackedScene>("res://UI/empire_info_box.tscn");
        _infoBox = _scene.Instantiate<EmpireInfoBox>();

        _capitalStar = new Star
        {
            StarName = "Sol Prime",
            StarPopulation = 75000,
            Position = new Vector2(100, 200)
        };

        _colonyStar = new Star
        {
            StarName = "Proxima Centauri",
            StarPopulation = 25000,
            Position = new Vector2(400, 600)
        };

        _empire = new Empire
        {
            EmpireName = "Terran Ascendancy",
            EmpireColor = new Color(0, 0.9f, 1.0f, 1.0f),
            HomeStar = _capitalStar
        };

        _empire.AddStar(_capitalStar);
        _empire.AddStar(_colonyStar);

        _empire.ResourceStockpiles[ResourceType.Food] = 1200;
        _empire.ProductionPerTick[ResourceType.Food] = 45;
        _empire.ResourceStockpiles[ResourceType.Ore] = 850;
        _empire.ProductionPerTick[ResourceType.Ore] = 30;
        _empire.ResourceStockpiles[ResourceType.Money] = 5000;
        _empire.ProductionPerTick[ResourceType.Money] = 150;
    }

    [AfterTest]
    public void TearDown()
    {
        if (GodotObject.IsInstanceValid(_infoBox) && !_infoBox.IsQueuedForDeletion())
        {
            _infoBox.Free();
        }
        if (GodotObject.IsInstanceValid(_capitalStar) && !_capitalStar.IsQueuedForDeletion())
        {
            _capitalStar.Free();
        }
        if (GodotObject.IsInstanceValid(_colonyStar) && !_colonyStar.IsQueuedForDeletion())
        {
            _colonyStar.Free();
        }
        if (GodotObject.IsInstanceValid(_empire) && !_empire.IsQueuedForDeletion())
        {
            _empire.Free();
        }
    }

    [TestCase]
    public void TestEmpireInfoBoxLoadsAndInstantiates()
    {
        AssertThat(_scene).IsNotNull();
        AssertThat(_infoBox).IsNotNull();
        AssertThat(_infoBox.CustomMinimumSize.X).IsEqual(380.0f);
        AssertThat(_infoBox.CustomMinimumSize.Y).IsEqual(540.0f);
    }

    [TestCase]
    public void TestBackgroundNinePatchConfiguration()
    {
        var bg = _infoBox.GetNodeOrNull<NinePatchRect>("Background");
        AssertThat(bg).IsNotNull();
        AssertThat(bg.Texture).IsNotNull();

        // 24px patch margins matching 9-slice standard
        AssertThat(bg.PatchMarginLeft).IsEqual(24);
        AssertThat(bg.PatchMarginTop).IsEqual(24);
        AssertThat(bg.PatchMarginRight).IsEqual(24);
        AssertThat(bg.PatchMarginBottom).IsEqual(24);

        // Tile Fit axis stretch mode
        AssertThat(bg.AxisStretchHorizontal).IsEqual(NinePatchRect.AxisStretchMode.TileFit);
        AssertThat(bg.AxisStretchVertical).IsEqual(NinePatchRect.AxisStretchMode.TileFit);

        // Mouse filter Pass
        AssertThat(bg.MouseFilter).IsEqual(Control.MouseFilterEnum.Pass);
    }

    [TestCase]
    public void TestContentMarginPaddingAndClearance()
    {
        var contentMargin = _infoBox.GetNodeOrNull<MarginContainer>("Background/ContentMargin");
        AssertThat(contentMargin).IsNotNull();

        // Horizontal and bottom margins >= patch margin (24px)
        AssertThat(contentMargin.GetThemeConstant("margin_left")).IsGreaterEqual(24);
        AssertThat(contentMargin.GetThemeConstant("margin_right")).IsGreaterEqual(24);
        AssertThat(contentMargin.GetThemeConstant("margin_bottom")).IsGreaterEqual(24);

        // Top margin >= 44px to clear close button (Y: 10..34)
        AssertThat(contentMargin.GetThemeConstant("margin_top")).IsGreaterEqual(44);
    }

    [TestCase]
    public void TestDisplayEmpireBindsData()
    {
        _infoBox._Ready();
        _infoBox.DisplayEmpire(_empire);

        AssertThat(_infoBox.CurrentEmpire).IsEqual(_empire);

        var nameLabel = _infoBox.GetNodeOrNull<Label>("%NameLabel");
        AssertThat(nameLabel).IsNotNull();
        AssertThat(nameLabel.Text).IsEqual("TERRAN ASCENDANCY");

        var capitalLabel = _infoBox.GetNodeOrNull<Label>("%CapitalLabel");
        AssertThat(capitalLabel).IsNotNull();
        AssertThat(capitalLabel.Text).Contains("SOL PRIME");

        var territoryLabel = _infoBox.GetNodeOrNull<Label>("%TerritoryLabel");
        AssertThat(territoryLabel).IsNotNull();
        AssertThat(territoryLabel.Text).Contains("2 SYSTEMS");

        var popValueLabel = _infoBox.GetNodeOrNull<Label>("%PopValueLabel");
        AssertThat(popValueLabel).IsNotNull();
        AssertThat(popValueLabel.Text).Contains("100,000");

        var foodLabel = _infoBox.GetNodeOrNull<Label>("%FoodValueLabel");
        AssertThat(foodLabel).IsNotNull();
        AssertThat(foodLabel.Text).Contains("1,200");
        AssertThat(foodLabel.Text).Contains("+45/t");

        var oreLabel = _infoBox.GetNodeOrNull<Label>("%OreValueLabel");
        AssertThat(oreLabel).IsNotNull();
        AssertThat(oreLabel.Text).Contains("850");
        AssertThat(oreLabel.Text).Contains("+30/t");

        var creditsLabel = _infoBox.GetNodeOrNull<Label>("%CreditsValueLabel");
        AssertThat(creditsLabel).IsNotNull();
        AssertThat(creditsLabel.Text).Contains("5,000");
        AssertThat(creditsLabel.Text).Contains("+150/t");

        var starsList = _infoBox.GetNodeOrNull<VBoxContainer>("%StarsList");
        AssertThat(starsList).IsNotNull();
        AssertThat(starsList.GetChildCount()).IsEqual(2);
    }

    [TestCase]
    public void TestFocusCapitalButton()
    {
        var camera = new CameraController();
        camera._Ready();

        _infoBox._Ready();
        _infoBox.DisplayEmpire(_empire);

        var focusCapBtn = _infoBox.GetNodeOrNull<Button>("%FocusCapitalButton");
        AssertThat(focusCapBtn).IsNotNull();

        focusCapBtn.EmitSignal(Button.SignalName.Pressed);

        AssertThat(camera.Position).IsEqual(_capitalStar.GlobalPosition);

        camera.Free();
    }

    [TestCase]
    public void TestEmpiresListCreatesInteractiveButtons()
    {
        var empiresListScene = GD.Load<PackedScene>("res://UI/empires_list.tscn");
        AssertThat(empiresListScene).IsNotNull();

        var empiresList = empiresListScene.Instantiate<EmpiresList>();
        AssertThat(empiresList).IsNotNull();

        empiresList._Ready();
        empiresList.AddEmpire(_empire);

        var container = empiresList.GetNodeOrNull<VBoxContainer>("%EmpiresContainer");
        AssertThat(container).IsNotNull();
        AssertThat(container.GetChildCount()).IsEqual(1);

        var button = container.GetChild<Button>(0);
        AssertThat(button).IsNotNull();
        AssertThat(button.Text).Contains("Terran Ascendancy");
        AssertThat(button.Text).Contains("[2]");

        empiresList.Free();
    }

    [TestCase]
    public void TestActiveConstructionQueueUpdatesLive()
    {
        _infoBox._Ready();
        _infoBox.DisplayEmpire(_empire);

        var queueList = _infoBox.GetNodeOrNull<VBoxContainer>("%BuildQueueList");
        AssertThat(queueList).IsNotNull();

        // Initially empty
        AssertThat(queueList.GetChildCount()).IsEqual(1); // "• Industrial queue idle"

        // Queue an improvement
        var farm = new ImprovementResource
        {
            ImprovementName = "Orbital Hydroponics",
            BuildTime = 3
        };
        farm.BuildCost[ResourceType.Money] = 100;
        _empire.ResourceStockpiles[ResourceType.Money] = 500;

        _empire.QueueImprovement(farm, _capitalStar);

        // Advance tick to begin construction
        _empire.UpdateTick(1);

        // Trigger EmpireInfoBox process to detect the queue change
        _infoBox._Process(0.016);

        // The queue should now have the active project
        AssertThat(queueList.GetChildCount()).IsEqual(1);
        var projectHBox = queueList.GetChild<HBoxContainer>(0);
        AssertThat(projectHBox).IsNotNull();
        var projectBtn = projectHBox.GetChild<Button>(0);
        AssertThat(projectBtn).IsNotNull();
        AssertThat(projectBtn.Text).Contains("Orbital Hydroponics");
        AssertThat(projectBtn.Text).Contains("Sol Prime");
        AssertThat(projectBtn.Text).Contains("2t left");
    }
}

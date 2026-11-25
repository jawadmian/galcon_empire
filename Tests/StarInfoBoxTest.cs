using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class StarInfoBoxTest
{
    private PackedScene _scene;
    private StarInfoBox _infoBox;
    private Star _star;

    [BeforeTest]
    public void Setup()
    {
        _scene = GD.Load<PackedScene>("res://UI/star_info_box.tscn");
        _infoBox = _scene.Instantiate<StarInfoBox>();

        _star = new Star
        {
            StarName = "Alpha Centauri",
            StarNameColor = new Color(0, 1, 1, 1),
            StarPopulation = 42000,
            MaxPopulation = 100000,
            GrowthRate = 0.02f,
            Position = new Vector2(450, -250)
        };
    }

    [AfterTest]
    public void TearDown()
    {
        if (GodotObject.IsInstanceValid(_infoBox) && !_infoBox.IsQueuedForDeletion())
        {
            _infoBox.QueueFree();
        }
        if (GodotObject.IsInstanceValid(_star) && !_star.IsQueuedForDeletion())
        {
            _star.QueueFree();
        }
    }

    [TestCase]
    public void TestSceneLoadsAndInstantiates()
    {
        AssertThat(_scene).IsNotNull();
        AssertThat(_infoBox).IsNotNull();
        AssertThat(_infoBox.CustomMinimumSize.X).IsEqual(380.0f);
        AssertThat(_infoBox.CustomMinimumSize.Y).IsEqual(520.0f);
    }

    [TestCase]
    public void TestDisplayStarBindsData()
    {
        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        AssertThat(_infoBox.CurrentStar).IsEqual(_star);

        var entityTypeLabel = _infoBox.GetNodeOrNull<Label>("%EntityTypeLabel");
        AssertThat(entityTypeLabel).IsNotNull();
        AssertThat(entityTypeLabel.Text).IsEqual("STAR SYSTEM");

        var nameLabel = _infoBox.GetNodeOrNull<Label>("%NameLabel");
        AssertThat(nameLabel).IsNotNull();
        AssertThat(nameLabel.Text).IsEqual("ALPHA CENTAURI");

        var coordsLabel = _infoBox.GetNodeOrNull<Label>("%CoordsLabel");
        AssertThat(coordsLabel).IsNotNull();
        AssertThat(coordsLabel.Text).Contains("450");
        AssertThat(coordsLabel.Text).Contains("-250");

        var popValueLabel = _infoBox.GetNodeOrNull<Label>("%PopValueLabel");
        AssertThat(popValueLabel).IsNotNull();
        AssertThat(popValueLabel.Text).Contains("42,000");

        var progressBar = _infoBox.GetNodeOrNull<ProgressBar>("%PopProgressBar");
        AssertThat(progressBar).IsNotNull();
        AssertThat(progressBar.Value).IsEqual(42000.0);
    }

    [TestCase]
    public void TestPopulateImprovements()
    {
        var improvement = new ImprovementResource
        {
            ImprovementName = "Hydroponics Bay I"
        };
        improvement.ResourceOutput[ResourceType.Food] = 25;
        _star.AddImprovement(improvement);

        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        var foodLabel = _infoBox.GetNodeOrNull<Label>("%FoodValueLabel");
        AssertThat(foodLabel).IsNotNull();
        AssertThat(foodLabel.Text).IsEqual("+25");

        var improvementsTitle = _infoBox.GetNodeOrNull<Label>("%ImprovementsTitle");
        AssertThat(improvementsTitle).IsNotNull();
        AssertThat(improvementsTitle.Text).IsEqual($"FACILITIES (1 / {_star.MaxImprovements})");

        var improvementsList = _infoBox.GetNodeOrNull<VBoxContainer>("%ImprovementsList");
        AssertThat(improvementsList).IsNotNull();
        AssertThat(improvementsList.GetChildCount()).IsGreater(0);
    }

    [TestCase]
    public void TestFocusCameraOnStar()
    {
        var camera = new CameraController();
        camera._Ready();

        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        var focusBtn = _infoBox.GetNodeOrNull<Button>("%FocusButton");
        AssertThat(focusBtn).IsNotNull();

        focusBtn.EmitSignal(Button.SignalName.Pressed);

        AssertThat(camera.Position).IsEqual(_star.GlobalPosition);

        camera.Free();
    }

    [TestCase]
    public void TestBackgroundNinePatchConfiguration()
    {
        var bg = _infoBox.GetNodeOrNull<NinePatchRect>("Background");
        AssertThat(bg).IsNotNull();
        AssertThat(bg.Texture).IsNotNull();

        // Verify 24px patch margins matching the texture
        AssertThat(bg.PatchMarginLeft).IsEqual(24);
        AssertThat(bg.PatchMarginTop).IsEqual(24);
        AssertThat(bg.PatchMarginRight).IsEqual(24);
        AssertThat(bg.PatchMarginBottom).IsEqual(24);

        // Verify Tile Fit axis stretch mode (value 2)
        AssertThat(bg.AxisStretchHorizontal).IsEqual(NinePatchRect.AxisStretchMode.TileFit);
        AssertThat(bg.AxisStretchVertical).IsEqual(NinePatchRect.AxisStretchMode.TileFit);

        // Verify mouse filter is Pass
        AssertThat(bg.MouseFilter).IsEqual(Control.MouseFilterEnum.Pass);
    }

    [TestCase]
    public void TestBackgroundTextureDimensionsAndContrast()
    {
        var bg = _infoBox.GetNodeOrNull<NinePatchRect>("Background");
        AssertThat(bg).IsNotNull();
        AssertThat(bg.Texture).IsNotNull();

        Texture2D tex = bg.Texture;
        // Verify texture is the new 128x128 9-slice asset, not the old 1MB oversized image
        AssertThat(tex.GetWidth()).IsEqual(128);
        AssertThat(tex.GetHeight()).IsEqual(128);

        Image img = tex.GetImage();
        AssertThat(img).IsNotNull();

        // Verify center area is high-contrast deep obsidian (low R, G, B; high alpha)
        Color centerPixel = img.GetPixel(64, 64);
        AssertThat(centerPixel.R).IsLess(0.15f);
        AssertThat(centerPixel.G).IsLess(0.15f);
        AssertThat(centerPixel.B).IsLess(0.20f);
        AssertThat(centerPixel.A).IsGreater(0.85f);

        // Verify corner chamfer/bracket has glowing cyan border (high blue/green)
        Color bracketPixel = img.GetPixel(10, 0);
        AssertThat(bracketPixel.B).IsGreater(0.70f);
        AssertThat(bracketPixel.G).IsGreater(0.60f);
    }

    [TestCase]
    public void TestContentMarginHierarchyAndPadding()
    {
        // ContentMargin must be a direct child of Background per scalable UI rules
        var contentMargin = _infoBox.GetNodeOrNull<MarginContainer>("Background/ContentMargin");
        AssertThat(contentMargin).IsNotNull();

        // Margin constants must be >= patch margin (24px) to ensure no overlap with borders
        AssertThat(contentMargin.GetThemeConstant("margin_left")).IsGreaterEqual(24);
        AssertThat(contentMargin.GetThemeConstant("margin_top")).IsGreaterEqual(24);
        AssertThat(contentMargin.GetThemeConstant("margin_right")).IsGreaterEqual(24);
        AssertThat(contentMargin.GetThemeConstant("margin_bottom")).IsGreaterEqual(24);
    }

    [TestCase]
    public void TestRenderStarInfoBoxLayout()
    {
        var viewport = new SubViewport
        {
            Size = new Vector2I(400, 600)
        };

        viewport.AddChild(_infoBox);
        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        AssertThat(_infoBox.Visible).IsTrue();

        var bg = _infoBox.GetNodeOrNull<NinePatchRect>("Background");
        AssertThat(bg).IsNotNull();
        AssertThat(bg.Visible).IsTrue();
        AssertThat(bg.Size.X).IsEqual(380.0f);
        AssertThat(bg.Size.Y).IsEqual(520.0f);

        var contentMargin = _infoBox.GetNodeOrNull<MarginContainer>("Background/ContentMargin");
        AssertThat(contentMargin).IsNotNull();
        AssertThat(contentMargin.Visible).IsTrue();
        AssertThat(contentMargin.Size.X).IsEqual(380.0f);
        AssertThat(contentMargin.Size.Y).IsEqual(520.0f);

        viewport.RemoveChild(_infoBox);
        viewport.Free();
    }

    [TestCase]
    public void TestCloseAndFocusButtonsDoNotOverlap()
    {
        var viewport = new SubViewport
        {
            Size = new Vector2I(400, 600)
        };

        viewport.AddChild(_infoBox);
        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        var closeBtn = _infoBox.GetNodeOrNull<Button>("%CloseButton");
        var focusBtn = _infoBox.GetNodeOrNull<Button>("%FocusButton");

        AssertThat(closeBtn).IsNotNull();
        AssertThat(focusBtn).IsNotNull();

        // CloseButton bottom edge must be above the ContentMargin / FocusButton top edge
        // to guarantee zero collision and clean visual separation
        float closeBottom = closeBtn.Position.Y + closeBtn.Size.Y;
        var contentMargin = _infoBox.GetNodeOrNull<MarginContainer>("Background/ContentMargin");
        float marginTop = contentMargin.GetThemeConstant("margin_top");

        AssertThat(marginTop).IsGreater(closeBottom);

        viewport.RemoveChild(_infoBox);
        viewport.Free();
    }

    [TestCase]
    public void TestImprovementsListUpdatesLiveWithoutReopening()
    {
        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        var list = _infoBox.GetNodeOrNull<VBoxContainer>("%ImprovementsList");
        AssertThat(list).IsNotNull();

        // Initially empty
        AssertThat(list.GetChildCount()).IsEqual(1); // "• No facilities installed"

        // Add an improvement while panel is already open
        var facility = new ImprovementResource
        {
            ImprovementName = "Deep Core Mine I"
        };
        _star.AddImprovement(facility);

        // Run _Process to simulate next frame update
        _infoBox._Process(0.016);

        // Should automatically update without needing to close and reopen!
        AssertThat(list.GetChildCount()).IsEqual(1);
        var hbox = list.GetChild<HBoxContainer>(0);
        AssertThat(hbox).IsNotNull();
        var label = hbox.GetChild<Label>(0);
        AssertThat(label).IsNotNull();
        AssertThat(label.Text).Contains("Deep Core Mine I");
    }

    [TestCase]
    public void TestStarTypeAndColorModulation()
    {
        var starScene = GD.Load<PackedScene>("res://Gameplay/star.tscn");
        AssertThat(starScene).IsNotNull();

        var starInstance = starScene.Instantiate<Star>();
        AssertThat(starInstance).IsNotNull();

        // Default type should be YellowDwarf
        AssertThat(starInstance.StarType).IsEqual(StarType.YellowDwarf);

        starInstance.SetStarType(StarType.BlueGiant);
        AssertThat(starInstance.StarType).IsEqual(StarType.BlueGiant);
        AssertThat(starInstance.StarColor).IsEqual(Star.GetDefaultColor(StarType.BlueGiant));

        // When entering tree / calling _Ready, Sprite2D should modulate to StarColor
        starInstance._Ready();
        var sprite = starInstance.GetNodeOrNull<Sprite2D>("%Sprite2D") ?? starInstance.GetNodeOrNull<Sprite2D>("Sprite2D");
        AssertThat(sprite).IsNotNull();
        AssertThat(sprite.Texture.ResourcePath).IsEqual("res://Images/star_001.png");
        AssertThat(sprite.Modulate).IsEqual(starInstance.StarColor);

        // Dynamically changing star color updates sprite modulate immediately
        Color customRed = new Color("ff4444");
        starInstance.StarColor = customRed;
        AssertThat(sprite.Modulate).IsEqual(customRed);

        // Display in StarInfoBox verifies HUD type name and color
        _infoBox._Ready();
        _infoBox.DisplayStar(starInstance);
        var typeLabel = _infoBox.GetNodeOrNull<Label>("%TypeLabel");
        AssertThat(typeLabel).IsNotNull();
        AssertThat(typeLabel.Text).IsEqual(Star.GetTypeName(StarType.BlueGiant).ToUpperInvariant());
        AssertThat(typeLabel.SelfModulate).IsEqual(customRed);

        starInstance.QueueFree();
    }

    [TestCase]
    public void TestOrbitingShipsDisplayedInStarInfoBox()
    {
        var empire = new Empire { EmpireName = "Starfleet", EmpireColor = Colors.Cyan };
        empire.AddStar(_star);

        var design = new ShipDesignResource
        {
            DesignId = "colony_ship_v1",
            DesignName = "Ark-Class Colony Ship",
            Speed = 40f,
            MaxHitPoints = 100,
        };
        var ship = new Ship(design) { ShipName = "Beluga", ClassName = "Beluga-Class" };
        var fleet = new Fleet();
        fleet.Initialize(empire, _star, ship);
        empire.RegisterFleet(fleet);

        _infoBox._Ready();
        _infoBox.DisplayStar(_star);

        var orbitingTitle = _infoBox.GetNodeOrNull<Label>("%OrbitingTitle");
        var orbitingList = _infoBox.GetNodeOrNull<VBoxContainer>("%OrbitingList");
        AssertThat(orbitingTitle).IsNotNull();
        AssertThat(orbitingList).IsNotNull();
        AssertThat(orbitingTitle.Text).IsEqual("ORBITING SHIPS (1)");
        AssertThat(orbitingList.GetChildCount()).IsEqual(1);
        var btn = orbitingList.GetChild(0) as Button;
        AssertThat(btn).IsNotNull();
        AssertThat(btn.Text).Contains("Beluga");

        fleet.Free();
        empire.Free();
    }

    [TestCase]
    public void TestStarChronicleRendersAndFiltersByPriority()
    {
        var previousManager = ChronicleManager.Instance;
        var testManager = new ChronicleManager();
        testManager.InitializeDatabase("Data Source=StarInfoBoxChronicleTest;Mode=Memory;Cache=Shared");
        ChronicleManager.SetInstanceForTesting(testManager);

        try
        {
            var empire = new Empire { EmpireName = "Solar Republic", HomeStar = _star };

            ChronicleEvent.Create(EventCategory.Political, "Colony Founded", EventPriority.High)
                .Involving(empire, isPrimary: true)
                .Involving(_star)
                .WithTemplate("{empire} founded a colony at {star}.")
                .Record();

            ChronicleEvent.Create(EventCategory.Economic, "Ore Survey", EventPriority.Low)
                .Involving(_star, isPrimary: true)
                .WithTemplate("Survey drones scanned {star}.")
                .Record();

            _infoBox._Ready();
            _infoBox.DisplayStar(_star);

            var chronicleList = _infoBox.GetNodeOrNull<VBoxContainer>("%ChronicleList");
            var filterBtn = _infoBox.GetNodeOrNull<Button>("%ChronicleFilterButton");
            AssertThat(chronicleList).IsNotNull();
            AssertThat(filterBtn).IsNotNull();

            // Default CondensedHigh shows only High priority as RichTextLabel with BBCode link
            AssertThat(chronicleList.GetChildCount()).IsEqual(1);
            var highEntry = chronicleList.GetChild(0) as RichTextLabel;
            AssertThat(highEntry).IsNotNull();
            AssertThat(highEntry.BbcodeEnabled).IsTrue();
            AssertThat(highEntry.Text).Contains("Colony Founded");
            AssertThat(highEntry.Text).Contains($"[url=entity:{empire.ChronicleId}]");

            // Clicking the empire hyperlink opens EmpireInfoBox for empire
            highEntry.EmitSignal(RichTextLabel.SignalName.MetaClicked, $"entity:{empire.ChronicleId}");
            var openedEmpireBox = EmpireInfoBox.Instance;
            AssertThat(openedEmpireBox).IsNotNull();
            AssertThat(openedEmpireBox.CurrentEmpire).IsEqual(empire);
            openedEmpireBox.Free();

            // Pressing filter cycles to DetailedAll (both High and Low)
            filterBtn.EmitSignal(Button.SignalName.Pressed);
            AssertThat(chronicleList.GetChildCount()).IsEqual(2);

            empire.Free();
        }
        finally
        {
            testManager.CloseDatabase();
            testManager.Free();
            ChronicleManager.SetInstanceForTesting(previousManager);
        }
    }
}

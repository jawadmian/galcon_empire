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
            _infoBox.Free();
        }
        if (GodotObject.IsInstanceValid(_star) && !_star.IsQueuedForDeletion())
        {
            _star.Free();
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
}

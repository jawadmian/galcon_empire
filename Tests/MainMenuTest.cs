using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class MainMenuTest
{
    private readonly List<Node> _createdNodes = new List<Node>();
    private bool _previousPauseState;

    [BeforeTest]
    public void Setup()
    {
        _previousPauseState = TickManager.HasInstance && TickManager.Instance.IsPaused;
        GameConfig.HasPendingConfig = false;
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
        GameConfig.HasPendingConfig = false;
        if (TickManager.HasInstance)
        {
            TickManager.Instance.SetPaused(_previousPauseState);
        }
    }

    [TestCase]
    public void TestMainMenuInitialStateAndDisabledLoadButton()
    {
        var scene = GD.Load<PackedScene>("res://UI/main_menu.tscn");
        AssertThat(scene).IsNotNull();

        var menu = scene.Instantiate<MainMenu>();
        AssertThat(menu).IsNotNull();
        _createdNodes.Add(menu);
        menu._Ready();

        var mainButtons = menu.GetNodeOrNull<VBoxContainer>("%MainButtonsVBox");
        var setupVBox = menu.GetNodeOrNull<VBoxContainer>("%SetupVBox");
        var loadBtn = menu.GetNodeOrNull<Button>("%LoadGameButton");
        var quickStartBtn = menu.GetNodeOrNull<Button>("%QuickStartButton");
        var newGameBtn = menu.GetNodeOrNull<Button>("%NewGameButton");

        AssertThat(mainButtons).IsNotNull();
        AssertThat(setupVBox).IsNotNull();
        AssertThat(mainButtons.Visible).IsTrue();
        AssertThat(setupVBox.Visible).IsFalse();
        AssertThat(loadBtn).IsNotNull();
        AssertThat(loadBtn.Disabled).IsTrue();
        AssertThat(quickStartBtn).IsNotNull();
        AssertThat(newGameBtn).IsNotNull();
    }

    [TestCase]
    public void TestNewGameSetupConstraintsStarsAndEmpires()
    {
        var scene = GD.Load<PackedScene>("res://UI/main_menu.tscn");
        var menu = scene.Instantiate<MainMenu>();
        _createdNodes.Add(menu);
        menu._Ready();

        var newGameBtn = menu.GetNodeOrNull<Button>("%NewGameButton");
        var setupVBox = menu.GetNodeOrNull<VBoxContainer>("%SetupVBox");
        var starsSlider = menu.GetNodeOrNull<HSlider>("%StarsSlider");
        var empiresSlider = menu.GetNodeOrNull<HSlider>("%EmpiresSlider");
        var starsLabel = menu.GetNodeOrNull<Label>("%StarsValueLabel");
        var empiresLabel = menu.GetNodeOrNull<Label>("%EmpiresValueLabel");

        newGameBtn.EmitSignal(Button.SignalName.Pressed);
        AssertThat(setupVBox.Visible).IsTrue();
        AssertThat(starsSlider.MinValue).IsEqual(10.0);
        AssertThat(starsSlider.MaxValue).IsEqual(100.0);
        AssertThat(starsSlider.Step).IsEqual(10.0);
        AssertThat(empiresSlider.MinValue).IsEqual(1.0);
        AssertThat(empiresSlider.MaxValue).IsEqual(12.0);

        // Select 30 stars and 12 empires
        menu.SetSelectedStars(30);
        menu.SetSelectedEmpires(12);
        AssertThat(menu.SelectedStars).IsEqual(30);
        AssertThat(menu.SelectedEmpires).IsEqual(12);
        AssertThat(starsLabel.Text).Contains("30");
        AssertThat(empiresLabel.Text).Contains("12");

        // Dropping stars to 10 must clamp empires to <= 10 so there are enough stars per empire
        menu.SetSelectedStars(10);
        AssertThat(menu.SelectedStars).IsEqual(10);
        AssertThat(menu.SelectedEmpires).IsEqual(10);
        AssertThat(empiresSlider.MaxValue).IsEqual(10.0);

        // Increasing to 100 stars (snapped to increment of 10)
        menu.SetSelectedStars(97);
        AssertThat(menu.SelectedStars).IsEqual(100);
        AssertThat(empiresSlider.MaxValue).IsEqual(12.0);

        // QuickStart config uses defaults
        GameConfig.ConfigureQuickStart();
        AssertThat(GameConfig.HasPendingConfig).IsTrue();
        AssertThat(GameConfig.NumberOfStars).IsEqual(GameConfig.DefaultQuickStartStars);
        AssertThat(GameConfig.NumberOfEmpires).IsEqual(GameConfig.DefaultQuickStartEmpires);
    }

    [TestCase]
    public void TestStarAndEmpireNamePoolsCoverMaximumSettings()
    {
        var starNames = GD.Load<StringArrayResource>("res://Data/StarNames.tres");
        var empireNames = GD.Load<StringArrayResource>("res://Data/EmpireNames.tres");

        AssertThat(starNames).IsNotNull();
        AssertThat(empireNames).IsNotNull();

        var uniqueStars = new HashSet<string>(starNames.Items);
        var uniqueEmpires = new HashSet<string>(empireNames.Items);

        AssertThat(uniqueStars.Count).IsGreaterEqual(GameConfig.MaxStars);
        AssertThat(uniqueEmpires.Count).IsGreaterEqual(GameConfig.MaxEmpires);

        // Scaled spawn bounds expand for 100 stars
        var starManager = new StarManager();
        var (minSmall, maxSmall) = starManager.GetScaledSpawnBounds(10);
        var (minLarge, maxLarge) = starManager.GetScaledSpawnBounds(100);
        AssertThat((maxLarge - minLarge).Length()).IsGreater((maxSmall - minSmall).Length());
        starManager.Free();
    }
}

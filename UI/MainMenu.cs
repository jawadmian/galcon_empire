using System;
using Godot;

public partial class MainMenu : Control
{
    private const string GalaxyScenePath = "res://Gameplay/galaxy_spawner.tscn";

    private VBoxContainer _mainButtonsVBox;
    private VBoxContainer _setupVBox;

    private Button _quickStartButton;
    private Button _newGameButton;
    private Button _loadGameButton;
    private Button _quitButton;

    private Label _starsValueLabel;
    private HSlider _starsSlider;
    private Button _starsDecButton;
    private Button _starsIncButton;

    private Label _empiresValueLabel;
    private HSlider _empiresSlider;
    private Button _empiresDecButton;
    private Button _empiresIncButton;

    private Button _startCustomGameButton;
    private Button _backToMenuButton;

    public int SelectedStars { get; private set; } = GameConfig.DefaultQuickStartStars;
    public int SelectedEmpires { get; private set; } = GameConfig.DefaultQuickStartEmpires;

    public override void _Ready()
    {
        if (TickManager.HasInstance)
        {
            TickManager.Instance.SetPaused(true);
        }

        _mainButtonsVBox = GetNodeOrNull<VBoxContainer>("%MainButtonsVBox");
        _setupVBox = GetNodeOrNull<VBoxContainer>("%SetupVBox");

        _quickStartButton = GetNodeOrNull<Button>("%QuickStartButton");
        _newGameButton = GetNodeOrNull<Button>("%NewGameButton");
        _loadGameButton = GetNodeOrNull<Button>("%LoadGameButton");
        _quitButton = GetNodeOrNull<Button>("%QuitButton");

        _starsValueLabel = GetNodeOrNull<Label>("%StarsValueLabel");
        _starsSlider = GetNodeOrNull<HSlider>("%StarsSlider");
        _starsDecButton = GetNodeOrNull<Button>("%StarsDecButton");
        _starsIncButton = GetNodeOrNull<Button>("%StarsIncButton");

        _empiresValueLabel = GetNodeOrNull<Label>("%EmpiresValueLabel");
        _empiresSlider = GetNodeOrNull<HSlider>("%EmpiresSlider");
        _empiresDecButton = GetNodeOrNull<Button>("%EmpiresDecButton");
        _empiresIncButton = GetNodeOrNull<Button>("%EmpiresIncButton");

        _startCustomGameButton = GetNodeOrNull<Button>("%StartCustomGameButton");
        _backToMenuButton = GetNodeOrNull<Button>("%BackToMenuButton");

        if (_loadGameButton != null)
        {
            _loadGameButton.Disabled = true;
        }

        if (_quickStartButton != null)
            _quickStartButton.Pressed += OnQuickStartPressed;
        if (_newGameButton != null)
            _newGameButton.Pressed += OnNewGamePressed;
        if (_quitButton != null)
            _quitButton.Pressed += OnQuitPressed;

        if (_starsSlider != null)
        {
            _starsSlider.MinValue = GameConfig.MinStars;
            _starsSlider.MaxValue = GameConfig.MaxStars;
            _starsSlider.Step = GameConfig.StarIncrement;
            _starsSlider.Value = SelectedStars;
            _starsSlider.ValueChanged += OnStarsSliderChanged;
        }
        if (_starsDecButton != null)
            _starsDecButton.Pressed += () => SetSelectedStars(SelectedStars - GameConfig.StarIncrement);
        if (_starsIncButton != null)
            _starsIncButton.Pressed += () => SetSelectedStars(SelectedStars + GameConfig.StarIncrement);

        if (_empiresSlider != null)
        {
            _empiresSlider.MinValue = GameConfig.MinEmpires;
            _empiresSlider.MaxValue = GameConfig.MaxEmpires;
            _empiresSlider.Step = 1;
            _empiresSlider.Value = SelectedEmpires;
            _empiresSlider.ValueChanged += OnEmpiresSliderChanged;
        }
        if (_empiresDecButton != null)
            _empiresDecButton.Pressed += () => SetSelectedEmpires(SelectedEmpires - 1);
        if (_empiresIncButton != null)
            _empiresIncButton.Pressed += () => SetSelectedEmpires(SelectedEmpires + 1);

        if (_startCustomGameButton != null)
            _startCustomGameButton.Pressed += OnStartCustomGamePressed;
        if (_backToMenuButton != null)
            _backToMenuButton.Pressed += ShowMainButtons;

        ApplyButtonStyles(_quickStartButton);
        ApplyButtonStyles(_newGameButton);
        ApplyButtonStyles(_loadGameButton);
        ApplyButtonStyles(_quitButton);
        ApplyButtonStyles(_starsDecButton);
        ApplyButtonStyles(_starsIncButton);
        ApplyButtonStyles(_empiresDecButton);
        ApplyButtonStyles(_empiresIncButton);
        ApplyButtonStyles(_startCustomGameButton);
        ApplyButtonStyles(_backToMenuButton);

        ShowMainButtons();
        RefreshSetupDisplay();
    }

    private static void ApplyButtonStyles(Button btn)
    {
        if (btn == null) return;
        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        var pressedStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_pressed.tres");
        if (normalStyle != null)
        {
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("focus", normalStyle);
            btn.AddThemeStyleboxOverride("disabled", normalStyle);
        }
        if (hoverStyle != null)
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
        if (pressedStyle != null)
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);
    }

    public void ShowMainButtons()
    {
        if (_mainButtonsVBox != null) _mainButtonsVBox.Visible = true;
        if (_setupVBox != null) _setupVBox.Visible = false;
    }

    public void ShowSetupPanel()
    {
        if (_mainButtonsVBox != null) _mainButtonsVBox.Visible = false;
        if (_setupVBox != null) _setupVBox.Visible = true;
        RefreshSetupDisplay();
    }

    public void SetSelectedStars(int stars)
    {
        SelectedStars = GameConfig.NormalizeStarCount(stars);
        SelectedEmpires = GameConfig.ClampEmpiresForStars(SelectedEmpires, SelectedStars);
        RefreshSetupDisplay();
    }

    public void SetSelectedEmpires(int empires)
    {
        SelectedEmpires = GameConfig.ClampEmpiresForStars(empires, SelectedStars);
        RefreshSetupDisplay();
    }

    private void OnStarsSliderChanged(double value)
    {
        SetSelectedStars((int)Math.Round(value));
    }

    private void OnEmpiresSliderChanged(double value)
    {
        SetSelectedEmpires((int)Math.Round(value));
    }

    private void RefreshSetupDisplay()
    {
        int maxEmpires = Math.Min(GameConfig.MaxEmpires, SelectedStars);

        if (_starsSlider != null && !Mathf.IsEqualApprox((float)_starsSlider.Value, SelectedStars))
        {
            _starsSlider.SetValueNoSignal(SelectedStars);
        }
        if (_empiresSlider != null)
        {
            _empiresSlider.MaxValue = maxEmpires;
            if (!Mathf.IsEqualApprox((float)_empiresSlider.Value, SelectedEmpires))
            {
                _empiresSlider.SetValueNoSignal(SelectedEmpires);
            }
        }

        if (_starsValueLabel != null)
        {
            _starsValueLabel.Text = $"STAR SYSTEMS: {SelectedStars}";
        }
        if (_empiresValueLabel != null)
        {
            _empiresValueLabel.Text = $"EMPIRES: {SelectedEmpires} (MAX {maxEmpires})";
        }
    }

    private void OnQuickStartPressed()
    {
        GameConfig.ConfigureQuickStart();
        LaunchGalaxy();
    }

    private void OnNewGamePressed()
    {
        ShowSetupPanel();
    }

    private void OnStartCustomGamePressed()
    {
        GameConfig.ConfigureCustomGame(SelectedStars, SelectedEmpires);
        LaunchGalaxy();
    }

    private void LaunchGalaxy()
    {
        if (IsInsideTree() && GetTree() != null)
        {
            GetTree().ChangeSceneToFile(GalaxyScenePath);
        }
    }

    private void OnQuitPressed()
    {
        if (IsInsideTree() && GetTree() != null)
        {
            GetTree().Quit();
        }
    }
}

using System;
using Godot;

public partial class StarInfoBox : Control
{
    public static StarInfoBox Instance { get; private set; }

    private Star _currentStar;

    private Label _nameLabel;
    private Label _coordsLabel;
    private Label _typeLabel;
    private Button _focusButton;
    private Button _closeButton;

    private Label _popValueLabel;
    private ProgressBar _popProgressBar;
    private Label _popGrowthLabel;

    private Label _foodValueLabel;
    private Label _oreValueLabel;
    private Label _moneyValueLabel;

    private VBoxContainer _improvementsList;

    // Window dragging
    private bool _isDraggingWindow = false;
    private Vector2 _dragOffset = Vector2.Zero;

    public Star CurrentStar => _currentStar;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Ready()
    {
        // Cache node references using Scene Unique Names (%NodeName)
        _nameLabel = GetNodeOrNull<Label>("%NameLabel");
        _coordsLabel = GetNodeOrNull<Label>("%CoordsLabel");
        _typeLabel = GetNodeOrNull<Label>("%TypeLabel");
        _focusButton = GetNodeOrNull<Button>("%FocusButton");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");

        _popValueLabel = GetNodeOrNull<Label>("%PopValueLabel");
        _popProgressBar = GetNodeOrNull<ProgressBar>("%PopProgressBar");
        _popGrowthLabel = GetNodeOrNull<Label>("%PopGrowthLabel");

        _foodValueLabel = GetNodeOrNull<Label>("%FoodValueLabel");
        _oreValueLabel = GetNodeOrNull<Label>("%OreValueLabel");
        _moneyValueLabel = GetNodeOrNull<Label>("%MoneyValueLabel");

        _improvementsList = GetNodeOrNull<VBoxContainer>("%ImprovementsList");

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnCloseButtonPressed;
        }

        if (_focusButton != null)
        {
            _focusButton.Pressed += OnFocusButtonPressed;
        }

        if (_currentStar != null)
        {
            RefreshDisplay();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isDraggingWindow = true;
                    _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                }
                else
                {
                    _isDraggingWindow = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDraggingWindow)
        {
            GlobalPosition = GetGlobalMousePosition() - _dragOffset;
        }
    }

    public override void _Process(double delta)
    {
        if (_currentStar != null && GodotObject.IsInstanceValid(_currentStar))
        {
            UpdateRealtimeMetrics();
        }
    }

    /// <summary>
    /// Opens the StarInfoBox for the given star. Reuses an existing open instance if available.
    /// </summary>
    public static StarInfoBox OpenForStar(Star star, Node contextNode)
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            Instance.DisplayStar(star);
            Instance.Show();
            return Instance;
        }

        var scene = GD.Load<PackedScene>("res://UI/star_info_box.tscn");
        if (scene == null)
        {
            GD.PushError("StarInfoBox: Failed to load res://UI/star_info_box.tscn");
            return null;
        }

        var infoBox = scene.Instantiate<StarInfoBox>();
        if (infoBox == null)
        {
            GD.PushError("StarInfoBox: Failed to instantiate StarInfoBox scene.");
            return null;
        }

        // Prefer adding to an active CanvasLayer so the HUD doesn't translate with the camera
        CanvasLayer canvasLayer = contextNode?.GetTree()?.Root?.FindChild("CanvasLayer", true, false) as CanvasLayer;
        if (canvasLayer != null)
        {
            canvasLayer.AddChild(infoBox);
        }
        else
        {
            contextNode?.GetTree()?.Root?.AddChild(infoBox);
        }

        infoBox.DisplayStar(star);
        return infoBox;
    }

    /// <summary>
    /// Binds a star and updates the panel contents.
    /// </summary>
    public void DisplayStar(Star star)
    {
        _currentStar = star;
        if (IsNodeReady() || _nameLabel != null)
        {
            RefreshDisplay();
        }
    }

    private void RefreshDisplay()
    {
        if (_currentStar == null || !GodotObject.IsInstanceValid(_currentStar))
        {
            return;
        }

        // Header info
        if (_nameLabel != null)
        {
            _nameLabel.Text = _currentStar.StarName.ToUpperInvariant();
            _nameLabel.SelfModulate = _currentStar.StarNameColor;
        }

        if (_coordsLabel != null)
        {
            Vector2 pos = _currentStar.GlobalPosition;
            _coordsLabel.Text = $"SEC: [{(int)pos.X}, {(int)pos.Y}]";
        }

        if (_typeLabel != null)
        {
            _typeLabel.Text = "STELLAR COLONY";
        }

        // Demographics
        UpdateRealtimeMetrics();

        // Facilities / Improvements
        PopulateImprovements();
    }

    private void UpdateRealtimeMetrics()
    {
        if (_currentStar == null || !GodotObject.IsInstanceValid(_currentStar))
        {
            return;
        }

        // Population
        int pop = _currentStar.StarPopulation;
        int maxPop = _currentStar.MaxPopulation > 0 ? _currentStar.MaxPopulation : 100000;
        int growthPerTick = (int)(pop * _currentStar.GrowthRate);
        float growthPct = _currentStar.GrowthRate * 100.0f;

        if (_popValueLabel != null)
        {
            _popValueLabel.Text = $"{pop:N0} / {maxPop:N0}";
        }

        if (_popProgressBar != null)
        {
            _popProgressBar.MaxValue = maxPop;
            _popProgressBar.Value = pop;
        }

        if (_popGrowthLabel != null)
        {
            _popGrowthLabel.Text = $"Growth: +{growthPerTick:N0} / tick (+{growthPct:0.0}%)";
        }

        // Production
        if (_foodValueLabel != null)
        {
            _foodValueLabel.Text = $"+{_currentStar.StarFoodProduction:N0}";
        }

        if (_oreValueLabel != null)
        {
            _oreValueLabel.Text = $"+{_currentStar.StarOreProduction:N0}";
        }

        if (_moneyValueLabel != null)
        {
            _moneyValueLabel.Text = $"+{_currentStar.StarMoneyProduction:N0}";
        }
    }

    private void PopulateImprovements()
    {
        if (_improvementsList == null)
        {
            return;
        }

        // Remove old children
        foreach (Node child in _improvementsList.GetChildren())
        {
            child.QueueFree();
        }

        if (_currentStar.Improvements == null || _currentStar.Improvements.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "• No facilities installed"
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _improvementsList.AddChild(emptyLabel);
            return;
        }

        foreach (ImprovementResource imp in _currentStar.Improvements)
        {
            if (imp == null) continue;

            var itemHBox = new HBoxContainer();

            string desc = imp.ImprovementName;
            var label = new Label
            {
                Text = $"• {desc}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color("00e5ff"));
            itemHBox.AddChild(label);

            _improvementsList.AddChild(itemHBox);
        }
    }

    private void OnFocusButtonPressed()
    {
        if (_currentStar != null && GodotObject.IsInstanceValid(_currentStar))
        {
            CameraController.Instance?.FocusOn(_currentStar.GlobalPosition);
        }
    }

    private void OnCloseButtonPressed()
    {
        QueueFree();
    }
}

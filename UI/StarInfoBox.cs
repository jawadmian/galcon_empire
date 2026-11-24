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

    private Label _improvementsTitle;
    private VBoxContainer _improvementsList;
    private Label _orbitingTitle;
    private VBoxContainer _orbitingList;
    private int _lastImprovementsCount = -1;
    private int _lastActiveConstructionCount = -1;
    private int _lastOrbitingShipsCount = -1;

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

        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal -= OnTickUpdate;
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

        _improvementsTitle = GetNodeOrNull<Label>("%ImprovementsTitle")
            ?? GetNodeOrNull<Label>("Background/ContentMargin/MainVBox/ImprovementsVBox/ImprovementsTitle");
        _improvementsList = GetNodeOrNull<VBoxContainer>("%ImprovementsList");
        _orbitingTitle = GetNodeOrNull<Label>("%OrbitingTitle");
        _orbitingList = GetNodeOrNull<VBoxContainer>("%OrbitingList");

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnCloseButtonPressed;
        }

        if (_focusButton != null)
        {
            _focusButton.Pressed += OnFocusButtonPressed;
        }

        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal += OnTickUpdate;
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

            int curImpCount = _currentStar.Improvements?.Count ?? 0;
            int curProjCount = GetActiveProjectsForStar(_currentStar).Count;
            if (curImpCount != _lastImprovementsCount || curProjCount != _lastActiveConstructionCount)
            {
                PopulateImprovements();
            }

            int curOrbitingCount = CountOrbitingShips(_currentStar);
            if (curOrbitingCount != _lastOrbitingShipsCount)
            {
                PopulateOrbitingShips();
            }
        }
    }

    private void OnTickUpdate(int tick)
    {
        if (!GodotObject.IsInstanceValid(this) || IsQueuedForDeletion())
            return;

        if (_currentStar != null && GodotObject.IsInstanceValid(_currentStar))
        {
            UpdateRealtimeMetrics();
            PopulateImprovements();
            PopulateOrbitingShips();
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
        if (contextNode != null && contextNode.IsInsideTree())
        {
            CanvasLayer canvasLayer = contextNode.GetTree()?.Root?.FindChild("CanvasLayer", true, false) as CanvasLayer;
            if (canvasLayer != null)
            {
                canvasLayer.AddChild(infoBox);
            }
            else
            {
                contextNode.GetTree()?.Root?.AddChild(infoBox);
            }
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
        _lastImprovementsCount = -1;
        _lastActiveConstructionCount = -1;
        _lastOrbitingShipsCount = -1;
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
            _typeLabel.Text = Star.GetTypeName(_currentStar.StarType).ToUpperInvariant();
            _typeLabel.SelfModulate = _currentStar.StarColor;
        }

        // Demographics
        UpdateRealtimeMetrics();

        // Facilities / Improvements
        PopulateImprovements();

        // Orbiting Ships
        PopulateOrbitingShips();
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
            _popGrowthLabel.Text = $"Growth: +{growthPerTick:N0} / mo (+{growthPct:0.0}%)";
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
        if (_improvementsList == null || _currentStar == null || !GodotObject.IsInstanceValid(_currentStar))
        {
            return;
        }

        _lastImprovementsCount = _currentStar.Improvements?.Count ?? 0;
        var activeProjects = GetActiveProjectsForStar(_currentStar);
        _lastActiveConstructionCount = activeProjects.Count;

        if (_improvementsTitle != null)
        {
            int maxSlots = _currentStar.MaxImprovements;
            _improvementsTitle.Text = activeProjects.Count > 0
                ? $"FACILITIES ({_lastImprovementsCount} + {activeProjects.Count} / {maxSlots})"
                : $"FACILITIES ({_lastImprovementsCount} / {maxSlots})";
        }

        // Remove old children
        foreach (Node child in _improvementsList.GetChildren())
        {
            _improvementsList.RemoveChild(child);
            child.QueueFree();
        }

        bool hasItems = false;

        // 1. Installed Facilities
        if (_currentStar.Improvements != null)
        {
            foreach (ImprovementResource imp in _currentStar.Improvements)
            {
                if (imp == null) continue;
                hasItems = true;

                var itemHBox = new HBoxContainer();

                var label = new Label
                {
                    Text = $"• {imp.ImprovementName}",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                label.AddThemeFontSizeOverride("font_size", 12);
                label.AddThemeColorOverride("font_color", new Color("00e5ff"));
                itemHBox.AddChild(label);

                // Show primary output badge if available
                string outputSummary = GetOutputSummary(imp);
                if (!string.IsNullOrEmpty(outputSummary))
                {
                    var outputLabel = new Label
                    {
                        Text = outputSummary
                    };
                    outputLabel.AddThemeFontSizeOverride("font_size", 11);
                    outputLabel.AddThemeColorOverride("font_color", new Color("8fa3bf"));
                    itemHBox.AddChild(outputLabel);
                }

                _improvementsList.AddChild(itemHBox);
            }
        }

        // 2. Ongoing Construction Projects on this star
        if (activeProjects.Count > 0)
        {
            foreach (var proj in activeProjects)
            {
                if (proj.Improvement == null) continue;
                hasItems = true;

                var itemHBox = new HBoxContainer();

                string statusText = proj.IsUnderConstruction ? $"{proj.RemainingTicks}m left" : "Pending Funds";
                var label = new Label
                {
                    Text = $"🔨 {proj.Improvement.ImprovementName} [{statusText}]",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                label.AddThemeFontSizeOverride("font_size", 11);
                label.AddThemeColorOverride("font_color", proj.IsUnderConstruction ? new Color("ffd700") : new Color("ff9800"));
                itemHBox.AddChild(label);

                _improvementsList.AddChild(itemHBox);
            }
        }

        if (!hasItems)
        {
            var emptyLabel = new Label
            {
                Text = "• No facilities installed"
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _improvementsList.AddChild(emptyLabel);
        }
    }

    private System.Collections.Generic.List<Fleet> GetOrbitingFleetsForStar(Star star)
    {
        var result = new System.Collections.Generic.List<Fleet>();
        if (star == null)
            return result;

        if (FleetManager.HasInstance)
        {
            foreach (var fleet in FleetManager.Instance.GetFleetsOrbitingStar(star))
            {
                if (!result.Contains(fleet))
                {
                    result.Add(fleet);
                }
            }
        }

        if (star.OwningEmpire?.OwnedFleets != null)
        {
            foreach (var fleet in star.OwningEmpire.OwnedFleets)
            {
                if (
                    fleet != null
                    && GodotObject.IsInstanceValid(fleet)
                    && !fleet.IsQueuedForDeletion()
                    && !fleet.IsEmpty
                    && fleet.State == FleetState.InOrbit
                    && fleet.OrbitingStar == star
                    && !result.Contains(fleet)
                )
                {
                    result.Add(fleet);
                }
            }
        }
        return result;
    }

    private int CountOrbitingShips(Star star)
    {
        int count = 0;
        foreach (var fleet in GetOrbitingFleetsForStar(star))
        {
            count += fleet.Ships.Count;
        }
        return count;
    }

    private void PopulateOrbitingShips()
    {
        if (_orbitingList == null || _currentStar == null || !GodotObject.IsInstanceValid(_currentStar))
            return;

        var orbitingFleets = GetOrbitingFleetsForStar(_currentStar);
        int totalShips = 0;
        foreach (var fleet in orbitingFleets)
        {
            totalShips += fleet.Ships.Count;
        }
        _lastOrbitingShipsCount = totalShips;

        if (_orbitingTitle != null)
        {
            _orbitingTitle.Text = $"ORBITING SHIPS ({totalShips})";
        }

        foreach (Node child in _orbitingList.GetChildren())
        {
            _orbitingList.RemoveChild(child);
            child.QueueFree();
        }

        if (totalShips == 0)
        {
            var emptyLabel = new Label
            {
                Text = "• No ships in orbit",
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _orbitingList.AddChild(emptyLabel);
            return;
        }

        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        var pressedStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_pressed.tres");

        foreach (var fleet in orbitingFleets)
        {
            Color empColor = fleet.OwningEmpire?.EmpireColor ?? new Color("00e5ff");
            foreach (var ship in fleet.Ships)
            {
                Ship capturedShip = ship;
                var btn = new Button
                {
                    Text = $"{ship.ChronicleName} ({ship.ClassName}) — {fleet.FleetName}",
                    Alignment = HorizontalAlignment.Left,
                    ClipText = true,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    CustomMinimumSize = new Vector2(0, 22),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                btn.AddThemeFontSizeOverride("font_size", 11);
                btn.AddThemeColorOverride("font_color", empColor);
                btn.AddThemeColorOverride("font_hover_color", Colors.White);

                if (normalStyle != null)
                {
                    btn.AddThemeStyleboxOverride("normal", normalStyle);
                    btn.AddThemeStyleboxOverride("focus", normalStyle);
                }
                if (hoverStyle != null)
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);
                if (pressedStyle != null)
                    btn.AddThemeStyleboxOverride("pressed", pressedStyle);

                btn.Pressed += () => ShipInfoBox.OpenForShip(capturedShip, this);
                _orbitingList.AddChild(btn);
            }
        }
    }

    private System.Collections.Generic.List<Empire.ConstructionDisplayInfo> GetActiveProjectsForStar(Star star)
    {
        var result = new System.Collections.Generic.List<Empire.ConstructionDisplayInfo>();
        if (star == null) return result;

        if (star.OwningEmpire != null && GodotObject.IsInstanceValid(star.OwningEmpire))
        {
            foreach (var proj in star.OwningEmpire.GetActiveConstructionProjects())
            {
                if (proj.TargetStar == star)
                {
                    result.Add(proj);
                }
            }
            return result;
        }

        if (EmpireManager.HasInstance && EmpireManager.Instance.SpawnedEmpires != null)
        {
            foreach (var emp in EmpireManager.Instance.SpawnedEmpires)
            {
                if (emp != null && GodotObject.IsInstanceValid(emp))
                {
                    foreach (var proj in emp.GetActiveConstructionProjects())
                    {
                        if (proj.TargetStar == star)
                        {
                            result.Add(proj);
                        }
                    }
                }
            }
        }

        return result;
    }

    private string GetOutputSummary(ImprovementResource imp)
    {
        if (imp.ResourceOutput == null || imp.ResourceOutput.Count == 0)
            return string.Empty;

        var parts = new System.Collections.Generic.List<string>();
        foreach (var (resType, val) in imp.ResourceOutput)
        {
            if (val > 0)
            {
                parts.Add($"+{val} {resType}");
            }
        }
        return string.Join(", ", parts);
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

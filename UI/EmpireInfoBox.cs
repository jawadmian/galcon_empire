using System;
using System.Collections.Generic;
using Godot;

public partial class EmpireInfoBox : Control
{
    public static EmpireInfoBox Instance { get; private set; }

    private Empire _currentEmpire;

    private Label _nameLabel;
    private Label _capitalLabel;
    private Label _territoryLabel;
    private Button _focusCapitalButton;
    private Button _closeButton;

    private Label _popValueLabel;
    private Label _foodValueLabel;
    private Label _oreValueLabel;
    private Label _creditsValueLabel;

    private VBoxContainer _starsList;
    private VBoxContainer _buildQueueList;
    private int _lastBuildQueueHash = -1;
    private int _lastStarsCount = -1;

    // Window dragging
    private bool _isDraggingWindow = false;
    private Vector2 _dragOffset = Vector2.Zero;

    public Empire CurrentEmpire => _currentEmpire;

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
        _capitalLabel = GetNodeOrNull<Label>("%CapitalLabel");
        _territoryLabel = GetNodeOrNull<Label>("%TerritoryLabel");
        _focusCapitalButton = GetNodeOrNull<Button>("%FocusCapitalButton");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");

        _popValueLabel = GetNodeOrNull<Label>("%PopValueLabel");
        _foodValueLabel = GetNodeOrNull<Label>("%FoodValueLabel");
        _oreValueLabel = GetNodeOrNull<Label>("%OreValueLabel");
        _creditsValueLabel = GetNodeOrNull<Label>("%CreditsValueLabel");

        _starsList = GetNodeOrNull<VBoxContainer>("%StarsList");
        _buildQueueList = GetNodeOrNull<VBoxContainer>("%BuildQueueList");

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnCloseButtonPressed;
        }

        if (_focusCapitalButton != null)
        {
            _focusCapitalButton.Pressed += OnFocusCapitalButtonPressed;
        }

        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal += OnTickUpdate;
        }

        if (_currentEmpire != null)
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
        if (_currentEmpire != null && GodotObject.IsInstanceValid(_currentEmpire))
        {
            UpdateRealtimeMetrics();

            int currentHash = ComputeBuildQueueHash();
            if (currentHash != _lastBuildQueueHash)
            {
                PopulateBuildQueue();
            }

            int currentStars = _currentEmpire.OwnedStars?.Count ?? 0;
            if (currentStars != _lastStarsCount)
            {
                _lastStarsCount = currentStars;
                PopulateStarsList();
            }
        }
    }

    private void OnTickUpdate(int tick)
    {
        if (_currentEmpire != null && GodotObject.IsInstanceValid(_currentEmpire))
        {
            UpdateRealtimeMetrics();
            PopulateBuildQueue();
            PopulateStarsList();
        }
    }

    private int ComputeBuildQueueHash()
    {
        if (_currentEmpire == null) return 0;
        int hash = 17;
        var projs = _currentEmpire.GetActiveConstructionProjects();
        hash = hash * 31 + projs.Count;
        foreach (var p in projs)
        {
            hash = hash * 31 + p.RemainingTicks;
            hash = hash * 31 + (p.IsUnderConstruction ? 1 : 0);
        }
        return hash;
    }

    /// <summary>
    /// Opens the EmpireInfoBox for the given empire. Reuses an existing open instance if available.
    /// </summary>
    public static EmpireInfoBox OpenForEmpire(Empire empire, Node contextNode)
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            Instance.DisplayEmpire(empire);
            Instance.Show();
            return Instance;
        }

        var scene = GD.Load<PackedScene>("res://UI/empire_info_box.tscn");
        if (scene == null)
        {
            GD.PushError("EmpireInfoBox: Failed to load res://UI/empire_info_box.tscn");
            return null;
        }

        var infoBox = scene.Instantiate<EmpireInfoBox>();
        if (infoBox == null)
        {
            GD.PushError("EmpireInfoBox: Failed to instantiate EmpireInfoBox scene.");
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

        infoBox.DisplayEmpire(empire);
        return infoBox;
    }

    /// <summary>
    /// Binds an empire and updates the panel contents.
    /// </summary>
    public void DisplayEmpire(Empire empire)
    {
        _currentEmpire = empire;
        if (IsNodeReady() || _nameLabel != null)
        {
            RefreshDisplay();
        }
    }

    public void RefreshDisplay()
    {
        if (_currentEmpire == null || !GodotObject.IsInstanceValid(_currentEmpire))
        {
            return;
        }

        // Header info
        if (_nameLabel != null)
        {
            _nameLabel.Text = _currentEmpire.EmpireName?.ToUpperInvariant() ?? "UNKNOWN EMPIRE";
            _nameLabel.SelfModulate = _currentEmpire.EmpireColor;
        }

        if (_capitalLabel != null)
        {
            string capName = _currentEmpire.HomeStar?.StarName ?? "None";
            _capitalLabel.Text = $"CAPITAL: {capName.ToUpperInvariant()}";
        }

        if (_territoryLabel != null)
        {
            int starCount = _currentEmpire.OwnedStars?.Count ?? 0;
            _territoryLabel.Text = $"TERRITORY: {starCount} SYSTEM{(starCount == 1 ? "" : "S")}";
        }

        // Demographics and Economy
        UpdateRealtimeMetrics();

        // Controlled Stars
        PopulateStarsList();

        // Build Queue
        PopulateBuildQueue();
    }

    private void UpdateRealtimeMetrics()
    {
        if (_currentEmpire == null || !GodotObject.IsInstanceValid(_currentEmpire))
        {
            return;
        }

        // Total Population across all owned stars
        long totalPop = 0;
        if (_currentEmpire.OwnedStars != null)
        {
            foreach (Star star in _currentEmpire.OwnedStars)
            {
                if (star != null && GodotObject.IsInstanceValid(star))
                {
                    totalPop += star.StarPopulation;
                }
            }
        }

        if (_popValueLabel != null)
        {
            _popValueLabel.Text = $"{totalPop:N0} citizens";
        }

        // Production & Stockpiles
        _currentEmpire.ResourceStockpiles.TryGetValue(ResourceType.Food, out long foodStockpile);
        _currentEmpire.ProductionPerTick.TryGetValue(ResourceType.Food, out long foodProd);

        _currentEmpire.ResourceStockpiles.TryGetValue(ResourceType.Ore, out long oreStockpile);
        _currentEmpire.ProductionPerTick.TryGetValue(ResourceType.Ore, out long oreProd);

        _currentEmpire.ResourceStockpiles.TryGetValue(ResourceType.Money, out long moneyStockpile);
        _currentEmpire.ProductionPerTick.TryGetValue(ResourceType.Money, out long moneyProd);

        if (_foodValueLabel != null)
        {
            _foodValueLabel.Text = $"{foodStockpile:N0} (+{foodProd:N0}/t)";
        }

        if (_oreValueLabel != null)
        {
            _oreValueLabel.Text = $"{oreStockpile:N0} (+{oreProd:N0}/t)";
        }

        if (_creditsValueLabel != null)
        {
            _creditsValueLabel.Text = $"{moneyStockpile:N0} (+{moneyProd:N0}/t)";
        }
    }

    private void PopulateStarsList()
    {
        if (_starsList == null)
            return;

        // Remove old children
        foreach (Node child in _starsList.GetChildren())
        {
            _starsList.RemoveChild(child);
            child.QueueFree();
        }

        if (_currentEmpire.OwnedStars == null || _currentEmpire.OwnedStars.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "• No star systems controlled"
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _starsList.AddChild(emptyLabel);
            return;
        }

        foreach (Star star in _currentEmpire.OwnedStars)
        {
            if (star == null || !GodotObject.IsInstanceValid(star)) continue;

            var itemHBox = new HBoxContainer();

            var starBtn = new Button
            {
                Text = $"★ {star.StarName} ({star.StarPopulation:N0})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left
            };
            starBtn.AddThemeFontSizeOverride("font_size", 11);
            starBtn.AddThemeColorOverride("font_color", new Color("00e5ff"));
            starBtn.AddThemeColorOverride("font_hover_color", Colors.White);

            // Style with scifi button styles
            var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
            var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
            if (normalStyle != null) starBtn.AddThemeStyleboxOverride("normal", normalStyle);
            if (hoverStyle != null) starBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            Star capturedStar = star;
            starBtn.Pressed += () => OnStarClicked(capturedStar);

            itemHBox.AddChild(starBtn);
            _starsList.AddChild(itemHBox);
        }
    }

    private void PopulateBuildQueue()
    {
        if (_buildQueueList == null || _currentEmpire == null || !GodotObject.IsInstanceValid(_currentEmpire))
            return;

        _lastBuildQueueHash = ComputeBuildQueueHash();

        foreach (Node child in _buildQueueList.GetChildren())
        {
            _buildQueueList.RemoveChild(child);
            child.QueueFree();
        }

        var projects = _currentEmpire.GetActiveConstructionProjects();
        if (projects == null || projects.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "• Industrial queue idle"
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _buildQueueList.AddChild(emptyLabel);
            return;
        }

        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");

        foreach (var proj in projects)
        {
            if (proj.Improvement == null) continue;

            var itemHBox = new HBoxContainer();

            string statusText = proj.IsUnderConstruction ? $"{proj.RemainingTicks}t left" : "Pending Funds";
            string targetName = proj.TargetStar != null ? $"@{proj.TargetStar.StarName}" : "";

            var projBtn = new Button
            {
                Text = $"🔨 {proj.Improvement.ImprovementName} {targetName} [{statusText}]",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left
            };
            projBtn.AddThemeFontSizeOverride("font_size", 11);
            projBtn.AddThemeColorOverride("font_color", proj.IsUnderConstruction ? new Color("00e5ff") : new Color("ffd700"));
            projBtn.AddThemeColorOverride("font_hover_color", Colors.White);

            if (normalStyle != null) projBtn.AddThemeStyleboxOverride("normal", normalStyle);
            if (hoverStyle != null) projBtn.AddThemeStyleboxOverride("hover", hoverStyle);

            if (proj.TargetStar != null)
            {
                Star capturedStar = proj.TargetStar;
                projBtn.Pressed += () => OnStarClicked(capturedStar);
            }

            itemHBox.AddChild(projBtn);
            _buildQueueList.AddChild(itemHBox);
        }
    }

    private void OnStarClicked(Star star)
    {
        if (star != null && GodotObject.IsInstanceValid(star))
        {
            CameraController.Instance?.FocusOn(star.GlobalPosition);
            StarInfoBox.OpenForStar(star, this);
        }
    }

    private void OnFocusCapitalButtonPressed()
    {
        if (_currentEmpire?.HomeStar != null && GodotObject.IsInstanceValid(_currentEmpire.HomeStar))
        {
            CameraController.Instance?.FocusOn(_currentEmpire.HomeStar.GlobalPosition);
            StarInfoBox.OpenForStar(_currentEmpire.HomeStar, this);
        }
    }

    private void OnCloseButtonPressed()
    {
        QueueFree();
    }
}

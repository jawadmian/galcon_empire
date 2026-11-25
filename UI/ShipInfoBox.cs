using System.Collections.Generic;
using Godot;

public partial class ShipInfoBox : Control
{
    public static ShipInfoBox Instance { get; private set; }

    private Ship _currentShip;

    private Label _nameLabel;
    private Label _classLabel;
    private Label _originLabel;
    private Label _fleetLabel;
    private Label _statsLabel;
    private Label _abilitiesLabel;
    private Button _focusButton;
    private Button _closeButton;
    private VBoxContainer _chronicleList;
    private Button _chronicleFilterButton;
    private ChronicleViewMode _chronicleViewMode = ChronicleViewMode.CondensedHigh;

    private int _lastChronicleCount = -1;
    private long _lastChronicleLatestId = -1;

    private bool _isDraggingWindow = false;
    private Vector2 _dragOffset = Vector2.Zero;

    public Ship CurrentShip => _currentShip;

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
        _nameLabel = GetNodeOrNull<Label>("%NameLabel");
        _classLabel = GetNodeOrNull<Label>("%ClassLabel");
        _originLabel = GetNodeOrNull<Label>("%OriginLabel");
        _fleetLabel = GetNodeOrNull<Label>("%FleetLabel");
        _statsLabel = GetNodeOrNull<Label>("%StatsLabel");
        _abilitiesLabel = GetNodeOrNull<Label>("%AbilitiesLabel");
        _focusButton = GetNodeOrNull<Button>("%FocusButton");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _chronicleList = GetNodeOrNull<VBoxContainer>("%ChronicleList");
        _chronicleFilterButton = GetNodeOrNull<Button>("%ChronicleFilterButton");
        ChroniclePanelHelper.ConfigureFilterButton(
            _chronicleFilterButton,
            _chronicleViewMode,
            OnChronicleFilterPressed
        );

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

        if (_currentShip != null)
        {
            RefreshDisplay();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_isDraggingWindow && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            _isDraggingWindow = false;
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
                    if (ChroniclePanelHelper.IsPointerOverChronicle(_chronicleList))
                        return;
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

    private void OnTickUpdate(int tick)
    {
        if (!GodotObject.IsInstanceValid(this) || IsQueuedForDeletion())
            return;

        if (_currentShip != null)
        {
            RefreshDisplay();
        }
    }

    public static ShipInfoBox OpenForShip(Ship ship, Node contextNode)
    {
        if (ship == null)
            return null;

        if (Instance != null && GodotObject.IsInstanceValid(Instance) && !Instance.IsQueuedForDeletion())
        {
            Instance.DisplayShip(ship);
            return Instance;
        }

        var scene = GD.Load<PackedScene>("res://UI/ship_info_box.tscn");
        if (scene == null)
        {
            GD.PushError("ShipInfoBox: Failed to load res://UI/ship_info_box.tscn");
            return null;
        }

        var box = scene.Instantiate<ShipInfoBox>();
        box.Position = new Vector2(520, 60);
        Instance = box;

        Node parent = FindUiLayer(contextNode);
        if (parent != null)
        {
            parent.AddChild(box);
        }

        box.DisplayShip(ship);
        return box;
    }

    private static Node FindUiLayer(Node contextNode)
    {
        if (contextNode == null || !contextNode.IsInsideTree())
            return null;

        var tree = contextNode.GetTree();
        if (tree == null)
            return null;

        var root = tree.CurrentScene;
        if (root != null)
        {
            var canvasLayer = root.GetNodeOrNull<CanvasLayer>("CanvasLayer");
            if (canvasLayer != null)
                return canvasLayer;
            return root;
        }

        return (Node)(tree.Root?.FindChild("CanvasLayer", true, false) as CanvasLayer) ?? tree.Root;
    }

    public void DisplayShip(Ship ship)
    {
        _currentShip = ship;
        _lastChronicleCount = -1;
        _lastChronicleLatestId = -1;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_currentShip == null)
            return;

        if (_nameLabel != null)
        {
            _nameLabel.Text = _currentShip.ChronicleName.ToUpperInvariant();
            _nameLabel.AddThemeColorOverride("font_color", new Color("00e5ff"));
        }

        if (_classLabel != null)
        {
            string className = _currentShip.ClassName ?? _currentShip.Design?.DesignName ?? "Unknown";
            int techLvl = _currentShip.Design?.RequiredTechLevel ?? 1;
            _classLabel.Text = $"CLASS: {className} | TECH LVL {techLvl}";
        }

        if (_originLabel != null)
        {
            string builtAt = string.IsNullOrEmpty(_currentShip.BuiltAtStarName)
                ? "Unknown"
                : _currentShip.BuiltAtStarName;
            _originLabel.Text = $"COMMISSIONED: {TickManager.FormatDate(_currentShip.CommissionedTick)} at {builtAt}";
        }

        if (_fleetLabel != null)
        {
            if (_currentShip.CurrentFleet != null && GodotObject.IsInstanceValid(_currentShip.CurrentFleet))
            {
                _fleetLabel.Text = $"FLEET: {_currentShip.CurrentFleet.FleetName}";
            }
            else
            {
                _fleetLabel.Text = "FLEET: Decommissioned / Consumed";
            }
        }

        if (_statsLabel != null)
        {
            _statsLabel.Text =
                $"HP: {_currentShip.CurrentHitPoints}/{_currentShip.MaxHitPoints} | SPD: {_currentShip.Speed:F0} u/mo\n"
                + $"ATK: {_currentShip.AttackDamage} | DEF: {_currentShip.DefenseRating} | CARGO: {_currentShip.CargoCapacity} | SENSORS: {_currentShip.SensorRange:F0}";
        }

        if (_abilitiesLabel != null)
        {
            var abilities = _currentShip.Design?.SpecialAbilities;
            if (abilities != null && abilities.Count > 0)
            {
                var names = new List<string>();
                foreach (var ab in abilities)
                    names.Add(ab.ToString());
                _abilitiesLabel.Text = $"ABILITIES: {string.Join(", ", names)}";
            }
            else
            {
                _abilitiesLabel.Text = "ABILITIES: None";
            }
        }

        PopulateChronicle();
    }

    private void OnChronicleFilterPressed()
    {
        SetChronicleViewMode(ChronicleFilter.NextViewMode(_chronicleViewMode));
    }

    public void SetChronicleViewMode(ChronicleViewMode mode)
    {
        _chronicleViewMode = mode;
        _lastChronicleCount = -1;
        _lastChronicleLatestId = -1;
        if (_chronicleFilterButton != null)
        {
            _chronicleFilterButton.Text = ChronicleFilter.GetViewModeLabel(_chronicleViewMode);
        }
        PopulateChronicle();
    }

    private void PopulateChronicle()
    {
        if (_currentShip == null)
            return;

        ChroniclePanelHelper.PopulateChronicle(
            _chronicleList,
            _currentShip,
            _chronicleViewMode,
            "• No recorded ship events yet",
            this,
            ref _lastChronicleCount,
            ref _lastChronicleLatestId
        );
    }

    private void OnFocusButtonPressed()
    {
        if (_currentShip?.WorldPosition != null)
        {
            CameraController.Instance?.FocusOn(_currentShip.WorldPosition.Value);
        }
    }

    private void OnCloseButtonPressed()
    {
        QueueFree();
    }
}

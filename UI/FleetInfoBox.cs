using System.Collections.Generic;
using Godot;

public partial class FleetInfoBox : Control
{
    public static FleetInfoBox Instance { get; private set; }

    private Fleet _currentFleet;

    private Label _nameLabel;
    private Label _empireLabel;
    private Label _statusLabel;
    private Label _speedLabel;
    private Button _focusButton;
    private Button _closeButton;

    private VBoxContainer _shipsList;
    private VBoxContainer _chronicleList;

    private int _lastShipsCount = -1;
    private FleetState _lastState = FleetState.InOrbit;
    private int _lastChronicleCount = -1;

    private bool _isDraggingWindow = false;
    private Vector2 _dragOffset = Vector2.Zero;

    public Fleet CurrentFleet => _currentFleet;

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
        _empireLabel = GetNodeOrNull<Label>("%EmpireLabel");
        _statusLabel = GetNodeOrNull<Label>("%StatusLabel");
        _speedLabel = GetNodeOrNull<Label>("%SpeedLabel");
        _focusButton = GetNodeOrNull<Button>("%FocusButton");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");

        _shipsList = GetNodeOrNull<VBoxContainer>("%ShipsList");
        _chronicleList = GetNodeOrNull<VBoxContainer>("%ChronicleList");

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

        if (_currentFleet != null)
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
        if (_currentFleet == null || !GodotObject.IsInstanceValid(_currentFleet) || _currentFleet.IsQueuedForDeletion())
        {
            return;
        }

        UpdateStatusLabels();

        int shipCount = _currentFleet.Ships.Count;
        if (shipCount != _lastShipsCount || _currentFleet.State != _lastState)
        {
            PopulateShipsList();
            PopulateChronicle();
        }
    }

    private void OnTickUpdate(int tick)
    {
        if (_currentFleet != null && GodotObject.IsInstanceValid(_currentFleet))
        {
            RefreshDisplay();
        }
    }

    public static FleetInfoBox OpenForFleet(Fleet fleet, Node contextNode)
    {
        if (fleet == null)
            return null;

        if (Instance != null && GodotObject.IsInstanceValid(Instance) && !Instance.IsQueuedForDeletion())
        {
            Instance.DisplayFleet(fleet);
            return Instance;
        }

        var scene = GD.Load<PackedScene>("res://UI/fleet_info_box.tscn");
        if (scene == null)
        {
            GD.PushError("FleetInfoBox: Failed to load res://UI/fleet_info_box.tscn");
            return null;
        }

        var box = scene.Instantiate<FleetInfoBox>();
        box.Position = new Vector2(120, 60);

        Node parent = FindUiLayer(contextNode);
        if (parent != null)
        {
            parent.AddChild(box);
        }

        box.DisplayFleet(fleet);
        return box;
    }

    private static Node FindUiLayer(Node contextNode)
    {
        if (contextNode != null && contextNode.GetTree() != null)
        {
            var root = contextNode.GetTree().CurrentScene;
            if (root != null)
            {
                var canvasLayer = root.GetNodeOrNull<CanvasLayer>("CanvasLayer");
                if (canvasLayer != null)
                    return canvasLayer;
                return root;
            }
        }
        return null;
    }

    public void DisplayFleet(Fleet fleet)
    {
        _currentFleet = fleet;
        _lastShipsCount = -1;
        _lastChronicleCount = -1;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_currentFleet == null || !GodotObject.IsInstanceValid(_currentFleet))
            return;

        if (_nameLabel != null)
        {
            _nameLabel.Text = _currentFleet.FleetName.ToUpperInvariant();
            _nameLabel.AddThemeColorOverride(
                "font_color",
                _currentFleet.OwningEmpire?.EmpireColor ?? new Color("00e5ff")
            );
        }

        if (_empireLabel != null)
        {
            _empireLabel.Text = $"EMPIRE: {_currentFleet.OwningEmpire?.EmpireName ?? "Independent"}";
        }

        UpdateStatusLabels();
        PopulateShipsList();
        PopulateChronicle();
    }

    private void UpdateStatusLabels()
    {
        if (_currentFleet == null || !GodotObject.IsInstanceValid(_currentFleet))
            return;

        if (_statusLabel != null)
        {
            if (_currentFleet.State == FleetState.InTransit && _currentFleet.DestinationStar != null)
            {
                _statusLabel.Text =
                    $"STATUS: IN TRANSIT → {_currentFleet.DestinationStar.StarName} ({_currentFleet.CurrentOrder})";
            }
            else if (_currentFleet.OrbitingStar != null)
            {
                _statusLabel.Text = $"STATUS: ORBITING {_currentFleet.OrbitingStar.StarName}";
            }
            else
            {
                _statusLabel.Text = "STATUS: DEEP SPACE";
            }
        }

        if (_speedLabel != null)
        {
            _speedLabel.Text = $"SPEED: {_currentFleet.FleetSpeed:F0} u/tick | SHIPS: {_currentFleet.Ships.Count}";
        }
    }

    private void PopulateShipsList()
    {
        if (_shipsList == null || _currentFleet == null)
            return;

        _lastShipsCount = _currentFleet.Ships.Count;
        _lastState = _currentFleet.State;

        foreach (Node child in _shipsList.GetChildren())
        {
            child.QueueFree();
        }

        if (_currentFleet.Ships.Count == 0)
        {
            var emptyLabel = new Label { Text = "• No active ships in fleet" };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _shipsList.AddChild(emptyLabel);
            return;
        }

        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        var pressedStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_pressed.tres");

        foreach (Ship ship in _currentFleet.Ships)
        {
            Ship capturedShip = ship;
            var btn = new Button
            {
                Text = $"{ship.Design?.DesignName ?? "Ship"} #{ship.ShipId}  ({ship.CurrentHitPoints}/{ship.MaxHitPoints} HP)",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 26),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.AddThemeColorOverride("font_color", new Color("00e5ff"));
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
            _shipsList.AddChild(btn);
        }
    }

    private void PopulateChronicle()
    {
        if (_chronicleList == null || _currentFleet == null)
            return;

        List<ChronicleRecord> events = ChronicleManager.Instance != null
            ? ChronicleManager.Instance.QueryEvents(
                new ChronicleFilter { EntityId = _currentFleet.ChronicleId, Limit = 25 }
            )
            : new List<ChronicleRecord>();

        if (events.Count == _lastChronicleCount && _lastChronicleCount >= 0)
            return;

        _lastChronicleCount = events.Count;

        foreach (Node child in _chronicleList.GetChildren())
        {
            child.QueueFree();
        }

        if (events.Count == 0)
        {
            var emptyLabel = new Label { Text = "• No recorded fleet events yet" };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            _chronicleList.AddChild(emptyLabel);
            return;
        }

        foreach (var ev in events)
        {
            var label = new Label
            {
                Text = $"[Day {ev.Tick}] {ev.Title}: {ev.RawText}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color("8fa3bf"));
            _chronicleList.AddChild(label);
        }
    }

    private void OnFocusButtonPressed()
    {
        if (_currentFleet != null && GodotObject.IsInstanceValid(_currentFleet))
        {
            CameraController.Instance?.FocusOn(_currentFleet.GlobalPosition);
        }
    }

    private void OnCloseButtonPressed()
    {
        QueueFree();
    }
}

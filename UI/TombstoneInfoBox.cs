using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// Single-instance draggable archive dossier displayed when clicking a Chronicle hyperlink
/// for an entity that is no longer active in the live game (e.g. an expended colony ship
/// or disbanded fleet).
/// </summary>
public partial class TombstoneInfoBox : Control
{
    public static TombstoneInfoBox Instance { get; private set; }

    private ChronicleTombstone _currentTombstone;

    private Label _entityTypeLabel;
    private Label _nameLabel;
    private Label _statusLabel;
    private Label _lastSeenLabel;
    private Label _summaryLabel;
    private Button _closeButton;
    private VBoxContainer _chronicleList;
    private Button _chronicleFilterButton;
    private ChronicleViewMode _chronicleViewMode = ChronicleViewMode.DetailedAll;

    private int _lastChronicleCount = -1;
    private long _lastChronicleLatestId = -1;

    private bool _isDraggingWindow = false;
    private Vector2 _dragOffset = Vector2.Zero;

    public ChronicleTombstone CurrentTombstone => _currentTombstone;

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
        EnsureNodesCached();

        ChroniclePanelHelper.ConfigureFilterButton(
            _chronicleFilterButton,
            _chronicleViewMode,
            OnChronicleFilterPressed
        );

        if (_closeButton != null)
        {
            _closeButton.Pressed += OnCloseButtonPressed;
        }

        if (_currentTombstone != null)
        {
            RefreshDisplay();
        }
    }

    private void EnsureNodesCached()
    {
        if (_entityTypeLabel != null)
            return;

        _entityTypeLabel = GetNodeOrNull<Label>("%EntityTypeLabel");
        _nameLabel = GetNodeOrNull<Label>("%NameLabel");
        _statusLabel = GetNodeOrNull<Label>("%StatusLabel");
        _lastSeenLabel = GetNodeOrNull<Label>("%LastSeenLabel");
        _summaryLabel = GetNodeOrNull<Label>("%SummaryLabel");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _chronicleList = GetNodeOrNull<VBoxContainer>("%ChronicleList");
        _chronicleFilterButton = GetNodeOrNull<Button>("%ChronicleFilterButton");
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
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
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
        else if (@event is InputEventMouseMotion && _isDraggingWindow)
        {
            GlobalPosition = GetGlobalMousePosition() - _dragOffset;
        }
    }

    public static TombstoneInfoBox OpenForTombstone(ChronicleTombstone tombstone, Node contextNode)
    {
        if (tombstone == null)
            return null;

        if (Instance != null && GodotObject.IsInstanceValid(Instance) && !Instance.IsQueuedForDeletion())
        {
            Instance.DisplayTombstone(tombstone);
            Instance.Show();
            return Instance;
        }

        var scene = GD.Load<PackedScene>("res://UI/tombstone_info_box.tscn");
        if (scene == null)
        {
            GD.PushError("TombstoneInfoBox: Failed to load res://UI/tombstone_info_box.tscn");
            return null;
        }

        var box = scene.Instantiate<TombstoneInfoBox>();
        if (box == null)
            return null;

        box.Position = new Vector2(460, 80);
        Instance = box;

        Node parent = FindUiLayer(contextNode);
        if (parent != null)
        {
            parent.AddChild(box);
        }

        box.DisplayTombstone(tombstone);
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

    public void DisplayTombstone(ChronicleTombstone tombstone)
    {
        _currentTombstone = tombstone;
        _lastChronicleCount = -1;
        _lastChronicleLatestId = -1;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_currentTombstone == null)
            return;

        EnsureNodesCached();

        string typeUpper = string.IsNullOrEmpty(_currentTombstone.EntityType)
            ? "RECORD"
            : _currentTombstone.EntityType.ToUpperInvariant();

        if (_entityTypeLabel != null)
        {
            _entityTypeLabel.Text = $"ARCHIVED {typeUpper}";
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = string.IsNullOrEmpty(_currentTombstone.DisplayName)
                ? _currentTombstone.ChronicleId.ToUpperInvariant()
                : _currentTombstone.DisplayName.ToUpperInvariant();
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = $"STATUS: {_currentTombstone.Status.ToUpperInvariant()}";
        }

        if (_lastSeenLabel != null)
        {
            _lastSeenLabel.Text = $"LAST RECORDED: {TickManager.FormatDate(_currentTombstone.LastKnownTick)}";
        }

        if (_summaryLabel != null)
        {
            _summaryLabel.Text = FormatSummary(_currentTombstone.SummaryJson);
        }

        PopulateChronicle();
    }

    private static string FormatSummary(string summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson) || summaryJson == "{}")
            return "ARCHIVE NOTE: Active game object is no longer present.";

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(summaryJson);
            if (dict == null || dict.Count == 0)
                return "ARCHIVE NOTE: Active game object is no longer present.";

            var parts = new List<string>();
            foreach (var (key, value) in dict)
            {
                string label = key.Replace('_', ' ').ToUpperInvariant();
                parts.Add($"{label}: {value}");
            }
            return string.Join(" | ", parts);
        }
        catch
        {
            return "ARCHIVE NOTE: Active game object is no longer present.";
        }
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
        if (_currentTombstone == null)
            return;

        ChroniclePanelHelper.PopulateChronicle(
            _chronicleList,
            _currentTombstone,
            _chronicleViewMode,
            "• No archived events found",
            this,
            ref _lastChronicleCount,
            ref _lastChronicleLatestId
        );
    }

    private void OnCloseButtonPressed()
    {
        QueueFree();
    }
}

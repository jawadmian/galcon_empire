using System;
using System.Collections.Generic;
using Godot;

public partial class EmpiresList : Control
{
    private static EmpiresList _instance;
    public static EmpiresList Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushWarning(
                    "EmpiresList.Instance was accessed before it was ready or EmpiresList is not in the scene tree."
                );
            }
            return _instance;
        }
        private set => _instance = value;
    }

    private VBoxContainer _empiresContainer;
    private Label _dayLabel;
    private readonly Dictionary<Empire, Button> _empireButtons = new Dictionary<Empire, Button>();

    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning(
                $"Duplicate EmpiresList instance detected. Old: {_instance.GetPath()}, New: {GetPath()}. Destroying new instance."
            );
            QueueFree();
            return;
        }
        _instance = this;

        // Cache node references using Scene Unique Names (%NodeName) with fallback to legacy paths
        _empiresContainer = GetNodeOrNull<VBoxContainer>("%EmpiresContainer")
            ?? GetNodeOrNull<VBoxContainer>("PanelContainer/VBoxContainer")
            ?? GetNodeOrNull<VBoxContainer>("Background/ContentMargin/MainVBox/EmpiresScroll/EmpiresContainer");

        if (_empiresContainer == null)
        {
            GD.PushError("EmpiresList: EmpiresContainer not found. UI will not function correctly.");
        }

        _dayLabel = GetNodeOrNull<Label>("%DayLabel")
            ?? GetNodeOrNull<Label>("PanelContainer/VBoxContainer/DayLabel")
            ?? GetNodeOrNull<Label>("Background/ContentMargin/MainVBox/HeaderHBox/DayLabel");

        if (_dayLabel == null)
        {
            GD.PushError("EmpiresList: DayLabel not found. Day count will not be displayed.");
        }

        // Connect to the TickManager's signal
        if (TickManager.Instance != null)
        {
            TickManager.Instance.TickUpdateSignal += OnTickUpdate;
        }
        else
        {
            GD.PushWarning("EmpiresList: TickManager.Instance is null. Cannot connect to TickUpdateSignal.");
        }
    }

    /// <summary>
    /// Adds a new interactive Button representing the empire to the list.
    /// Clicking the button opens the EmpireInfoBox dossier.
    /// </summary>
    /// <param name="empire">The Empire to add.</param>
    public void AddEmpire(Empire empire)
    {
        if (_empiresContainer == null)
        {
            GD.PushWarning("EmpiresList: EmpiresContainer is not initialized. Cannot add empire.");
            return;
        }
        if (empire == null)
        {
            GD.PushWarning("EmpiresList: Attempted to add a null empire.");
            return;
        }

        // If a button for this empire already exists, remove the old one first.
        if (_empireButtons.TryGetValue(empire, out Button existingBtn))
        {
            existingBtn.QueueFree();
            _empireButtons.Remove(empire);
        }

        int starCount = empire.OwnedStars?.Count ?? 0;
        string name = empire.EmpireName ?? "Unnamed Empire";

        Button empireBtn = new Button
        {
            Text = $"★ {name} [{starCount}]",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"Click to view {name} dossier"
        };

        empireBtn.AddThemeFontSizeOverride("font_size", 11);
        empireBtn.AddThemeColorOverride("font_color", empire.EmpireColor);
        empireBtn.AddThemeColorOverride("font_hover_color", Colors.White);
        empireBtn.AddThemeColorOverride("font_pressed_color", empire.EmpireColor);

        // Load sci-fi button styles
        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        var pressedStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_pressed.tres");

        if (normalStyle != null) empireBtn.AddThemeStyleboxOverride("normal", normalStyle);
        if (hoverStyle != null) empireBtn.AddThemeStyleboxOverride("hover", hoverStyle);
        if (pressedStyle != null) empireBtn.AddThemeStyleboxOverride("pressed", pressedStyle);
        if (normalStyle != null) empireBtn.AddThemeStyleboxOverride("focus", normalStyle);

        Empire capturedEmpire = empire;
        empireBtn.Pressed += () => OnEmpireClicked(capturedEmpire);

        _empiresContainer.AddChild(empireBtn);
        _empireButtons[empire] = empireBtn;
    }

    /// <summary>
    /// Removes the Button associated with the given empire from the list.
    /// </summary>
    /// <param name="empire">The Empire to remove.</param>
    public void RemoveEmpire(Empire empire)
    {
        if (empire != null && _empireButtons.TryGetValue(empire, out Button btnToRemove))
        {
            btnToRemove.QueueFree();
            _empireButtons.Remove(empire);
        }
    }

    private void OnEmpireClicked(Empire empire)
    {
        if (empire != null && GodotObject.IsInstanceValid(empire))
        {
            EmpireInfoBox.OpenForEmpire(empire, this);
        }
    }

    /// <summary>
    /// Called when the TickManager emits the TickUpdateSignal.
    /// Updates the day label and empire star counts.
    /// </summary>
    /// <param name="tickCount">The current tick count from TickManager.</param>
    private void OnTickUpdate(int tickCount)
    {
        if (_dayLabel != null)
        {
            _dayLabel.Text = $"Day: {tickCount}";
        }

        // Update star count badges on each empire button
        foreach (var (empire, btn) in _empireButtons)
        {
            if (empire != null && GodotObject.IsInstanceValid(empire) && btn != null && GodotObject.IsInstanceValid(btn))
            {
                int starCount = empire.OwnedStars?.Count ?? 0;
                string name = empire.EmpireName ?? "Unnamed Empire";
                btn.Text = $"★ {name} [{starCount}]";
            }
        }
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

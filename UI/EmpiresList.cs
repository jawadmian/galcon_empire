using System;
using System.Collections.Generic; // Required for Dictionary
using Godot;

public partial class EmpiresList : Control
{
    // Path to the VBoxContainer node where empire labels will be added.
    // As per the request, this is "PanelContainer/VBoxContainer".
    private const string VBoxContainerPath = "PanelContainer/VBoxContainer";
    private const string DayLabelPath = "PanelContainer/VBoxContainer/DayLabel";

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

    private VBoxContainer _vboxContainer;
    private Label _dayLabel;
    private readonly Dictionary<Empire, Label> _empireLabels = new Dictionary<Empire, Label>();

    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning(
                $"Duplicate EmpiresList instance detected. Old: {_instance.GetPath()}, New: {GetPath()}. Destroying new instance."
            );
            QueueFree(); // Remove the duplicate
            return;
        }
        _instance = this;

        _vboxContainer = GetNodeOrNull<VBoxContainer>(VBoxContainerPath);
        if (_vboxContainer == null)
        {
            GD.PushError(
                $"EmpiresList: VBoxContainer not found at path '{VBoxContainerPath}'. UI will not function correctly."
            );
        }

        _dayLabel = GetNodeOrNull<Label>(DayLabelPath);
        if (_dayLabel == null)
        {
            GD.PushError(
                $"EmpiresList: DayLabel not found at path '{DayLabelPath}'. Day count will not be displayed."
            );
        }

        // Connect to the TickManager's signal
        if (TickManager.Instance != null)
        {
            TickManager.Instance.TickUpdateSignal += OnTickUpdate;
        }
        else
        {
            GD.PushWarning(
                "EmpiresList: TickManager.Instance is null. Cannot connect to TickUpdateSignal."
            );
        }
    }

    /// <summary>
    /// Adds a new Label representing the empire to the list.
    /// If a label for this empire already exists, it will be removed and a new one created.
    /// </summary>
    /// <param name="empire">The Empire to add.</param>
    public void AddEmpire(Empire empire)
    {
        if (_vboxContainer == null)
        {
            GD.PushWarning(
                "EmpiresList: VBoxContainer is not initialized. Cannot add empire label."
            );
            return;
        }
        if (empire == null)
        {
            GD.PushWarning("EmpiresList: Attempted to add a null empire.");
            return;
        }

        // If a label for this empire already exists, remove the old one first.
        if (_empireLabels.TryGetValue(empire, out Label existingLabel))
        {
            existingLabel.QueueFree();
            _empireLabels.Remove(empire);
        }

        Label empireLabel = new Label();
        empireLabel.Text = empire.EmpireName ?? "Unnamed Empire"; // Use empire's name
        empireLabel.SelfModulate = empire.EmpireColor; // Use empire's color

        _vboxContainer.AddChild(empireLabel);
        _empireLabels[empire] = empireLabel; // Store the reference for easy removal
    }

    /// <summary>
    /// Removes the Label associated with the given empire from the list.
    /// </summary>
    /// <param name="empire">The Empire to remove.</param>
    public void RemoveEmpire(Empire empire)
    {
        if (empire != null && _empireLabels.TryGetValue(empire, out Label labelToRemove))
        {
            labelToRemove.QueueFree();
            _empireLabels.Remove(empire);
        }
    }

    /// <summary>
    /// Called when the TickManager emits the TickUpdateSignal.
    /// Updates the day label with the current tick count.
    /// </summary>
    /// <param name="tickCount">The current tick count from TickManager.</param>
    private void OnTickUpdate(int tickCount)
    {
        if (_dayLabel != null)
        {
            _dayLabel.Text = $"Day: {tickCount}";
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

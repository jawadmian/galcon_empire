using System;
using Godot;
using Godot.Collections; // Required for Array<T>

public partial class Star : Node2D, IChronicleEntity
{
    // IChronicleEntity implementation
    public string ChronicleId => $"star:{StarName?.ToLowerInvariant().Replace(' ', '_') ?? Name}";
    public string ChronicleName => StarName ?? Name;
    public string ChronicleType => "star";
    public Vector2? WorldPosition => IsInsideTree() ? GlobalPosition : Position;

    private Label _myLabel;
    private string _starName = "Unnamed Star"; // Default name
    private Color _starNameColor = Colors.White; // Default color for the star's name label

    // Population and growth parameters
    private int _population = 0;

    // Production map: ResourceType -> production amount per tick
    public Dictionary<ResourceType, int> ProductionPerTick { get; private set; } =
        new Dictionary<ResourceType, int>();

    public int MaxPopulation { get; set; } = 100000;
    public float GrowthRate { get; set; } = 0.01f; // Represents a factor, e.g., 1.01 for specific growth behavior.

    // List of improvements on this star
    [Export]
    public Array<ImprovementResource> Improvements { get; set; } = new Array<ImprovementResource>();

    // Owning empire reference
    public Empire OwningEmpire { get; set; }

    // Public property to set the name label color
    public Color StarNameColor
    {
        get => _starNameColor;
        set
        {
            _starNameColor = value;
            if (_myLabel != null) // If _myLabel is already initialized, update its color
            {
                _myLabel.SelfModulate = _starNameColor;
            }
        }
    }

    // Public property to set the name, can be set from the spawner
    public string StarName
    {
        get => _starName;
        set
        {
            _starName = value;
            if (_myLabel != null) // If _myLabel is already initialized, update it
            {
                _myLabel.Text = _starName;
            }
        }
    }

    // Public property to set the name, can be set from the spawner
    public int StarPopulation
    {
        get => _population;
        set
        {
            // Clamp the incoming population value
            if (value > MaxPopulation)
            {
                _population = MaxPopulation;
            }
            else if (value < 0)
            {
                _population = 0;
            }
            else
            {
                _population = value;
            }
        }
    }

    // Public property to set the name label color
    public int StarFoodProduction
    {
        get
        {
            ProductionPerTick.TryGetValue(ResourceType.Food, out int value);
            return value;
        }
    }

    // Public property for Ore Production
    public int StarOreProduction
    {
        get
        {
            ProductionPerTick.TryGetValue(ResourceType.Ore, out int value);
            return value;
        }
    }

    // Public property for Money Production
    public int StarMoneyProduction
    {
        get
        {
            ProductionPerTick.TryGetValue(ResourceType.Money, out int value);
            return value;
        }
    }

    public void UpdateTick()
    {
        // Calculate the population increase based on the current population (_population),
        // growth rate, and delta time.
        int growthAmount = (int)(_population * GrowthRate);
        StarPopulation += growthAmount; // The StarPopulation setter will handle clamping
    }

    public override void _Ready()
    {
        _myLabel = GetNode<Label>("InfoVBox/NameLabel"); // Assuming the Label node is a direct child named "Label"

        if (_myLabel != null)
        {
            _myLabel.Text = _starName; // Set the text using the potentially updated StarName
            _myLabel.SelfModulate = _starNameColor; // Apply the initial or set color
        }
        else
        {
            GD.PushWarning("Child Label node 'Label' not found in Star instance!");
        }
    }

    public void AddImprovement(ImprovementResource improvement)
    {
        GD.Print($"Adding improvement to star: {improvement.ImprovementName}");
        Improvements.Add(improvement);

        // Recalculate star productions by resetting the map and summing up all improvements
        ProductionPerTick.Clear();

        foreach (ImprovementResource entry in Improvements)
        {
            foreach (var kvp in entry.ResourceOutput)
            {
                ResourceType type = kvp.Key;
                int amount = kvp.Value;

                if (ProductionPerTick.ContainsKey(type))
                {
                    ProductionPerTick[type] += amount;
                }
                else
                {
                    ProductionPerTick[type] = amount;
                }
            }
        }
    }

    public void _on_area_2d_input_event(Viewport viewport, InputEvent @event, int shape_idx)
    {
        // Check if the input event is a left mouse button press
        if (
            @event is InputEventMouseButton mouseButtonEvent
            && mouseButtonEvent.Pressed
            && mouseButtonEvent.ButtonIndex == MouseButton.Left
        )
        {
            StarInfoBox.OpenForStar(this, this);
            GD.Print($"Star {StarName} selected. Improvements: {Improvements.Count}.");
        }
    }
}

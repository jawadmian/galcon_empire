using System;
using Godot;
using Godot.Collections; // Required for Array<T>

public partial class Star : Node2D
{
    private Label _myLabel;
    private string _starName = "Unnamed Star"; // Default name
    private Color _starNameColor = Colors.White; // Default color for the star's name label

    // Population and growth parameters
    private int _population = 0;
    private int foodProductionPerTick = 0;
    private int oreProductionPerTick = 0;
    private int moneyProductionPerTick = 0;

    public int MaxPopulation { get; set; } = 100000;
    public float GrowthRate { get; set; } = 1.01f; // Represents a factor, e.g., 1.01 for specific growth behavior.

    // List of improvements on this star
    [Export]
    public Array<ImprovementResource> Improvements { get; set; } = new Array<ImprovementResource>();

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
        get => foodProductionPerTick;
    }

    // Public property for Ore Production
    public int StarOreProduction
    {
        get => oreProductionPerTick;
    }

    // Public property for Money Production
    public int StarMoneyProduction
    {
        get => moneyProductionPerTick;
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

        // Recalculate star productions
        foodProductionPerTick = 0;
        oreProductionPerTick = 0;
        moneyProductionPerTick = 0;
        foreach (ImprovementResource entry in Improvements)
        {
            if (entry.ResourceOutput.TryGetValue(ResourceType.Food, out int foodOutput))
            {
                foodProductionPerTick += foodOutput;
                GD.Print($"Food output {foodOutput}");
            }
            if (entry.ResourceOutput.TryGetValue(ResourceType.Ore, out int oreOutput))
            {
                GD.Print($"Ore output {oreOutput}");
                oreProductionPerTick += oreOutput;
            }
            if (entry.ResourceOutput.TryGetValue(ResourceType.Money, out int moneyOutput))
            {
                GD.Print($"Money output {moneyOutput}");
                moneyProductionPerTick += moneyOutput;
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
            // 1. Load the StarInfoPopup scene.
            //    Ensure "res://StarInfoPopup.tscn" is the correct path to your popup scene file.
            var starInfoPopupScene = GD.Load<PackedScene>("res://UI/star_info_box.tscn");
            if (starInfoPopupScene == null)
            {
                GD.PushError("Failed to load StarInfoPopup.tscn. Please check the path.");
                return;
            }

            // 2. Instantiate the StarInfoPopup scene.
            Node starInfoPopupInstance = starInfoPopupScene.Instantiate();
            if (starInfoPopupInstance == null)
            {
                GD.PushError("Failed to instantiate StarInfoPopup scene.");
                return;
            }

            // 3. Get the NameLabel node from the popup instance.
            //    This assumes "NameLabel" is a child of the StarInfoPopup's root node.
            //    If it's nested deeper, adjust the path accordingly (e.g., "Path/To/NameLabel").
            Label nameLabel = starInfoPopupInstance.GetNode<Label>(
                "PanelContainer/VBoxContainer/NameLabel"
            );
            if (nameLabel != null)
            {
                nameLabel.Text = _starName;
                nameLabel.SelfModulate = _starNameColor; // Set the color of the label
            }
            else
            {
                GD.PushWarning("Child Label node 'NameLabel' not found in StarInfoPopup instance.");
            }

            Label popLabel = starInfoPopupInstance.GetNode<Label>(
                "PanelContainer/VBoxContainer/PopInfoHBox/Label"
            );
            if (popLabel != null)
            {
                popLabel.Text = StarPopulation.ToString();
            }
            else
            {
                GD.PushWarning("Child Label node 'NameLabel' not found in StarInfoPopup instance.");
            }

            Label foodLabel = starInfoPopupInstance.GetNode<Label>(
                "PanelContainer/VBoxContainer/FoodInfoHBox/Label"
            );

            if (foodLabel != null)
            {
                foodLabel.Text = StarFoodProduction.ToString();
            }
            else
            {
                GD.PushWarning("Label node 'FoodLabel' not found in StarInfoPopup instance.");
            }

            // Hook up Ore Label
            Label oreLabel = starInfoPopupInstance.GetNode<Label>(
                "PanelContainer/VBoxContainer/OreInfoHBox/Label"
            );
            if (oreLabel != null)
            {
                oreLabel.Text = StarOreProduction.ToString();
            }
            else
            {
                GD.PushWarning(
                    "Label node 'OreInfoHBox/Label' not found in StarInfoPopup instance."
                );
            }

            // Hook up Money Label
            Label moneyLabel = starInfoPopupInstance.GetNode<Label>(
                "PanelContainer/VBoxContainer/MoneyInfoHBox/Label"
            );
            if (moneyLabel != null)
            {
                moneyLabel.Text = StarMoneyProduction.ToString();
            }
            else
            {
                GD.PushWarning(
                    "Label node 'MoneyInfoHBox/Label' not found in StarInfoPopup instance."
                );
            }

            // 4. Add the popup to the main scene tree to make it visible.
            //    This adds it to the root of the scene. You might want to add it to a specific UI layer if you have one.
            GetTree().Root.AddChild(starInfoPopupInstance);

            // Optional: If your StarInfoPopup is a Control node, you might want to set its position,
            // e.g., to center it on the screen or position it near the star.
            // Example for centering a Control node:
            if (starInfoPopupInstance is Control controlPopup)
            {
                GD.Print("star info is a Control node");
                // Center the popup on the star's global position
                controlPopup.GlobalPosition = this.GlobalPosition - controlPopup.Size / 2f;
            }
            else
            {
                GD.Print("star info is not a Control node");
            }
            GD.Print($"Star has {Improvements.Count} improvements.");
        }
    }
}

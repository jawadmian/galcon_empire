using Godot;
using Godot.Collections; // Required for Dictionary

// Define an enum for the resource types
public enum ResourceType
{
    Food,
    Ore,
    Money,
}

// This makes your custom resource type available in the Godot editor
[GlobalClass]
public partial class ImprovementResource : Resource
{
    // [Export] allows you to define and edit this list directly in the Godot Inspector
    [Export]
    public string ImprovementName { get; set; }

    [Export]
    public string ImprovementDescription { get; set; }

    // Use a Dictionary to store resource outputs
    [Export]
    public Dictionary<ResourceType, int> ResourceOutput { get; set; } =
        new Dictionary<ResourceType, int>();
}

using Godot;

// This makes your custom resource type available in the Godot editor
[GlobalClass]
public partial class StringArrayResource : Resource
{
    // [Export] allows you to define and edit this list directly in the Godot Inspector
    [Export]
    public Godot.Collections.Array<string> Items { get; set; } =
        new Godot.Collections.Array<string>();
}

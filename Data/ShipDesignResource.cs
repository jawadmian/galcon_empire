using Godot;
using Godot.Collections;

public enum ShipAbility
{
    Colonize,
    Stealth,
    AdvancedSensors,
    Repair,
    TroopTransport,
    Bombardment,
}

[Tool]
[GlobalClass]
public partial class ShipDesignResource : Resource
{
    [Export]
    public string DesignId { get; set; } = string.Empty;

    [Export]
    public string DesignName { get; set; } = "Unnamed Ship";

    [Export]
    public string Description { get; set; } = string.Empty;

    [ExportGroup("Stats")]
    [Export]
    public float Speed { get; set; } = 40.0f;

    [Export]
    public int MaxHitPoints { get; set; } = 100;

    [Export]
    public int AttackDamage { get; set; } = 0;

    [Export]
    public int DefenseRating { get; set; } = 0;

    [Export]
    public int CargoCapacity { get; set; } = 0;

    [Export]
    public float SensorRange { get; set; } = 150.0f;

    [ExportGroup("Production")]
    [Export]
    public int ProductionCost { get; set; } = 100;

    [Export]
    public Dictionary<ResourceType, int> ResourceCosts { get; set; } =
        new Dictionary<ResourceType, int>();

    [ExportGroup("Abilities & Tech")]
    [Export]
    public Array<ShipAbility> SpecialAbilities { get; set; } = new Array<ShipAbility>();

    [Export]
    public string RequiredTechId { get; set; } = string.Empty;

    [Export]
    public int RequiredTechLevel { get; set; } = 1;

    public bool HasAbility(ShipAbility ability)
    {
        return SpecialAbilities != null && SpecialAbilities.Contains(ability);
    }
}

using Godot;
using Godot.Collections;

public enum ShipSizeCategory
{
    Small,
    Medium,
    Large,
}

public enum ShipRoleCategory
{
    Combat,
    NonCombat,
}

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

    [Export]
    public ShipSizeCategory SizeCategory { get; set; } = ShipSizeCategory.Medium;

    [Export]
    public ShipRoleCategory RoleCategory { get; set; } = ShipRoleCategory.Combat;

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

    /// <summary>
    /// Ongoing per-tick upkeep rate as a fraction of <see cref="ResourceCosts"/> (default 5%).
    /// </summary>
    [Export]
    public float UpkeepRate { get; set; } = 0.05f;

    /// <summary>
    /// Optional explicit per-tick upkeep override. When empty, upkeep is <see cref="UpkeepRate"/> of <see cref="ResourceCosts"/>.
    /// </summary>
    [Export]
    public Dictionary<ResourceType, int> UpkeepCost { get; set; } =
        new Dictionary<ResourceType, int>();

    public System.Collections.Generic.Dictionary<ResourceType, int> GetUpkeepCost()
    {
        var result = new System.Collections.Generic.Dictionary<ResourceType, int>();
        if (UpkeepCost != null && UpkeepCost.Count > 0)
        {
            foreach (var kvp in UpkeepCost)
            {
                if (kvp.Key.IsStockpileable() && kvp.Value > 0)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }

        if (UpkeepRate <= 0f || ResourceCosts == null)
            return result;

        foreach (var kvp in ResourceCosts)
        {
            if (kvp.Key.IsStockpileable() && kvp.Value > 0)
            {
                result[kvp.Key] = Mathf.Max(1, Mathf.RoundToInt(kvp.Value * UpkeepRate));
            }
        }
        return result;
    }

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

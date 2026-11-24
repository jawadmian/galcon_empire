using Godot;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class ShipNameCollectionResource : Resource
{
    [Export]
    public string CollectionName { get; set; } = "Unnamed Collection";

    [ExportGroup("Combat Vessels")]
    [Export]
    public Array<string> SmallShipNames { get; set; } = new Array<string>();

    [Export]
    public Array<string> MediumShipNames { get; set; } = new Array<string>();

    [Export]
    public Array<string> LargeShipNames { get; set; } = new Array<string>();

    [ExportGroup("Non-Combat Vessels")]
    [Export]
    public Array<string> NonCombatSmallShipNames { get; set; } = new Array<string>();

    [Export]
    public Array<string> NonCombatMediumShipNames { get; set; } = new Array<string>();

    [Export]
    public Array<string> NonCombatLargeShipNames { get; set; } = new Array<string>();

    public Array<string> GetNamesForSize(ShipSizeCategory size)
    {
        return GetNamesFor(size, ShipRoleCategory.Combat);
    }

    public Array<string> GetNamesFor(ShipSizeCategory size, ShipRoleCategory role)
    {
        if (role == ShipRoleCategory.NonCombat)
        {
            Array<string> nonCombat = size switch
            {
                ShipSizeCategory.Small => NonCombatSmallShipNames,
                ShipSizeCategory.Medium => NonCombatMediumShipNames,
                ShipSizeCategory.Large => NonCombatLargeShipNames,
                _ => NonCombatMediumShipNames,
            };

            if (nonCombat != null && nonCombat.Count > 0)
                return nonCombat;

            if (NonCombatMediumShipNames != null && NonCombatMediumShipNames.Count > 0)
                return NonCombatMediumShipNames;
            if (NonCombatSmallShipNames != null && NonCombatSmallShipNames.Count > 0)
                return NonCombatSmallShipNames;
            if (NonCombatLargeShipNames != null && NonCombatLargeShipNames.Count > 0)
                return NonCombatLargeShipNames;
        }

        Array<string> combat = size switch
        {
            ShipSizeCategory.Small => SmallShipNames,
            ShipSizeCategory.Medium => MediumShipNames,
            ShipSizeCategory.Large => LargeShipNames,
            _ => MediumShipNames,
        };

        if (combat != null && combat.Count > 0)
            return combat;

        if (MediumShipNames != null && MediumShipNames.Count > 0)
            return MediumShipNames;
        if (SmallShipNames != null && SmallShipNames.Count > 0)
            return SmallShipNames;
        if (LargeShipNames != null && LargeShipNames.Count > 0)
            return LargeShipNames;

        return new Array<string>();
    }
}

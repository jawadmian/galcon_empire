using System;
using Godot;

/// <summary>
/// Runtime instance of a ship constructed from a ShipDesignResource blueprint.
/// </summary>
public class Ship : IChronicleEntity
{
    public string ShipId { get; }
    public string ShipName { get; set; }
    public string ClassName { get; set; }
    public ShipDesignResource Design { get; }
    public int CurrentHitPoints { get; set; }
    public int CommissionedTick { get; set; }
    public string BuiltAtStarName { get; set; } = string.Empty;
    public Fleet CurrentFleet { get; set; }
    public int LoadedColonists { get; set; } = 0;

    public int RequiredColonists =>
        HasAbility(ShipAbility.Colonize) ? (CargoCapacity > 0 ? CargoCapacity : 5000) : 0;

    // IChronicleEntity implementation
    public string ChronicleId => $"ship:{ShipId}";
    public string ChronicleName => !string.IsNullOrEmpty(ShipName) ? ShipName : (Design?.DesignName ?? "Ship");
    public string ChronicleType => "ship";
    public Vector2? WorldPosition =>
        CurrentFleet != null && GodotObject.IsInstanceValid(CurrentFleet)
            ? CurrentFleet.WorldPosition
            : null;

    public int MaxHitPoints => Design?.MaxHitPoints ?? 100;
    public float Speed => Design?.Speed ?? 0f;
    public int AttackDamage => Design?.AttackDamage ?? 0;
    public int DefenseRating => Design?.DefenseRating ?? 0;
    public int CargoCapacity => Design?.CargoCapacity ?? 0;
    public float SensorRange => Design?.SensorRange ?? 0f;
    public bool IsDestroyed => CurrentHitPoints <= 0;

    public Ship(ShipDesignResource design)
    {
        Design = design ?? throw new ArgumentNullException(nameof(design));
        ShipId = Guid.NewGuid().ToString("N")[..8];
        ShipName = design.DesignName;
        ClassName = design.DesignName;
        CurrentHitPoints = design.MaxHitPoints;
    }

    public bool HasAbility(ShipAbility ability)
    {
        return Design != null && Design.HasAbility(ability);
    }
}

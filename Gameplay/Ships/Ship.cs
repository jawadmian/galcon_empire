using System;
using Godot;

/// <summary>
/// Runtime instance of a ship constructed from a ShipDesignResource blueprint.
/// </summary>
public class Ship : IChronicleEntity
{
    public string ShipId { get; }
    public ShipDesignResource Design { get; }
    public int CurrentHitPoints { get; set; }
    public int CommissionedTick { get; set; }
    public string BuiltAtStarName { get; set; } = string.Empty;
    public Fleet CurrentFleet { get; set; }

    // IChronicleEntity implementation
    public string ChronicleId => $"ship:{ShipId}";
    public string ChronicleName => Design?.DesignName ?? "Ship";
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
        CurrentHitPoints = design.MaxHitPoints;
    }

    public bool HasAbility(ShipAbility ability)
    {
        return Design != null && Design.HasAbility(ability);
    }
}

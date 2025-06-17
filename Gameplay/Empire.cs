using System;
using System.Collections.Generic;
using Godot;

public partial class Empire : Node
{
    private Star _homeStar;
    public Star HomeStar
    {
        get => _homeStar;
        set
        {
            if (_homeStar == value)
                return;

            // If there was an old home star, it might need to be removed from owned stars
            // or handled based on game logic (e.g., if an empire can lose its capital)
            // For now, we just update and ensure the new one is in the list.
            _homeStar = value;
            if (_homeStar != null && !_ownedStars.Contains(_homeStar))
            {
                _ownedStars.Add(_homeStar);
                // Potentially assign this empire to the star as well
                // _homeStar.Owner = this;
            }
        }
    }
    public string EmpireName { get; set; }
    public Color EmpireColor { get; set; }

    private readonly List<Star> _ownedStars = new List<Star>();
    public IReadOnlyList<Star> OwnedStars => _ownedStars.AsReadOnly();

    // Stockpile of resources for the empire
    public Dictionary<ResourceType, long> ResourceStockpiles { get; private set; } =
        new Dictionary<ResourceType, long>();

    public override void _Ready()
    {
        base._Ready();
        if (HomeStar == null)
        {
            GD.PrintErr($"Empire '{EmpireName ?? Name}' initialized WITHOUT a Home Star!");
        }
        else if (!_ownedStars.Contains(HomeStar)) // Ensure HomeStar is in the list if set before _Ready
        {
            _ownedStars.Add(HomeStar);
        }

        // Initialize stockpiles
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            ResourceStockpiles[type] = 0; // Start with 0 of each resource
        }
    }

    public void UpdateTick(int tickCount)
    {
        // GD.Print($"Empire {EmpireName} updating for tick {tickCount}.");
        // - Perform other empire-level tick-based actions (e.g., AI decisions, unit production)

        // Temporary dictionary to hold this tick's total production before adding to stockpiles
        Dictionary<ResourceType, long> tickProduction = new Dictionary<ResourceType, long>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            tickProduction[type] = 0;
        }

        foreach (Star star in _ownedStars)
        {
            foreach (var productionEntry in star.ProductionPerTick)
            {
                tickProduction[productionEntry.Key] += productionEntry.Value;
            }
        }

        // Update empire's resource stockpiles
        foreach (var productionEntry in tickProduction)
        {
            ResourceStockpiles[productionEntry.Key] += productionEntry.Value;
            // GD.Print($"Empire {EmpireName} {productionEntry.Key} updated by {productionEntry.Value}, new total: {ResourceStockpiles[productionEntry.Key]}");
        }
    }
}

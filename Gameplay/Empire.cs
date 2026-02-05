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

    // Current production per tick for the empire
    public Dictionary<ResourceType, long> ProductionPerTick { get; private set; } =
        new Dictionary<ResourceType, long>();

    // Classes to manage the build system
    private class BuildRequest
    {
        public ImprovementResource Improvement { get; set; }
        public Star TargetStar { get; set; }
    }

    private class BuildQueueItem
    {
        public ImprovementResource Improvement { get; set; }
        public Star TargetStar { get; set; }
        public int RemainingTicks { get; set; }
    }

    private readonly List<BuildRequest> _buildRequests = new List<BuildRequest>();
    private readonly List<BuildQueueItem> _buildQueue = new List<BuildQueueItem>();
    private readonly List<IAdvisor> _advisors = new List<IAdvisor>();

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

        // Initialize stockpiles and production tracking
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            ResourceStockpiles[type] = 0; // Start with 0 of each resource
            ProductionPerTick[type] = 0;
        }

        // Register default advisors
        _advisors.Add(new BasicAdvisor());
    }

    /// <summary>
    /// Requests to build an improvement on a specific star.
    /// </summary>
    public void QueueImprovement(ImprovementResource improvement, Star star)
    {
        if (improvement == null || star == null)
            return;

        GD.Print($"Empire {EmpireName} requested {improvement.ImprovementName} at {star.StarName}.");
        _buildRequests.Add(new BuildRequest { Improvement = improvement, TargetStar = star });
    }

    private bool CanAfford(ImprovementResource improvement)
    {
        foreach (var cost in improvement.BuildCost)
        {
            if (
                !ResourceStockpiles.ContainsKey(cost.Key)
                || ResourceStockpiles[cost.Key] < cost.Value
            )
            {
                return false;
            }
        }
        return true;
    }

    private void StartConstruction(BuildRequest request)
    {
        // Deduct resources
        foreach (var cost in request.Improvement.BuildCost)
        {
            ResourceStockpiles[cost.Key] -= cost.Value;
        }

        GD.Print(
            $"Empire {EmpireName} started construction of {request.Improvement.ImprovementName} at {request.TargetStar.StarName}. Build time: {request.Improvement.BuildTime} ticks."
        );

        // Add to build queue
        _buildQueue.Add(
            new BuildQueueItem
            {
                Improvement = request.Improvement,
                TargetStar = request.TargetStar,
                RemainingTicks = request.Improvement.BuildTime,
            }
        );
    }

    public void UpdateTick(int tickCount)
    {
        // 1. Production Logic (Existing)
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

        foreach (var productionEntry in tickProduction)
        {
            ResourceStockpiles[productionEntry.Key] += productionEntry.Value;
            ProductionPerTick[productionEntry.Key] = productionEntry.Value;
            if (productionEntry.Value > 0)
            {
                GD.Print(
                    $"Empire {EmpireName} {productionEntry.Key} updated by {productionEntry.Value}, new total: {ResourceStockpiles[productionEntry.Key]}"
                );
            }
        }

        // 2. Advisor Recommendations
        foreach (IAdvisor advisor in _advisors)
        {
            advisor.Recommend(this);
        }

        // 3. Process Build Requests
        for (int i = _buildRequests.Count - 1; i >= 0; i--)
        {
            var request = _buildRequests[i];
            if (CanAfford(request.Improvement))
            {
                StartConstruction(request);
                _buildRequests.RemoveAt(i);
            }
        }

        // 3. Process Build Queue
        for (int i = _buildQueue.Count - 1; i >= 0; i--)
        {
            var item = _buildQueue[i];
            item.RemainingTicks--;

            if (item.RemainingTicks <= 0)
            {
                GD.Print(
                    $"Empire {EmpireName} completed construction of {item.Improvement.ImprovementName} at {item.TargetStar.StarName}."
                );
                item.TargetStar.AddImprovement(item.Improvement);
                _buildQueue.RemoveAt(i);
            }
        }
    }
}

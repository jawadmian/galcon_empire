using System;
using System.Collections.Generic;
using Godot;

public partial class Empire : Node, IChronicleEntity
{
    // IChronicleEntity implementation
    public string ChronicleId => $"empire:{EmpireName?.ToLowerInvariant().Replace(' ', '_') ?? Name}";
    public string ChronicleName => EmpireName ?? Name;
    public string ChronicleType => "empire";
    public Vector2? WorldPosition => HomeStar?.WorldPosition;

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
                _homeStar.OwningEmpire = this;
            }
        }
    }
    public string EmpireName { get; set; }
    public Color EmpireColor { get; set; }
    public ShipNameCollectionResource ShipNameCollection { get; set; }

    private readonly Dictionary<string, string> _designClassNames =
        new Dictionary<string, string>();
    private readonly Dictionary<string, int> _shipNameCounts = new Dictionary<string, int>();
    private readonly Dictionary<(ShipSizeCategory, ShipRoleCategory), int> _sizeRoleNameCursors =
        new Dictionary<(ShipSizeCategory, ShipRoleCategory), int>();
    private int _fleetCounter = 0;

    private readonly List<Star> _ownedStars = new List<Star>();
    public IReadOnlyList<Star> OwnedStars => _ownedStars.AsReadOnly();

    public void AddStar(Star star)
    {
        if (star != null && !_ownedStars.Contains(star))
        {
            _ownedStars.Add(star);
            star.OwningEmpire = this;
        }
    }

    public void RemoveStar(Star star)
    {
        if (star != null)
        {
            _ownedStars.Remove(star);
            if (star.OwningEmpire == this)
            {
                star.OwningEmpire = null;
            }
        }
    }

    // Stockpile of resources for the empire
    public Dictionary<ResourceType, long> ResourceStockpiles { get; private set; } =
        new Dictionary<ResourceType, long>();

    // Current production per tick for the empire
    public Dictionary<ResourceType, long> ProductionPerTick { get; private set; } =
        new Dictionary<ResourceType, long>();

    // Classes to manage the build system
    public class BuildRequest
    {
        public ImprovementResource Improvement { get; set; }
        public Star TargetStar { get; set; }
    }

    public class BuildQueueItem
    {
        public ImprovementResource Improvement { get; set; }
        public Star TargetStar { get; set; }
        public int RemainingTicks { get; set; }
    }

    public class ConstructionDisplayInfo
    {
        public ImprovementResource Improvement { get; set; }
        public Star TargetStar { get; set; }
        public int RemainingTicks { get; set; }
        public int TotalTicks { get; set; }
        public bool IsUnderConstruction { get; set; }
    }

    public class ShipBuildRequest
    {
        public ShipDesignResource Design { get; set; }
        public Star TargetStar { get; set; }
    }

    public class ShipBuildQueueItem
    {
        public ShipDesignResource Design { get; set; }
        public Star TargetStar { get; set; }
        public int AccumulatedProduction { get; set; }
        public int TotalProductionCost { get; set; }
    }

    private readonly List<BuildRequest> _buildRequests = new List<BuildRequest>();
    private readonly List<BuildQueueItem> _buildQueue = new List<BuildQueueItem>();
    private readonly List<ShipBuildRequest> _shipBuildRequests = new List<ShipBuildRequest>();
    private readonly List<ShipBuildQueueItem> _shipBuildQueue = new List<ShipBuildQueueItem>();
    private readonly List<Fleet> _fleets = new List<Fleet>();
    private readonly List<IAdvisor> _advisors = new List<IAdvisor>();

    public IReadOnlyList<BuildRequest> BuildRequests => _buildRequests.AsReadOnly();
    public IReadOnlyList<BuildQueueItem> BuildQueue => _buildQueue.AsReadOnly();
    public IReadOnlyList<ShipBuildRequest> ShipBuildRequests => _shipBuildRequests.AsReadOnly();
    public IReadOnlyList<ShipBuildQueueItem> ShipBuildQueue => _shipBuildQueue.AsReadOnly();
    public IReadOnlyList<Fleet> OwnedFleets => _fleets.AsReadOnly();

    /// <summary>
    /// Returns all active construction projects and pending build requests for UI display.
    /// </summary>
    public List<ConstructionDisplayInfo> GetActiveConstructionProjects()
    {
        var list = new List<ConstructionDisplayInfo>();
        foreach (var item in _buildQueue)
        {
            list.Add(new ConstructionDisplayInfo
            {
                Improvement = item.Improvement,
                TargetStar = item.TargetStar,
                RemainingTicks = item.RemainingTicks,
                TotalTicks = item.Improvement?.BuildTime ?? item.RemainingTicks,
                IsUnderConstruction = true
            });
        }
        foreach (var req in _buildRequests)
        {
            list.Add(new ConstructionDisplayInfo
            {
                Improvement = req.Improvement,
                TargetStar = req.TargetStar,
                RemainingTicks = req.Improvement?.BuildTime ?? 0,
                TotalTicks = req.Improvement?.BuildTime ?? 0,
                IsUnderConstruction = false
            });
        }
        return list;
    }

    public Empire()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            ResourceStockpiles[type] = 0;
            ProductionPerTick[type] = 0;
        }
    }

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
        _advisors.Add(new GoapAdvisor(this));
    }

    public int GetPendingImprovementsCount(Star star)
    {
        if (star == null)
            return 0;

        int count = 0;
        foreach (var req in _buildRequests)
        {
            if (req.TargetStar == star)
                count++;
        }
        foreach (var item in _buildQueue)
        {
            if (item.TargetStar == star)
                count++;
        }
        return count;
    }

    public bool HasAvailableBuildingSlot(Star star)
    {
        if (star == null)
            return false;

        return (star.Improvements.Count + GetPendingImprovementsCount(star)) < star.MaxImprovements;
    }

    /// <summary>
    /// Returns the owned star with the most remaining improvement slots, or null if all stars are full.
    /// </summary>
    public Star GetStarWithAvailableBuildingSlot()
    {
        Star bestStar = null;
        int mostFreeSlots = 0;

        foreach (var star in _ownedStars)
        {
            if (star == null)
                continue;

            int freeSlots = star.MaxImprovements - (star.Improvements.Count + GetPendingImprovementsCount(star));
            if (freeSlots > mostFreeSlots)
            {
                mostFreeSlots = freeSlots;
                bestStar = star;
            }
        }
        return bestStar;
    }

    /// <summary>
    /// Requests to build an improvement on a specific star.
    /// </summary>
    public void QueueImprovement(ImprovementResource improvement, Star star)
    {
        if (improvement == null || star == null)
            return;

        if (!HasAvailableBuildingSlot(star))
        {
            GD.Print($"Empire {EmpireName} cannot queue {improvement.ImprovementName} at {star.StarName}: building cap ({star.MaxImprovements}) reached.");
            return;
        }

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

        ChronicleEvent.Create(EventCategory.Economic, "Construction Commenced")
            .Involving(this, isPrimary: true)
            .Involving(request.TargetStar)
            .WithTemplate("{empire} commenced construction of [b]{improvement}[/b] on {star}.")
            .WithArg("improvement", request.Improvement.ImprovementName)
            .WithMetadata("build_time", request.Improvement.BuildTime)
            .Record();

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

    public bool IsImprovementPending(ImprovementResource improvement, Star star)
    {
        if (improvement == null || star == null)
            return false;

        // Check build requests
        foreach (var request in _buildRequests)
        {
            if (request.Improvement == improvement && request.TargetStar == star)
                return true;
        }

        // Check build queue
        foreach (var item in _buildQueue)
        {
            if (item.Improvement == improvement && item.TargetStar == star)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a shipyard improvement is queued or currently under construction.
    /// </summary>
    public bool HasShipyardPending()
    {
        foreach (var req in _buildRequests)
        {
            if (req.Improvement != null && req.Improvement.ResourceOutput.ContainsKey(ResourceType.ShipyardProduction))
                return true;
        }
        foreach (var item in _buildQueue)
        {
            if (item.Improvement != null && item.Improvement.ResourceOutput.ContainsKey(ResourceType.ShipyardProduction))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if this empire owns a completed shipyard, or has one pending construction.
    /// </summary>
    public bool HasShipyard()
    {
        foreach (var star in _ownedStars)
        {
            if (star.HasShipyard)
                return true;
        }
        return HasShipyardPending();
    }

    /// <summary>
    /// Returns the owned star with the highest local ShipyardProduction capacity, or null if none.
    /// </summary>
    public Star GetBestShipyardStar()
    {
        Star best = null;
        int bestSpp = 0;
        foreach (var star in _ownedStars)
        {
            if (star != null && star.StarShipyardProduction > bestSpp)
            {
                bestSpp = star.StarShipyardProduction;
                best = star;
            }
        }
        return best;
    }

    /// <summary>
    /// Requests to construct a ship at a star with a shipyard (auto-selects best shipyard if star is null).
    /// </summary>
    public bool QueueShip(ShipDesignResource design, Star star = null)
    {
        if (design == null)
            return false;

        Star target = star ?? GetBestShipyardStar();
        if (target == null || !target.HasShipyard)
            return false;

        GD.Print($"Empire {EmpireName} requested {design.DesignName} at {target.StarName}.");
        _shipBuildRequests.Add(new ShipBuildRequest { Design = design, TargetStar = target });
        return true;
    }

    public bool CanAffordShip(ShipDesignResource design)
    {
        if (design == null)
            return false;

        foreach (var cost in design.ResourceCosts)
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

    public bool HasShipPending(ShipAbility ability)
    {
        foreach (var req in _shipBuildRequests)
        {
            if (req.Design != null && req.Design.HasAbility(ability))
                return true;
        }
        foreach (var item in _shipBuildQueue)
        {
            if (item.Design != null && item.Design.HasAbility(ability))
                return true;
        }
        return false;
    }

    public void RegisterFleet(Fleet fleet)
    {
        if (fleet != null && !_fleets.Contains(fleet))
        {
            _fleets.Add(fleet);
        }
    }

    public void UnregisterFleet(Fleet fleet)
    {
        if (fleet != null)
        {
            _fleets.Remove(fleet);
        }
    }

    public List<Fleet> GetIdleFleetsWithAbility(ShipAbility ability)
    {
        var result = new List<Fleet>();
        for (int i = _fleets.Count - 1; i >= 0; i--)
        {
            var fleet = _fleets[i];
            if (fleet == null || !GodotObject.IsInstanceValid(fleet) || fleet.IsQueuedForDeletion() || fleet.IsEmpty)
            {
                _fleets.RemoveAt(i);
                continue;
            }
            if (fleet.IsIdle && fleet.HasAbility(ability))
            {
                result.Add(fleet);
            }
        }
        return result;
    }

    public bool HasFleetWithAbility(ShipAbility ability)
    {
        for (int i = _fleets.Count - 1; i >= 0; i--)
        {
            var fleet = _fleets[i];
            if (fleet == null || !GodotObject.IsInstanceValid(fleet) || fleet.IsQueuedForDeletion() || fleet.IsEmpty)
            {
                _fleets.RemoveAt(i);
                continue;
            }
            if (fleet.HasAbility(ability))
                return true;
        }
        return false;
    }

    private bool IsStarBuildingShip(Star star)
    {
        foreach (var item in _shipBuildQueue)
        {
            if (item.TargetStar == star)
                return true;
        }
        return false;
    }

    private void StartShipConstruction(ShipBuildRequest request)
    {
        foreach (var cost in request.Design.ResourceCosts)
        {
            ResourceStockpiles[cost.Key] -= cost.Value;
        }

        GD.Print(
            $"Empire {EmpireName} started constructing {request.Design.DesignName} at {request.TargetStar.StarName} (Cost: {request.Design.ProductionCost} SPP)."
        );

        _shipBuildQueue.Add(
            new ShipBuildQueueItem
            {
                Design = request.Design,
                TargetStar = request.TargetStar,
                AccumulatedProduction = 0,
                TotalProductionCost = Math.Max(1, request.Design.ProductionCost),
            }
        );
    }

    /// <summary>
    /// Assigns a unique ship name from the empire's ShipNameCollection and establishes
    /// the design's class name from the first commissioned ship of that design (lead-ship tradition).
    /// </summary>
    public void AssignShipAndClassName(Ship ship)
    {
        if (ship?.Design == null)
            return;

        ShipNameCollectionResource collection = ShipNameCollection;
        if (collection == null && FleetManager.HasInstance && FleetManager.Instance.AvailableShipNameCollections.Count > 0)
        {
            collection = FleetManager.Instance.AvailableShipNameCollections[0];
            ShipNameCollection = collection;
        }

        string baseName = null;
        if (collection != null)
        {
            var pool = collection.GetNamesFor(ship.Design.SizeCategory, ship.Design.RoleCategory);
            if (pool != null && pool.Count > 0)
            {
                var key = (ship.Design.SizeCategory, ship.Design.RoleCategory);
                _sizeRoleNameCursors.TryGetValue(key, out int cursor);
                baseName = pool[cursor % pool.Count];
                _sizeRoleNameCursors[key] = cursor + 1;
            }
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = ship.Design.DesignName ?? "Ship";
        }

        _shipNameCounts.TryGetValue(baseName, out int usedCount);
        usedCount++;
        _shipNameCounts[baseName] = usedCount;
        ship.ShipName = usedCount == 1 ? baseName : $"{baseName} {usedCount}";

        string designKey = !string.IsNullOrEmpty(ship.Design.DesignId)
            ? ship.Design.DesignId
            : ship.Design.DesignName;

        if (!_designClassNames.TryGetValue(designKey, out string className))
        {
            className = collection != null ? $"{baseName}-Class" : (ship.Design.DesignName ?? $"{baseName}-Class");
            _designClassNames[designKey] = className;
        }
        ship.ClassName = className;
    }

    /// <summary>
    /// Returns the next sequential ordinal fleet name for this empire (e.g. "1st Fleet", "2nd Fleet").
    /// </summary>
    public string GetNextFleetName()
    {
        _fleetCounter++;
        int mod100 = _fleetCounter % 100;
        string suffix = (mod100 >= 11 && mod100 <= 13)
            ? "th"
            : (_fleetCounter % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        return $"{_fleetCounter}{suffix} Fleet";
    }

    private Fleet SpawnCompletedShip(ShipDesignResource design, Star star)
    {
        var ship = new Ship(design)
        {
            CommissionedTick = TickManager.HasInstance ? TickManager.Instance.tickCount : 0,
            BuiltAtStarName = star?.StarName ?? "Unknown",
        };
        AssignShipAndClassName(ship);

        Fleet fleet;
        if (FleetManager.HasInstance)
        {
            fleet = FleetManager.Instance.SpawnFleet(this, star, ship);
        }
        else
        {
            fleet = new Fleet();
            fleet.Initialize(this, star, ship);
        }

        RegisterFleet(fleet);

        GD.Print(
            $"Empire {EmpireName} completed ship {ship.ShipName} ({ship.ClassName}) at {star.StarName}."
        );

        ChronicleEvent.Create(EventCategory.Military, "Ship Commissioned")
            .Involving(this, isPrimary: true)
            .Involving(star)
            .Involving(fleet)
            .Involving(ship)
            .WithTemplate("{empire} commissioned {ship} ({class}) into {fleet} at {star}.")
            .WithArg("class", ship.ClassName)
            .WithMetadata("ship_id", ship.ShipId)
            .WithMetadata("ship_class", ship.ClassName)
            .WithMetadata("design_id", design.DesignId)
            .Record();

        return fleet;
    }

    public void UpdateTick(int tickCount)
    {
        // 1. Production Logic
        Dictionary<ResourceType, long> tickProduction = new Dictionary<ResourceType, long>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            tickProduction[type] = 0;
        }

        foreach (Star star in _ownedStars)
        {
            // Population-based output: +1 Ore/Money per 100 pop, but population CONSUMES Food (-1 per 100 pop)
            // offset by the star's natural BaseFoodProduction.
            int popResources = star.StarPopulation / 100;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (!type.IsStockpileable())
                    continue;

                if (type == ResourceType.Food)
                {
                    tickProduction[ResourceType.Food] += star.BaseFoodProduction - star.FoodConsumptionPerTick;
                }
                else
                {
                    tickProduction[type] += popResources;
                }
            }

            foreach (var productionEntry in star.ProductionPerTick)
            {
                tickProduction[productionEntry.Key] += productionEntry.Value;
            }
        }

        foreach (var productionEntry in tickProduction)
        {
            if (productionEntry.Key.IsStockpileable())
            {
                ResourceStockpiles[productionEntry.Key] = Math.Max(
                    0,
                    ResourceStockpiles[productionEntry.Key] + productionEntry.Value
                );
            }
            ProductionPerTick[productionEntry.Key] = productionEntry.Value;
        }

        // 2. Advisor Recommendations
        foreach (IAdvisor advisor in _advisors)
        {
            advisor.Recommend(this);
        }

        // 3. Process Improvement Build Requests
        for (int i = _buildRequests.Count - 1; i >= 0; i--)
        {
            var request = _buildRequests[i];
            if (CanAfford(request.Improvement))
            {
                StartConstruction(request);
                _buildRequests.RemoveAt(i);
            }
        }

        // 4. Process Improvement Build Queue
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

                ChronicleEvent.Create(EventCategory.Economic, "Facility Completed")
                    .Involving(this, isPrimary: true)
                    .Involving(item.TargetStar)
                    .WithTemplate("{empire} completed construction of [b]{improvement}[/b] on {star}.")
                    .WithArg("improvement", item.Improvement.ImprovementName)
                    .Record();

                _buildQueue.RemoveAt(i);
            }
        }

        // 5. Process Ship Build Requests (1 active ship per star shipyard at a time)
        for (int i = _shipBuildRequests.Count - 1; i >= 0; i--)
        {
            var request = _shipBuildRequests[i];
            if (
                request.TargetStar != null
                && request.TargetStar.HasShipyard
                && !IsStarBuildingShip(request.TargetStar)
                && CanAffordShip(request.Design)
            )
            {
                StartShipConstruction(request);
                _shipBuildRequests.RemoveAt(i);
            }
        }

        // 6. Process Ship Build Queue (applies each star's local non-stockpileable SPP per tick)
        for (int i = _shipBuildQueue.Count - 1; i >= 0; i--)
        {
            var item = _shipBuildQueue[i];
            int localSpp = item.TargetStar?.StarShipyardProduction ?? 0;
            if (localSpp <= 0)
                continue;

            item.AccumulatedProduction += localSpp;
            if (item.AccumulatedProduction >= item.TotalProductionCost)
            {
                SpawnCompletedShip(item.Design, item.TargetStar);
                _shipBuildQueue.RemoveAt(i);
            }
        }
    }
}

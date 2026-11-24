using System.Collections.Generic;
using Godot;

/// <summary>
/// Global manager for ship design blueprints and active fleets across the galaxy.
/// Advances fleet movement on each simulation tick from TickManager.
/// </summary>
public partial class FleetManager : Node
{
    private static FleetManager _instance;
    public static bool HasInstance => _instance != null;
    public static FleetManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushError(
                    "FleetManager.Instance was accessed before it was ready or FleetManager is not in the scene tree. Ensure it's added as an Autoload."
                );
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Export(PropertyHint.Dir)]
    public string ShipDesignsFolderPath { get; set; } = "res://Data/Ships";

    [Export(PropertyHint.Dir)]
    public string ShipNameCollectionsFolderPath { get; set; } = "res://Data/ShipNames";

    private readonly List<ShipDesignResource> _shipDesigns = new List<ShipDesignResource>();
    public IReadOnlyList<ShipDesignResource> AvailableShipDesigns => _shipDesigns.AsReadOnly();

    private readonly List<ShipNameCollectionResource> _shipNameCollections =
        new List<ShipNameCollectionResource>();
    public IReadOnlyList<ShipNameCollectionResource> AvailableShipNameCollections =>
        _shipNameCollections.AsReadOnly();

    private readonly List<Fleet> _fleets = new List<Fleet>();
    public IReadOnlyList<Fleet> ActiveFleets => _fleets.AsReadOnly();

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning(
                $"Duplicate FleetManager instance detected. Old: {_instance.GetPath()}, New: {GetPath()}. Destroying new instance."
            );
            QueueFree();
            return;
        }
        _instance = this;
    }

    public override void _Ready()
    {
        if (_instance != this)
            return;

        LoadShipDesigns();
        LoadShipNameCollections();

        if (TickManager.HasInstance)
        {
            if (TickManager.Instance.IsNodeReady())
            {
                OnTickManagerReady();
            }
            else
            {
                TickManager.Instance.Ready += OnTickManagerReady;
            }
        }
    }

    public override void _ExitTree()
    {
        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal -= OnFleetManagerUpdateTick;
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnTickManagerReady()
    {
        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal += OnFleetManagerUpdateTick;
        }
    }

    private void OnFleetManagerUpdateTick(int tickCount)
    {
        UpdateTick();
    }

    public void UpdateTick()
    {
        var snapshot = new List<Fleet>(_fleets);
        foreach (Fleet fleet in snapshot)
        {
            if (fleet == null || !GodotObject.IsInstanceValid(fleet) || fleet.IsQueuedForDeletion())
            {
                _fleets.Remove(fleet);
                continue;
            }

            fleet.UpdateTick();

            if (fleet.IsEmpty || fleet.IsQueuedForDeletion())
            {
                _fleets.Remove(fleet);
            }
        }
    }

    public void LoadShipDesigns()
    {
        _shipDesigns.Clear();
        if (string.IsNullOrEmpty(ShipDesignsFolderPath))
        {
            return;
        }

        using var dir = DirAccess.Open(ShipDesignsFolderPath);
        if (dir == null)
        {
            GD.PushWarning(
                $"FleetManager: Could not open ship designs folder at '{ShipDesignsFolderPath}'. Error: {DirAccess.GetOpenError()}"
            );
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                string fullPath = $"{ShipDesignsFolderPath}/{fileName}";
                ShipDesignResource design = ResourceLoader.Load<ShipDesignResource>(fullPath);
                if (design != null)
                {
                    _shipDesigns.Add(design);
                }
                else
                {
                    GD.PushWarning($"FleetManager: Failed to load ship design at {fullPath}");
                }
            }
            fileName = dir.GetNext();
        }
        GD.Print($"FleetManager: Loaded {_shipDesigns.Count} ship design(s) from {ShipDesignsFolderPath}.");
    }

    public void LoadShipNameCollections()
    {
        _shipNameCollections.Clear();
        if (string.IsNullOrEmpty(ShipNameCollectionsFolderPath))
            return;

        using var dir = DirAccess.Open(ShipNameCollectionsFolderPath);
        if (dir == null)
        {
            GD.PushWarning(
                $"FleetManager: Could not open ship name collections folder at '{ShipNameCollectionsFolderPath}'. Error: {DirAccess.GetOpenError()}"
            );
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                string fullPath = $"{ShipNameCollectionsFolderPath}/{fileName}";
                var collection = ResourceLoader.Load<ShipNameCollectionResource>(fullPath);
                if (collection != null)
                {
                    _shipNameCollections.Add(collection);
                }
            }
            fileName = dir.GetNext();
        }
        GD.Print(
            $"FleetManager: Loaded {_shipNameCollections.Count} ship name collection(s) from {ShipNameCollectionsFolderPath}."
        );
    }

    public ShipDesignResource GetDesignById(string designId)
    {
        if (string.IsNullOrEmpty(designId))
            return null;

        foreach (var design in _shipDesigns)
        {
            if (design.DesignId == designId)
                return design;
        }
        return null;
    }

    public List<ShipDesignResource> GetDesignsWithAbility(ShipAbility ability)
    {
        var results = new List<ShipDesignResource>();
        foreach (var design in _shipDesigns)
        {
            if (design.HasAbility(ability))
            {
                results.Add(design);
            }
        }
        return results;
    }

    /// <summary>
    /// Spawns a new Fleet for the given empire at the specified star containing the initial ship.
    /// </summary>
    public Fleet SpawnFleet(Empire owner, Star star, Ship initialShip)
    {
        if (owner == null || star == null || initialShip == null)
            return null;

        var fleet = new Fleet();
        fleet.Initialize(owner, star, initialShip);
        RegisterFleet(fleet);

        if (IsInsideTree())
        {
            CallDeferred(Node.MethodName.AddChild, fleet);
        }

        return fleet;
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

    public List<Fleet> GetFleetsOrbitingStar(Star star)
    {
        var result = new List<Fleet>();
        if (star == null)
            return result;

        foreach (var fleet in _fleets)
        {
            if (
                fleet != null
                && GodotObject.IsInstanceValid(fleet)
                && !fleet.IsQueuedForDeletion()
                && !fleet.IsEmpty
                && fleet.State == FleetState.InOrbit
                && fleet.OrbitingStar == star
            )
            {
                result.Add(fleet);
            }
        }
        return result;
    }

    public List<Fleet> GetFleetsForEmpire(Empire empire)
    {
        var result = new List<Fleet>();
        if (empire == null)
            return result;

        foreach (var fleet in _fleets)
        {
            if (
                fleet != null
                && GodotObject.IsInstanceValid(fleet)
                && !fleet.IsQueuedForDeletion()
                && fleet.OwningEmpire == empire
                && !fleet.IsEmpty
            )
            {
                result.Add(fleet);
            }
        }
        return result;
    }

    public List<Fleet> GetIdleFleetsWithAbility(Empire empire, ShipAbility ability)
    {
        var result = new List<Fleet>();
        foreach (var fleet in GetFleetsForEmpire(empire))
        {
            if (fleet.IsIdle && fleet.HasAbility(ability))
            {
                result.Add(fleet);
            }
        }
        return result;
    }

    public bool HasFleetWithAbility(Empire empire, ShipAbility ability)
    {
        foreach (var fleet in GetFleetsForEmpire(empire))
        {
            if (fleet.HasAbility(ability))
                return true;
        }
        return false;
    }

    public bool HasFleetTargetingStar(Empire empire, Star star, FleetOrderType orderType)
    {
        if (empire == null || star == null)
            return false;

        foreach (var fleet in GetFleetsForEmpire(empire))
        {
            if (fleet.DestinationStar == star && fleet.CurrentOrder == orderType)
                return true;
        }
        return false;
    }
}

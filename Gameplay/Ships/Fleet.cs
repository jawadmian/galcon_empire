using System;
using System.Collections.Generic;
using Godot;

public enum FleetState
{
    InOrbit,
    InTransit,
}

public enum FleetOrderType
{
    None,
    MoveTo,
    Colonize,
}

/// <summary>
/// Represents an operational group of one or more ships on the galaxy map.
/// Moves in straight lines strictly on simulation ticks.
/// </summary>
public partial class Fleet : Node2D, IChronicleEntity
{
    public string FleetId { get; private set; } = Guid.NewGuid().ToString("N")[..8];
    public string FleetName { get; set; } = "Unnamed Fleet";

    // IChronicleEntity implementation
    public string ChronicleId => $"fleet:{FleetId}";
    public string ChronicleName => FleetName;
    public string ChronicleType => "fleet";
    public Vector2? WorldPosition => IsInsideTree() ? GlobalPosition : Position;

    public Empire OwningEmpire { get; set; }

    private readonly List<Ship> _ships = new List<Ship>();
    public IReadOnlyList<Ship> Ships => _ships.AsReadOnly();

    public FleetState State { get; private set; } = FleetState.InOrbit;
    public FleetOrderType CurrentOrder { get; private set; } = FleetOrderType.None;
    public Star OrbitingStar { get; private set; }
    public Star DestinationStar { get; private set; }

    private Area2D _clickArea;

    [Export]
    public float ArrivalThreshold { get; set; } = 5.0f;

    [Signal]
    public delegate void FleetArrivedSignalEventHandler(Fleet fleet, Star destination);

    [Signal]
    public delegate void StarColonizedSignalEventHandler(Fleet fleet, Star colonizedStar, Empire empire);

    public bool IsIdle => State == FleetState.InOrbit && CurrentOrder == FleetOrderType.None;
    public bool IsEmpty => _ships.Count == 0;

    /// <summary>
    /// Fleet speed in map units per tick, constrained by the slowest ship in the fleet.
    /// </summary>
    public float FleetSpeed
    {
        get
        {
            if (_ships.Count == 0)
                return 0f;

            float minSpeed = float.MaxValue;
            foreach (var ship in _ships)
            {
                if (ship.Speed < minSpeed)
                {
                    minSpeed = ship.Speed;
                }
            }
            return minSpeed == float.MaxValue ? 0f : minSpeed;
        }
    }

    public override void _Ready()
    {
        base._Ready();
        ZIndex = 10;
        EnsureClickArea();
    }

    private void EnsureClickArea()
    {
        if (_clickArea != null && GodotObject.IsInstanceValid(_clickArea))
            return;

        _clickArea = new Area2D
        {
            Name = "ClickArea",
            InputPickable = true,
        };
        var shape = new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Shape = new CircleShape2D { Radius = 16.0f },
        };
        _clickArea.AddChild(shape);
        _clickArea.InputEvent += OnArea2DInputEvent;
        AddChild(_clickArea);
    }

    private void OnArea2DInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (
            @event is InputEventMouseButton mouseButtonEvent
            && mouseButtonEvent.Pressed
            && mouseButtonEvent.ButtonIndex == MouseButton.Left
        )
        {
            FleetInfoBox.OpenForFleet(this, this);
        }
    }

    public void Initialize(Empire owner, Star startingStar, Ship initialShip = null)
    {
        OwningEmpire = owner;
        OrbitingStar = startingStar;
        State = FleetState.InOrbit;
        CurrentOrder = FleetOrderType.None;

        if (startingStar != null)
        {
            Position = startingStar.WorldPosition ?? startingStar.Position;
        }

        if (initialShip != null)
        {
            AddShip(initialShip);
            FleetName = $"{owner?.EmpireName ?? "Independent"} {initialShip.Design.DesignName}";
        }
        else
        {
            FleetName = $"{owner?.EmpireName ?? "Independent"} Fleet {FleetId}";
        }

        Name = $"Fleet_{FleetId}";
        QueueRedraw();
    }

    public void AddShip(Ship ship)
    {
        if (ship != null && !_ships.Contains(ship))
        {
            _ships.Add(ship);
            ship.CurrentFleet = this;
        }
    }

    public bool RemoveShip(Ship ship)
    {
        if (ship != null && _ships.Remove(ship))
        {
            if (ship.CurrentFleet == this)
                ship.CurrentFleet = null;
            return true;
        }
        return false;
    }

    public bool HasAbility(ShipAbility ability)
    {
        foreach (var ship in _ships)
        {
            if (ship.HasAbility(ability))
                return true;
        }
        return false;
    }

    public Ship GetShipWithAbility(ShipAbility ability)
    {
        foreach (var ship in _ships)
        {
            if (ship.HasAbility(ability))
                return ship;
        }
        return null;
    }

    /// <summary>
    /// Orders the fleet to travel in a straight line to targetStar.
    /// </summary>
    public void OrderMoveTo(Star targetStar)
    {
        IssueOrder(targetStar, FleetOrderType.MoveTo);
    }

    /// <summary>
    /// Orders the fleet to travel to targetStar and colonize it upon arrival.
    /// </summary>
    public void OrderColonize(Star targetStar)
    {
        IssueOrder(targetStar, FleetOrderType.Colonize);
    }

    private void IssueOrder(Star targetStar, FleetOrderType orderType)
    {
        if (targetStar == null || !GodotObject.IsInstanceValid(targetStar))
            return;

        DestinationStar = targetStar;
        CurrentOrder = orderType;

        if (OrbitingStar == targetStar)
        {
            ArriveAtDestination();
            return;
        }

        OrbitingStar = null;
        State = FleetState.InTransit;

        Vector2 targetPos = targetStar.WorldPosition ?? targetStar.Position;
        Vector2 toTarget = targetPos - Position;
        if (toTarget.LengthSquared() > 0.0001f)
        {
            Rotation = toTarget.Angle();
        }

        RecordFleetAndShipsEvent(
            EventCategory.Military,
            "Fleet Dispatched",
            "{fleet} departed for {star} (Order: {order}).",
            targetStar,
            ("order", orderType.ToString())
        );

        QueueRedraw();
    }

    /// <summary>
    /// Advances fleet movement by one simulation tick.
    /// </summary>
    public void UpdateTick()
    {
        if (State != FleetState.InTransit)
            return;

        if (DestinationStar == null || !GodotObject.IsInstanceValid(DestinationStar))
        {
            DestinationStar = null;
            CurrentOrder = FleetOrderType.None;
            State = FleetState.InOrbit;
            return;
        }

        // If heading to colonize a star that was claimed mid-flight, redirect immediately
        if (CurrentOrder == FleetOrderType.Colonize && DestinationStar.OwningEmpire != null)
        {
            GD.Print(
                $"Fleet '{FleetName}' detected {DestinationStar.StarName} was claimed mid-flight by {DestinationStar.OwningEmpire.EmpireName}."
            );
            if (!TryRedirectColonization())
            {
                DestinationStar = null;
                CurrentOrder = FleetOrderType.None;
                State = FleetState.InOrbit;
                return;
            }
        }

        float speed = FleetSpeed;
        if (speed <= 0f)
            return;

        Vector2 targetPos = DestinationStar.WorldPosition ?? DestinationStar.Position;
        Vector2 toTarget = targetPos - Position;
        float distance = toTarget.Length();

        if (distance <= speed || distance <= ArrivalThreshold)
        {
            Position = targetPos;
            ArriveAtDestination();
        }
        else
        {
            Position += toTarget.Normalized() * speed;
            Rotation = toTarget.Angle();
            QueueRedraw();
        }
    }

    private void ArriveAtDestination()
    {
        Star arrivedStar = DestinationStar;
        OrbitingStar = arrivedStar;
        DestinationStar = null;
        State = FleetState.InOrbit;

        FleetOrderType pendingOrder = CurrentOrder;
        CurrentOrder = FleetOrderType.None;

        EmitSignal(SignalName.FleetArrivedSignal, this, arrivedStar);

        RecordFleetAndShipsEvent(
            EventCategory.Military,
            "Fleet Arrived",
            "{fleet} arrived in orbit at {star}.",
            arrivedStar
        );

        if (pendingOrder == FleetOrderType.Colonize)
        {
            ColonizeCurrentStar();
        }

        QueueRedraw();
    }

    /// <summary>
    /// Consumes a ship with ShipAbility.Colonize to claim the currently orbited unowned star.
    /// Aborts gracefully if the star was already claimed by another empire.
    /// </summary>
    public bool ColonizeCurrentStar()
    {
        if (
            OwningEmpire == null
            || OrbitingStar == null
            || !GodotObject.IsInstanceValid(OrbitingStar)
        )
        {
            return false;
        }

        if (OrbitingStar.OwningEmpire != null)
        {
            GD.Print(
                $"Fleet '{FleetName}' aborted colonization of {OrbitingStar.StarName}: already owned by {OrbitingStar.OwningEmpire.EmpireName}."
            );
            RecordFleetAndShipsEvent(
                EventCategory.Political,
                "Colonization Aborted",
                "{fleet} aborted colonization of {star} (already claimed).",
                OrbitingStar
            );
            TryRedirectColonization();
            return false;
        }

        Ship colonyShip = GetShipWithAbility(ShipAbility.Colonize);
        if (colonyShip == null)
        {
            return false;
        }

        Star targetStar = OrbitingStar;
        _ships.Remove(colonyShip);
        colonyShip.CurrentFleet = null;

        OwningEmpire.AddStar(targetStar);
        targetStar.StarNameColor = OwningEmpire.EmpireColor;

        int initialPopulation = colonyShip.CargoCapacity > 0 ? colonyShip.CargoCapacity : 5000;
        if (targetStar.StarPopulation < initialPopulation)
        {
            targetStar.StarPopulation = initialPopulation;
        }

        GD.Print(
            $"Empire {OwningEmpire.EmpireName} established a new colony on {targetStar.StarName} (Pop: {targetStar.StarPopulation})."
        );

        ChronicleEvent.Create(EventCategory.Political, "New Colony Established")
            .Involving(OwningEmpire, isPrimary: true)
            .Involving(targetStar)
            .Involving(this)
            .Involving(colonyShip)
            .WithTemplate("{empire} established a new colony on {star} via {ship} ({fleet}).")
            .WithMetadata("initial_population", targetStar.StarPopulation)
            .Record();

        EmitSignal(SignalName.StarColonizedSignal, this, targetStar, OwningEmpire);

        if (_ships.Count == 0)
        {
            Disband();
        }

        return true;
    }

    /// <summary>
    /// Attempts to redirect a colony fleet to the nearest remaining unowned star.
    /// Returns true if a new colonization target was found and ordered.
    /// </summary>
    public bool TryRedirectColonization()
    {
        if (!HasAbility(ShipAbility.Colonize) || !StarManager.HasInstance)
            return false;

        Vector2 currentPos = WorldPosition ?? Position;
        Star nextTarget = StarManager.Instance.GetNearestUnownedStar(currentPos, OwningEmpire);
        if (nextTarget == null)
            return false;

        GD.Print($"Fleet '{FleetName}' redirecting colonization order to {nextTarget.StarName}.");
        OrderColonize(nextTarget);
        return true;
    }

    private void RecordFleetAndShipsEvent(
        EventCategory category,
        string title,
        string template,
        Star relatedStar,
        params (string key, string value)[] args
    )
    {
        var builder = ChronicleEvent.Create(category, title)
            .Involving(this, isPrimary: true);

        if (OwningEmpire != null)
            builder.Involving(OwningEmpire);

        if (relatedStar != null)
            builder.Involving(relatedStar);

        foreach (var ship in _ships)
        {
            builder.Involving(ship);
        }

        builder.WithTemplate(template);
        if (args != null)
        {
            foreach (var (key, value) in args)
            {
                builder.WithArg(key, value);
            }
        }
        builder.Record();
    }

    public void Disband()
    {
        OwningEmpire?.UnregisterFleet(this);
        if (FleetManager.HasInstance)
        {
            FleetManager.Instance.UnregisterFleet(this);
        }

        if (IsInsideTree() && !IsQueuedForDeletion())
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        Color color = OwningEmpire?.EmpireColor ?? Colors.White;
        // Draw a small directional chevron/triangle representing the fleet
        Vector2[] points =
        {
            new Vector2(10, 0),
            new Vector2(-7, -6),
            new Vector2(-4, 0),
            new Vector2(-7, 6),
        };
        DrawColoredPolygon(points, color);
        DrawPolyline(
            new[] { points[0], points[1], points[2], points[3], points[0] },
            Colors.White,
            1.2f,
            true
        );
    }
}

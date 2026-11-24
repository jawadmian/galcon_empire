# Subsystem: Ships, Fleets, Shipyards & Colonization

This document maps ship blueprints, per-star shipyard queues, tick-driven straight-line fleet traversal, and star colonization mechanics. For the high-level design roadmap and future combat/tech plans, see [Ship_Design.md](../../Ship_Design.md).

---

## 1. Key Files & Responsibilities

| File | Class / Asset | Role |
| :--- | :--- | :--- |
| [ShipDesignResource.cs](../../Data/ShipDesignResource.cs) | `ShipDesignResource` (`[Tool] [GlobalClass]`), `ShipAbility` | Data-driven ship blueprint resource: stats (`Speed`, `MaxHitPoints`, `AttackDamage`, `DefenseRating`, `CargoCapacity`, `SensorRange`), `ProductionCost` (SPP), `ResourceCosts`, strongly typed `Array<ShipAbility>`, and future tech hooks (`RequiredTechId`, `RequiredTechLevel`). |
| [ColonyShip.tres](../../Data/Ships/ColonyShip.tres) | `Ark-Class Colony Ship` | Initial colony ship blueprint (`colony_ship_v1`): Speed `40`/tick, HP `100`, Defense `5`, Cargo `5000`, Cost `150` SPP + `1,500` Ore + `500` Money, `SpecialAbilities = [ShipAbility.Colonize]`. |
| [Ship.cs](../../Gameplay/Ships/Ship.cs) | `Ship` (`IChronicleEntity`) | Lightweight pure-C# runtime ship instance holding `ShipId`, `Design`, `CurrentHitPoints`, `CommissionedTick`, `BuiltAtStarName`, `CurrentFleet`, stat accessors, and `HasAbility(ShipAbility)`. |
| [Fleet.cs](../../Gameplay/Ships/Fleet.cs) | `Fleet` (`Node2D`, `IChronicleEntity`), `FleetState`, `FleetOrderType` | Map entity grouping one or more `Ship`s. Executes tick-driven straight-line movement, programmatic directional chevron rendering (`_Draw()`), clickable `Area2D` opening `FleetInfoBox`, arrival signals, colonization, automatic redirection when targets are claimed, and Chronicle event recording for itself and its ships. |
| [FleetManager.cs](../../Gameplay/Managers/FleetManager.cs) | `FleetManager` (Autoload) | Loads all `ShipDesignResource` `.tres` assets from `res://Data/Ships`, tracks `ActiveFleets`, advances movement on `TickManager.TickUpdateSignal`, spawns fleets, and provides empire/ability/target queries. |
| [Empire.cs](../../Gameplay/Empire.cs) | `ShipBuildRequest`, `ShipBuildQueueItem` | Manages per-star ship build requests, resource deduction, local SPP progress, and `OwnedFleets` tracking. |

---

## 2. Ship Construction Flow (Per-Star Shipyard Queue)

1. **Queuing**: `empire.QueueShip(ShipDesignResource design, Star star = null)` selects `star` or `empire.GetBestShipyardStar()` (owned star with highest `StarShipyardProduction > 0`) and appends a `ShipBuildRequest`.
2. **Starting Construction** (in `Empire.UpdateTick()`):
   - Only **1 active ship per star shipyard** at a time (`!IsStarBuildingShip(targetStar)`).
   - Validates `CanAffordShip(design)` against `ResourceStockpiles` and deducts `design.ResourceCosts` upfront.
   - Moves item to `_shipBuildQueue` with `AccumulatedProduction = 0`, `TotalProductionCost = Max(1, design.ProductionCost)`.
3. **Advancing Per Tick**:
   - Adds `item.TargetStar.StarShipyardProduction` (local, non-stockpileable SPP) to `AccumulatedProduction`.
   - With a Tier 1 Orbital Shipyard (`25` SPP/tick), an Ark-Class Colony Ship (`150` SPP) completes in exactly `6` ticks.
4. **Spawning**:
   - `SpawnCompletedShip()` creates `new Ship(design)`, spawns a `Fleet` via `FleetManager.Instance.SpawnFleet(this, star, ship)` (with isolated fallback if `FleetManager.HasInstance == false`), registers it on `empire.OwnedFleets`, and records a `Military` Chronicle event (`"Ship Commissioned"`).

---

## 3. Fleet Movement & Colonization Flow

### Strongly Typed Abilities (`ShipAbility`)
`Colonize`, `Stealth`, `AdvancedSensors`, `Repair`, `TroopTransport`, `Bombardment`.

### Tick-Driven Straight-Line Traversal ([Fleet.cs](../../Gameplay/Ships/Fleet.cs))
- `FleetSpeed`: minimum `Speed` across all ships in `Fleet.Ships` (map units per tick).
- Orders: `OrderMoveTo(Star targetStar)` or `OrderColonize(Star targetStar)`.
- On each tick (`Fleet.UpdateTick()`, dispatched by `FleetManager`):
  - If `CurrentOrder == FleetOrderType.Colonize` and `DestinationStar.OwningEmpire != null` (claimed mid-flight by another empire), calls `TryRedirectColonization()` immediately to pick the next nearest unowned star (or returns to `InOrbit` idle if none remain).
  - Moves along `(targetPos - Position).Normalized() * FleetSpeed`.
  - When `distance <= FleetSpeed || distance <= ArrivalThreshold` (`5.0f`), snaps to `targetPos`, sets `State = FleetState.InOrbit`, clears `CurrentOrder`, emits `FleetArrivedSignal(fleet, arrivedStar)`, and runs `ColonizeCurrentStar()` if the order was `Colonize`.

### Colonization Execution (`Fleet.ColonizeCurrentStar()`)
1. If `OrbitingStar.OwningEmpire != null`: logs abort, calls `TryRedirectColonization()`, and returns `false` (colony ship is **not** consumed).
2. Otherwise:
   - Consumes the first ship with `ShipAbility.Colonize`.
   - Calls `OwningEmpire.AddStar(targetStar)` and sets `targetStar.StarNameColor = OwningEmpire.EmpireColor`.
   - Seeds initial population (`colonyShip.CargoCapacity`, default `5,000`).
   - Records a `Political` Chronicle event (`"New Colony Established"`).
   - Emits `StarColonizedSignal(fleet, targetStar, OwningEmpire)`.
   - If no ships remain in the fleet, calls `Disband()` (unregisters from `Empire` and `FleetManager` and `QueueFree()`s safely).

---

## 4. Common Extension Recipes

- **Add a new Ship Blueprint** (e.g., Scout Corvette, Frigate): Create a `.tres` of type `ShipDesignResource` in `res://Data/Ships/` via `godot-ai` MCP `resource_manage(op="create", ...)`. `FleetManager.LoadShipDesigns()` discovers it automatically.
- **Add a new Ship Ability**: Add a value to `enum ShipAbility` in [ShipDesignResource.cs](../../Data/ShipDesignResource.cs), then implement its runtime behavior in [Fleet.cs](../../Gameplay/Ships/Fleet.cs) or a future combat resolver.
- **Add Fleet Combat / Merging**: Extend `FleetOrderType` (e.g., `Attack`, `Guard`) and arrival handling in `Fleet.ArriveAtDestination()` and `FleetManager.UpdateTick()`.

## 5. Related Tests
- [ShipSystemTests.cs](../../Tests/ShipSystemTests.cs) — Covers non-stockpileable SPP, 6-tick shipyard build timing, slowest-ship fleet speed & straight-line movement, colonization success, mid-flight claim abort, and GOAP colonization plan chaining.

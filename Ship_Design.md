# GalconEmpire Spaceship & Fleet Design Document

## 1. Overview
This document specifies the architecture, data models, mechanics, and progression for spaceships and fleets in *GalconEmpire*. 

The spaceship system provides the backbone for Phase 2 ("Expansion & Fleets") and sets the foundation for Phase 3 ("Combat Planning"). In this phase, our primary objective is to enable **shipyards**, **ship construction**, **fleet movement**, and **colonization of unowned stars** using a basic **Colony Ship**.

---

## 2. Core Concepts & Terminology

1. **Ship Design (Blueprint / Archetype)**:
   - A data template (`ShipDesignResource`) defining the characteristics, required resources, production cost, stats, and special abilities of a ship class.
   - Designed to be data-driven (Godot `.tres` resource) and compatible with a future technology tree.

2. **Ship (Instance)**:
   - An individual ship instance in the game world. Contains current hit points, status effects, and a reference to its parent `ShipDesignResource`.

3. **Fleet**:
   - An operational unit grouping one or more ships under a single command.
   - Fleets exist in space as physical entities (`Node2D` on the galaxy map) or in orbit around a star.
   - Movement and combat occur at the **Fleet** level. Fleet movement speed is determined by the slowest ship in the fleet.

4. **Shipyard**:
   - A star improvement (or orbital facility) capable of generating **Ship Production Points (SPP)** each tick.
   - Shipyards manage a local construction queue and apply their production rate towards completing ships.

---

## 3. Ship Stats & Abilities

### 3.1. Core Stats
Every ship design is defined by a set of foundational stats:

| Stat | Type | Description |
| :--- | :--- | :--- |
| **Speed** | `float` | Movement speed across galactic coordinates (map units per game tick). |
| **MaxHitPoints** | `int` | Maximum structural health and armor. When HP reaches 0, the ship is destroyed. |
| **AttackDamage** | `int` | Offensive combat output per round/tick during tactical resolution (0 for non-combat ships). |
| **DefenseRating** | `int` | Damage mitigation or shield rating that reduces incoming damage. |
| **CargoCapacity** | `int` | Capacity for transporting resources, colonist population, or supplies. |
| **SensorRange** | `float` | Detection radius around the ship/fleet for vision and discovering undiscovered stars/fleets (future Fog of War pass). |
| **ProductionCost**| `int` | Total construction points (SPP) required to build the ship. Explicitly authored on `.tres` blueprints in Phase 2 (stat-weighted formula deferred to Ship Designer / Tech phase). |
| **ResourceCosts** | `Dictionary<ResourceType, int>` | Material resources (e.g., Ore, Money) deducted when construction begins. |

### 3.2. Special Abilities (Strongly Typed)
Ships can possess one or more strongly typed `ShipAbility` enum values:

```csharp
public enum ShipAbility
{
    Colonize,
    Stealth,
    AdvancedSensors,
    Repair,
    TroopTransport,
    Bombardment
}
```

- **Colonize**:
  - Allows the ship to deploy onto an uncolonized star, establishing an outpost/colony with initial population and claiming ownership for the empire. The ship is consumed in the process.
- **Stealth (Cloaking)**:
  - The fleet remains hidden from other empires' sensor grids unless scanned by an entity with **Advanced Sensors** within a close threshold.
- **Advanced Sensors**:
  - Extends sensor range significantly and counters stealth cloaking within scan distance.
- **Repair (Support)**:
  - Automatically restores hit points to damaged ships in the same fleet or orbit over time.
- **TroopTransport / Bombardment**:
  - Specialized capabilities for assaulting inhabited enemy stars (Phase 3).

---

## 4. Ship Design & Technology Progression

### 4.1. Starting Blueprints vs. Tech Unlocks
Empires start the game with a set of default starter blueprints:
- **Colony Ship**: Slow, unarmed, high cargo/population pod, ability `Colonize`.
- **Scout Corvette**: High speed, light armor, minimal attack damage, high sensor range.

### 4.2. Future Technology Integration
While the tech tree system will be implemented separately, the ship system is designed with explicit hooks:
```csharp
public partial class ShipDesignResource : Resource
{
    [Export] public string DesignId { get; set; }
    [Export] public string DesignName { get; set; }
    [Export] public string RequiredTechId { get; set; } = string.Empty;
    [Export] public int RequiredTechLevel { get; set; } = 1;
    [Export] public Godot.Collections.Array<ShipAbility> SpecialAbilities { get; set; } = new();
    ...
}
```
- When an empire researches a technology (e.g. `Tech_AdvancedEngines` or `Tech_HullReinforcement`), new ship designs unlock, and existing designs can be upgraded or retrofitted at shipyards.

### 4.3. Dynamic Production Cost Calculation
The production cost (SPP) can either be set explicitly on pre-authored resources or calculated dynamically from base component costs:
$$\text{ProductionCost} = \text{BaseCost} + (\text{HP} \times W_{hp}) + (\text{Speed} \times W_{spd}) + (\text{Damage} \times W_{atk}) + \sum \text{AbilityCosts}$$

This formula guarantees that high-performance or stealth ships inherently require more production time and resources to field.

---

## 5. Shipyard & Construction System

### 5.1. Shipyard Facility & Non-Stockpileable SPP
A **Shipyard** is a standard `ImprovementResource` built on a star (e.g., `Shipyard_1.tres`) that outputs `ResourceType.ShipyardProduction` in its `ResourceOutput` dictionary:
- Provides a **local per-star Shipyard Output (SPP per tick)**, for example:
  - *Tier 1 Orbital Shipyard*: 25 SPP / tick
  - *Tier 2 Shipyard*: 50 SPP / tick
  - *Tier 3 Naval Orbital Complex*: 100 SPP / tick
- **Non-Stockpileable Resource**: `ResourceType.ShipyardProduction` is a local per-tick capacity, **not** a stockpileable global currency. Idle shipyards do not bank unused SPP for future turns, and raw star population does not generate `ShipyardProduction`.

### 5.2. Construction Queue & Time
1. **Queuing**: An empire orders a ship design at a star that possesses `StarShipyardProduction > 0`.
2. **Resource Deduction**: Upfront material costs (`ResourceCosts`, e.g., Ore, Money) are validated against empire stockpiles and deducted upon starting construction.
3. **Turn Progress**:
   - Each tick, the star's local `StarShipyardProduction` (SPP) is applied to the active ship in that star's queue.
   - $\text{RemainingTicks} = \lceil \frac{\text{RemainingProductionCost}}{\text{StarShipyardProduction}} \rceil$.
4. **Completion & Spawning**:
   - When accumulated SPP $\ge$ `ProductionCost`, the ship is spawned via `FleetManager`.
   - The ship forms a new fleet (or merges into an orbiting fleet) at the star, ready to receive orders.

---

## 6. Fleet Architecture & Straight-Line Traversal

### 6.1. Galaxy Map Navigation
Space in *GalconEmpire* contains no prohibitive space terrain or blockades. Movement is straight-line point-to-point navigation:
- **Origin**: Current `Vector2` coordinates of the starting star or orbital point.
- **Target**: `Vector2` coordinates of the destination star.
- **Direction**: Normalized vector $\vec{D} = \frac{\vec{Target} - \vec{Current}}{\|\vec{Target} - \vec{Current}\|}$.
- **Speed**: $\text{Speed}_{fleet} = \min_{s \in Fleet}(\text{Speed}_s)$ (measured in map units per tick).

### 6.2. Tick-Driven Movement Simulation
Fleets operate in two primary states:
1. **In Orbit**: Stationed at a friendly or neutral star.
2. **In Transit**: Navigating through open space towards target coordinates.

Logical movement is **strictly tick-driven** (dispatched by `FleetManager` on `TickManager.TickUpdateSignal`) so simulation speed, pausing, and headless unit tests remain completely deterministic:
$$\vec{Pos}_{new} = \vec{Pos}_{current} + (\vec{D} \times \text{Speed}_{fleet})$$

When $\|\vec{Target} - \vec{Pos}\| \le \text{Speed}_{fleet}$ (or $\le \text{ArrivalThreshold}$), the fleet snaps to the target star and arrives:
- State transitions to `In Orbit` at the target star.
- An `Arrival` signal/event is emitted (notifying Chronicle and AI).
- Any queued arrival order (e.g., `Colonize`, `Guard`, `Attack`) is executed automatically.

*(Visual rendering may optionally interpolate smoothly between ticks in `_Process` without affecting logical tick state.)*

---

## 7. Phase 2 Focus: The Colony Ship & Colonization Flow

### 7.1. Basic Colony Ship Specifications
```
Design ID: colony_ship_v1
Name: Ark-Class Colony Ship
MaxHitPoints: 100
AttackDamage: 0
DefenseRating: 5
Speed: 40.0
CargoCapacity: 5000 (Colonists & Initial Building Material)
SensorRange: 150.0
ProductionCost: 150 SPP
ResourceCost: 1,500 Ore, 500 Money
SpecialAbilities: [ShipAbility.Colonize]
```

### 7.2. Colonization Sequence
1. **AI / Player Directive**: Target the nearest unowned star (full map visibility in Phase 2; Fog of War discovery deferred to a later phase).
2. **Dispatch**: Issue a `Colonize` order to an idle fleet containing a ship with `ShipAbility.Colonize` targeting `targetStar`.
3. **Transit**: Fleet navigates in a straight line to `targetStar`, advancing each game tick.
4. **Execution**:
   - Fleet reaches orbit of `targetStar`.
   - System confirms star is still unowned (`star.OwningEmpire == null`). If another empire claimed it mid-transit, the colonization order aborts gracefully and the fleet remains idle in orbit.
   - Colony Ship executes `Colonize`:
     - Ship is consumed.
     - `empire.AddStar(targetStar)` claims ownership and updates star visuals/color.
     - Initial population established (e.g., `CargoCapacity` / 5,000 colonists).
     - Chronicle event recorded: *"{empire} has established a new colony on {star}."*
     - If the fleet has no remaining ships, the fleet node is cleanly removed.

---

## 8. AI & GOAP Integration (Phase 2 Roadmap)

To make the AI capable of natural expansion, Phase 2 integrates the ship system into our GOAP architecture:

1. **Goals**:
   - `BuildShipyardGoal`: Prioritizes constructing a shipyard once basic economy is active (already implemented).
   - `ColonizeStarGoal`: Evaluates whether uncolonized stars exist and drives the empire to expand its owned star count.

2. **GOAP Actions**:
   - `BuildImprovementAction` *(existing generic action)*: Handles building shipyards (and all other improvements) dynamically via `ResourceType.ShipyardProduction`. No dedicated `Action_BuildShipyard` is needed.
   - `BuildColonyShipAction`:
     - *Preconditions*: Has star with shipyard (`HasShipyard` / `Production_ShipyardProduction > 0`), sufficient material resources (`ResourceCosts`), no idle/pending colony ship already available.
     - *Effects*: `HasColonyShip = true` (fire-and-forget queue command to the empire's shipyard).
   - `ColonizeAction`:
     - *Preconditions*: `HasIdleColonyShip == true` (or `HasColonyShip == true` during planning), `HasUnownedStars == true`.
     - *Effects*: `OwnedStarsCount + 1`, `HasColonyShip = false`.
     - *Execution*: Dispatches an idle colony ship fleet to the nearest unowned star with a `Colonize` arrival order; the fleet handles multi-tick transit and colonization autonomously.

---

## 9. Next Steps for Implementation
1. **Data Models**:
   - Implement `ShipDesignResource.cs` as Godot `Resource`.
   - Create initial resource definitions (`ColonyShip.tres`, `Shipyard_1.tres`).
2. **Game Logic**:
   - Implement `Ship.cs` (runtime ship instance).
   - Implement `Fleet.cs` (Node2D, handles movement, ship inventory, arrival signals).
   - Implement `FleetManager.cs` to track all active fleets across empires.
   - Update `Star.cs` and `Empire.cs` to manage shipyard queues and SPP.
3. **Visual & Input**:
   - Add simple visual sprite representation for fleets traversing between stars.
4. **AI Expansion**:
   - Implement `ColonizeStarGoal` and supporting GOAP actions.

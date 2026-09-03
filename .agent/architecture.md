# Technical Architecture Reference

This document details the architectural patterns, simulation flow, manager lifecycles, and data contracts of **Galcon Empire**.

---

## 1. Simulation Pipeline & The Tick Loop

The simulation is **tick-driven**, decoupled from real-time rendering delta. This enables flexible speed adjustments, deterministic calculations, and easy simulation pausing.

```
+-------------------------------------------------------------+
|                 TickManager (Autoload)                      |
|  - Tracks real-time delta until TickUpdateInterval reached   |
|  - Increments tickCount                                     |
|  - Calls StarManager.Instance.UpdateTick()                  |
|  - Emits TickUpdateSignal(tickCount)                        |
+------------------------------+------------------------------+
                               |
            +------------------+------------------+
            |                                     |
            v                                     v
+-----------------------+             +-----------------------+
|     StarManager       |             |     EmpireManager     |
| - Loops all Stars     |             | - Listens to tick     |
| - Updates population  |             | - Progresses build    |
| - Sums improvement    |             |   queues              |
|   resource output     |             | - Triggers advisor AI |
|                       |             | - Updates stockpiles  |
+-----------------------+             +-----------------------+
```

### Execution Flow per Tick:
1. **Clock Advance**: `TickManager` accumulates `delta`. When `_tickUpdateTimer >= TickUpdateInterval`, `tickCount` increments.
2. **Star System Update**: `StarManager.Instance.UpdateTick()` iterates through registered `Star` instances:
   - Evaluates population dynamics.
   - Calculates resource yields based on active `ImprovementResource` items attached to each star.
3. **Empire Orchestration**:
   - `Empire` advances its active construction: decrements remaining build time for queued `ImprovementResource` items.
   - Upon construction completion, the improvement is added to the target `Star`.
   - Resource stockpiles are credited with star production.
   - Advisor AI evaluates recommendations for idle build queues.

---

## 2. Manager Roles & Singletons

All managers inherit from `Node` and utilize singleton instances with defensive initialization. We want to use a singleton pattern to access the managers from anywhere in the codebase without passing references around. Managers should be declared as Autoloads so that they are always available.

### `TickManager` (Autoload)
- **Path**: `res://Gameplay/Managers/tick_manager.tscn`
- **Role**: Global heart rate of the game. Dispatches `TickUpdateSignal`.
- **Properties**:
  - `TickUpdateInterval` (default: 10.0s, adjustable for simulation acceleration).
  - `tickCount`: Total elapsed simulation ticks.

### `EmpireManager`
- **Path**: `res://Gameplay/Managers/empire_manager.tscn`
- **Role**:
  - Manages the collection of all spawned `Empire` nodes.
  - Loads improvement resources dynamically from `res://Data/Improvements`.
  - Exposes `AvailableImprovements` catalog (`IReadOnlyList<ImprovementResource>`).

### `StarManager`
- **Path**: `res://Gameplay/Managers/star_manager.tscn`
- **Role**:
  - Registry for all `Star` nodes in the galaxy.
  - Facilitates spatial queries (nearest stars, star ownership).
  - Drives per-tick star state updates.

---

## 3. Core Entities

### `Empire` (`res://Gameplay/Empire.cs`)
- **State**:
  - `EmpireName`: Unique string identifier.
  - `HomeStar`: Initial star system assigned at generation.
  - `ControlledStars`: List of stars owned by this empire.
  - `ResourceStockpiles`: `Dictionary<ResourceType, int>` storing `Food`, `Ore`, and `Money`.
  - `ActiveBuildQueue`: Tracks the `ImprovementResource` currently being built, target star, and remaining ticks.
  - `Advisor`: Implements `IAdvisor` (e.g. `BasicAdvisor`) for automated economic decision-making.

### `Star` (`res://Gameplay/Star.cs`)
- **State**:
  - `StarName`: System name.
  - `Population`: Current population count.
  - `Improvements`: List of installed `ImprovementResource` instances.
  - `Position`: Spatial coordinate in the galaxy.

---

## 4. Data-Driven Resource System

Gameplay content is defined via custom Godot `Resource` classes in `res://Data/`:

### `ImprovementResource` (`res://Data/ImprovementResource.cs`)
- `ImprovementName` (`string`)
- `ImprovementDescription` (`string`)
- `BuildTime` (`int` in ticks)
- `ResourceOutput` (`Dictionary<ResourceType, int>`): Output produced per tick.
- `BuildCost` (`Dictionary<ResourceType, int>`): Cost deducted upon queuing construction.

### `ResourceType` Enum
- `Food`: Sustains and grows population.
- `Ore`: Required for physical construction and industry.
- `Money`: Economic maintenance, trade, and diplomacy.

---

## 5. Architectural Invariants & Agent Guidelines

1. **Loose Coupling via Signals**: Systems should never directly mutate other systems without clear boundaries. Prefer emitting signals when state changes.
2. **Defensive Singleton Handling**: When writing or testing unit logic, always assume singletons might be null or unattached to the tree.
3. **Immutability of Data Definitions**: Never mutate `ImprovementResource` fields at runtime; resources are shared templates. State belonging to an entity (e.g., remaining build time) belongs on the `Empire` or `Star`, not in the resource asset.
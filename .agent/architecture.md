# Technical Architecture Reference

This document provides the cross-cutting architectural overview of **Galcon Empire**: the simulation tick pipeline, Autoload vs. scene managers, and system-wide invariants. For file-level details of any specific area, see the dedicated docs under `.agent/subsystems/`.

---

## 1. Simulation Pipeline & The Tick Loop

The simulation is **strictly tick-driven**, decoupled from real-time rendering `delta`. This enables flexible simulation speeds, pausing, and deterministic headless tests.

```
+-----------------------------------------------------------------------+
|                       TickManager (Autoload)                          |
|  - Accumulates delta until TickUpdateInterval reached                 |
|  - Increments tickCount                                               |
|  - Calls StarManager.Instance.UpdateTick()                            |
|  - Emits TickUpdateSignal(tickCount)                                  |
+-------+-------------------+-------------------+-------------------+---+
        |                   |                   |                   |
        v                   v                   v                   v
+---------------+   +---------------+   +---------------+   +---------------+
| EmpireManager |   |   AIManager   |   | FleetManager  |   |   UI Panels   |
| - Economy &   |   |  (Autoload)   |   |  (Autoload)   |   | - EmpiresList |
|   food/pop    |   | - Time-slices |   | - Advances    |   | - EmpireInfo  |
| - Building &  |   |   GoapAdvisor |   |   fleets per  |   | - StarInfo    |
|   ship queues |   |   updates in  |   |   tick & runs |   |   live values |
| - Sensor sync |   |   _Process()  |   |   colonize    |   |               |
+---------------+   +---------------+   +---------------+   +---------------+
```

---

## 2. Global Autoloads vs. Scene Managers

| Manager | Registration | Path | Primary Responsibility |
| :--- | :--- | :--- | :--- |
| `TickManager` | Autoload | `res://Gameplay/Managers/tick_manager.tscn` | Simulation clock and `TickUpdateSignal` publisher. |
| `AIManager` | Autoload | `res://Gameplay/Managers/AIManager.cs` | Spreads `GoapAdvisor.UpdateAI()` calls across frames to prevent CPU spikes. |
| `ChronicleManager` | Autoload | `res://Gameplay/Managers/ChronicleManager.cs` | Thread-safe SQLite historical event store (`res://saves/chronicle.db`). |
| `FleetManager` | Autoload | `res://Gameplay/Managers/FleetManager.cs` | Loads `ShipDesignResource` blueprints from `res://Data/Ships` and drives tick-based fleet movement. |
| `StarManager` | Scene (`galaxy_spawner.tscn`) | `res://Gameplay/Managers/star_manager.tscn` | Procedural star placement, spatial/unowned star queries, star tick propagation. |
| `EmpireManager` | Scene (`galaxy_spawner.tscn`) | `res://Gameplay/Managers/empire_manager.tscn` | Loads `ImprovementResource` blueprints from `res://Data/Improvements`, spawns empires, and forwards ticks. |

---

## 3. Subsystem Reference Map

Deep-dive documentation is split by subsystem:
- [Simulation, Galaxy, Stars & Economy](subsystems/simulation_and_economy.md)
- [Ships, Fleets, Shipyards & Colonization](subsystems/ships_and_fleets.md)
- [Pluggable AI & GOAP Planning](subsystems/ai_and_goap.md)
- [Chronicle Event History & SQLite Persistence](subsystems/chronicle_and_persistence.md)
- [UI Panels, HUD, Camera & Space Visuals](subsystems/ui_and_camera.md)

---

## 4. Architectural Invariants

1. **Loose Coupling via Signals**: Prefer Godot signals (`TickUpdateSignal`, `FleetArrivedSignal`, `StarColonizedSignal`, `EventRecorded`) over direct cross-system mutation.
2. **Defensive Singleton Handling**: Always guard singleton access with `HasInstance` (or null checks) so entities remain testable in isolation without booting the full scene tree.
3. **Immutability of Data Definitions**: Never mutate `ImprovementResource` or `ShipDesignResource` properties at runtime; resources are shared immutable templates.
4. **Stockpileable vs. Capacity Resources**: Always consult `ResourceTypeExtensions.IsStockpileable()` rather than assuming all `ResourceType` values accumulate globally.

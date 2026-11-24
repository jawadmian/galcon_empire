# Subsystem: Core Simulation, Galaxy, Stars & Economy

This document maps the core tick loop, galaxy generation, star systems, empire state, resources, population mechanics, and planetary improvement construction.

---

## 1. Key Files & Responsibilities

| File | Class / Asset | Role |
| :--- | :--- | :--- |
| [TickManager.cs](../../Gameplay/Managers/TickManager.cs) | `TickManager` (Autoload) | Global simulation clock where each tick represents 1 elapsed month (`int tickCount`). Default `TickUpdateInterval` is `1.0s` (`1x`, with `0.25x`/`4x` multipliers and `IsPaused`). Calls `StarManager.Instance.UpdateTick()` and emits `TickUpdateSignal(int tickCount)`. Provides static calendar formatters `GetYear()`, `GetMonth()`, `FormatDate()` (`"Year X, Month Y"`), and `FormatShortDate()` (`"YX MY"`). |
| [StarManager.cs](../../Gameplay/Managers/StarManager.cs) | `StarManager` | Scene manager in `galaxy_spawner.tscn`. Spawns well-spaced `Star` nodes (`MinStarDistanceSquared = 90000`, `SpawnAreaMin = (-400, -300)`, `SpawnAreaMax = (2800, 1600)`) with random `StarType`, random `SystemSize` (`3..12`), and names from `StarNames.tres`. Provides `GetSpawnedStars()`, `GetUnownedStars()`, `GetNearestUnownedStar()`, and `GetBestUnownedStarToColonize()` (weighted by system size and distance). |
| [EmpireManager.cs](../../Gameplay/Managers/EmpireManager.cs) | `EmpireManager` | Scene manager in `galaxy_spawner.tscn`. Loads all `ImprovementResource` `.tres` files from `res://Data/Improvements`, spawns `Empire` nodes, assigns each a `HomeStar` (`SystemSize = 10` -> 10 building slots & 200,000 max pop, 5,000 starting pop, 1 random starter improvement), and forwards `TickUpdateSignal` to every empire's `UpdateTick()`. |
| [GalaxySpawner.cs](../../Gameplay/GalaxySpawner.cs) | `GalaxySpawner` | Root scene (`res://Gameplay/galaxy_spawner.tscn`) hosting `StarManager`, `EmpireManager`, `CameraController`, `space_background.tscn`, and HUD `CanvasLayer`. |
| [Star.cs](../../Gameplay/Star.cs) | `Star` (`Node2D`, `IChronicleEntity`) | Star system entity: `StarType`, `StarColor`, `SystemSize` (`3..12`, set via `SetSystemSize()`), `StarPopulation`, `MaxPopulation` (`SystemSize * 20,000`), `GrowthRate` (`0.01f`), `BaseFoodProduction` (`100`), `MaxImprovements` (`SystemSize`), installed `Improvements`, and `ProductionPerTick`. |
| [Empire.cs](../../Gameplay/Empire.cs) | `Empire` (`Node`, `IChronicleEntity`) | Empire entity: `HomeStar`, `OwnedStars`, `ResourceStockpiles`, `ProductionPerTick`, improvement build requests/queue, ship build requests/queue, `OwnedFleets`, and registered `IAdvisor` list. |
| [ImprovementResource.cs](../../Data/ImprovementResource.cs) | `ImprovementResource`, `ResourceType`, `ResourceTypeExtensions` | Data-driven building blueprint (`BuildTime`, `ResourceOutput`, `BuildCost`) and resource type definitions. |
| [StringArrayResource.cs](../../Data/StringArrayResource.cs) | `StringArrayResource` | String list template used by `StarNames.tres` and `EmpireNames.tres`. |

---

## 2. Tick Execution Order

When `TickManager` advances a tick:
1. `StarManager.Instance.UpdateTick()` -> calls `star.UpdateTick()` on every star (updates population based on net food balance).
2. `TickManager` emits `TickUpdateSignal(tickCount)` to all subscribers:
   - **`EmpireManager.OnEmpireManagerUpdateTick`** -> calls `empire.UpdateTick(tickCount)` for each empire:
     1. Aggregates per-star economy (population outputs/consumption + improvement `ProductionPerTick`) into `Empire.ProductionPerTick` and `ResourceStockpiles` (only for `IsStockpileable()` resources).
     2. Calls `advisor.Recommend(this)` (updates AI sensors).
     3. Starts affordable improvement `_buildRequests` (deducting `BuildCost` upfront) and advances `_buildQueue` (`RemainingTicks--`, installing completed improvements via `star.AddImprovement()`).
     4. Starts affordable `_shipBuildRequests` (1 active ship per star shipyard) and advances `_shipBuildQueue` by each star's local `StarShipyardProduction`.
   - **`AIManager.OnTickUpdate`** -> queues registered `GoapAdvisor`s for time-sliced `UpdateAI()` execution in `_Process()`.
   - **`FleetManager.OnFleetManagerUpdateTick`** -> advances all active `Fleet`s by one tick of straight-line movement.
   - **UI Panels** (`EmpiresList`, `EmpireInfoBox`, `StarInfoBox`) -> refresh displayed day counts and live metrics.

---

## 3. Resource & Economy Rules

### `ResourceType` Enum ([ImprovementResource.cs](../../Data/ImprovementResource.cs))
| Value | Enum Int | Stockpileable? | Population Effect (per 100 pop) | Primary Role |
| :--- | :---: | :---: | :--- | :--- |
| `Food` | `0` | Yes | **Consumes `-1`** (offset by `Star.BaseFoodProduction`) | Sustains population and enables growth above baseline |
| `Ore` | `1` | Yes | **Produces `+1`** | Primary material for farms, shipyards, and ships |
| `Money` | `2` | Yes | **Produces `+1`** | Currency for mines, shipyards, and ships |
| `ShipyardProduction` | `3` | **No** | `0` (improvements only) | Local per-star SPP/tick applied to ship construction; idle SPP is not banked |

*`ResourceTypeExtensions.IsStockpileable()` is the single source of truth distinguishing stockpileable economy resources from local/capacity resources.*

### Food, Base Production & Population Growth ([Star.cs](../../Gameplay/Star.cs))
- `BaseFoodProduction` (default `100`): natural biosphere output sustaining a baseline population of `10,000` (`BaseFoodProduction * 100`) without any farms.
- `FoodConsumptionPerTick`: `_population / 100`.
- `NetFoodProductionPerTick`: `BaseFoodProduction + StarFoodProduction - FoodConsumptionPerTick`.
- Population growth in `Star.UpdateTick()`:
  - `netFood > 0`: grows by `Max(1, (int)(_population * GrowthRate))` (clamped at `MaxPopulation`).
  - `netFood == 0`: holds steady at carrying capacity.
  - `netFood < 0`: shrinks toward `BaseFoodProduction * 100` (never starves below baseline).
- In `Empire.UpdateTick()`: `ResourceStockpiles[ResourceType.Food]` is clamped at `>= 0`; `ProductionPerTick[ResourceType.Food]` retains the signed net balance so UI and AI can detect deficits.

### Per-Star Building Cap
- `Star.MaxImprovements` (default `6`) limits total improvements per star.
- `Empire.HasAvailableBuildingSlot(star)` checks `(star.Improvements.Count + GetPendingImprovementsCount(star)) < star.MaxImprovements`.
- `Empire.QueueImprovement(improvement, star)` enforces the cap and refuses requests on full stars.
- `Empire.GetStarWithAvailableBuildingSlot()` returns the owned star with the most free slots so expansion worlds are developed automatically.

### Active Improvement Blueprints (`res://Data/Improvements/`)
| Asset | BuildTime | BuildCost | ResourceOutput |
| :--- | :---: | :--- | :--- |
| `Farm_1.tres` ("Farm") | 2 | 2,000 Ore | +500 Food |
| `Mine_1.tres` ("Basic Mine") | 3 | 1,000 Money | +2,000 Ore |
| `Market_1.tres` ("Basic Market") | 2 | 2,000 Food | +2,000 Money |
| `Shipyard_1.tres` ("Orbital Shipyard") | 3 | 1,000 Ore, 500 Money | +25 ShipyardProduction |

---

## 4. Common Extension Recipes

- **Add a new Improvement**: Create a `.tres` of type `ImprovementResource` in `res://Data/Improvements/` via `godot-ai` MCP `resource_manage(op="create", ...)`. `EmpireManager` and `GoapAdvisor` discover it automatically.
- **Add a new ResourceType** (e.g., `Research`, `Culture`): Add to `enum ResourceType` in [ImprovementResource.cs](../../Data/ImprovementResource.cs), update `ResourceTypeExtensions.IsStockpileable()` if non-stockpileable, and check population output logic in [Empire.cs](../../Gameplay/Empire.cs).
- **Tune Star capacities or growth**: Adjust `[Export]` defaults `BaseFoodProduction`, `MaxImprovements`, `MaxPopulation`, or `GrowthRate` on [Star.cs](../../Gameplay/Star.cs).

## 5. Related Tests
- [EmpireTests.cs](../../Tests/EmpireTests.cs) — Stockpile initialization, production summation, build affordability/timing, food consumption, and pending tracking.
- [ShipSystemTests.cs](../../Tests/ShipSystemTests.cs) — Non-stockpileable `ShipyardProduction`, building cap & spillover, and base food / population dynamics.

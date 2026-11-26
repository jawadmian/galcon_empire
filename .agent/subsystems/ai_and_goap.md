# Subsystem: Pluggable AI & GOAP Planning

This document maps the AI advisor abstraction, time-sliced update manager, async A* GOAP planner, thread-safe Blackboard state, and all active Goals and Actions. For high-level architecture and multi-tier AI roadmap (System Governors, Military Commanders, Combat Planning), see [AI_Design.md](../../AI_Design.md).

---

## 1. Key Files & Responsibilities

| File | Class / Interface | Role |
| :--- | :--- | :--- |
| [IAdvisor.cs](../../Gameplay/IAdvisor.cs) | `IAdvisor` | Pluggable AI boundary (`void Recommend(Empire empire)`). Allows swapping GOAP for HTN or Utility AI without modifying `Empire`. |
| [BasicAdvisor.cs](../../Gameplay/BasicAdvisor.cs) | `BasicAdvisor` | Legacy simple fallback advisor (retained for reference; `Empire._Ready()` registers `GoapAdvisor` by default). |
| [AIManager.cs](../../Gameplay/Managers/AIManager.cs) | `AIManager` (Autoload) | Time-slices AI execution across frames. On `TickManager.TickUpdateSignal`, queues a pass over all registered `GoapAdvisor`s without resetting the round-robin cursor, dynamically scaling the per-frame budget (`AdvisorsToUpdatePerFrame` floor) with advisor count and `SpeedMultiplier` so `8x`/`16x` and large galaxies (50+ empires) never starve later advisors. |
| [GoapAdvisor.cs](../../Gameplay/AI/GoapAdvisor.cs) | `GoapAdvisor` | Main AI controller per empire. Populates `AvailableActions` and `Goals` dynamically from `EmpireManager` and `FleetManager`, updates Blackboard sensors on `Recommend()`, runs async planning via `Task.Run`, and executes `_currentPlan` one action per AI update. |
| [Blackboard.cs](../../Gameplay/AI/GOAP/Blackboard.cs) | `Blackboard` | Thread-safe world-state container (`ConcurrentDictionary<string, object>`). Supports `GetValue<T>`, `SetValue<T>`, `Clone()`, and order-independent XOR `GetHashCode()` + `Equals()` for A* closed-set deduplication. |
| [GoapPlanner.cs](../../Gameplay/AI/GOAP/GoapPlanner.cs) | `GoapPlanner`, `GoapNode` | Forward-search Dijkstra/A* planner (`iterationLimit = 1000`, `H = 0`) that returns `Queue<GoapAction>` achieving `goal.IsAchieved(state)`. |
| [GoapAction.cs](../../Gameplay/AI/GOAP/GoapAction.cs) | `GoapAction` | Abstract action base (`Cost`, `CheckPreconditions`, `ApplyEffects`, `Perform`). |
| [GoapGoal.cs](../../Gameplay/AI/GOAP/GoapGoal.cs) | `GoapGoal` | Abstract goal base (`Priority`, `IsValid`, `IsAchieved`, `UpdatePriority`). |

---

## 2. Blackboard Schema (Sensors Updated in `GoapAdvisor.UpdateSensors()`)

| Key | Type | Source / Meaning |
| :--- | :--- | :--- |
| `Stockpile_<ResourceType>` | `long` | `Empire.ResourceStockpiles[type]` (e.g., `Stockpile_Ore`, `Stockpile_Money`, `Stockpile_Food`) |
| `Production_<ResourceType>` | `long` | `Empire.ProductionPerTick[type]` (note: `Production_Food` is signed net food balance) |
| `OwnedStarsCount` | `int` | `Empire.OwnedStars.Count` |
| `AvailableBuildingSlots` | `int` | Sum of `MaxImprovements - (Improvements.Count + Pending)` across all owned stars |
| `HasShipyard` | `bool` | `Empire.HasShipyard()` (completed on any star OR pending in build queue) |
| `ShipyardProduction` | `int` | Total local `StarShipyardProduction` across all owned stars |
| `HasColonyShip` | `bool` | Empire owns a fleet with `ShipAbility.Colonize` OR has one pending/building |
| `HasIdleColonyShip` | `bool` | Empire has at least one idle fleet with `ShipAbility.Colonize` ready to dispatch |
| `HasUnownedStars` | `bool` | `StarManager` has at least one unowned star not already targeted by this empire |

---

## 3. Active Goals & Priority System

In `GoapAdvisor.FormulatePlanAsync()`, all valid unachieved goals with `Priority > 0` are sorted by **priority descending** and tried in order until one yields a valid plan (so an unaffordable high-priority goal gracefully falls back to actionable economic goals).

| Goal | File | Valid When | Achieved When | Priority Logic |
| :--- | :--- | :--- | :--- | :--- |
| `ColonizeStarGoal` | [ColonizeStarGoal.cs](../../Gameplay/AI/Goals/ColonizeStarGoal.cs) | `OwnedStarsCount > 0 && HasUnownedStars` | `OwnedStarsCount >= current + 1` | `3.5` if `HasIdleColonyShip`; `2.0` if `HasShipyard && !HasColonyShip`; `0.2` while waiting for ship/shipyard; `0` if no unowned stars |
| `BuildShipyardGoal` | [BuildShipyardGoal.cs](../../Gameplay/AI/Goals/BuildShipyardGoal.cs) | `OwnedStarsCount > 0` | `HasShipyard \|\| Production_ShipyardProduction > 0` | `0` if achieved; `2.5` once `Production_Ore > 0 && Production_Money > 0`; `0.5` otherwise |
| `GrowResourceGoal` | [GrowResourceGoal.cs](../../Gameplay/AI/Goals/GrowResourceGoal.cs) | `AvailableBuildingSlots > 0` (or `HasScrappableImprovement` for critical `Food <= 0`) | `Production_<Type> >= _targetProduction` | Registered for every `IsStockpileable()` resource. **Food**: `3.0` if `<= 0`, `1.4` if `< 200`, `0.4` if surplus. **Ore/Money**: smooth inverse curve `1.0 + 1.5 / (1 + prod / 1000)` so lagging resource always wins |
| `ReduceDeficitGoal` | [ReduceDeficitGoal.cs](../../Gameplay/AI/Goals/ReduceDeficitGoal.cs) | Any `Production_<Type> < 0` with `TotalUpkeep_<Type> > 0` & scrappable assets | All `Production_<Type> >= 0` (or no upkeep) | `3.2` when stockpile `< 3 * abs(netProd)`; `0.8` when buffered; `0` otherwise |

---

## 4. Active Actions

| Action | File | Cost | Preconditions | Planning Effects | Runtime `Perform()` |
| :--- | :--- | :---: | :--- | :--- | :--- |
| `BuildImprovementAction` | [BuildImprovementAction.cs](../../Gameplay/AI/Actions/BuildImprovementAction.cs) | `BuildTime` (`2–3`) | `OwnedStarsCount > 0`, `AvailableBuildingSlots > 0`, stockpiles cover `BuildCost` | Deducts costs, adds `ResourceOutput`, sets `HasShipyard = true` if outputting `ShipyardProduction`, decrements `AvailableBuildingSlots` | Calls `empire.QueueImprovement()` on `GetStarWithAvailableBuildingSlot()` |
| `BuildColonyShipAction` | [BuildColonyShipAction.cs](../../Gameplay/AI/Actions/BuildColonyShipAction.cs) | `2.0` | `HasShipyard`, `!HasColonyShip`, stockpiles cover `design.ResourceCosts` | Deducts costs, sets `HasColonyShip = true` and `HasIdleColonyShip = true` | Fire-and-forget `empire.QueueShip(_design)` |
| `ColonizeAction` | [ColonizeAction.cs](../../Gameplay/AI/Actions/ColonizeAction.cs) | `2.0` | `HasIdleColonyShip && HasUnownedStars` | `HasColonyShip = false`, `HasIdleColonyShip = false`, `OwnedStarsCount + 1` | Dispatches idle colony fleet to nearest unowned star via `fleet.OrderColonize()` |
| `ScrapShipAction` | [ScrapShipAction.cs](../../Gameplay/AI/Actions/ScrapShipAction.cs) | `3.0` | `HasScrappableShip && ShipCount > 0` | Adds `ScrappableShipUpkeep_*` back to `Production_*`, decrements `ShipCount` | Calls `empire.ScrapShip(empire.GetMostExpensiveScrapCandidateShip())` |
| `ScrapImprovementAction` | [ScrapImprovementAction.cs](../../Gameplay/AI/Actions/ScrapImprovementAction.cs) | `12.0` | `HasScrappableImprovement && InstalledImprovementsCount > 0` | Adjusts `Production_*` by `Upkeep - Output`, increments `AvailableBuildingSlots` | Calls `empire.ScrapImprovement()` on `GetMostExpensiveScrapCandidateImprovement(preferNonShipyard: freeingSlot)` |
| `WaitAction` *(tests only)* | [WaitAction.cs](../../Gameplay/AI/Actions/WaitAction.cs) | `1.0` | Always true | Adds `Production_*` to `Stockpile_*` | **Not registered in `GoapAdvisor`** — omitted from live AI to prevent A* state-space explosion; used only in unit tests |

---

## 5. Key Design Invariants & Pitfalls
1. **No `WaitAction` in live `GoapAdvisor`**: Simulating discrete wait steps with passive income explodes the Dijkstra state space (every wait creates a unique `Blackboard` hash). Let game ticks accumulate resources natively and fail fast.
2. **Keep action `Cost` values on a comparable scale (`1–5`)**: Because `GoapPlanner` currently runs Dijkstra (`H = 0`) with `iterationLimit = 1000`, high costs (e.g., `15–20`) get buried behind thousands of combinations of cheap `2–3` cost actions.
3. **Clear `_currentPlan` on failed `Perform()`**: `GoapAdvisor.UpdateAI()` resets `_currentPlan = null` whenever `Perform()` returns `false` or preconditions fail, preventing stalls.

---

## 6. Common Extension Recipes
- **Add a new GOAP Goal**: Create a subclass of `GoapGoal` in `Gameplay/AI/Goals/`, register it in `GoapAdvisor`'s constructor, and ensure any new Blackboard keys it inspects are populated in `GoapAdvisor.UpdateSensors()`.
- **Add a new GOAP Action**: Create a subclass of `GoapAction` in `Gameplay/AI/Actions/`, register it in `GoapAdvisor`'s constructor (prefer data-driven instantiation from `EmpireManager`/`FleetManager`), keep `Cost` normalized (`~2–5`), and add a planner test.
- **Swap the AI Paradigm**: Implement `IAdvisor` (e.g., `HtnAdvisor`) and assign it in `Empire._Ready()`.

## 7. Related Tests
- [BlackboardTests.cs](../../Tests/BlackboardTests.cs) — Set/get, deep clone, and order-independent hash/equality.
- [GoapPlannerTests.cs](../../Tests/GoapPlannerTests.cs) — Economic expansion and shipyard planning.
- [ShipSystemTests.cs](../../Tests/ShipSystemTests.cs) — Colonization plan chaining with all improvements in the action pool.

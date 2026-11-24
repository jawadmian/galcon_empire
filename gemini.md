# Galcon Empire — Agent Guidelines & System Directive

## 📌 Project Overview
**Galcon Empire** is a **data-driven 4X space empire simulation game** built in **Godot 4.7.1** using **C# (.NET 8)**.
The player acts as an incorporeal galactic observer and shaper, watching and influencing the rise and fall of automated star empires.

---

## 🧭 Subsystem Routing Index (Start Here!)

> [!TIP]
> **On a fresh session or restart, consult this table first** and read the relevant subsystem doc before scanning the codebase. Each subsystem doc maps exact files, data contracts, invariants, extension recipes, and test suites.

| If you are working on... | Read Subsystem Doc First | Primary Code / Data Locations | Test Suites |
| :--- | :--- | :--- | :--- |
| **Tick loop, Galaxy gen, Stars, Empires, Resources, Food/Pop, Building caps** | [simulation_and_economy.md](.agent/subsystems/simulation_and_economy.md) | `Gameplay/Managers/{Tick,Star,Empire}Manager.cs`, `Gameplay/{Star,Empire}.cs`, `Data/ImprovementResource.cs`, `Data/Improvements/` | [EmpireTests.cs](Tests/EmpireTests.cs), [ShipSystemTests.cs](Tests/ShipSystemTests.cs) |
| **Ships, Ship blueprints, Shipyards, Fleets, Space movement, Colonization** | [ships_and_fleets.md](.agent/subsystems/ships_and_fleets.md) *(Roadmap: [Ship_Design.md](Ship_Design.md))* | `Data/ShipDesignResource.cs`, `Data/Ships/`, `Gameplay/Ships/{Ship,Fleet}.cs`, `Gameplay/Managers/FleetManager.cs`, `Gameplay/Empire.cs` | [ShipSystemTests.cs](Tests/ShipSystemTests.cs), [FleetAndShipInfoBoxTest.cs](Tests/FleetAndShipInfoBoxTest.cs) |
| **GOAP AI, Goals, Actions, Blackboard, AIManager, Pluggable Advisors** | [ai_and_goap.md](.agent/subsystems/ai_and_goap.md) *(Roadmap: [AI_Design.md](AI_Design.md))* | `Gameplay/Managers/AIManager.cs`, `Gameplay/AI/GoapAdvisor.cs`, `Gameplay/AI/{GOAP,Goals,Actions}/`, `Gameplay/IAdvisor.cs` | [BlackboardTests.cs](Tests/BlackboardTests.cs), [GoapPlannerTests.cs](Tests/GoapPlannerTests.cs), [ShipSystemTests.cs](Tests/ShipSystemTests.cs) |
| **Chronicle history log, SQLite DB, Entity hyperlinks, Event categories** | [chronicle_and_persistence.md](.agent/subsystems/chronicle_and_persistence.md) | `Modules/Chronicle/` (`ChronicleManager.cs`, `ChronicleEvent.cs`, `ChronicleRecord.cs`, `EventCategory.cs`, `IChronicleEntity.cs`) | [ChronicleManagerTest.cs](Tests/ChronicleManagerTest.cs) |
| **UI panels, HUD, NinePatchRect styling, Camera, Space background** | [ui_and_camera.md](.agent/subsystems/ui_and_camera.md) *(Style Rules: [UI/agents.md](UI/agents.md))* | `UI/`, `Gameplay/CameraController.cs`, `Gameplay/space_background.tscn`, `Images/` | [StarInfoBoxTest.cs](Tests/StarInfoBoxTest.cs), [EmpireInfoBoxTest.cs](Tests/EmpireInfoBoxTest.cs), [FleetAndShipInfoBoxTest.cs](Tests/FleetAndShipInfoBoxTest.cs), [CameraControllerTest.cs](Tests/CameraControllerTest.cs), [SpaceBackgroundTest.cs](Tests/SpaceBackgroundTest.cs) |
| **Writing or running tests (GdUnit4)** | [testing_guide.md](.agent/testing_guide.md) | `Tests/`, `addons/gdUnit4/` | All 10 suites under `res://Tests/` |

---

## 🏛️ Core Architectural Principles
1. **Data-Driven Design**: All gameplay definitions (improvements, ship designs, future technologies, empire/star names) are defined as Godot `Resource` files (`.tres`) in `res://Data/`. Code implements systems; data defines content.
2. **Tick-Based Simulation**: Simulation logic advances in discrete intervals governed by `TickManager` (Autoload singleton). Economy, population growth, construction queues, AI time-slicing, and fleet movement all evaluate per-tick.
3. **Component & Node Composition**: Game entities (`Star`, `Empire`, `Fleet`) are modular `Node` hierarchies. Behavior is composed, not deeply inherited.
4. **Signal-Based Decoupling**: Systems and UI communicate via Godot signals (`TickUpdateSignal`, `FleetArrivedSignal`, `EventRecorded`, etc.). Avoid tight coupling between UI and simulation internals.

---

## ⚠️ Non-Negotiable Agent Rules of Engagement

### 1. Never Edit Godot Serialization Files Directly
> [!CAUTION]
> **DO NOT** manually edit or generate `.tscn`, `.tres`, or `.uid` files with text editing tools.
> Always inspect, create, or update scenes, resources, and autoloads using:
> - The `godot-ai` MCP server tools (`scene_manage`, `node_create`, `node_set_property`, `resource_manage`, `autoload_manage`, `script_attach`, `scene_save`, etc.)
> - Godot Engine C# APIs at runtime

### 2. Maintain Data-Driven Discipline
> [!IMPORTANT]
> **Never hardcode** stats, resource outputs, build times, costs, or entity names in C# logic (`Empire.cs`, `Star.cs`, `Fleet.cs`, etc.).
> - Define and reference custom `[Tool] [GlobalClass]` `Resource` classes (e.g., `ImprovementResource`, `ShipDesignResource`, `StringArrayResource`).
> - Load definitions from `Data/` or pass them through `[Export]` properties.

### 3. Safe Node Lifecycle & Defensive SceneTree Access
- Always verify instance validity before accessing or freeing nodes:
  ```csharp
  if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
  {
      // Safe to interact or free
  }
  ```
- **Guard Singleton and SceneTree access**: In unit tests or headless runs, singletons (`TickManager.HasInstance`, `EmpireManager.HasInstance`, `StarManager.HasInstance`, `FleetManager.HasInstance`, `AIManager.HasInstance`) or `GetTree()` may not be initialized. Always use `HasInstance` / null checks so simulation entities remain testable in isolation.

### 4. C# Code Conventions
- Every class attached to a Godot node or resource MUST be declared as `public partial class`.
- Add `[Tool]` alongside `[GlobalClass]` on custom `Resource` classes so Godot and `resource_manage` can instantiate them in the editor.
- Use `[Export]` properties with appropriate hints (e.g., `[Export(PropertyHint.Dir)]`) for Inspector configurability.
- Follow Godot C# event handler conventions for signals:
  ```csharp
  [Signal]
  public delegate void TickUpdateSignalEventHandler(int tickCount);
  ```

### 5. UI Scene Architecture
- Place UI scenes and scripts under `res://UI/` and follow all styling rules in [UI/agents.md](UI/agents.md).
- Prefer Scene Unique Names (`%NodeName`) for dynamic controls (labels, buttons, containers) to avoid brittle `GetNode("VBox/HBox/...")` paths.

### 6. Documentation-First Navigation & Subsystem Sync
> [!IMPORTANT]
> Follow the full protocol in **[documentation_sync.md](.agent/rules/documentation_sync.md)**:
> - **Before coding**: Use the Subsystem Routing Index above and read the relevant `.agent/subsystems/*.md` doc first rather than parsing the whole codebase.
> - **After coding**: Keep the owning `.agent/subsystems/*.md` doc in sync whenever files, mechanics, data schemas, signals, Blackboard keys, or test suites change. Create a new `.agent/subsystems/<name>.md` and link it here whenever adding a new subsystem.
> - **Keep `gemini.md` lean**: Put deep implementation details in `.agent/subsystems/*.md`, not inline in `gemini.md`.

---

## 📁 Codebase Anatomy

```
GalconEmpire/
├── gemini.md                       # Primary entry point, routing index & core rules (this file)
├── AI_Design.md                    # High-level AI system design & multi-tier roadmap
├── Ship_Design.md                  # High-level ship, fleet & colonization design doc
├── .agent/                         # Agent technical reference & custom rules
│   ├── rules/
│   │   ├── documentation_sync.md   # Formal rule for doc-first navigation & subsystem sync
│   │   └── chronicle_component.md  # Portable Chronicle boundary & doc sync rule
│   ├── subsystems/                 # Detailed per-subsystem reference docs
│   │   ├── simulation_and_economy.md
│   │   ├── ships_and_fleets.md
│   │   ├── ai_and_goap.md
│   │   ├── chronicle_and_persistence.md
│   │   └── ui_and_camera.md
│   ├── architecture.md             # High-level architecture & autoload overview
│   ├── game_design.md              # Game concept, core loops & macro roadmap
│   └── testing_guide.md            # GdUnit4 standards & CLI runner guide
├── Gameplay/                       # Core simulation domain logic
│   ├── Empire.cs                   # Empire stockpiles, improvement & ship queues, fleets
│   ├── Star.cs                     # Star state: improvements, building cap, food/pop growth
│   ├── GalaxySpawner.cs            # Main scene root hosting galaxy managers & HUD
│   ├── CameraController.cs         # Pan/zoom/focus Camera2D controller
│   ├── IAdvisor.cs / BasicAdvisor.cs
│   ├── Managers/                   # Autoloads & scene orchestrators
│   │   ├── TickManager.cs          # Autoload: simulation clock & TickUpdateSignal
│   │   ├── AIManager.cs            # Autoload: time-sliced GOAP advisor updates
│   │   ├── FleetManager.cs         # Autoload: ship design catalog & fleet tick updates
│   │   ├── EmpireManager.cs        # Scene manager: empire spawning & improvement catalog
│   │   └── StarManager.cs          # Scene manager: star spawning & spatial queries
│   └── Ships/                      # Ship.cs (instance), Fleet.cs (Node2D traversal & colonization)
│   └── AI/                         # GoapAdvisor.cs + GOAP/ engine, Goals/, Actions/
├── Modules/                        # Portable, self-contained game components
│   └── Chronicle/                  # Portable SQLite event history component (Autoload: ChronicleManager)
│       ├── ChronicleManager.cs     # Autoload: SQLite event history (res://saves/chronicle.db)
│       ├── ChronicleEvent.cs       # Fluent event builder & BBCode token formatter
│       ├── ChronicleRecord.cs      # Record DTO, ChronicleFilter, ChronicleViewMode
│       ├── EventCategory.cs        # EventCategory & EventPriority enums
│       ├── IChronicleEntity.cs     # Linkable entity interface & ToChronicleLink()
│       ├── README.md               # User & developer setup guide
│       └── AGENT_INTEGRATION.md    # Agentic integration checklist
├── Data/                           # Data definitions & .tres blueprints
│   ├── ImprovementResource.cs      # ImprovementResource, ResourceType, IsStockpileable()
│   ├── ShipDesignResource.cs       # ShipDesignResource, ShipAbility
│   ├── StringArrayResource.cs      # Name lists (StarNames.tres, EmpireNames.tres)
│   ├── Improvements/               # Farm_1, Mine_1, Market_1, Shipyard_1 (.tres)
│   └── Ships/                      # ColonyShip.tres
├── UI/                             # HUD & inspector panels (see UI/agents.md)
│   ├── agents.md                   # UI visual & NinePatchRect guidelines
│   ├── EmpiresList.cs              # Empire roster & day counter
│   ├── EmpireInfoBox.cs            # Draggable empire dossier
│   ├── StarInfoBox.cs              # Draggable star inspector
│   ├── FleetInfoBox.cs             # Draggable fleet dossier (ships + Chronicle)
│   ├── ShipInfoBox.cs              # Draggable ship details (stats + Chronicle)
│   └── DebugUi.cs                  # Tick speed slider
├── Tests/                          # Unified GdUnit4 C# test suites (10 suites)
└── Tracking/                       # FeaturePlan.md, ClassDiagram.mmd, DataERDiagram.mmd
```

---

## 🛠️ Mandatory Verification Workflow

Every code modification MUST be validated using the following verification steps:

### 1. Compile C# Project
```powershell
dotnet build
```
Ensure build completes with **0 errors** and **0 warnings** in project code.

### 2. Execute Unified GdUnit4 Test Suite
```powershell
.\addons\gdUnit4\runtest.cmd --godot_binary "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" -a res://Tests/
```
All 10 test suites (`BlackboardTests`, `CameraControllerTest`, `ChronicleManagerTest`, `EmpireInfoBoxTest`, `EmpireTests`, `FleetAndShipInfoBoxTest`, `GoapPlannerTests`, `ShipSystemTests`, `SpaceBackgroundTest`, `StarInfoBoxTest`) run under GdUnit4 and must pass with **0 errors, 0 failures, and 0 orphans**.

---

## 📚 Detailed Documentation References
- **Custom Rules**: [documentation_sync.md](.agent/rules/documentation_sync.md) | [chronicle_component.md](.agent/rules/chronicle_component.md) | [UI/agents.md](UI/agents.md)
- **Subsystem Docs**: [Simulation & Economy](.agent/subsystems/simulation_and_economy.md) | [Ships & Fleets](.agent/subsystems/ships_and_fleets.md) | [AI & GOAP](.agent/subsystems/ai_and_goap.md) | [Chronicle & Persistence](.agent/subsystems/chronicle_and_persistence.md) | [UI & Camera](.agent/subsystems/ui_and_camera.md)
- **High-Level Guides & Roadmaps**: [Architecture Overview](.agent/architecture.md) | [Game Design](.agent/game_design.md) | [Testing Guide](.agent/testing_guide.md) | [AI Design](AI_Design.md) | [Ship Design](Ship_Design.md)

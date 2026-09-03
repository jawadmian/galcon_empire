# Galcon Empire — Agent Guidelines & System Directive

## 📌 Project Overview
**Galcon Empire** is a **data-driven 4X space empire simulation game** built in **Godot 4.7.1** using **C# (.NET 8)**.
The player acts as an incorporeal galactic observer and shaper, watching and influencing the rise and fall of automated star empires.

---

## 🏛️ Core Architectural Principles
1. **Data-Driven Design**: All gameplay definitions (improvements, buildings, technologies, empire names, star names) are defined as Godot `Resource` files (`.tres`) in `res://Data/`. Code implements systems; data defines content.
2. **Tick-Based Simulation**: Simulation logic advances in discrete intervals governed by `TickManager` (Autoload singleton). All economic updates, population growth, and construction queues evaluate per-tick.
3. **Component & Node Composition**: Game entities (`Star`, `Empire`, etc.) are modular `Node` hierarchies. Behavior is composed, not inherited deeply.
4. **Signal-Based Decoupling**: Systems and UI communicate via Godot signals (`TickUpdateSignal`, custom events). Avoid tight coupling between UI and simulation internals.

---

## ⚠️ Non-Negotiable Agent Rules of Engagement

### 1. Never Edit Godot Serialization Files Directly
> [!CAUTION]
> **DO NOT** manually edit or generate `.tscn`, `.tres`, or `.uid` files with text editing tools.
> Always inspect, create, or update scenes and nodes using:
> - The `godot-ai` MCP server tools (`scene_manage`, `node_create`, `node_set_property`, `script_attach`, `scene_save`, etc.)
> - Godot Engine C# APIs at runtime

### 2. Maintain Data-Driven Discipline
> [!IMPORTANT]
> **Never hardcode** stats, resource outputs, build times, costs, or entity names in C# logic (`Empire.cs`, `Star.cs`, `StarManager.cs`, etc.).
> - Define and reference custom `Resource` classes (e.g., `ImprovementResource`, `StringArrayResource`).
> - Load definitions from `Data/` or pass them through `[Export]` properties.

### 3. Safe Node Lifecycle & Defensive SceneTree Access
- Always verify instance validity before accessing or freeing nodes:
  ```csharp
  if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
  {
      // Safe to interact or free
  }
  ```
- **Guard Singleton and SceneTree access**: In unit tests or headless runs, singletons (`TickManager.Instance`, `EmpireManager.Instance`, `StarManager.Instance`) or `GetTree()` may not be initialized. Always use null-checks or defensive guards so simulation entities remain testable in isolation.

### 4. C# Code Conventions
- Every class attached to a Godot node or resource MUST be declared as `public partial class`.
- Use `[Export]` properties with appropriate hints (e.g., `[Export(PropertyHint.Dir)]`) for Inspector configurability.
- Follow Godot C# event handler conventions for signals:
  ```csharp
  [Signal]
  public delegate void TickUpdateSignalEventHandler(int tickCount);
  ```

### 5. UI Scene Architecture
- Place UI scenes and scripts under `res://UI/`.
- Prefer Scene Unique Names (`%NodeName`) for dynamic controls (labels, buttons, containers) to avoid brittle `GetNode("VBox/HBox/...")` paths when reorganizing layouts.

---

## 📁 Codebase Anatomy

```
GalconEmpire/
├── gemini.md                 # Agent rules & core operational instructions (this file)
├── .agent/                   # Deep-dive specs & agent knowledge
│   ├── architecture.md       # Technical architecture, tick loop & manager lifecycle
│   ├── game_design.md        # Game concept, core loops, mechanics & roadmap
│   └── testing_guide.md      # Headless test runner & testing best practices
├── Gameplay/                 # Core simulation domain logic
│   ├── Empire.cs             # Empire state: stockpiles, build queues, population
│   ├── Star.cs               # Star state: improvements, population, resources
│   ├── GalaxySpawner.cs      # Procedural galaxy generation
│   ├── IAdvisor.cs           # Advisor interface
│   ├── BasicAdvisor.cs       # AI decision-making for empire improvements
│   └── Managers/             # Orchestrators & singletons
│       ├── TickManager.cs    # Simulation clock & tick dispatcher (Autoload)
│       ├── EmpireManager.cs  # Empire lifecycle & improvement catalog
│       └── StarManager.cs    # Star registry & tick propagation
├── Data/                     # Data definitions & resources (.tres)
│   ├── ImprovementResource.cs# Custom Resource: build costs, outputs, build time
│   ├── StringArrayResource.cs# Name lists resource
│   ├── Improvements/         # Concrete improvement resources (.tres)
│   ├── StarNames.tres        # Star name catalog
│   └── EmpireNames.tres      # Empire name catalog
├── UI/                       # User Interface scenes & controllers
│   ├── EmpiresList.cs        # Empire overview panel
│   ├── StarInfoBox.cs        # Star selection inspector
│   └── DebugUi.cs            # Simulation controls & debug readouts
├── Tests/                    # Automated testing suite
│   ├── EmpireTests.cs        # Test runner & simulation unit tests
│   └── EmpireTests.tscn      # Headless test runner scene
└── Tracking/                 # Design tracking & diagrams
    ├── FeaturePlan.md        # Active task roadmap
    ├── ClassDiagram.mmd      # Architecture diagram
    └── DataERDiagram.mmd     # Entity relationship diagram
```

---

## 🛠️ Mandatory Verification Workflow

Every code modification MUST be validated using the following verification steps:

### 1. Compile C# Project
```powershell
dotnet build
```
Ensure build completes with **0 warnings and 0 errors**.

### 2. Execute GdUnit4 Test Suite
```powershell
.\addons\gdUnit4\runtest.cmd --godot_binary "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" -a res://Tests/
```
*(Fallback for standalone scene tests: `& "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" --headless res://Tests/EmpireTests.tscn --quit`)*

Ensure all test cases in the test suite pass with `[PASS]` / 0 errors.

---

## 📚 Detailed Documentation References
For deep technical and game design context, consult:
- [Architecture & Technical Reference](file:///e:/Development/Godot/GalconEmpire/.agent/architecture.md)
- [Game Design & Simulation Mechanics](file:///e:/Development/Godot/GalconEmpire/.agent/game_design.md)
- [Testing & Quality Assurance Guide](file:///e:/Development/Godot/GalconEmpire/.agent/testing_guide.md)
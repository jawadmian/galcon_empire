# Rule: Portable Chronicle Component & Documentation Sync

The **Chronicle** system is a self-contained, portable Godot C# component located at `Modules/Chronicle/`.

---

## 1. Strict Folder Boundary

- **All portable Chronicle code and setup documentation MUST reside inside `Modules/Chronicle/`** (`ChronicleManager.cs`, `ChronicleEvent.cs`, `ChronicleRecord.cs`, `EventCategory.cs`, `IChronicleEntity.cs`, `README.md`, `AGENT_INTEGRATION.md`).
- **Zero game-specific dependencies**: Files inside `Modules/Chronicle/` MUST NOT reference Galcon Empire classes, scenes, or singletons (such as `TickManager`, `Empire`, `Star`, `Fleet`, `Ship`, `GalaxySpawner`, or UI panels).
- Integrate host-game systems strictly through:
  - `ChronicleManager.TickProvider` (clock/turn/tick delegate)
  - `IChronicleEntity` (entity hyperlinking contract)
  - `ChronicleManager.Instance.EventRecorded` (signal)
  - `ChronicleEvent.Create(...)` / `ChronicleFilter` (supports both `EventCategory` and raw `string` categories)

---

## 2. Mandatory Documentation Sync on Evolution

Whenever `Modules/Chronicle/` evolves (new SQLite columns/migrations, new `ChronicleFilter` fields, builder API changes, new NuGet/Godot dependencies, or autoload changes), you MUST update **all three** before finishing the task:

1. [Modules/Chronicle/README.md](../../Modules/Chronicle/README.md) — Keep user setup, dependency versions, `.gitignore` notes, and usage examples accurate.
2. [Modules/Chronicle/AGENT_INTEGRATION.md](../../Modules/Chronicle/AGENT_INTEGRATION.md) — Keep the agentic integration checklist, `TickProvider` wiring, and GdUnit4 test isolation instructions accurate.
3. [.agent/subsystems/chronicle_and_persistence.md](../subsystems/chronicle_and_persistence.md) — Keep the Galcon Empire subsystem reference in sync.

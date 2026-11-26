# Subsystem: Chronicle Event History & SQLite Persistence

This document maps the historical event logging system that captures emergent galactic history in SQLite and supports rich entity hyperlinking, filtering, and pagination for UI panels.

> [!NOTE]
> Chronicle is a **portable, self-contained component** located at `Modules/Chronicle/`. See [README.md](../../Modules/Chronicle/README.md), [AGENT_INTEGRATION.md](../../Modules/Chronicle/AGENT_INTEGRATION.md), and [chronicle_component.md](../rules/chronicle_component.md) for cross-project reuse and documentation-sync rules.

---

## 1. Key Files & Responsibilities

| File | Class / Interface | Role |
| :--- | :--- | :--- |
| [ChronicleManager.cs](../../Modules/Chronicle/ChronicleManager.cs) | `ChronicleManager` (Autoload) | Thread-safe SQLite storage manager (`Microsoft.Data.Sqlite`). Initializes DB at `res://saves/chronicle.db` (`ResetOnStart = false`; reset only via `GalaxySpawner._Ready()` on new game) and automatically switches to `res://saves/chronicle_test.db` when `IsRunningTests()` detects GdUnit4. Uses `TickProvider` (wired by `TickManager._EnterTree()`) for clock-agnostic timestamps, supports nested per-tick transaction batching (`BeginBatch()`, `EndBatch()`), maintains an extensible entity registry (`RegisterEntity`, `RegisterEntityResolver`, `ResolveEntity`) and typed click dispatch (`RegisterLinkHandler<T>`, `TryHandleLinkClick`, `EntityLinkClicked`), emits `EventRecorded`, inserts records, and serves priority/entity/category-filtered queries. |
| [ChronicleEvent.cs](../../Modules/Chronicle/ChronicleEvent.cs) | `ChronicleEvent`, `ChronicleEventBuilder` | Fluent builder API (`ChronicleEvent.Create(category, title, priority).Involving(...).WithTemplate(...).Record()`) supporting `EventCategory` or raw `string` categories, `.AtTick(...)` overrides, and `{entity}` token resolution into both plain `RawText` and BBCode `[url=entity:...]Name[/url]` hyperlinks. |
| [ChronicleRecord.cs](../../Modules/Chronicle/ChronicleRecord.cs) | `ChronicleRecord`, `ChronicleFilter`, `ChronicleViewMode` | Data transfer object for stored events (`Priority`) and query filter parameters (`EntityId`, `Category`, `CategoryName`, `MinPriority`, `ExactPriority`, `MinTick`, `MaxTick`, `SearchText`, `Limit`, `Offset`) plus `ApplyViewMode()` helpers for condensed (`High`) vs. detailed views. |
| [EventCategory.cs](../../Modules/Chronicle/EventCategory.cs) | `EventCategory`, `EventPriority` | Categories (`General`, `Political`, `Economic`, `Military`, `Demographic`, `Scientific`, `Diplomatic`) and priorities (`Low = 0`, `Normal = 1`, `High = 2`). |
| [IChronicleEntity.cs](../../Modules/Chronicle/IChronicleEntity.cs) | `IChronicleEntity`, `ChronicleEntityExtensions` | Contract for linkable game entities (`ChronicleId`, `ChronicleName`, `ChronicleType`, `WorldPosition`) and `ToChronicleLink()`. Implemented by `Empire`, `Star`, `Fleet`, and `Ship`. |

---

## 2. Database Schema & Resilience

- **Default Path**: `res://saves/chronicle.db` (`ResetOnStart = false`; cleared via `ResetWorldState()` only when `GalaxySpawner._Ready()` starts a new game). Test runs automatically isolate to `TestDatabasePath` (`res://saves/chronicle_test.db`).
- **Tables**:
  - `chronicle_events`: `id` (PK autoincrement), `tick`, `category`, `priority` (`0=Low`, `1=Normal`, `2=High`), `title`, `raw_text`, `formatted_bbcode`, `primary_entity_id`, `entity_tags`, `metadata_json`. Indexes: `idx_chronicle_tick`, `idx_chronicle_category`, `idx_chronicle_priority`, `idx_chronicle_primary_entity`, `idx_chronicle_entity_tags`. (Auto-migrates older DBs missing `priority`.)
  - `chronicle_entities`: `chronicle_id` (PK), `entity_type`, `display_name`, `status` (`Active`, `Expended in Colonization`, `Disbanded`, etc.), `last_known_tick`, `summary_json`. Powers **tombstone** fallback (`GetTombstone()`, `RegisterTombstoneHandler()`, `RegisterDefaultTombstoneHandler()`, `TombstoneLinkClicked`) when a clicked entity is no longer active in the game.
- **Crash & Corruption Resilience** ([ChronicleManager.cs](../../Modules/Chronicle/ChronicleManager.cs)):
  - Runs `PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;` on open.
  - Calls `SqliteConnection.ClearAllPools()` in `CloseDatabase()` and before deleting files so Windows OS file locks do not block cleanup.
  - Automatically catches `SqliteException` Error `11` (`SQLITE_CORRUPT` / `"malformed"`) during `InitializeDatabase()`, deletes the corrupt DB/WAL/SHM files, and recreates a clean database.
  - `RecordEvent()` wraps SQLite execution in `try/catch` so DB errors never halt the tick loop.

---

## 3. Currently Emitted Events

| Category | Title | Emitted From |
| :--- | :--- | :--- |
| `Political` | `"Empire Founded"` | `EmpireManager.SpawnEmpiresInternal()` |
| `Political` | `"New Colony Established"` | `Fleet.ColonizeCurrentStar()` *(involves empire, star, fleet, ship)* |
| `Political` | `"Colonization Aborted"` | `Fleet.ColonizeCurrentStar()` *(involves fleet, empire, star, ships)* |
| `Economic` | `"Construction Commenced"` | `Empire.StartConstruction()` |
| `Economic` | `"Facility Completed"` | `Empire.UpdateTick()` |
| `Military` | `"Ship Commissioned"` | `Empire.SpawnCompletedShip()` *(involves empire, star, fleet, ship)* |
| `Military` | `"Fleet Dispatched"` | `Fleet.IssueOrder()` *(involves fleet, empire, target star, ships)* |
| `Military` | `"Fleet Arrived"` | `Fleet.ArriveAtDestination()` *(involves fleet, empire, arrived star, ships)* |

---

## 4. Common Extension Recipes

- **Record a new historical event**:
  ```csharp
  ChronicleEvent.Create(EventCategory.Political, "Event Title")
      .Involving(empire, isPrimary: true)
      .Involving(star)
      .WithTemplate("{empire} did something at {star} with [b]{detail}[/b].")
      .WithArg("detail", "value")
      .WithMetadata("key", 123)
      .Record();
  ```
- **Make a new entity linkable**: Implement `IChronicleEntity` (`ChronicleId` as `"<type>:<slug>"`, `ChronicleName`, `ChronicleType`, `WorldPosition`).
- **Isolate in unit tests**: Call `manager.InitializeDatabase("Data Source=MyTest;Mode=Memory;Cache=Shared")` and `ChronicleManager.SetInstanceForTesting(manager)`, then clear in `[AfterTest]`.

## 5. Related Tests
- [ChronicleManagerTest.cs](../../Tests/ChronicleManagerTest.cs) — Covers schema setup, recording/retrieval, entity-tag & tick-range filtering, pagination, BBCode token formatting, Empire/Star integration, and `ResetWorldState()`.

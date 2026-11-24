# Subsystem: Chronicle Event History & SQLite Persistence

This document maps the historical event logging system that captures emergent galactic history in SQLite and supports rich entity hyperlinking, filtering, and pagination for UI panels.

---

## 1. Key Files & Responsibilities

| File | Class / Interface | Role |
| :--- | :--- | :--- |
| [ChronicleManager.cs](../../Gameplay/Managers/ChronicleManager.cs) | `ChronicleManager` (Autoload) | Thread-safe SQLite storage manager (`Microsoft.Data.Sqlite`). Initializes DB at `res://saves/chronicle.db` (reset on start by default), emits `EventRecorded`, inserts records, and serves filtered/paginated queries. |
| [ChronicleEvent.cs](../../Gameplay/Chronicle/ChronicleEvent.cs) | `ChronicleEvent`, `ChronicleEventBuilder` | Fluent builder API (`ChronicleEvent.Create(...).Involving(...).WithTemplate(...).Record()`) that resolves `{entity}` tokens into both plain `RawText` and BBCode `[url=entity:...]Name[/url]` hyperlinks. |
| [ChronicleRecord.cs](../../Gameplay/Chronicle/ChronicleRecord.cs) | `ChronicleRecord`, `ChronicleFilter` | Data transfer object for stored events and query filter parameters (`EntityId`, `Category`, `MinTick`, `MaxTick`, `SearchText`, `Limit`, `Offset`). |
| [EventCategory.cs](../../Gameplay/Chronicle/EventCategory.cs) | `EventCategory` | Enum: `General`, `Political`, `Economic`, `Military`, `Demographic`, `Scientific`, `Diplomatic`. |
| [IChronicleEntity.cs](../../Gameplay/Chronicle/IChronicleEntity.cs) | `IChronicleEntity`, `ChronicleEntityExtensions` | Contract for linkable game entities (`ChronicleId`, `ChronicleName`, `ChronicleType`, `WorldPosition`) and `ToChronicleLink()`. Implemented by `Empire`, `Star`, `Fleet`, and `Ship`. |

---

## 2. Database Schema & Resilience

- **Default Path**: `res://saves/chronicle.db` (`ResetOnStart = true`).
- **Table**: `chronicle_events`
  - Columns: `id` (PK autoincrement), `tick`, `category`, `title`, `raw_text`, `formatted_bbcode`, `primary_entity_id`, `entity_tags` (pipe-delimited `|empire:sol|star:alpha|`), `metadata_json`.
  - Indexes: `idx_chronicle_tick`, `idx_chronicle_category`, `idx_chronicle_primary_entity`, `idx_chronicle_entity_tags`.
- **Crash & Corruption Resilience** ([ChronicleManager.cs](../../Gameplay/Managers/ChronicleManager.cs)):
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

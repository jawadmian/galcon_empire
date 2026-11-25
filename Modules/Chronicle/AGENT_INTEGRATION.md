# Chronicle Component — Agentic Integration Guide

This document instructs an AI coding agent on how to integrate the portable **Chronicle** component (`Modules/Chronicle/`) into a Godot 4.x C# (.NET 8) game cleanly, without introducing tight coupling or breaking tests.

---

## 1. Integration Checklist

Execute these steps in order when adding Chronicle to a target Godot project:

### Step 1: Verify Project & Dependencies
1. Inspect the target project's `.csproj`:
   - Confirm `<TargetFramework>net8.0</TargetFramework>` (or compatible .NET version for `Microsoft.Data.Sqlite`).
   - Check for `<PackageReference Include="Microsoft.Data.Sqlite" ... />`.
   - If missing, add `Microsoft.Data.Sqlite` (e.g. `dotnet add package Microsoft.Data.Sqlite --version 10.0.11`) and run `dotnet restore` + `dotnet build`.
2. Verify `Modules/Chronicle/` is present at `res://Modules/Chronicle/` (containing `ChronicleManager.cs`, `ChronicleEvent.cs`, `ChronicleRecord.cs`, `EventCategory.cs`, `IChronicleEntity.cs`).

### Step 2: Register the Autoload Singleton
Never edit `project.godot` manually if `godot-ai` MCP tools are available:
1. Build first (`dotnet build`) so Godot recognizes the C# `Node` script.
2. Call `autoload_manage`:
   ```json
   {
     "op": "add",
     "params": {
       "name": "ChronicleManager",
       "path": "res://Modules/Chronicle/ChronicleManager.cs",
       "singleton": true
     }
   }
   ```
   *(If MCP is unavailable, instruct the user to add `res://Modules/Chronicle/ChronicleManager.cs` as `ChronicleManager` in Project Settings → Globals → Autoload.)*

### Step 3: Wire the Simulation Clock (`TickProvider`)
Chronicle is clock-agnostic. Identify the target game's time/turn/tick source (e.g., `TurnManager`, `TickManager`, `GameState.Day`) and bind `ChronicleManager.TickProvider` during startup (e.g. in the clock's `_EnterTree()` or main scene `_Ready()`):

```csharp
ChronicleManager.TickProvider = () => MyClock.HasInstance ? MyClock.Instance.CurrentTick : 0;
```

- Always guard singleton access inside `TickProvider` so headless unit tests without the clock in the SceneTree do not throw or log errors.
- For retrospective or custom-timestamped events, use `.AtTick(int tick)` on `ChronicleEventBuilder`.

### Step 4: Wire New-Game Reset
Locate where the target game initializes a fresh run/campaign/world and call:

```csharp
ChronicleManager.Instance?.ResetWorldState();
```

Do **not** set `ResetOnStart = true` on `ChronicleManager` if the game transitions between menus and gameplay scenes unless you intentionally want the DB wiped every time the autoload enters the tree.

### Step 5: Implement `IChronicleEntity` on Domain Models
For every entity type that should appear in history logs or be clickable in UI:
1. Implement `IChronicleEntity`:
   - `ChronicleId`: Globally unique stable ID prefixed by type, e.g. `$"character:{Id}"` or `$"city:{Id}"`.
   - `ChronicleName`: Human-readable display name.
   - `ChronicleType`: Lowercase singular type noun (`"character"`, `"city"`, `"fleet"`). Used as the default `{type}` token in templates.
   - `WorldPosition`: `Vector2?` for 2D camera focusing (return `null` for abstract or 3D-only entities).
2. **Collision rule in `Involving()`**: If two entities of the **same** `ChronicleType` are involved in one event (e.g., two cities in a trade route), `Involving()` maps the first one to `{city}` and **both** to `{<ChronicleId>}` (e.g., `{city:alpha}` and `{city:beta}`). Use `.WithArg(...)` or full IDs when disambiguating same-type entities in templates.

### Step 6: Choose Categories (Enum vs. Custom Strings)
- If the game fits the built-in `EventCategory` enum (`General`, `Political`, `Economic`, `Military`, `Demographic`, `Scientific`, `Diplomatic`), use `ChronicleEvent.Create(EventCategory.Military, ...)`.
- If the game needs domain-specific categories (e.g., `"Combat"`, `"Dialogue"`, `"Crafting"`), either:
  - Pass strings directly: `ChronicleEvent.Create("Crafting", "Sword Forged")` and query with `new ChronicleFilter { CategoryName = "Crafting" }`, **or**
  - Extend `EventCategory` in `EventCategory.cs` (document any extension in your project's subsystem doc).

### Step 7: Emit Events & Guard Nulls
- `ChronicleEvent.Create(...).Record()` automatically checks `ChronicleManager.Instance != null`, so domain code can safely emit events even in isolated unit tests where `ChronicleManager` is not running.
- Wrap rich UI text in BBCode only via `WithTemplate(...)`; `ChronicleEventBuilder` automatically produces both `FormattedBbcode` and plain `RawText`.

### Step 8: Connect UI Panels & Hyperlink Dispatch
- Subscribe to `ChronicleManager.Instance.EventRecorded` (emitted via `CallDeferred` for thread safety).
- Query with `ChronicleManager.Instance.QueryEvents(filter)` and `GetEventCount(filter)`.
- Register panel openers / click handlers once at startup (or via a shared UI helper):
  ```csharp
  ChronicleManager.Instance.RegisterLinkHandler<MyEntity>("mytype", (entity, ctx) => MyInfoBox.OpenFor(entity, ctx));
  ChronicleManager.Instance.RegisterEntityResolver("mytype", id => MyRegistry.FindByChronicleId(id)); // optional fallback
  ChronicleManager.Instance.RegisterDefaultTombstoneHandler((tombstone, ctx) => TombstoneInfoBox.OpenForTombstone(tombstone, ctx));
  ```
- When an entity is expended, disbanded, or destroyed, call `ChronicleManager.Instance?.MarkEntityTombstoned(entity, "Status", summaryDict)` so clicking its historical link opens the tombstone view.
- Note: `ChronicleEventBuilder.Record()` automatically caches all involved entities in `ChronicleManager` and prunes freed `GodotObject` instances on lookup.
- In `RichTextLabel` (`BbcodeEnabled = true`, `FitContent = true`, `ScrollActive = false`), render `record.FormattedBbcode` and forward `MetaClicked`:
  ```csharp
  richLabel.MetaClicked += meta => ChronicleManager.Instance?.TryHandleLinkClick(meta.AsString(), ownerControl);
  ```

---

## 2. Testing & Isolation Rules for Agents

When writing GdUnit4 or unit tests in a project using Chronicle:
1. **Never mutate the live game DB** (`res://saves/chronicle.db`).
2. `ChronicleManager._Ready()` auto-switches to `TestDatabasePath` (`res://saves/chronicle_test.db`) when `gdUnit` is in command-line args.
3. For isolated unit tests, instantiate a local `ChronicleManager` with an in-memory SQLite connection:
   ```csharp
   var manager = new ChronicleManager();
   manager.InitializeDatabase("Data Source=ChronicleTest;Mode=Memory;Cache=Shared");
   ChronicleManager.SetInstanceForTesting(manager);
   // ... run test ...
   manager.CloseDatabase();
   manager.Free();
   ChronicleManager.SetInstanceForTesting(null);
   ```
4. Always call `CloseDatabase()` before `Free()` so SQLite connection pools are cleared and Windows file locks are released.

---

## 3. Keeping Documentation in Sync

Whenever you modify `Modules/Chronicle/` (schema columns, `ChronicleFilter` fields, `ChronicleEventBuilder` methods, or dependencies):
1. Update `Modules/Chronicle/README.md` (user setup & API examples).
2. Update `Modules/Chronicle/AGENT_INTEGRATION.md` (agent integration steps).
3. Update the host project's subsystem documentation (e.g., `.agent/subsystems/chronicle_and_persistence.md`).

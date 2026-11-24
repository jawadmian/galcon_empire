# Chronicle Component (`Modules/Chronicle/`)

A self-contained, portable **SQLite-backed event history & entity-hyperlinking system** for **Godot 4.x C# (.NET 8)** games.

Chronicle records emergent gameplay events with structured entity tags, priority levels, custom metadata, plain-text summaries, and BBCode hyperlinks (`[url=entity:<type>:<id>]Name[/url]`) so UI panels can render clickable, filterable, paginated history logs.

---

## 1. Folder Boundary (What Belongs Here)

Everything inside `Modules/Chronicle/` is designed to be **copied wholesale** into any Godot C# project with zero game-specific code dependencies:

| File | Purpose |
| :--- | :--- |
| `ChronicleManager.cs` | Godot Autoload singleton (`Node`) managing thread-safe SQLite persistence, schema creation/migration, crash/corruption recovery, queries, and `EventRecorded` signal emission. |
| `ChronicleEvent.cs` | Fluent builder API (`ChronicleEvent.Create(...).Involving(...).WithTemplate(...).Record()`) that resolves `{entity}` and `{arg}` tokens into both plain text and BBCode links. |
| `ChronicleRecord.cs` | `ChronicleRecord` DTO, `ChronicleFilter` query object (entity, category, priority, tick range, search, pagination), and `ChronicleViewMode` presets. |
| `EventCategory.cs` | Default `EventCategory` enum (`General`, `Political`, `Economic`, `Military`, `Demographic`, `Scientific`, `Diplomatic`) and `EventPriority` (`Low = 0`, `Normal = 1`, `High = 2`). |
| `IChronicleEntity.cs` | Interface (`ChronicleId`, `ChronicleName`, `ChronicleType`, `WorldPosition`) and `ToChronicleLink()` extension for any game object that can appear in history. |
| `README.md` | User & developer setup guide (this file). |
| `AGENT_INTEGRATION.md` | Step-by-step integration protocol for AI coding agents. |

> [!IMPORTANT]
> **Keep this folder game-agnostic.** Do not reference game-specific singletons, scenes, or domain classes (e.g., `TickManager`, `Empire`, `Star`, `Player`) inside `Modules/Chronicle/`. Integrate game clocks and entities via `ChronicleManager.TickProvider` and `IChronicleEntity`.

---

## 2. User Setup Guide (Adding Chronicle to a New Godot Game)

### Step 1: Copy the Component Folder
Copy the entire `Modules/Chronicle/` directory into your Godot project (for example, `res://Modules/Chronicle/`).

### Step 2: Install SQLite Dependency
Chronicle uses `Microsoft.Data.Sqlite` (which bundles native `e_sqlite3` binaries for Windows, Linux, and macOS).

Run the following in your project root (where your `.csproj` lives):

```powershell
dotnet add package Microsoft.Data.Sqlite --version 10.0.11
dotnet restore
dotnet build
```

Or add the package reference directly to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />
</ItemGroup>
```

### Step 3: Register the `ChronicleManager` Autoload
1. Open your project in the Godot Editor.
2. Go to **Project → Project Settings → Globals → Autoload**.
3. Set **Path** to `res://Modules/Chronicle/ChronicleManager.cs` and **Node Name** to `ChronicleManager`.
4. Click **Add** (ensure **Enable** / Singleton is checked).

### Step 4: Wire Your Game Clock (Optional but Recommended)
By default, events record at tick `0` unless you wire `ChronicleManager.TickProvider` or pass `.AtTick(tick)` on an event.

In your game's clock, turn manager, or main scene `_EnterTree()` / `_Ready()`:

```csharp
ChronicleManager.TickProvider = () => MyGameClock.CurrentTick;
```

### Step 5: Handle New Game Resets & `.gitignore`
- **Default database path**: `res://saves/chronicle.db` (`ResetOnStart` defaults to `false` so scene reloads do not wipe history).
- **GdUnit4 test isolation**: When running under GdUnit4, `ChronicleManager` automatically switches to `res://saves/chronicle_test.db` and resets it.
- **New game reset**: Call `ChronicleManager.Instance?.ResetWorldState();` when starting a fresh campaign/run.
- **Git**: Add database artifacts to `.gitignore` so local playtest logs aren't committed:
  ```gitignore
  saves/*.db
  saves/*.db-wal
  saves/*.db-shm
  ```

---

## 3. Quick Usage Examples

### Make a Game Entity Linkable
```csharp
public partial class Faction : Node, IChronicleEntity
{
    public string Id { get; set; }
    public string DisplayName { get; set; }

    public string ChronicleId => $"faction:{Id}";
    public string ChronicleName => DisplayName;
    public string ChronicleType => "faction";
    public Vector2? WorldPosition => null; // Or GlobalPosition for 2D entities
}
```

### Record an Event
```csharp
ChronicleEvent.Create(EventCategory.Diplomatic, "Treaty Signed", EventPriority.High)
    .Involving(factionA, isPrimary: true)
    .Involving(factionB)
    .WithTemplate("{faction} signed a non-aggression pact with {faction:b_id} for {years} years.")
    .WithArg("years", 10)
    .WithMetadata("treaty_type", "non_aggression")
    .Record();
```

You can also use custom string categories (useful for RPGs, colony sims, etc.):
```csharp
ChronicleEvent.Create("Quest", "Dragon Slain", EventPriority.High)
    .Involving(hero, isPrimary: true)
    .WithTemplate("{hero} defeated the ancient dragon.")
    .Record();
```

### Query Events & Handle UI Hyperlinks
```csharp
var filter = new ChronicleFilter
{
    EntityId = factionA.ChronicleId,
    MinPriority = EventPriority.Normal,
    Limit = 25,
    Offset = 0
};
List<ChronicleRecord> records = ChronicleManager.Instance.QueryEvents(filter);
```

In a Godot `RichTextLabel` with `BbcodeEnabled = true`:
- Render `record.FormattedBbcode`.
- Connect `MetaClicked` to parse `meta.AsString()` (format: `"entity:<type>:<id>"`), look up the entity, and focus the camera or open its inspector.

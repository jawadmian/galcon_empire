# Subsystem: UI Panels, HUD, Camera & Space Visuals

This document maps all user-facing HUD controls, inspector windows, camera navigation, and galaxy visual layers. For mandatory styling, NinePatchRect hierarchy, color palette, and Scene Unique Name rules, always consult **[UI/agents.md](../../UI/agents.md)**.

---

## 1. Key Files & Responsibilities

| File | Scene / Asset | Role |
| :--- | :--- | :--- |
| [MainMenu.cs](../../UI/MainMenu.cs) | [main_menu.tscn](../../UI/main_menu.tscn) | Startup entry scene (`project.godot` main scene). Offers Quick Start (defaults via `GameConfig`), Create New Game detailed setup (Stars `10..100` step `10`, Empires `1..12` clamped to `<= stars`), and disabled Load Game placeholder. |
| [EmpiresList.cs](../../UI/EmpiresList.cs) | [empires_list.tscn](../../UI/empires_list.tscn) | Top-level HUD roster listing all active empires and the current simulation date (`Year X, Month Y` via `TickManager.FormatDate()`). Clicking an empire opens its `EmpireInfoBox`. Singleton `EmpiresList.Instance`. |
| [EmpireInfoBox.cs](../../UI/EmpireInfoBox.cs) | [empire_info_box.tscn](../../UI/empire_info_box.tscn) | Single-instance draggable empire dossier panel (`EmpireInfoBox.OpenForEmpire()`, header badge `EMPIRE`). Displays population, Food/Ore/Money stockpiles + per-month deltas, controlled star systems, active construction projects, wrapped/scrollable empire Chronicle history (`%ChronicleList`, `%ChronicleFilterButton`), and camera focus button. |
| [StarInfoBox.cs](../../UI/StarInfoBox.cs) | [star_info_box.tscn](../../UI/star_info_box.tscn) | Single-instance draggable star inspector (`StarInfoBox.OpenForStar()`, header badge `STAR SYSTEM`). Opened via clicking a `Star`'s `Area2D`. Displays star name, coordinates, `StarType`, population + progress bar, yields, building cap usage (`FACILITIES (installed / max)`), installed improvements (live-updating), orbiting ships (`%OrbitingList`, clickable to open `ShipInfoBox`), wrapped/scrollable system Chronicle history (`%ChronicleList`, `%ChronicleFilterButton`), and focus button. |
| [FleetInfoBox.cs](../../UI/FleetInfoBox.cs) | [fleet_info_box.tscn](../../UI/fleet_info_box.tscn) | Single-instance draggable fleet dossier (`FleetInfoBox.OpenForFleet()`, header badge `FLEET`). Opened by clicking a `Fleet` on the map. Displays empire, orbit/transit status, speed (`u/mo`), clickable ship buttons (GUID hidden, opening `ShipInfoBox`), wrapped/scrollable hyperlinked fleet Chronicle history (`%ChronicleList`, `%ChronicleFilterButton`), and focus button. |
| [ShipInfoBox.cs](../../UI/ShipInfoBox.cs) | [ship_info_box.tscn](../../UI/ship_info_box.tscn) | Single-instance draggable ship dossier (`ShipInfoBox.OpenForShip()`, header badge `SHIP`). Opened by clicking a ship in `FleetInfoBox`. Displays design/tech level (GUID hidden), commissioning date (`Year X, Month Y`) & origin star, parent fleet, combat/movement/cargo stats, `ShipAbility` list, wrapped/scrollable hyperlinked ship Chronicle history (`%ChronicleList`, `%ChronicleFilterButton`), and focus button. |
| [ChroniclePanelHelper.cs](../../UI/ChroniclePanelHelper.cs) | *(helper)* | Shared UI helper that wires `ChronicleManager` entity resolvers/link handlers (`empire`, `star`, `fleet`, `ship`) plus the default `TombstoneInfoBox` fallback, configures `%ChronicleFilterButton`, guards window drag over the Chronicle feed, and renders hyperlinked `RichTextLabel` Chronicle feeds. |
| [TombstoneInfoBox.cs](../../UI/TombstoneInfoBox.cs) | [tombstone_info_box.tscn](../../UI/tombstone_info_box.tscn) | Single-instance draggable archive dossier (`TombstoneInfoBox.OpenForTombstone()`, header badge `ARCHIVED <TYPE>`). Opens when clicking a Chronicle hyperlink for an entity no longer active in the game (e.g. expended colony ship, disbanded fleet), showing final status, last recorded date, archived summary, and hyperlinked event history. |
| [DebugUi.cs](../../UI/DebugUi.cs) | `Data/Improvements/debug_ui.tscn` | Simulation speed & pause bar (`<<`, `||`/`▶`, `>>`) stepping between `0.25x`, `0.5x`, `1x`, `2x`, `4x` with `%SpeedStatusLabel` displaying the active speed or paused state. |
| [CameraController.cs](../../Gameplay/CameraController.cs) | `Camera2D` in `galaxy_spawner.tscn` | Singleton `CameraController.Instance`. Handles WASD/Arrow keyboard panning (Shift for fast pan), right-click mouse drag panning, scroll-wheel zoom (`MinZoom` `0.1` to `MaxZoom` `3.0`), smooth damping, and programmatic `FocusOn(Vector2 target)`. |
| `space_background.tscn` | [space_background.tscn](../../Gameplay/space_background.tscn) | Parallax starfield & nebula backdrop layered behind the galaxy (`Images/Space/`, `2048x2048` tiles with `repeat_times = 4` to prevent zoom-out clipping). |
| `UI/Styles/*.tres` | Button StyleBoxes | Shared tactical sci-fi button & close-button states (`normal`, `hover`, `pressed`). |

---

## 2. Core UI Architecture Patterns

1. **Screen-Space `CanvasLayer`**: All HUD panels and popups attach under `galaxy_spawner.tscn`'s `CanvasLayer` so they never zoom or pan with `Camera2D`.
2. **Single-Instance Inspector Windows**: `StarInfoBox` and `EmpireInfoBox` expose static `Instance` and `OpenFor*(entity, contextNode)` helpers so selecting a new star or empire retargets the existing window rather than stacking duplicates.
3. **Scene Unique Names (`%NodeName`)**: All dynamic labels, buttons, progress bars, and containers are queried with `GetNodeOrNull<T>("%NodeName")`.
4. **Live Tick Refresh**: Inspector panels subscribe to `TickManager.Instance.TickUpdateSignal` (and unsubscribe in `_ExitTree()`) to update stockpiles, population, and improvement lists live without reopening.
5. **Scene Editing Rule**: Never edit `.tscn` or `.tres` files directly—use `godot-ai` MCP tools (`node_create`, `node_set_property`, `ui_manage`, `scene_save`).

---

## 3. Common Extension Recipes

- **Add a new Inspector / HUD Panel**:
  1. Read [UI/agents.md](../../UI/agents.md) for the mandatory `Control -> NinePatchRect (Background) -> MarginContainer (ContentMargin) -> VBoxContainer (MainVBox)` hierarchy.
  2. Build the `.tscn` under `res://UI/` using `godot-ai` MCP tools and assign Scene Unique Names to all script-referenced nodes.
  3. Attach a `public partial class` script in `res://UI/` with static single-instance open logic and `TickUpdateSignal` cleanup in `_ExitTree()`.
- **Add Camera Focus to a New Entity**: Call `CameraController.Instance?.FocusOn(entity.WorldPosition.Value)` (guarded for null in headless tests).
- **Display New Simulation Metrics** (e.g., `ShipyardProduction`, active ship queues, Chronicle log tab): Add `%NodeName` labels/containers to `EmpireInfoBox` / `StarInfoBox` via MCP, then bind in their `Refresh*()` methods.

## 4. Related Tests
- [MainMenuTest.cs](../../Tests/MainMenuTest.cs) — Main menu state, disabled Load button, Star/Empire setup constraints, `GameConfig`, scaled spawn bounds, and name pool sizes.
- [StarInfoBoxTest.cs](../../Tests/StarInfoBoxTest.cs) — Data binding, live improvement updates, NinePatchRect margins, button non-overlap, star type modulation.
- [EmpireInfoBoxTest.cs](../../Tests/EmpireInfoBoxTest.cs) — Scene load, NinePatchRect margins, empire data & construction display, camera focus.
- [FleetAndShipInfoBoxTest.cs](../../Tests/FleetAndShipInfoBoxTest.cs) — Fleet/Ship Chronicle lifecycle events, `FleetInfoBox` & `ShipInfoBox` scene loading, NinePatchRect margins, ship buttons, and Chronicle rendering.
- [CameraControllerTest.cs](../../Tests/CameraControllerTest.cs) — Initialization, `FocusOn`, zoom clamping, right-click drag pan, keyboard pan.
- [SpaceBackgroundTest.cs](../../Tests/SpaceBackgroundTest.cs) — Parallax layers and `galaxy_spawner.tscn` integration.

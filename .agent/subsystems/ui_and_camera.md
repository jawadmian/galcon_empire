# Subsystem: UI Panels, HUD, Camera & Space Visuals

This document maps all user-facing HUD controls, inspector windows, camera navigation, and galaxy visual layers. For mandatory styling, NinePatchRect hierarchy, color palette, and Scene Unique Name rules, always consult **[UI/agents.md](../../UI/agents.md)**.

---

## 1. Key Files & Responsibilities

| File | Scene / Asset | Role |
| :--- | :--- | :--- |
| [EmpiresList.cs](../../UI/EmpiresList.cs) | [empires_list.tscn](../../UI/empires_list.tscn) | Top-level HUD roster listing all active empires and the current simulation day (`TickUpdateSignal`). Clicking an empire opens its `EmpireInfoBox`. Singleton `EmpiresList.Instance`. |
| [EmpireInfoBox.cs](../../UI/EmpireInfoBox.cs) | [empire_info_box.tscn](../../UI/empire_info_box.tscn) | Single-instance draggable empire dossier panel (`EmpireInfoBox.OpenForEmpire()`). Displays population, Food/Ore/Money stockpiles + per-tick deltas, controlled star systems, active construction projects, and camera focus button. |
| [StarInfoBox.cs](../../UI/StarInfoBox.cs) | [star_info_box.tscn](../../UI/star_info_box.tscn) | Single-instance draggable star inspector (`StarInfoBox.OpenForStar()`). Opened via clicking a `Star`'s `Area2D`. Displays star name, coordinates, `StarType`, population + progress bar, yields, installed improvements (live-updating), and focus button. |
| [DebugUi.cs](../../UI/DebugUi.cs) | `Data/Improvements/debug_ui.tscn` | Simulation speed control slider mutating `TickManager.Instance.TickUpdateInterval`. |
| [CameraController.cs](../../Gameplay/CameraController.cs) | `Camera2D` in `galaxy_spawner.tscn` | Singleton `CameraController.Instance`. Handles WASD/Arrow keyboard panning (Shift for fast pan), right-click mouse drag panning, scroll-wheel zoom (`MinZoom` `0.1` to `MaxZoom` `3.0`), smooth damping, and programmatic `FocusOn(Vector2 target)`. |
| `space_background.tscn` | [space_background.tscn](../../Gameplay/space_background.tscn) | Parallax starfield & nebula backdrop layered behind the galaxy (`Images/Space/`). |
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
- [StarInfoBoxTest.cs](../../Tests/StarInfoBoxTest.cs) — Data binding, live improvement updates, NinePatchRect margins, button non-overlap, star type modulation.
- [EmpireInfoBoxTest.cs](../../Tests/EmpireInfoBoxTest.cs) — Scene load, NinePatchRect margins, empire data & construction display, camera focus.
- [CameraControllerTest.cs](../../Tests/CameraControllerTest.cs) — Initialization, `FocusOn`, zoom clamping, right-click drag pan, keyboard pan.
- [SpaceBackgroundTest.cs](../../Tests/SpaceBackgroundTest.cs) — Parallax layers and `galaxy_spawner.tscn` integration.

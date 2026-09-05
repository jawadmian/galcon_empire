# UI Design & Architecture Guidelines

This guide establishes the stylistic standards, architectural conventions, and node structuring rules for all user interface components in **Galcon Empire**.

---

## 1. Visual & Stylistic Principles

### Aesthetic: Tactical Sci-Fi HUD
All game UI should evoke a futuristic, military/command holographic display: clean, high-contrast, glowing accents, and dark glassmorphic panels.

### Color Palette
- **Panel Background**: Deep space obsidian / translucent slate-navy (`#070a12` / `#0a0e17` at 90-95% opacity).
- **Primary Accent**: Neon Plasma Cyan (`#00e5ff`) — active borders, system titles, reticles, primary focus states.
- **Resource Accents**:
  - **Food**: Bio-Emerald (`#00e676`)
  - **Ore**: Plasma Cyan (`#00e5ff`) / Sky Blue (`#00b4d8`)
  - **Credits / Money**: Solar Amber / Gold (`#ffd700`)
- **Muted / Subdued Text**: Cool Steel Gray (`#8fa3bf`) for section labels and metadata, Dark Slate (`#556b82`) for disabled/empty states.

### Background Texture Design Rules
- **High Contrast & Visual Clarity**: Panel backgrounds must have a clean, uniform, dark center area. Never use busy, high-frequency, or centered decorative art (circuits, vignettes, baked-in text, tech dials) in the center of scalable panel textures — these ruin text legibility and clash with dynamic UI content.
- **Perimeter Decoration Only**: Restrict decorative tech elements (chamfers, corner brackets, glowing neon accents) strictly to the 9-slice border margins (corners and perimeter). The inner 9-slice area must remain pure, flat, dark slate/obsidian to guarantee maximum readability.

### Typography & Sizing Rules
- **No Arbitrary Node Scaling**: Never stretch UI controls or fonts using `scale = Vector2(2, 2)`. This introduces blurring, misaligned hitboxes, and layout glitches.
- **Font Sizes**: Set explicit font sizes using `theme_override_font_sizes/font_size`:
  - Panel Titles / Headers: `14` – `16` (bold, uppercase)
  - Section Headers: `11` – `12` (subdued color, uppercase)
  - Values / Body Text: `12`
  - Sub-labels / Coordinates / Tooltips: `10` – `11`

### Assets
- Store all UI icons, HUD textures, and panel frames in `res://Images/UI/`.
- Icons must be 1:1 aspect ratio with transparent backgrounds.

---

## 2. Scalable UI Panel Construction (NinePatchRect Specification)

Use the `godot_ai` MCP tools to build scalable UI panel scenes. Follow these exact node hierarchy and styling rules:

### Standard Hierarchy
```
Control (e.g. StarInfoBox)
└── NinePatchRect (e.g. Background)
    └── MarginContainer (e.g. ContentMargin)
        └── VBoxContainer (e.g. MainVBox)
            ├── Header Container
            ├── Content Containers
            └── Action Buttons
```

### Configuration Steps
1. **Create the Base NinePatchRect**:
   - Create a `NinePatchRect` node as the base of the UI panel. Name it `Background` (or `[PanelName]Background`).
2. **Set Anchor Preset**:
   - Set its Anchor Preset to `Center` or `Full Rect` depending on layout needs (e.g. Full Rect if the parent Control governs position/size, or Center if anchored).
3. **Assign Texture**:
   - Assign the texture asset located at `res://Images/UI/<image_name>.png` to its `texture` property.
4. **Configure 9-Slice Patch Margins**:
   - Set the margins to match the border pixels of the image exactly:
     - `patch_margin_left: [X]px`
     - `patch_margin_top: [X]px`
     - `patch_margin_right: [X]px`
     - `patch_margin_bottom: [X]px`
5. **Set Axis Stretch Properties**:
   - `axis_stretch_horizontal`: `Tile Fit` (enum value `2` in Godot 4)
   - `axis_stretch_vertical`: `Tile Fit` (enum value `2` in Godot 4)
   - *Why Tile Fit*: Ensures the borders scale without blurry pixel distortion or arbitrary elongation.
6. **Set Mouse Filter**:
   - On the `NinePatchRect`, set `mouse_filter = 1` (`MOUSE_FILTER_PASS`) or `2` (`MOUSE_FILTER_IGNORE`) so it doesn't unintentionally block background inputs unless intended as a modal blocker.
7. **Add MarginContainer as Direct Child**:
   - Add a `MarginContainer` as a **direct child** of the `NinePatchRect`.
   - Set its layout to Full Rect (`anchors_preset = 15`).
   - Configure theme margin overrides (`theme_override_constants/margin_*`) with values **greater than or equal to the patch margins** (e.g. if patch margin is 24px, use 28px margins).
   - This guarantees all text, icons, and buttons stay strictly inside the un-distorted center area without clipping into decorative corner brackets or borders.
   - **Header Clearance for Window Controls**: When titlebar controls (such as a top-right `CloseButton` at Y: 10..34) are anchored to the panel, increase `margin_top` (e.g. `44px`) so interior header rows (such as titles and action buttons like `FocusButton`) start safely below the close button with generous vertical clearance, preventing any visual collision.

---

## 3. Structural Architecture & Node Conventions

### Use Scene Unique Names (`%NodeName`)
To prevent fragile scripts that break whenever UI layout containers are reordered or reparented:
1. Enable **Scene Unique Name** on every node referenced by script:
   - In Godot Editor: Right-click node -> *Access as Unique Name* (or set `unique_name_in_owner = true` via MCP/code).
2. Retrieve nodes in C# using `%NodeName`:
   ```csharp
   _nameLabel = GetNodeOrNull<Label>("%NameLabel");
   _focusButton = GetNodeOrNull<Button>("%FocusButton");
   _popProgressBar = GetNodeOrNull<ProgressBar>("%PopProgressBar");
   ```
3. **NEVER** use hardcoded relative path strings like:
   ```csharp
   // AVOID: Fragile and breaks when container structure changes
   GetNodeOrNull<Label>("ContentMargin/MainVBox/HeaderHBox/NameLabel");
   ```

### Flow & Expansion
- Use `HBoxContainer` and `VBoxContainer` with `theme_override_constants/separation` (4–8px).
- Use `SizeFlagsHorizontal = SizeFlags.ExpandFill` on expanding labels/spacers.
- Wrap variable-length elements (e.g. improvements, facilities, event logs) in a `ScrollContainer` with `SizeFlagsVertical = SizeFlags.ExpandFill`.

---

## 4. UI Lifecycle & State Management

### Single-Instance Windows
For informational dialogs and inspection panels (like `StarInfoBox`):
- Only one panel should be active at a time to prevent stacking windows.
- Maintain a static instance reference (`public static StarInfoBox Instance { get; private set; }`).
- When a user selects another entity, call the open method to re-target the existing window rather than spawning duplicate instances:
  ```csharp
  public static StarInfoBox OpenForStar(Star star, Node contextNode)
  {
      if (Instance != null && GodotObject.IsInstanceValid(Instance))
      {
          Instance.DisplayStar(star);
          return Instance;
      }
      // Otherwise instantiate and add to tree...
  }
  ```

### Screen-Space CanvasLayer
- Always attach HUD windows and popups under a `CanvasLayer` (e.g. `galaxy_spawner.tscn`'s `CanvasLayer`), not raw 2D world nodes. This prevents the UI from scrolling or zooming with the `Camera2D`.

### Real-Time Metric Updates
- Panels displaying dynamic simulation data (population growth, resource income) should refresh in `_Process(double delta)` or on simulation tick events, ensuring numbers feel alive and reactive.

---

## 5. Scene Modification Guardrails
- **NEVER** edit `.tscn`, `.tres`, or `.uid` files directly as raw text.
- Use `godot-ai` MCP tools (`node_create`, `node_set_property`, `batch_execute`, `scene_save`) to ensure schema integrity and prevent engine corruption.

# Rule: Documentation-First Navigation & Subsystem Sync

This rule formalizes how the agent navigates, updates, and expands the **Galcon Empire** documentation hierarchy anchored at [gemini.md](../../gemini.md).

---

## 1. Documentation-First Navigation (On Start / Restart)

Before broadly searching or parsing game source files for a feature request or bug fix:
1. Check the **Subsystem Routing Index** in [gemini.md](../../gemini.md) to identify the owning subsystem(s).
2. Read the relevant `.agent/subsystems/<subsystem>.md` document(s) first to pinpoint exact file paths, classes, signals, Blackboard keys, resource schemas, and test suites.
3. Open only the targeted source files needed for the task instead of scanning the entire codebase.

---

## 2. Mandatory Documentation Sync After Code Changes

Whenever a task modifies gameplay systems, architecture, or assets, update the documentation **before** finishing the task:

| Type of Change | Required Documentation Update |
| :--- | :--- |
| Modified mechanics, formulas, signals, queues, or resource rules | Update the owning `.agent/subsystems/<subsystem>.md` file. |
| Added/changed GOAP Goals, Actions, priorities, or Blackboard keys | Update [.agent/subsystems/ai_and_goap.md](../subsystems/ai_and_goap.md). |
| Added new `.tres` blueprints (improvements, ships) or `ResourceType`/`ShipAbility` values | Update the owning subsystem doc's asset/enum table. |
| Added, renamed, or deleted scripts, managers, autoloads, or test suites | Update the owning subsystem doc **and** the **Codebase Anatomy** / **Subsystem Routing Index** in [gemini.md](../../gemini.md). |
| Introduced a brand-new subsystem (e.g., Technology Tree, Diplomacy, Combat Resolver) | Create `.agent/subsystems/<new_subsystem>.md` using the standard template below and add it to [gemini.md](../../gemini.md). |
| Modified the portable Chronicle component (`Modules/Chronicle/`) | Follow [chronicle_component.md](chronicle_component.md): sync `Modules/Chronicle/README.md`, `Modules/Chronicle/AGENT_INTEGRATION.md`, and [.agent/subsystems/chronicle_and_persistence.md](../subsystems/chronicle_and_persistence.md). |

*Skip documentation edits only for purely internal refactors or bug fixes that do not alter file layout, data contracts, Blackboard keys, rules, or documented behavior.*

---

## 3. Standard Subsystem Document Template

Every file under `.agent/subsystems/<subsystem>.md` must follow this concise, agent-scannable structure:

1. **Key Files & Responsibilities**: Markdown table mapping relative markdown links (never `file://` or absolute machine paths) to classes/assets and single-sentence roles.
2. **Core Flow / Data Contracts**: Tick order, state machines, schemas, or formulas needed to reason about the system without reading every line of code.
3. **Key Invariants & Pitfalls**: Constraints and gotchas that must not be broken (e.g., non-stockpileable resources, no `WaitAction` in live GOAP).
4. **Common Extension Recipes**: Concrete 1–3 step instructions for the most frequent future additions.
5. **Related Tests**: Clickable links to the GdUnit4 test suites covering the subsystem.

---

## 4. Keep `gemini.md` Lean (Context Budget Guardrail)

- [gemini.md](../../gemini.md) is auto-loaded on every turn and is subject to the 24 KB per-file rule limit.
- Keep `gemini.md` strictly as the **primary entry point, routing index, and non-negotiable rules**.
- Put detailed schemas, formulas, Blackboard key catalogs, and extension recipes inside `.agent/subsystems/*.md`, not inline in `gemini.md`.

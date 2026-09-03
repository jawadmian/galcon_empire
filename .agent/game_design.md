# Game Design Document — Galcon Empire

## 🌌 High Concept
**Galcon Empire** is a deep, systemic **"Galactic Ant-Farm"** simulation where the player acts as an incorporeal observer and shaper, watching centuries of history unfold in minutes through the interactions of ambitious leaders and sprawling star empires.

Unlike traditional 4X games that mandate high-micro management of every ship and building, the galaxy runs on an autonomous, systemic simulation. The player guides the overarching destiny of the galaxy through indirect influence, systemic triggers, and cosmic interventions.

---

## 🔄 Core Gameplay Loop

### 1. Observe
- Watch star systems flourish, populations expand, fleets traverse the void, and leaders pursue their ambitions.
- Analyze economic bottlenecks, resource disparities, ideological fractures, and rising geopolitical tensions across star empires.
- We want an emergent narrative that unfolds through the simulation. The player should feel like they are watching a story play out. The observation should be faciliated by filterable logs, and UI charts. Each entity in the game should have a historic log and display of it's progress
   - For example, clicking on a leader should allow you to see major events in their life.
   - Clicking on a star system should allow you to see the history of the system, including all the planets, and improvements that have been built there, how it has grown over time.

### 2. Influence
- Rather than directly issuing orders to ships or planets, subtly nudge events:
  - Sway or undermine empire leaders.
  - Fund insurgent factions or bolster loyalists.
  - Trigger economic booms, resource windfalls, or crisis events.
  - Alter planetary conditions or cosmic anomalies.

### 3. Evolve
- Empires rise, fragment, and fall over galactic eras.
- New technologies emerge, borders shift dynamically, and cultural ideologies clash.
- Shape the long-term historical trajectory of the galaxy toward harmony, hegemony, or cosmic collapse.

---

## ⚙️ Core Simulation Mechanics

### The Economic Triad
All civilizational development hinges on three fundamental resources:
| Resource | Primary Role | Sinks / Requirements |
| :--- | :--- | :--- |
| **Food** | Sustains populations and drives demographic growth | Consumed per capita each tick; starvation triggers unrest |
| **Ore** | Raw industrial material for construction | Required to construct planetary improvements and fleets |
| **Money** | Currency lubricating commerce, wages, and statecraft | Upkeep for facilities, diplomatic actions, and trade |

### Planetary Improvements
Planetary infrastructure installed on stars to specialize worlds:
- **Farm / Agricultural Biosphere**: Generates high food output.
- **Mine / Asteroid Refinery**: Extracts raw ore.
- **Factory / Industrial Complex**: Converts raw materials into advanced goods and money.
- **Research Station / Laboratory** *(Planned)*: Generates scientific breakthroughs.

### Population & Demographics ("Pops")
- Stars support populations measured in discrete "Pops".
- Future iterations will introduce stratified pop classes (Workers, Specialists, Rulers) with educational requirements and happiness states sensitive to local economic stability.

### Automated Advisors
- Each empire utilizes an advisor (`IAdvisor`) evaluating internal shortages, security risks, and growth opportunities.
- Advisors dynamically queue construction requests when resources permit, giving each empire an emergent personality.

---

## 🗺️ Active Feature Roadmap

Derived from current backlog and active planning:
1. **Star System Inspector**:
   - Display installed improvements in real-time on the Star info panel.
   - Show local population growth rate and per-tick yields.
2. **Empire Management UI**:
   - Interactive navigation: click empire names in `EmpiresList` to focus the camera and open the Empire detail view.
   - Dedicated Empire detail panel showing real-time resource stockpiles, net income deltas, controlled star systems, and active build queues.
3. **Stratified Pops System**:
   - Model individual pop units with happiness, education levels, and resource demands.
4. **Fleets & Territorial Expansion**:
   - Autonomous ship construction, trade routes between colonized systems, and military expeditions to claim unaligned stars.

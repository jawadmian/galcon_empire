# GalconEmpire AI System Design Document

## 1. Overview
This document outlines the high-level design for the Empire AI in GalconEmpire. The AI is responsible for managing empire resources, expanding to new stars, building fleets, and executing combat strategies. 

To ensure future flexibility, the AI system is designed as a **pluggable subsystem**. While our initial implementation will utilize **Goal-Oriented Action Planning (GOAP)**, the architecture will allow swapping out the decision-making engine for Hierarchical Task Networks (HTN), Utility AI, or other paradigms with minimal refactoring.

## 2. Pluggable Architecture

The core of the pluggable system is the `IAdvisor` or `IAIController` interface. The `Empire` class will not know about GOAP or any specific implementation; it will simply ask its active advisor(s) for the next actions.

```csharp
public interface IAdvisor
{
    // Evaluates the current game state and queues actions/commands for the Empire.
    void Recommend(Empire empire);
}
```

We can have a `GoapAdvisor` that implements this interface. In the future, switching to an `HtnAdvisor` is as simple as injecting a different class into the `Empire`.

## 3. World State Representation: Blackboard Architecture

For any AI model to work, it needs a simplified representation of the game world. We will use a **Blackboard Architecture** instead of a simple key-value dictionary to represent the world state.

### Key-Value Pair vs. Blackboard Architecture

**Simple Key-Value Pair Dictionary:**
- *Pros*: Very easy to implement. Fast lookup for state values (e.g., `State["HasIron"] == true`).
- *Cons*: Flat structure. Doesn't scale well when you have multiple agents (Empire vs System Governor) needing different contexts or shared memory. Hard to represent complex objects (like "List of threatened stars").

**Blackboard Architecture:**
- *Pros*: Acts as a shared memory hub where different AI components (Empire Planner, System Governors, Military Commanders) can read from and write to. It supports hierarchical planning because a high-level Empire AI can write a goal to the Blackboard (e.g., "Defend Sector 7"), and a subordinate Military AI reads that and forms its own GOAP plan to execute it. Easily supports "Personalities" by having different agents interpret blackboard data differently.
- *Cons*: More complex to set up. Requires a robust system to manage reading/writing, and potentially event subscriptions.

**Decision**: We will proceed with a **Blackboard Architecture** because we plan to support multiple levels of planning (Empire, Governor, Commander) and personalities.

**Key Blackboard Entries:**
- `Global.ResourceStockpiles`
- `Global.ProductionRates`
- `Military.ThreatenedStars` (List of stars)
- `Command.HighLevelDirectives` (Goals set by the Empire for Governors)

## 4. GOAP Implementation Details

GOAP works by defining **Goals** (desired states) and **Actions** (things the AI can do to change states). A Planner then strings Actions together to reach a Goal.

### 4.1. Goals
The AI will evaluate various goals and pick the one with the highest priority/relevance at any given time.
- **Expand Economy**: Goal state `ProductionRate > X`
- **Build Military**: Goal state `MilitaryPower > Y`
- **Defend Territory**: Goal state `ThreatLevel == 0`
- **Conquer System**: Goal state `OwnsTargetStar == true`

### 4.2. Actions
Actions require **Preconditions** (state required to execute) and provide **Effects** (state changes after execution).

- **Action: Build Mine**
  - *Preconditions*: `HasEnoughIron`, `HasTargetStar`
  - *Effects*: `IncreasesIronProduction`
- **Action: Build Fleet**
  - *Preconditions*: `HasShipyard`, `HasEnoughResources`
  - *Effects*: `IncreasesMilitaryPower`
- **Action: Move Fleet to Attack**
  - *Preconditions*: `HasFleet`, `TargetIsHostile`
  - *Effects*: `OwnsTargetStar`, `ReducesThreatLevel`

### 4.3. The Planner
A standard A* search algorithm will be used by the GOAP engine. Starting from the current `WorldState`, it will search backwards from the desired Goal state, linking Action Effects to Preconditions until a valid plan is formed.

## 5. Fleet Movement and Combat Integration

Even though actual combat might be handled by simple math or a separate simulation system, the *planning* of combat must be integrated into the AI.

**Abstracting Combat for the AI:**
- We represent fleets with a `CombatPower` metric.
- Stars have a `DefensePower` metric (from stationed fleets or defense platforms).
- The action **"Conquer Star"** requires the precondition: `EmpireCombatPower > TargetDefensePower + Buffer`.
- If this precondition is not met, the planner will insert the **"Build Fleet"** action before the **"Conquer Star"** action.

## 6. Development Roadmap (Phases)

**Phase 1: Foundation & Economy**
- Implement the pluggable `IAdvisor` architecture.
- Create the core GOAP engine (Planner, Blackboard, Actions, Goals).
- **Bootstrap Actions**: We will implement the following initial actions for the planner to demonstrate action chaining:
  - `Action_BuildResourceExtractor`: Preconditions (Has target star, requires X resources). Effects (Increases specific resource production).
  - `Action_Wait`: Preconditions (None). Effects (Advances time / Allows resources to tick up). Crucial for when the AI needs to save up resources for a build.
  - `Action_BuildShipyard`: Preconditions (Has target star, requires X resources). Effects (Has Shipyard, Enables ship building).
  - `Action_BuildColonyShip`: Preconditions (Has Shipyard, requires Y resources). Effects (Has Colony Ship).
  - `Action_Colonize`: Preconditions (Has Colony Ship, sees unowned star). Effects (Increases owned stars, expands territory, consumes Colony Ship).
- Implement basic economic goals (e.g., `Goal_GrowEconomy` which pushes the AI to balance and grow its resource income).
- **Testing**: Add Unit Tests for the GOAP Planner (ensure it finds valid paths) and Blackboard (ensure thread-safe reads/writes).

**Phase 2: Expansion & Fleets**
- Add fleet building actions.
- Add goals for expansion (colonizing empty stars).
- **Testing**: Add integration tests verifying an AI can successfully queue a Colony Ship and expand to an adjacent star.

**Phase 3: Combat Planning**
- Introduce enemy threat detection to the World State.
- Implement defense and conquest goals.
- Integrate fleet movement commands into the `Empire` system.
- **Testing**: Add combat simulation tests verifying AI responds to threats by building military and dispatching fleets.

## 7. Scale & Performance Considerations

Supporting large galaxies (100+ empires, 500+ stars, hundreds of active agents) requires strict performance management from the outset.

- **Time Slicing (`AIManager`)**: AI agents will not update every game tick. An `AIManager` will distribute agent updates across multiple frames/seconds. Agents will only reconsider their plans periodically (e.g., every 5-30 seconds depending on game speed) to prevent CPU spikes.
- **Multithreading**: The GOAP A* planner will run on background threads using C# `Task`s. This prevents the main Godot thread from freezing when hundreds of agents are planning simultaneously.
- **Thread-Safe Data Structures**: Because agents plan asynchronously, the Blackboard must utilize thread-safe collections (e.g., `ConcurrentDictionary`) to prevent race conditions during reads/writes.
- **Object Pooling (Recommendation)**: To prevent massive Garbage Collection (GC) stutters during planning, we should implement Object Pooling for `GoapAction` nodes and `WorldState` snapshots used in the A* search tree.
- **Spatial Caching (Recommendation)**: Expensive queries like "find the closest uncolonized star" should be cached or use spatial partitioning (e.g., QuadTrees). Agents should not iterate over all 500 stars every time they evaluate the `Action_Colonize` preconditions.

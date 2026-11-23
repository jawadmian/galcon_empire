using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GoapAdvisor : IAdvisor
{
    private Empire _empire;
    public Blackboard State { get; private set; } = new Blackboard();
    public List<GoapAction> AvailableActions { get; private set; } = new List<GoapAction>();
    public List<GoapGoal> Goals { get; private set; } = new List<GoapGoal>();

    private GoapPlanner _planner = new GoapPlanner();
    private Queue<GoapAction> _currentPlan;
    
    public bool IsPlanning { get; private set; } = false;

    public GoapAdvisor(Empire empire)
    {
        _empire = empire;
        
        // Dynamically add BuildImprovementAction for every loaded improvement
        if (EmpireManager.HasInstance)
        {
            foreach (var improvement in EmpireManager.Instance.AvailableImprovements)
            {
                AvailableActions.Add(new BuildImprovementAction(improvement));
            }
        }

        // Dynamically add BuildColonyShipAction for every ship design with ShipAbility.Colonize
        bool addedColonyShipAction = false;
        if (FleetManager.HasInstance)
        {
            foreach (var design in FleetManager.Instance.GetDesignsWithAbility(ShipAbility.Colonize))
            {
                AvailableActions.Add(new BuildColonyShipAction(design));
                addedColonyShipAction = true;
            }
        }
        if (!addedColonyShipAction)
        {
            AvailableActions.Add(new BuildColonyShipAction());
        }

        AvailableActions.Add(new ColonizeAction());

        // Add goals: shipyard construction, expansion, and stockpileable resource growth
        Goals.Add(new BuildShipyardGoal());
        Goals.Add(new ColonizeStarGoal());

        // Dynamically create a GrowResourceGoal for every stockpileable ResourceType
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type.IsStockpileable())
            {
                Goals.Add(new GrowResourceGoal(type, 15)); // Target production of 15
            }
        }
        // Register with AIManager for time-slicing
        if (AIManager.HasInstance)
        {
            AIManager.Instance.RegisterAdvisor(this);
        }
        else
        {
            GD.PushWarning("AIManager is not in the scene tree. GoapAdvisor will not receive UpdateAI ticks.");
        }
    }

    /// <summary>
    /// Called by the Empire on every game tick. 
    /// We use this purely to update our "sensors" (the Blackboard state).
    /// </summary>
    public void Recommend(Empire empire)
    {
        UpdateSensors();
    }

    private void UpdateSensors()
    {
        foreach (var kvp in _empire.ResourceStockpiles)
        {
            State.SetValue($"Stockpile_{kvp.Key}", kvp.Value);
        }
        foreach (var kvp in _empire.ProductionPerTick)
        {
            State.SetValue($"Production_{kvp.Key}", kvp.Value);
        }
        State.SetValue("OwnedStarsCount", _empire.OwnedStars.Count);

        bool hasShipyard = _empire.HasShipyard();
        int shipyardCapacity = 0;
        foreach (var star in _empire.OwnedStars)
        {
            if (star.ProductionPerTick.TryGetValue(ResourceType.ShipyardProduction, out int spp))
            {
                shipyardCapacity += spp;
            }
        }
        State.SetValue("HasShipyard", hasShipyard);
        State.SetValue("ShipyardProduction", shipyardCapacity);

        bool hasColonyShip =
            _empire.HasFleetWithAbility(ShipAbility.Colonize)
            || _empire.HasShipPending(ShipAbility.Colonize);
        bool hasIdleColonyShip = _empire.GetIdleFleetsWithAbility(ShipAbility.Colonize).Count > 0;
        bool hasUnownedStars =
            StarManager.HasInstance
            && StarManager.Instance.GetNearestUnownedStar(
                _empire.HomeStar?.WorldPosition ?? Vector2.Zero,
                _empire
            ) != null;

        State.SetValue("HasColonyShip", hasColonyShip);
        State.SetValue("HasIdleColonyShip", hasIdleColonyShip);
        State.SetValue("HasUnownedStars", hasUnownedStars);
    }

    /// <summary>
    /// Called by the AIManager periodically (Time Slicing).
    /// This prevents 100 AIs from all planning on the exact same frame.
    /// </summary>
    public void UpdateAI()
    {
        if (IsPlanning) return;

        if (_currentPlan != null && _currentPlan.Count > 0)
        {
            // Execute the next action in the plan
            var action = _currentPlan.Peek();
            
            // Re-check preconditions before executing to ensure the plan hasn't been invalidated
            if (action.CheckPreconditions(State))
            {
                bool actionCompleted = action.Perform(_empire, State);
                if (actionCompleted)
                {
                    // Action is fully executed, remove it from the queue
                    _currentPlan.Dequeue(); 
                }
                // If actionCompleted is false, it might be a continuous action like WaitAction
            }
            else
            {
                // Plan invalidated (e.g. resources stolen, target star destroyed)
                GD.Print($"GoapAdvisor for {_empire.EmpireName}: Plan invalidated. Action {action.GetType().Name} failed preconditions.");
                _currentPlan = null; 
            }
        }
        else
        {
            // No valid plan, formulate a new one
            FormulatePlanAsync();
        }
    }

    private async void FormulatePlanAsync()
    {
        IsPlanning = true;
        try
        {
            // Create a snapshot of the current state for the background thread
            var planningState = State.Clone();

            // 1. Collect all valid, unachieved goals and sort by priority descending
            var candidateGoals = new List<GoapGoal>();
            foreach (var goal in Goals)
            {
                goal.UpdatePriority(planningState);
                if (goal.IsValid(planningState) && !goal.IsAchieved(planningState) && goal.Priority > 0f)
                {
                    candidateGoals.Add(goal);
                }
            }
            candidateGoals.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // 2. Try planning goals in priority order so an unaffordable top goal
            //    gracefully falls back to actionable economic/expansion goals
            foreach (var goal in candidateGoals)
            {
                Queue<GoapAction> plan = await Task.Run(() => _planner.Plan(planningState, goal, AvailableActions));

                if (plan != null && plan.Count > 0)
                {
                    _currentPlan = plan;
                    GD.Print($"GoapAdvisor for {_empire.EmpireName}: Found plan for {goal.GetType().Name} with {plan.Count} steps.");
                    break;
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"GoapAdvisor for {_empire.EmpireName}: Exception in FormulatePlanAsync: {ex}");
        }
        finally
        {
            IsPlanning = false;
        }
    }
}

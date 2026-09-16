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
        // Bootstrap actions and goals
        AvailableActions.Add(new WaitAction());
        
        // Dynamically add BuildImprovementAction for every loaded improvement
        if (EmpireManager.HasInstance)
        {
            foreach (var improvement in EmpireManager.Instance.AvailableImprovements)
            {
                AvailableActions.Add(new BuildImprovementAction(improvement));
            }
        }

        // Add bootstrap actions
        AvailableActions.Add(new BuildShipyardAction());
        AvailableActions.Add(new BuildColonyShipAction());
        AvailableActions.Add(new ColonizeAction());

        // Dynamically create a GrowResourceGoal for every ResourceType
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            Goals.Add(new GrowResourceGoal(type, 15)); // Target production of 15
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
        
        // In the future, we would also update threatened stars, etc. here
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

            GoapGoal topGoal = null;
            float highestPriority = -1f;

            // 1. Pick the highest priority goal that is valid and not yet achieved
            foreach (var goal in Goals)
            {
                goal.UpdatePriority(planningState);
                if (goal.IsValid(planningState) && !goal.IsAchieved(planningState))
                {
                    if (goal.Priority > highestPriority)
                    {
                        highestPriority = goal.Priority;
                        topGoal = goal;
                    }
                }
            }

            if (topGoal != null)
            {
                // 2. Offload the heavy A* pathfinding to a background thread
                Queue<GoapAction> plan = await Task.Run(() => _planner.Plan(planningState, topGoal, AvailableActions));
                
                if (plan != null && plan.Count > 0)
                {
                    _currentPlan = plan;
                    GD.Print($"GoapAdvisor for {_empire.EmpireName}: Found plan for {topGoal.GetType().Name} with {plan.Count} steps.");
                }
                else
                {
                    // Silently wait for resources to accumulate instead of spamming logs
                    // GD.Print($"GoapAdvisor for {_empire.EmpireName}: Failed to find a plan for {topGoal.GetType().Name}.");
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

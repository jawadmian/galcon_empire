using Godot;

public class GrowResourceGoal : GoapGoal
{
    private ResourceType _targetResource;
    private long _targetProduction;

    public GrowResourceGoal(ResourceType resourceType, long targetProduction)
    {
        _targetResource = resourceType;
        _targetProduction = targetProduction;
        Priority = 1.0f;
    }

    public override bool IsValid(Blackboard currentBlackboard)
    {
        return true; 
    }

    public override bool IsAchieved(Blackboard state)
    {
        long currentProduction = state.GetValue<long>($"Production_{_targetResource}", 0);
        return currentProduction >= _targetProduction;
    }

    public override void UpdatePriority(Blackboard currentBlackboard)
    {
        long currentProduction = currentBlackboard.GetValue<long>($"Production_{_targetResource}", 0);
        
        // If we've reached our target, bump it up so the AI never stops growing!
        if (currentProduction >= _targetProduction)
        {
            _targetProduction = currentProduction + 15;
        }

        // Dynamic priority scaling based on how far behind we are
        if (currentProduction < _targetProduction * 0.25f)
        {
            Priority = 3.0f; // Critical
        }
        else if (currentProduction < _targetProduction * 0.5f)
        {
            Priority = 2.0f; // High
        }
        else if (currentProduction < _targetProduction)
        {
            Priority = 1.0f; // Normal
        }
        else
        {
            Priority = 0.1f; 
        }
    }
}

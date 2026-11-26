using System;

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
        int availableSlots = currentBlackboard.GetValue<int>("AvailableBuildingSlots", 1);
        if (availableSlots > 0)
            return true;

        // When all building slots are full, only allow scrapping a facility to make room
        // for critical Food survival (prevents flip-flopping between Ore/Money facilities).
        if (_targetResource == ResourceType.Food)
        {
            long foodProd = currentBlackboard.GetValue<long>($"Production_{ResourceType.Food}", 0);
            bool canScrap = currentBlackboard.GetValue<bool>("HasScrappableImprovement", false);
            return canScrap && foodProd <= 0;
        }

        return false;
    }

    public override bool IsAchieved(Blackboard state)
    {
        long currentProduction = state.GetValue<long>($"Production_{_targetResource}", 0);
        return currentProduction >= _targetProduction;
    }

    public override void UpdatePriority(Blackboard currentBlackboard)
    {
        long currentProduction = currentBlackboard.GetValue<long>($"Production_{_targetResource}", 0);

        // If we've reached our target, bump it up so the AI keeps developing
        if (currentProduction >= _targetProduction)
        {
            _targetProduction = currentProduction + 15;
        }

        if (_targetResource == ResourceType.Food)
        {
            // Food is consumed by population: urgent when net food <= 0, low priority once in healthy surplus
            if (currentProduction <= 0)
            {
                Priority = 3.0f; // Critical: prevent stagnation/deficit
            }
            else if (currentProduction < 200)
            {
                Priority = 1.4f; // Moderate: build buffer for growing colonies
            }
            else
            {
                Priority = 0.4f; // Surplus: avoid over-indexing on Farms
            }
            return;
        }

        // For Ore & Money, scale priority inversely with current production so whichever
        // resource is lagging naturally takes precedence (preventing single-resource spam).
        float clampedProd = Math.Max(0L, currentProduction);
        Priority = 1.0f + (1.5f / (1.0f + (clampedProd / 1000.0f)));
    }
}

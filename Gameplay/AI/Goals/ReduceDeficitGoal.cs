using System;

/// <summary>
/// Prompts the GOAP planner to reduce unsustainable upkeep (by scrapping ships or facilities)
/// when any stockpileable resource is running a net per-tick deficit due to maintenance costs.
/// </summary>
public class ReduceDeficitGoal : GoapGoal
{
    public ReduceDeficitGoal()
    {
        Priority = 0.0f;
    }

    public override bool IsValid(Blackboard currentBlackboard)
    {
        bool hasScrappable =
            currentBlackboard.GetValue<bool>("HasScrappableShip", false)
            || currentBlackboard.GetValue<bool>("HasScrappableImprovement", false);
        if (!hasScrappable)
            return false;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!type.IsStockpileable())
                continue;

            long netProd = currentBlackboard.GetValue<long>($"Production_{type}", 0);
            long upkeep = currentBlackboard.GetValue<long>($"TotalUpkeep_{type}", 0);
            if (netProd < 0 && upkeep > 0)
                return true;
        }

        return false;
    }

    public override bool IsAchieved(Blackboard state)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!type.IsStockpileable())
                continue;

            long netProd = state.GetValue<long>($"Production_{type}", 0);
            long upkeep = state.GetValue<long>($"TotalUpkeep_{type}", 0);
            if (netProd < 0 && upkeep > 0)
                return false;
        }

        return true;
    }

    public override void UpdatePriority(Blackboard currentBlackboard)
    {
        if (!IsValid(currentBlackboard))
        {
            Priority = 0.0f;
            return;
        }

        bool criticalDeficit = false;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!type.IsStockpileable())
                continue;

            long netProd = currentBlackboard.GetValue<long>($"Production_{type}", 0);
            long upkeep = currentBlackboard.GetValue<long>($"TotalUpkeep_{type}", 0);
            long stockpile = currentBlackboard.GetValue<long>($"Stockpile_{type}", 0);
            if (netProd < 0 && upkeep > 0 && stockpile < Math.Abs(netProd) * 3)
            {
                criticalDeficit = true;
                break;
            }
        }

        Priority = criticalDeficit ? 3.2f : 0.8f;
    }
}

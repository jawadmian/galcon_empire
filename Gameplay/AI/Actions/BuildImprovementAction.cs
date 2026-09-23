using Godot;
using System.Collections.Generic;

public class BuildImprovementAction : GoapAction
{
    private ImprovementResource _improvement;

    public BuildImprovementAction(ImprovementResource improvement)
    {
        _improvement = improvement;
        // Cost is based on build time, providing a natural heuristic
        Cost = improvement.BuildTime > 0 ? improvement.BuildTime : 1.0f; 
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        // 1. Must own at least one star
        int ownedStars = state.GetValue<int>("OwnedStarsCount", 0);
        if (ownedStars <= 0) return false;

        // 2. Check if we have enough resources
        foreach (var cost in _improvement.BuildCost)
        {
            long currentStockpile = state.GetValue<long>($"Stockpile_{cost.Key}", 0);
            if (currentStockpile < cost.Value)
            {
                return false;
            }
        }
        
        return true;
    }

    public override void ApplyEffects(Blackboard state)
    {
        // 1. Deduct costs
        foreach (var cost in _improvement.BuildCost)
        {
            long currentStockpile = state.GetValue<long>($"Stockpile_{cost.Key}", 0);
            state.SetValue($"Stockpile_{cost.Key}", currentStockpile - cost.Value);
        }

        // 2. Apply production boosts
        foreach (var output in _improvement.ResourceOutput)
        {
            long currentProduction = state.GetValue<long>($"Production_{output.Key}", 0);
            state.SetValue($"Production_{output.Key}", currentProduction + output.Value);
            if (output.Key == ResourceType.ShipyardProduction && output.Value > 0)
            {
                state.SetValue("HasShipyard", true);
            }
        }
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        if (empire.OwnedStars.Count > 0)
        {
            // For now, always queue on the first owned star (usually HomeStar)
            Star target = empire.OwnedStars[0];
            empire.QueueImprovement(_improvement, target);
            GD.Print($"Empire {empire.EmpireName} queued improvement: {_improvement.ImprovementName}");
            return true;
        }
        return false;
    }
}

using Godot;
using System;
using System.Collections.Generic;

public class BasicAdvisor : IAdvisor
{
    private int _tickCooldown = 10; // Only check every 10 ticks to avoid flooding
    private int _ticksSinceLastRecommendation = 0;

    public void Recommend(Empire empire)
    {
        _ticksSinceLastRecommendation++;
        if (_ticksSinceLastRecommendation < _tickCooldown)
            return;

        // 1. Identify resource with lowest production
        ResourceType lackingResource = ResourceType.Food;
        long minProduction = long.MaxValue;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (empire.ProductionPerTick[type] < minProduction)
            {
                minProduction = empire.ProductionPerTick[type];
                lackingResource = type;
            }
        }

        // 2. Find an improvement that produces this resource
        var availableImprovements = EmpireManager.Instance.AvailableImprovements;
        ImprovementResource bestImprovement = null;
        int maxOutput = 0;

        foreach (var improvement in availableImprovements)
        {
            if (improvement.ResourceOutput.TryGetValue(lackingResource, out int output))
            {
                if (output > maxOutput)
                {
                    maxOutput = output;
                    bestImprovement = improvement;
                }
            }
        }

        if (bestImprovement == null)
            return;

        // 3. Find a star to build it on
        if (empire.OwnedStars.Count > 0)
        {
            Star targetStar = empire.HomeStar ?? empire.OwnedStars[0];
            GD.Print(
                $"Advisor recommends {bestImprovement.ImprovementName} for {empire.EmpireName} at {targetStar.StarName} because of low {lackingResource} production."
            );
            empire.QueueImprovement(bestImprovement, targetStar);
            _ticksSinceLastRecommendation = 0;
        }
    }
}

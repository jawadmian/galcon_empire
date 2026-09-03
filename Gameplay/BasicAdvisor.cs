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

        // 1. Sort resources by production (lowest first)
        List<ResourceType> resourcesByProduction = new List<ResourceType>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resourcesByProduction.Add(type);
        }
        resourcesByProduction.Sort(
            (a, b) => empire.ProductionPerTick[a].CompareTo(empire.ProductionPerTick[b])
        );

        // 2. Find a star to build on
        if (empire.OwnedStars.Count == 0)
            return;
        Star targetStar = empire.HomeStar ?? empire.OwnedStars[0];

        // 3. Try to find an improvement for the most lacking resources in order
        if (EmpireManager.Instance == null)
        {
            GD.PrintErr("BasicAdvisor: EmpireManager.Instance is null! Cannot recommend improvements.");
            return;
        }
        var availableImprovements = EmpireManager.Instance.AvailableImprovements;

        foreach (ResourceType lackingResource in resourcesByProduction)
        {
            ImprovementResource bestImprovement = null;
            int maxOutput = 0;

            foreach (var improvement in availableImprovements)
            {
                if (improvement.ResourceOutput.TryGetValue(lackingResource, out int output))
                {
                    // Check if this improvement is already pending on this star
                    if (empire.IsImprovementPending(improvement, targetStar))
                        continue;

                    if (output > maxOutput)
                    {
                        maxOutput = output;
                        bestImprovement = improvement;
                    }
                }
            }

            if (bestImprovement != null)
            {
                GD.Print(
                    $"Advisor recommends {bestImprovement.ImprovementName} for {empire.EmpireName} at {targetStar.StarName} because of low {lackingResource} production (and it's not already pending)."
                );
                empire.QueueImprovement(bestImprovement, targetStar);
                _ticksSinceLastRecommendation = 0;
                return;
            }
        }
    }
}

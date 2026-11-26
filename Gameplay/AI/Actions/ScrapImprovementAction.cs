using System;

/// <summary>
/// GOAP action that scraps the empire's most expensive installed facility to relieve upkeep costs.
/// </summary>
public class ScrapImprovementAction : GoapAction
{
    public ScrapImprovementAction()
    {
        // High cost prevents flip-flopping between facility types and ensures open slots
        // or ship scrapping are always preferred before tearing down existing infrastructure.
        Cost = 12.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        return state.GetValue<bool>("HasScrappableImprovement", false)
            && state.GetValue<int>("InstalledImprovementsCount", 0) > 0;
    }

    public override void ApplyEffects(Blackboard state)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!type.IsStockpileable())
                continue;

            long upkeepRelief = state.GetValue<long>($"ScrappableImprovementUpkeep_{type}", 0);
            long outputLost = state.GetValue<long>($"ScrappableImprovementOutput_{type}", 0);
            long netDelta = upkeepRelief - outputLost;
            if (netDelta != 0)
            {
                long currentProd = state.GetValue<long>($"Production_{type}", 0);
                state.SetValue($"Production_{type}", currentProd + netDelta);
            }
            if (upkeepRelief > 0)
            {
                long totalUpkeep = state.GetValue<long>($"TotalUpkeep_{type}", 0);
                state.SetValue($"TotalUpkeep_{type}", Math.Max(0, totalUpkeep - upkeepRelief));
            }
        }

        int installed = Math.Max(0, state.GetValue<int>("InstalledImprovementsCount", 1) - 1);
        state.SetValue("InstalledImprovementsCount", installed);
        state.SetValue("HasScrappableImprovement", installed > 0);

        int slots = state.GetValue<int>("AvailableBuildingSlots", 0);
        state.SetValue("AvailableBuildingSlots", slots + 1);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        if (empire == null)
            return false;

        bool freeingSlot = state != null && state.GetValue<int>("AvailableBuildingSlots", 0) <= 0;
        var (star, improvement) = empire.GetMostExpensiveScrapCandidateImprovement(preferNonShipyard: freeingSlot);
        return star != null && improvement != null && empire.ScrapImprovement(star, improvement);
    }
}

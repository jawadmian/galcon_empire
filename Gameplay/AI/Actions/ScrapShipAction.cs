using System;

/// <summary>
/// GOAP action that scraps the empire's most expensive ship to relieve ongoing upkeep costs.
/// </summary>
public class ScrapShipAction : GoapAction
{
    public ScrapShipAction()
    {
        Cost = 3.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        return state.GetValue<bool>("HasScrappableShip", false)
            && state.GetValue<int>("ShipCount", 0) > 0;
    }

    public override void ApplyEffects(Blackboard state)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!type.IsStockpileable())
                continue;

            long relief = state.GetValue<long>($"ScrappableShipUpkeep_{type}", 0);
            if (relief > 0)
            {
                long currentProd = state.GetValue<long>($"Production_{type}", 0);
                state.SetValue($"Production_{type}", currentProd + relief);

                long totalUpkeep = state.GetValue<long>($"TotalUpkeep_{type}", 0);
                state.SetValue($"TotalUpkeep_{type}", Math.Max(0, totalUpkeep - relief));
            }
        }

        int shipCount = Math.Max(0, state.GetValue<int>("ShipCount", 1) - 1);
        state.SetValue("ShipCount", shipCount);
        state.SetValue("HasScrappableShip", shipCount > 0);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        if (empire == null)
            return false;

        Ship candidate = empire.GetMostExpensiveScrapCandidateShip();
        return candidate != null && empire.ScrapShip(candidate);
    }
}

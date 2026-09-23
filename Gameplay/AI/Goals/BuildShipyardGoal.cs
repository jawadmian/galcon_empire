using Godot;

/// <summary>
/// A GOAP goal that drives an empire to construct an Orbital Shipyard
/// once basic economic foundations are established.
/// </summary>
public class BuildShipyardGoal : GoapGoal
{
    public BuildShipyardGoal(float priority = 2.5f)
    {
        Priority = priority;
    }

    public override bool IsValid(Blackboard currentBlackboard)
    {
        // Valid as long as the empire owns at least one star
        int ownedStars = currentBlackboard.GetValue<int>("OwnedStarsCount", 0);
        return ownedStars > 0;
    }

    public override bool IsAchieved(Blackboard state)
    {
        return state.GetValue<bool>("HasShipyard", false) ||
               state.GetValue<long>($"Production_{ResourceType.ShipyardProduction}", 0) > 0;
    }

    public override void UpdatePriority(Blackboard currentBlackboard)
    {
        bool hasShipyard = currentBlackboard.GetValue<bool>("HasShipyard", false) ||
                           currentBlackboard.GetValue<long>($"Production_{ResourceType.ShipyardProduction}", 0) > 0;
        if (hasShipyard)
        {
            // Already has a shipyard or has one under construction
            Priority = 0f;
            return;
        }

        // Check if basic economic infrastructure exists (Ore and Money production)
        long oreProd = currentBlackboard.GetValue<long>($"Production_{ResourceType.Ore}", 0);
        long moneyProd = currentBlackboard.GetValue<long>($"Production_{ResourceType.Money}", 0);

        if (oreProd > 0 && moneyProd > 0)
        {
            // High priority: ready to build shipyard to unlock fleet capabilities
            Priority = 2.5f;
        }
        else
        {
            // Low priority: wait until basic resource producers are built
            Priority = 0.5f;
        }
    }
}

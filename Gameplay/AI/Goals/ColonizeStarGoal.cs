/// <summary>
/// A GOAP goal that drives an empire to expand by colonizing unowned stars.
/// </summary>
public class ColonizeStarGoal : GoapGoal
{
    private int _targetStarCount = 2;

    public ColonizeStarGoal()
    {
        Priority = 1.5f;
    }

    public override bool IsValid(Blackboard currentBlackboard)
    {
        int ownedStars = currentBlackboard.GetValue<int>("OwnedStarsCount", 0);
        bool hasUnownedStars = currentBlackboard.GetValue<bool>("HasUnownedStars", false);
        return ownedStars > 0 && hasUnownedStars;
    }

    public override bool IsAchieved(Blackboard state)
    {
        int ownedStars = state.GetValue<int>("OwnedStarsCount", 0);
        return ownedStars >= _targetStarCount;
    }

    public override void UpdatePriority(Blackboard currentBlackboard)
    {
        int ownedStars = currentBlackboard.GetValue<int>("OwnedStarsCount", 0);
        bool hasUnownedStars = currentBlackboard.GetValue<bool>("HasUnownedStars", false);

        if (!hasUnownedStars || ownedStars <= 0)
        {
            Priority = 0f;
            return;
        }

        _targetStarCount = ownedStars + 1;

        bool hasIdleColonyShip = currentBlackboard.GetValue<bool>("HasIdleColonyShip", false);
        bool hasColonyShip = currentBlackboard.GetValue<bool>("HasColonyShip", false);
        bool hasShipyard = currentBlackboard.GetValue<bool>("HasShipyard", false);

        if (hasIdleColonyShip)
        {
            // Ready to dispatch immediately
            Priority = 3.5f;
        }
        else if (hasShipyard && !hasColonyShip)
        {
            // Have a shipyard and need to queue a colony ship
            Priority = 2.0f;
        }
        else
        {
            // Waiting for shipyard or colony ship construction to complete
            Priority = 0.2f;
        }
    }
}

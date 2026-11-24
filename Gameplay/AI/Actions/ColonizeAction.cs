using Godot;

/// <summary>
/// Dispatches an idle fleet containing a colony ship to the nearest unowned star
/// with a compound MoveTo + Colonize order.
/// </summary>
public class ColonizeAction : GoapAction
{
    public ColonizeAction()
    {
        Cost = 2.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        bool hasIdleColonyShip = state.GetValue<bool>("HasIdleColonyShip", false);
        bool hasUnownedStars = state.GetValue<bool>("HasUnownedStars", false);
        return hasIdleColonyShip && hasUnownedStars;
    }

    public override void ApplyEffects(Blackboard state)
    {
        state.SetValue("HasColonyShip", false);
        state.SetValue("HasIdleColonyShip", false);

        int ownedStars = state.GetValue<int>("OwnedStarsCount", 1);
        state.SetValue("OwnedStarsCount", ownedStars + 1);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        if (empire == null)
            return false;

        var idleColonyFleets = empire.GetIdleFleetsWithAbility(ShipAbility.Colonize);
        if (idleColonyFleets.Count == 0)
            return false;

        Fleet fleet = idleColonyFleets[0];
        Vector2 origin = fleet.WorldPosition ?? fleet.Position;

        Star targetStar = StarManager.HasInstance
            ? StarManager.Instance.GetBestUnownedStarToColonize(origin, empire)
            : null;

        if (targetStar == null)
            return false;

        fleet.OrderColonize(targetStar);
        GD.Print(
            $"Empire {empire.EmpireName} dispatched '{fleet.FleetName}' to colonize {targetStar.StarName}."
        );

        state.SetValue("HasIdleColonyShip", false);
        return true;
    }
}

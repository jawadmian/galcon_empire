using Godot;

/// <summary>
/// Fire-and-forget GOAP action that queues construction of a colony ship at an owned shipyard.
/// </summary>
public class BuildColonyShipAction : GoapAction
{
    private readonly ShipDesignResource _design;
    private readonly long _fallbackOreCost = 1500;
    private readonly long _fallbackMoneyCost = 500;

    public BuildColonyShipAction(ShipDesignResource design = null)
    {
        _design = design;
        Cost = 2.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        bool hasShipyard =
            state.GetValue<bool>("HasShipyard", false)
            || state.GetValue<long>($"Production_{ResourceType.ShipyardProduction}", 0) > 0;
        bool hasColonyShip = state.GetValue<bool>("HasColonyShip", false);

        if (!hasShipyard || hasColonyShip)
            return false;

        if (_design != null && _design.ResourceCosts.Count > 0)
        {
            foreach (var cost in _design.ResourceCosts)
            {
                long stockpile = state.GetValue<long>($"Stockpile_{cost.Key}", 0);
                if (stockpile < cost.Value)
                    return false;
            }
            return true;
        }

        long currentOre = state.GetValue<long>($"Stockpile_{ResourceType.Ore}", 0);
        long currentMoney = state.GetValue<long>($"Stockpile_{ResourceType.Money}", 0);
        return currentOre >= _fallbackOreCost && currentMoney >= _fallbackMoneyCost;
    }

    public override void ApplyEffects(Blackboard state)
    {
        if (_design != null && _design.ResourceCosts.Count > 0)
        {
            foreach (var cost in _design.ResourceCosts)
            {
                long stockpile = state.GetValue<long>($"Stockpile_{cost.Key}", 0);
                state.SetValue($"Stockpile_{cost.Key}", stockpile - cost.Value);
            }
        }
        else
        {
            long currentOre = state.GetValue<long>($"Stockpile_{ResourceType.Ore}", 0);
            long currentMoney = state.GetValue<long>($"Stockpile_{ResourceType.Money}", 0);
            state.SetValue($"Stockpile_{ResourceType.Ore}", currentOre - _fallbackOreCost);
            state.SetValue($"Stockpile_{ResourceType.Money}", currentMoney - _fallbackMoneyCost);
        }

        state.SetValue("HasColonyShip", true);
        state.SetValue("HasIdleColonyShip", true);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        if (_design != null && empire != null)
        {
            if (empire.QueueShip(_design))
            {
                state.SetValue("HasColonyShip", true);
                return true;
            }
            return false;
        }

        GD.Print($"Empire {empire?.EmpireName} queued a Colony Ship (simulated).");
        state.SetValue("HasColonyShip", true);
        return true;
    }
}

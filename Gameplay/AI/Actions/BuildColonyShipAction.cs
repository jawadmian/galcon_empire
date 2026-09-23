public class BuildColonyShipAction : GoapAction
{
    private long _oreCost = 150;

    public BuildColonyShipAction()
    {
        Cost = 15.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        long currentOre = state.GetValue<long>("Stockpile_Ore", 0);
        bool hasShipyard = state.GetValue<bool>("HasShipyard", false) ||
                           state.GetValue<long>($"Production_{ResourceType.ShipyardProduction}", 0) > 0;
        bool hasColonyShip = state.GetValue<bool>("HasColonyShip", false);
        
        return currentOre >= _oreCost && hasShipyard && !hasColonyShip;
    }

    public override void ApplyEffects(Blackboard state)
    {
        long currentOre = state.GetValue<long>("Stockpile_Ore", 0);
        state.SetValue("Stockpile_Ore", currentOre - _oreCost);
        state.SetValue("HasColonyShip", true);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        Godot.GD.Print($"Empire {empire.EmpireName} built a Colony Ship!");
        state.SetValue("HasColonyShip", true);
        return true;
    }
}

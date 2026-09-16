public class BuildShipyardAction : GoapAction
{
    private long _oreCost = 100;

    public BuildShipyardAction()
    {
        Cost = 10.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        long currentOre = state.GetValue<long>("Stockpile_Ore", 0);
        bool hasShipyard = state.GetValue<bool>("HasShipyard", false);
        return currentOre >= _oreCost && !hasShipyard;
    }

    public override void ApplyEffects(Blackboard state)
    {
        long currentOre = state.GetValue<long>("Stockpile_Ore", 0);
        state.SetValue("Stockpile_Ore", currentOre - _oreCost);
        state.SetValue("HasShipyard", true);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        Godot.GD.Print($"Empire {empire.EmpireName} is building a Shipyard!");
        // Update the actual blackboard since perform succeeded
        state.SetValue("HasShipyard", true);
        return true;
    }
}

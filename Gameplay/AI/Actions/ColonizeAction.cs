public class ColonizeAction : GoapAction
{
    public ColonizeAction()
    {
        Cost = 20.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        bool hasColonyShip = state.GetValue<bool>("HasColonyShip", false);
        // We'd also check if there is an unowned star available
        return hasColonyShip;
    }

    public override void ApplyEffects(Blackboard state)
    {
        state.SetValue("HasColonyShip", false); // Consumes ship
        
        int ownedStars = state.GetValue<int>("OwnedStarsCount", 1);
        state.SetValue("OwnedStarsCount", ownedStars + 1);
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        Godot.GD.Print($"Empire {empire.EmpireName} colonized a new star!");
        state.SetValue("HasColonyShip", false);
        return true;
    }
}

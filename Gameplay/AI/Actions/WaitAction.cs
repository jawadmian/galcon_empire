public class WaitAction : GoapAction
{
    public WaitAction()
    {
        Cost = 1.0f;
    }

    public override bool CheckPreconditions(Blackboard state)
    {
        return true; // You can always wait
    }

    public override void ApplyEffects(Blackboard state)
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            long currentStockpile = state.GetValue<long>($"Stockpile_{type}", 0);
            long production = state.GetValue<long>($"Production_{type}", 0);
            state.SetValue($"Stockpile_{type}", currentStockpile + production);
        }
    }

    public override bool Perform(Empire empire, Blackboard state)
    {
        // We do nothing, letting the TickManager naturally increase resources
        // We can return false to indicate it's not "done" but just an ongoing state,
        // or return true to consume it and let the planner pick the next action.
        return true;
    }
}

public abstract class GoapAction
{
    public float Cost { get; protected set; } = 1.0f;

    /// <summary>
    /// Validates if the action can be taken given the current world state.
    /// </summary>
    public abstract bool CheckPreconditions(Blackboard state);

    /// <summary>
    /// Modifies the provided state to simulate the consequences of this action.
    /// Used by the A* Planner during the planning phase.
    /// </summary>
    public abstract void ApplyEffects(Blackboard state);

    /// <summary>
    /// Actually performs the action in the game world.
    /// Returns true if the action was successfully initiated/completed.
    /// </summary>
    public abstract bool Perform(Empire empire, Blackboard currentBlackboard);
}

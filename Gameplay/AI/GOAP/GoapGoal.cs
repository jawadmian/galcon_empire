public abstract class GoapGoal
{
    public float Priority { get; protected set; } = 1.0f;

    /// <summary>
    /// Checks if this goal is still relevant or valid given the current world state.
    /// </summary>
    public abstract bool IsValid(Blackboard currentBlackboard);

    /// <summary>
    /// Evaluates if the goal has been successfully achieved in the given state.
    /// </summary>
    public abstract bool IsAchieved(Blackboard state);

    /// <summary>
    /// Dynamically updates the priority of the goal based on the current state.
    /// </summary>
    public virtual void UpdatePriority(Blackboard currentBlackboard) { }
}

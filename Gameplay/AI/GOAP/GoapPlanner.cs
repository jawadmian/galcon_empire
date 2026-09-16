using System.Collections.Generic;

public class GoapNode
{
    public Blackboard State { get; set; }
    public GoapNode Parent { get; set; }
    public GoapAction Action { get; set; }
    public float G { get; set; } // Cost from start to this node
    public float H { get; set; } // Heuristic cost to goal (optional)
    public float F => G + H;
}

public class GoapPlanner
{
    /// <summary>
    /// Performs a Forward Search A* (or Dijkstra if H is 0) to find a sequence of actions that achieves the goal.
    /// </summary>
    public Queue<GoapAction> Plan(Blackboard startState, GoapGoal goal, List<GoapAction> availableActions)
    {
        var openList = new List<GoapNode>();
        var closedList = new HashSet<int>(); // Hash of Blackboard state to prevent cycles

        var startNode = new GoapNode
        {
            State = startState.Clone(),
            Parent = null,
            Action = null,
            G = 0,
            H = 0 // Generic GOAP heuristic is complex, defaulting to 0 (Dijkstra)
        };

        openList.Add(startNode);

        // Sanity limit to prevent infinite loops during dev
        int iterationLimit = 1000;
        int iterations = 0;

        while (openList.Count > 0 && iterations < iterationLimit)
        {
            iterations++;

            // Find node with lowest F cost (simple sort, can be optimized with PriorityQueue)
            openList.Sort((a, b) => a.F.CompareTo(b.F));
            var currentNode = openList[0];
            openList.RemoveAt(0);

            // Check if goal is achieved
            if (goal.IsAchieved(currentNode.State))
            {
                return ConstructPlan(currentNode);
            }

            // Mark this state as evaluated
            int stateHash = currentNode.State.GetHashCode();
            if (!closedList.Add(stateHash))
            {
                continue; // We already evaluated this state configuration
            }

            // Generate neighbors by applying valid actions
            foreach (var action in availableActions)
            {
                if (action.CheckPreconditions(currentNode.State))
                {
                    var newState = currentNode.State.Clone();
                    action.ApplyEffects(newState);

                    var neighborNode = new GoapNode
                    {
                        State = newState,
                        Parent = currentNode,
                        Action = action,
                        G = currentNode.G + action.Cost,
                        H = 0
                    };

                    openList.Add(neighborNode);
                }
            }
        }

        // Return null if no plan was found or limit reached
        return null;
    }

    private Queue<GoapAction> ConstructPlan(GoapNode endNode)
    {
        var plan = new List<GoapAction>();
        var current = endNode;
        while (current.Parent != null)
        {
            plan.Insert(0, current.Action);
            current = current.Parent;
        }
        return new Queue<GoapAction>(plan);
    }
}

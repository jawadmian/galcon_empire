using Godot;
using System.Collections.Generic;

public partial class AIManager : Node
{
    private static AIManager _instance;
    public static bool HasInstance => _instance != null;
    public static AIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushError("AIManager.Instance was accessed before it was ready or is not in the scene tree.");
            }
            return _instance;
        }
        private set => _instance = value;
    }

    private readonly List<GoapAdvisor> _registeredAdvisors = new List<GoapAdvisor>();
    private int _currentAdvisorIndex = 0;
    
    [Export]
    public int AdvisorsToUpdatePerFrame { get; set; } = 1;

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterAdvisor(GoapAdvisor advisor)
    {
        if (!_registeredAdvisors.Contains(advisor))
        {
            _registeredAdvisors.Add(advisor);
        }
    }

    public void UnregisterAdvisor(GoapAdvisor advisor)
    {
        _registeredAdvisors.Remove(advisor);
    }

    private int _advisorsToUpdateThisTick = 0;

    public override void _Ready()
    {
        // Use CallDeferred to ensure TickManager is fully initialized if they are both Autoloads
        CallDeferred(nameof(ConnectToTickManager));
    }

    private void ConnectToTickManager()
    {
        if (TickManager.HasInstance)
        {
            TickManager.Instance.TickUpdateSignal += OnTickUpdate;
        }
    }

    private void OnTickUpdate(int value)
    {
        // A new day has started, queue all advisors for an update
        _advisorsToUpdateThisTick = _registeredAdvisors.Count;
        _currentAdvisorIndex = 0;
    }

    public override void _Process(double delta)
    {
        if (_advisorsToUpdateThisTick <= 0 || _registeredAdvisors.Count == 0) return;

        // Process a chunk of advisors to spread out the CPU load (Time Slicing)
        int updatesThisFrame = 0;
        while (updatesThisFrame < AdvisorsToUpdatePerFrame && _advisorsToUpdateThisTick > 0)
        {
            if (_currentAdvisorIndex >= _registeredAdvisors.Count)
                break;

            var advisor = _registeredAdvisors[_currentAdvisorIndex];
            
            // Only update if it's not currently running an async plan
            if (!advisor.IsPlanning)
            {
                advisor.UpdateAI();
            }

            _currentAdvisorIndex++;
            _advisorsToUpdateThisTick--;
            updatesThisFrame++;
        }
    }
}

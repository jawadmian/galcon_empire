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
        // Queue a full pass over all advisors without resetting the round-robin cursor,
        // so higher simulation speeds (8x/16x) and 50+ empires never starve later advisors.
        _advisorsToUpdateThisTick = _registeredAdvisors.Count;
        if (_registeredAdvisors.Count > 0 && _currentAdvisorIndex >= _registeredAdvisors.Count)
        {
            _currentAdvisorIndex %= _registeredAdvisors.Count;
        }
    }

    public override void _Process(double delta)
    {
        if (_advisorsToUpdateThisTick <= 0 || _registeredAdvisors.Count == 0) return;

        // Scale per-frame budget with empire count and speed so all empires keep pace
        float speedMult = TickManager.HasInstance ? Mathf.Max(1.0f, TickManager.Instance.SpeedMultiplier) : 1.0f;
        int scaledBudget = Mathf.CeilToInt(_registeredAdvisors.Count * speedMult / 30.0f);
        int frameBudget = Mathf.Max(AdvisorsToUpdatePerFrame, scaledBudget);

        int updatesThisFrame = 0;
        while (updatesThisFrame < frameBudget && _advisorsToUpdateThisTick > 0)
        {
            if (_currentAdvisorIndex >= _registeredAdvisors.Count)
                _currentAdvisorIndex = 0;

            var advisor = _registeredAdvisors[_currentAdvisorIndex];

            // Only update if it's not currently running an async plan
            if (!advisor.IsPlanning)
            {
                advisor.UpdateAI();
            }

            _currentAdvisorIndex = (_currentAdvisorIndex + 1) % _registeredAdvisors.Count;
            _advisorsToUpdateThisTick--;
            updatesThisFrame++;
        }
    }
}

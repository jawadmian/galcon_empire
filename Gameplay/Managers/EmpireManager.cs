using System; // Required for Random
using System.Collections.Generic;
using System.Linq; // Required for AsReadOnly() if not already present
using Godot;

public partial class EmpireManager : Node
{
    private static EmpireManager _instance;
    public static EmpireManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushError(
                    "EmpireManager.Instance was accessed before it was ready or EmpireManager is not in the scene tree. Ensure it's added to the scene, possibly as an Autoload."
                );
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [ExportGroup("Empire Spawning")]
    [Export]
    public PackedScene EmpireToSpawn { get; set; } // Drag your Empire.tscn here

    [Export]
    public int NumberOfEmpiresToSpawn { get; set; } = 1; // Number of empires to spawn

    [Export]
    public StringArrayResource EmpireNamesResource { get; set; }

    [ExportGroup("Improvement Spawning")]
    [Export(PropertyHint.Dir)]
    public string ImprovementsFolderPath { get; set; } = "res://Data/Improvements";

    private List<string> _availableEmpireNames;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<ImprovementResource> _loadedImprovements = new List<ImprovementResource>();

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning(
                $"Duplicate EmpireManager instance detected. Old: {_instance.GetPath()}, New: {GetPath()}. Destroying new instance."
            );
            QueueFree(); // Remove the duplicate
            return;
        }
        Instance = this;
        _rng.Randomize(); // Initialize RNG
    }

    public override void _Ready()
    {
        if (Instance != this)
            return; // Was queued for deletion

        if (EmpireToSpawn == null)
        {
            GD.PushError(
                "EmpireManager: EmpireToSpawn PackedScene is not assigned in the Inspector! Skipping empire spawning."
            );
            return;
        }
        if (EmpireNamesResource == null)
        {
            GD.Print(
                "EmpireManager: EmpireNamesResource not assigned. Empires will use default names."
            );
            // Non-critical, can proceed.
        }

        RandomizeEmpireNames(); // Prepare empire names
        LoadImprovements(); // Load available improvements
        // Defer empire spawning to ensure stars are ready (StarManager should have run its _Ready by now).
        CallDeferred(nameof(SpawnEmpiresInternal));
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadImprovements()
    {
        _loadedImprovements.Clear();
        if (string.IsNullOrEmpty(ImprovementsFolderPath))
        {
            GD.PushWarning(
                "EmpireManager: ImprovementsFolderPath is not set. Cannot load improvements."
            );
            return;
        }

        using var dir = DirAccess.Open(ImprovementsFolderPath);
        if (dir == null)
        {
            GD.PushError(
                $"EmpireManager: Could not open improvements folder at path: {ImprovementsFolderPath}. Error: {DirAccess.GetOpenError()}"
            );
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                string fullPath = string.Format("{0}/{1}", ImprovementsFolderPath, fileName);
                ImprovementResource improvement = ResourceLoader.Load<ImprovementResource>(
                    fullPath
                );
                if (improvement != null)
                {
                    _loadedImprovements.Add(improvement);
                }
                else
                    GD.PushWarning(
                        $"EmpireManager: Failed to load improvement resource at {fullPath}"
                    );
            }
            fileName = dir.GetNext();
        }
        GD.Print(
            $"EmpireManager: Loaded {_loadedImprovements.Count} improvements from {ImprovementsFolderPath}."
        );
    }

    private void RandomizeEmpireNames()
    {
        if (
            EmpireNamesResource == null
            || EmpireNamesResource.Items == null
            || EmpireNamesResource.Items.Count == 0
        )
        {
            _availableEmpireNames = new List<string>(); // Ensure it's not null
            GD.Print(
                "EmpireManager: No EmpireNamesResource provided or it's empty. Empires will use default names."
            );
            return;
        }
        _rng.Seed = (ulong)GD.Randi(); // Re-seed for this specific shuffle, consistent with original GalaxySpawner
        _availableEmpireNames = new List<string>(EmpireNamesResource.Items);
        ShuffleList(_availableEmpireNames);
        GD.Print($"EmpireManager: Loaded and shuffled {_availableEmpireNames.Count} empire names.");
    }

    private void SpawnEmpiresInternal()
    {
        if (NumberOfEmpiresToSpawn <= 0)
        {
            GD.Print(
                "EmpireManager: NumberOfEmpiresToSpawn is 0 or less. Skipping empire spawning."
            );
            return;
        }

        IReadOnlyList<Star> spawnedStars = StarManager.Instance?.GetSpawnedStars();

        if (spawnedStars == null || spawnedStars.Count == 0)
        {
            GD.Print(
                "EmpireManager: No stars available from StarManager to assign as home stars. Skipping empire spawning."
            );
            return;
        }

        int empiresToActuallySpawn = NumberOfEmpiresToSpawn;
        if (spawnedStars.Count < NumberOfEmpiresToSpawn)
        {
            GD.PushWarning(
                $"EmpireManager: Not enough spawned stars ({spawnedStars.Count}) to assign to {NumberOfEmpiresToSpawn} empires. Will spawn {spawnedStars.Count} empire(s) instead."
            );
            empiresToActuallySpawn = spawnedStars.Count;
        }

        if (empiresToActuallySpawn <= 0)
        {
            GD.Print("EmpireManager: No empires to spawn after checking available stars.");
            return;
        }

        GD.Print($"EmpireManager: Spawning {empiresToActuallySpawn} empire(s)...");

        for (int i = 0; i < empiresToActuallySpawn; i++)
        {
            Node empireNode = EmpireToSpawn.Instantiate();
            if (empireNode is Empire empireInstance)
            {
                Star homeStar = spawnedStars[i];
                empireInstance.HomeStar = homeStar;

                empireInstance.EmpireColor = new Color(_rng.Randf(), _rng.Randf(), _rng.Randf());
                homeStar.StarNameColor = empireInstance.EmpireColor;
                homeStar.StarPopulation = 5000; // Initial population for a home star

                empireInstance.EmpireName =
                    (i < _availableEmpireNames.Count)
                        ? _availableEmpireNames[i]
                        : $"Empire {i + 1}";
                if (i >= _availableEmpireNames.Count && _availableEmpireNames.Count > 0) // Only warn if names were expected
                {
                    GD.PrintErr(
                        $"EmpireManager: Ran out of unique empire names. Using default: {empireInstance.EmpireName}"
                    );
                }

                AddChild(empireInstance); // Add empire as a child of EmpireManager
                GD.Print(
                    $"EmpireManager: Spawned '{empireInstance.EmpireName}' (Color: {empireInstance.EmpireColor}) with Home Star: {homeStar.StarName} at {homeStar.GlobalPosition}. Parent: {empireInstance.GetParent()?.Name}"
                );

                // Add a random improvement to the home star
                if (_loadedImprovements.Count > 0)
                {
                    int randomIndex = (int)_rng.RandiRange(0, _loadedImprovements.Count - 1);
                    ImprovementResource randomImprovement = _loadedImprovements[randomIndex];
                    homeStar.AddImprovement(randomImprovement);
                    GD.Print(
                        $"EmpireManager: Added improvement '{randomImprovement.ImprovementName ?? "Unnamed Improvement"}' to {homeStar.StarName} for {empireInstance.EmpireName}."
                    );
                }
                else
                {
                    GD.Print(
                        $"EmpireManager: No improvements available to add to {homeStar.StarName} for {empireInstance.EmpireName}."
                    );
                }
            }
            else
            {
                GD.PushError(
                    $"EmpireManager: Spawned empire node is not of type 'Empire'. Node type: {empireNode.GetType().Name}. Discarding node."
                );
                empireNode.QueueFree();
            }
        }
    }

    private void ShuffleList<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = (int)_rng.RandiRange(0, n);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}

// e:\Development\Godot\GalconEmpire\Gameplay\StarManager.cs
using System.Collections.Generic;
using System.Linq; // Required for AsReadOnly() if not already present
using Godot;

public partial class StarManager : Node
{
    private static StarManager _instance;
    public static StarManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GD.PushError(
                    "StarManager.Instance was accessed before it was ready or StarManager is not in the scene tree. Ensure it's added to the scene, possibly as an Autoload."
                );
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Export]
    public PackedScene StarNode { get; set; } // Drag your Star.tscn here

    [ExportGroup("Star Spawning Properties")]
    [Export]
    public int NumberOfStarsToSpawn { get; set; } = 10;

    [Export]
    public Vector2 SpawnAreaMin { get; set; } = new Vector2(0, 0);

    [Export]
    public Vector2 SpawnAreaMax { get; set; } = new Vector2(1000, 1000);

    [Export]
    public StringArrayResource StarNamesResource { get; set; }

    [Export(PropertyHint.Range, "0,10000,1")]
    public float MinStarDistanceSquared { get; set; } = 1024;

    [Export(PropertyHint.Range, "1,1000,1")]
    public int MaxAttemptsPerPoint { get; set; } = 50;

    private List<string> _availableStarNames;
    private readonly List<Star> _spawnedStars = new List<Star>();
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    [ExportGroup("Star Population Update")]
    [Export(PropertyHint.Range, "0.05,60.0,0.01")] // e.g., update from 20 times/sec to once per minute
    public float PopulationUpdateInterval { get; set; } = 10.0f; // Time in seconds between population updates
    private float _populationUpdateTimer = 0.0f;

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning(
                $"Duplicate StarManager instance detected. Old: {_instance.GetPath()}, New: {this.GetPath()}. Destroying new instance."
            );
            QueueFree(); // Remove the duplicate
            return;
        }
        Instance = this;
        _rng.Randomize(); // Initialize RNG
    }

    public override void _Ready()
    {
        if (StarNode == null)
        {
            GD.PushError("StarManager: StarNode (PackedScene) is not assigned in the Inspector!");
            return; // Critical, cannot spawn stars
        }
        if (StarNamesResource == null)
        {
            GD.Print(
                "StarManager: StarNamesResource is not assigned. Stars will use default names."
            );
            // Non-critical, can proceed without names
        }

        _spawnedStars.Clear(); // Clear from previous runs if scene is reloaded
        InternalSpawnStars();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gets a read-only list of the spawned stars.
    /// </summary>
    public IReadOnlyList<Star> GetSpawnedStars()
    {
        return _spawnedStars.AsReadOnly();
    }

    public void UpdateTick()
    {
        foreach (Star star in _spawnedStars)
        {
            if (star == null || !IsInstanceValid(star))
                continue; // Safety check

            // Call UpdateTick with the configured interval as its delta.
            // The Star.UpdateTick logic will use this interval for its calculations.
            star.UpdateTick();
        }
    }

    private void InternalSpawnStars()
    {
        RandomizeStarNamesList();

        List<Vector2> starSpawnPoints = GenerateNonOverlappingPoints(
            NumberOfStarsToSpawn,
            SpawnAreaMin.X,
            SpawnAreaMax.X,
            SpawnAreaMin.Y,
            SpawnAreaMax.Y,
            MinStarDistanceSquared,
            MaxAttemptsPerPoint
        );

        int starNameIndex = 0;

        foreach (Vector2 vector in starSpawnPoints)
        {
            Node spawnedNode = StarNode.Instantiate();

            if (spawnedNode is Star starInstance)
            {
                starInstance.Position = vector;
                if (_availableStarNames != null && starNameIndex < _availableStarNames.Count)
                {
                    starInstance.StarName = _availableStarNames[starNameIndex];
                    starNameIndex++;
                }
                else
                {
                    GD.PrintErr(
                        "StarManager: Ran out of star names or _availableStarNames is null. Star will use default name."
                    );
                }
                _spawnedStars.Add(starInstance);
            }
            else if (spawnedNode is Node2D spawnedNode2D) // Fallback for other Node2D types
            {
                spawnedNode2D.Position = vector;
                GD.Print($"StarManager: Spawning Node2D (not a Star) at {vector}");
            }
            else
            {
                GD.PushWarning(
                    "StarManager: Spawned star node is not Node2D, cannot set position directly."
                );
            }

            // Add the spawned node as a child of StarManager
            CallDeferred(Node.MethodName.AddChild, spawnedNode);
        }
        GD.Print($"StarManager: Total stars spawned and tracked: {_spawnedStars.Count}");
    }

    private void RandomizeStarNamesList()
    {
        if (
            StarNamesResource == null
            || StarNamesResource.Items == null
            || StarNamesResource.Items.Count == 0
        )
        {
            _availableStarNames = new List<string>();
            GD.Print(
                "StarManager: No StarNamesResource provided or it's empty. Stars will use default names."
            );
            return;
        }
        _availableStarNames = new List<string>(StarNamesResource.Items);
        ShuffleList(_availableStarNames);
    }

    /// <summary>
    /// Shuffles a list using the Fisher-Yates algorithm.
    /// </summary>
    private void ShuffleList<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = (int)_rng.RandiRange(0, n);
            (list[k], list[n]) = (list[n], list[k]); // Tuple swap for conciseness
        }
    }

    private List<Vector2> GenerateNonOverlappingPoints(
        int count,
        float minX,
        float maxX,
        float minY,
        float maxY,
        float minDistanceSquared,
        int maxAttemptsPerPoint = 100
    )
    {
        if (count <= 0)
        {
            return new List<Vector2>();
        }

        if (maxX <= minX || maxY <= minY || minDistanceSquared < 0 || maxAttemptsPerPoint <= 0)
        {
            GD.PushWarning(
                "StarManager: Invalid parameters provided to GenerateNonOverlappingPoints."
            );
            return new List<Vector2>();
        }

        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            bool pointPlaced = false;
            for (int attempt = 0; attempt < maxAttemptsPerPoint; attempt++)
            {
                float x = _rng.RandfRange(minX, maxX);
                float y = _rng.RandfRange(minY, maxY);
                Vector2 newPoint = new Vector2(x, y);

                bool tooClose = false;
                foreach (Vector2 existingPoint in points)
                {
                    if (newPoint.DistanceSquaredTo(existingPoint) < minDistanceSquared)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    points.Add(newPoint);
                    pointPlaced = true;
                    break;
                }
            }

            if (!pointPlaced)
            {
                GD.PushWarning(
                    $"StarManager: Could not place star point {i + 1} after {maxAttemptsPerPoint} attempts. May result in fewer stars than requested."
                );
                // Continue trying to place remaining points if desired, or break.
                // For now, we break as the original code did.
                break;
            }
        }
        return points;
    }
}

using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class SpaceBackgroundTest
{
    private PackedScene _scene;
    private Node2D _spaceBackground;

    [BeforeTest]
    public void Setup()
    {
        _scene = GD.Load<PackedScene>("res://Gameplay/space_background.tscn");
        if (_scene != null)
        {
            _spaceBackground = _scene.Instantiate<Node2D>();
        }
    }

    [AfterTest]
    public void TearDown()
    {
        if (GodotObject.IsInstanceValid(_spaceBackground) && !_spaceBackground.IsQueuedForDeletion())
        {
            _spaceBackground.QueueFree();
        }
    }

    [TestCase]
    public void TestSceneLoadsAndInstantiates()
    {
        AssertThat(_scene).IsNotNull();
        AssertThat(_spaceBackground).IsNotNull();
        AssertThat(_spaceBackground.ZIndex).IsEqual(-100);
    }

    [TestCase]
    public void TestParallaxLayersConfiguration()
    {
        AssertThat(_spaceBackground).IsNotNull();

        // Nebula Layer
        var nebulaLayer = _spaceBackground.GetNodeOrNull<Parallax2D>("NebulaLayer");
        AssertThat(nebulaLayer).IsNotNull();
        AssertThat(nebulaLayer.ScrollScale.X).IsEqual(0.05f);
        AssertThat(nebulaLayer.RepeatSize).IsEqual(new Vector2(2048, 2048));

        var nebulaSprite = nebulaLayer.GetNodeOrNull<Sprite2D>("NebulaSprite");
        AssertThat(nebulaSprite).IsNotNull();
        AssertThat(nebulaSprite.Texture).IsNotNull();
        AssertThat(nebulaSprite.Texture.ResourcePath).IsEqual("res://Images/Space/nebula_layer.png");

        // Distant Stars Layer
        var distantLayer = _spaceBackground.GetNodeOrNull<Parallax2D>("DistantStarsLayer");
        AssertThat(distantLayer).IsNotNull();
        AssertThat(distantLayer.ScrollScale.X).IsEqual(0.15f);
        AssertThat(distantLayer.RepeatSize).IsEqual(new Vector2(2048, 2048));

        var distantSprite = distantLayer.GetNodeOrNull<Sprite2D>("DistantStarsSprite");
        AssertThat(distantSprite).IsNotNull();
        AssertThat(distantSprite.Texture).IsNotNull();
        AssertThat(distantSprite.Texture.ResourcePath).IsEqual("res://Images/Space/starfield_distant.png");

        // Clusters / Mid-Stars Layer
        var clustersLayer = _spaceBackground.GetNodeOrNull<Parallax2D>("ClustersLayer");
        AssertThat(clustersLayer).IsNotNull();
        AssertThat(clustersLayer.ScrollScale.X).IsEqual(0.30f);
        AssertThat(clustersLayer.RepeatSize).IsEqual(new Vector2(2048, 2048));

        var clustersSprite = clustersLayer.GetNodeOrNull<Sprite2D>("ClustersSprite");
        AssertThat(clustersSprite).IsNotNull();
        AssertThat(clustersSprite.Texture).IsNotNull();
        AssertThat(clustersSprite.Texture.ResourcePath).IsEqual("res://Images/Space/starfield_clusters.png");
    }

    [TestCase]
    public void TestGalaxySpawnerIncludesSpaceBackground()
    {
        var spawnerScene = GD.Load<PackedScene>("res://Gameplay/galaxy_spawner.tscn");
        AssertThat(spawnerScene).IsNotNull();

        var spawner = spawnerScene.Instantiate<Node2D>();
        AssertThat(spawner).IsNotNull();

        var bg = spawner.GetNodeOrNull<Node2D>("SpaceBackground");
        AssertThat(bg).IsNotNull();
        AssertThat(bg.ZIndex).IsEqual(-100);

        spawner.QueueFree();
    }
}

using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class CameraControllerTest
{
    private CameraController _camera;

    [BeforeTest]
    public void Setup()
    {
        _camera = new CameraController();
        _camera.Zoom = new Vector2(1.0f, 1.0f);
        _camera.MinZoom = new Vector2(0.2f, 0.2f);
        _camera.MaxZoom = new Vector2(2.0f, 2.0f);
        _camera.ZoomStep = 0.2f;
        _camera.SmoothZoom = false; // Direct zoom update for deterministic test
        _camera.SmoothPan = false;  // Direct pan update for deterministic test
        _camera._Ready();
    }

    [AfterTest]
    public void TearDown()
    {
        if (GodotObject.IsInstanceValid(_camera) && !_camera.IsQueuedForDeletion())
        {
            _camera.Free();
        }
    }

    [TestCase]
    public void TestCameraInitialization()
    {
        AssertThat(_camera).IsNotNull();
        AssertThat(_camera.PanSpeed).IsEqual(800.0f);
        AssertThat(_camera.FastPanMultiplier).IsEqual(2.0f);
        AssertThat(_camera.EnableKeyboardPan).IsTrue();
        AssertThat(_camera.EnableMouseDragPan).IsTrue();
    }

    [TestCase]
    public void TestFocusOn()
    {
        Vector2 targetPos = new Vector2(500, -300);
        _camera.FocusOn(targetPos);
        AssertThat(_camera.Position).IsEqual(targetPos);
    }

    [TestCase]
    public void TestZoomIn()
    {
        Vector2 initialZoom = _camera.Zoom;
        _camera.ZoomIn();
        AssertThat(_camera.Zoom.X).IsGreater(initialZoom.X);
        AssertThat(_camera.Zoom.Y).IsGreater(initialZoom.Y);
    }

    [TestCase]
    public void TestZoomOut()
    {
        Vector2 initialZoom = _camera.Zoom;
        _camera.ZoomOut();
        AssertThat(_camera.Zoom.X).IsLess(initialZoom.X);
        AssertThat(_camera.Zoom.Y).IsLess(initialZoom.Y);
    }

    [TestCase]
    public void TestZoomClamping()
    {
        for (int i = 0; i < 20; i++)
        {
            _camera.ZoomIn();
        }
        AssertThat(_camera.Zoom.X).IsEqual(_camera.MaxZoom.X);
        AssertThat(_camera.Zoom.Y).IsEqual(_camera.MaxZoom.Y);

        for (int i = 0; i < 20; i++)
        {
            _camera.ZoomOut();
        }
        AssertThat(_camera.Zoom.X).IsEqual(_camera.MinZoom.X);
        AssertThat(_camera.Zoom.Y).IsEqual(_camera.MinZoom.Y);
    }

    [TestCase]
    public void TestRightClickMouseDragPan()
    {
        Vector2 startPos = new Vector2(100, 100);
        _camera.Position = startPos;
        _camera.Zoom = new Vector2(2.0f, 2.0f); // At 2x zoom, 10px screen drag = 5px world move

        // Press Right Mouse Button
        var pressEvent = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true
        };
        _camera._UnhandledInput(pressEvent);

        // Move mouse by (40, -20)
        var motionEvent = new InputEventMouseMotion
        {
            Relative = new Vector2(40, -20)
        };
        _camera._UnhandledInput(motionEvent);

        // Position should move inversely proportional to zoom: startPos - Relative / Zoom
        // 100 - (40 / 2) = 80; 100 - (-20 / 2) = 110
        AssertThat(_camera.Position.X).IsEqual(80.0f);
        AssertThat(_camera.Position.Y).IsEqual(110.0f);

        // Release Right Mouse Button
        var releaseEvent = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = false
        };
        _camera._UnhandledInput(releaseEvent);

        // Move mouse again after release - position should NOT change
        _camera._UnhandledInput(motionEvent);
        AssertThat(_camera.Position.X).IsEqual(80.0f);
        AssertThat(_camera.Position.Y).IsEqual(110.0f);
    }

    [TestCase]
    public void TestKeyboardPanDirectionWithoutInput()
    {
        Vector2 dir = _camera.GetKeyboardPanDirection();
        AssertThat(dir).IsEqual(Vector2.Zero);
    }
}

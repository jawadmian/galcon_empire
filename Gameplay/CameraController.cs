using Godot;

public partial class CameraController : Camera2D
{
    [ExportGroup("Keyboard Pan")]
    [Export]
    public bool EnableKeyboardPan { get; set; } = true;

    [Export]
    public float PanSpeed { get; set; } = 800.0f;

    [Export]
    public float FastPanMultiplier { get; set; } = 2.0f;

    [Export]
    public bool SmoothPan { get; set; } = true;

    [Export]
    public float PanSmoothSpeed { get; set; } = 15.0f;

    [ExportGroup("Mouse Drag Pan")]
    [Export]
    public bool EnableMouseDragPan { get; set; } = true;

    [ExportGroup("Zoom")]
    [Export]
    public float ZoomStep { get; set; } = 0.15f;

    [Export]
    public Vector2 MinZoom { get; set; } = new Vector2(0.1f, 0.1f);

    [Export]
    public Vector2 MaxZoom { get; set; } = new Vector2(3.0f, 3.0f);

    [Export]
    public bool SmoothZoom { get; set; } = true;

    [Export]
    public float ZoomSmoothSpeed { get; set; } = 15.0f;

    public static CameraController Instance { get; private set; }

    private Vector2 _targetPosition;
    private Vector2 _targetZoom;
    private bool _isDragging = false;

    public override void _Ready()
    {
        Instance = this;
        _targetPosition = Position;
        _targetZoom = Zoom;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Reset dragging flag if the right mouse button was released outside the window
        if (_isDragging && !Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _isDragging = false;
        }

        // Handle keyboard panning (WASD and Arrow keys)
        if (EnableKeyboardPan && !_isDragging)
        {
            Vector2 inputDir = GetKeyboardPanDirection();
            if (inputDir != Vector2.Zero)
            {
                float speed = PanSpeed;
                if (Input.IsKeyPressed(Key.Shift))
                {
                    speed *= FastPanMultiplier;
                }

                // Adjust pan speed by zoom level so world navigation feels consistent at all zoom levels
                Vector2 moveDelta = inputDir * (speed / Zoom.X) * dt;
                _targetPosition += moveDelta;

                if (!SmoothPan)
                {
                    Position = _targetPosition;
                }
            }
        }

        // Smoothly interpolate position if smooth pan is enabled
        if (SmoothPan && Position != _targetPosition && !_isDragging)
        {
            Position = Position.Lerp(_targetPosition, Mathf.Clamp(PanSmoothSpeed * dt, 0.0f, 1.0f));
        }

        // Smoothly interpolate zoom if smooth zoom is enabled
        if (SmoothZoom && Zoom != _targetZoom)
        {
            Zoom = Zoom.Lerp(_targetZoom, Mathf.Clamp(ZoomSmoothSpeed * dt, 0.0f, 1.0f));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Handle right-click mouse drag panning
        if (EnableMouseDragPan)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isDragging = mouseButton.Pressed;
                if (_isDragging)
                {
                    _targetPosition = Position;
                }
            }
            else if (_isDragging && @event is InputEventMouseMotion mouseMotion)
            {
                // Dragging moves the camera directly in world units inversely proportional to zoom
                Vector2 dragDelta = mouseMotion.Relative / Zoom;
                Position -= dragDelta;
                _targetPosition = Position;
            }
        }

        // Handle mouse wheel zooming
        if (@event is InputEventMouseButton wheelEvent && wheelEvent.Pressed)
        {
            if (wheelEvent.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomIn();
            }
            else if (wheelEvent.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomOut();
            }
        }
    }

    /// <summary>
    /// Computes the directional movement vector based on WASD, arrow keys, and custom InputMap actions.
    /// </summary>
    public Vector2 GetKeyboardPanDirection()
    {
        Vector2 dir = Vector2.Zero;

        // Up: W key, Up Arrow, or camera_up action
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) || Input.IsActionPressed("camera_up") || Input.IsActionPressed("ui_up"))
        {
            dir.Y -= 1.0f;
        }

        // Down: S key, Down Arrow, or camera_down action
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) || Input.IsActionPressed("camera_down") || Input.IsActionPressed("ui_down"))
        {
            dir.Y += 1.0f;
        }

        // Left: A key, Left Arrow, or camera_left action
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) || Input.IsActionPressed("camera_left") || Input.IsActionPressed("ui_left"))
        {
            dir.X -= 1.0f;
        }

        // Right: D key, Right Arrow, or camera_right action
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) || Input.IsActionPressed("camera_right") || Input.IsActionPressed("ui_right"))
        {
            dir.X += 1.0f;
        }

        return dir.Normalized();
    }

    public void ZoomIn()
    {
        SetTargetZoom(_targetZoom * (1.0f + ZoomStep));
    }

    public void ZoomOut()
    {
        SetTargetZoom(_targetZoom * (1.0f - ZoomStep));
    }

    public void SetTargetZoom(Vector2 newTarget)
    {
        _targetZoom = new Vector2(
            Mathf.Clamp(newTarget.X, MinZoom.X, MaxZoom.X),
            Mathf.Clamp(newTarget.Y, MinZoom.Y, MaxZoom.Y)
        );

        if (!SmoothZoom)
        {
            Zoom = _targetZoom;
        }
    }

    /// <summary>
    /// Centers the camera on the specified world coordinates.
    /// </summary>
    public void FocusOn(Vector2 worldPosition)
    {
        _targetPosition = worldPosition;
        Position = worldPosition;
    }
}

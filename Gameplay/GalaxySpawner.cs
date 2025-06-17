using System;
using System.Collections.Generic;
using Godot;

public partial class GalaxySpawner : Node2D
{
    [ExportGroup("Camera Controls")]
    [Export]
    public float CameraSpeed { get; set; } = 300.0f;

    [Export]
    public float ZoomSpeed { get; set; } = 0.1f;

    [Export]
    public float MinZoom { get; set; } = 0.2f;

    [Export]
    public float MaxZoom { get; set; } = 3.0f;
    private Camera2D _camera;

    public override void _Ready()
    {
        // Get the Camera2D node
        _camera = GetNodeOrNull<Camera2D>("Camera2D"); // Assuming your camera node is named "Camera2D"
        if (_camera == null)
        {
            GD.PushError(
                "Camera2D node named 'Camera2D' not found as a child of GalaxySpawner. Camera controls will not work."
            );
        }

        // Star spawning is handled by StarManager.Instance._Ready()
        // Empire spawning is handled by EmpireManager in its own _Ready method or via direct call.
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
            return;

        Vector2 moveDirection = Vector2.Zero;

        if (Input.IsActionPressed("ui_right")) // D key by default (or right arrow)
        {
            moveDirection.X += 1;
        }
        if (Input.IsActionPressed("ui_left")) // A key by default (or left arrow)
            moveDirection.X -= 1;
        if (Input.IsActionPressed("ui_down")) // S key by default (or down arrow)
            moveDirection.Y += 1;
        if (Input.IsActionPressed("ui_up")) // W key by default (or up arrow)
            moveDirection.Y -= 1;

        if (moveDirection != Vector2.Zero)
        {
            _camera.Position += moveDirection.Normalized() * CameraSpeed * (float)delta;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_camera == null)
            return;

        if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed)
        {
            Vector2 currentZoom = _camera.Zoom;
            if (eventMouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                currentZoom -= new Vector2(ZoomSpeed, ZoomSpeed);
            }
            else if (eventMouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                currentZoom += new Vector2(ZoomSpeed, ZoomSpeed);
            }
            _camera.Zoom = currentZoom.Clamp(
                new Vector2(MinZoom, MinZoom),
                new Vector2(MaxZoom, MaxZoom)
            );
        }
    }
}

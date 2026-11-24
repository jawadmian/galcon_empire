using System;
using Godot;

public partial class DebugUi : Control
{
    private Label _gameSpeedLabel;
    private Button _slowDownButton;
    private Button _pauseButton;
    private Button _speedUpButton;

    public override void _Ready()
    {
        _gameSpeedLabel = GetNodeOrNull<Label>("%SpeedStatusLabel") ?? GetNodeOrNull<Label>("VBoxContainer/Label");
        _slowDownButton = GetNodeOrNull<Button>("%SlowDownButton");
        _pauseButton = GetNodeOrNull<Button>("%PauseButton");
        _speedUpButton = GetNodeOrNull<Button>("%SpeedUpButton");

        if (_slowDownButton != null)
            _slowDownButton.Pressed += OnSlowDownPressed;
        if (_pauseButton != null)
            _pauseButton.Pressed += OnPausePressed;
        if (_speedUpButton != null)
            _speedUpButton.Pressed += OnSpeedUpPressed;

        ApplyButtonStyles(_slowDownButton);
        ApplyButtonStyles(_pauseButton);
        ApplyButtonStyles(_speedUpButton);

        RefreshSpeedDisplay();
    }

    public override void _Process(double delta)
    {
        RefreshSpeedDisplay();
    }

    private static void ApplyButtonStyles(Button btn)
    {
        if (btn == null) return;
        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        var pressedStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_pressed.tres");
        if (normalStyle != null)
        {
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("focus", normalStyle);
        }
        if (hoverStyle != null)
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
        if (pressedStyle != null)
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);
    }

    private void OnSlowDownPressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.HalveSpeed();
        RefreshSpeedDisplay();
    }

    private void OnPausePressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.TogglePause();
        RefreshSpeedDisplay();
    }

    private void OnSpeedUpPressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.DoubleSpeed();
        RefreshSpeedDisplay();
    }

    public void RefreshSpeedDisplay()
    {
        if (!TickManager.HasInstance)
        {
            if (_gameSpeedLabel != null)
                _gameSpeedLabel.Text = "SPEED: 1x";
            return;
        }

        var tm = TickManager.Instance;
        bool paused = tm.IsPaused;
        float mult = tm.SpeedMultiplier;

        if (_gameSpeedLabel != null)
        {
            _gameSpeedLabel.Text = paused
                ? $"SPEED: PAUSED ({FormatMultiplier(mult)})"
                : $"SPEED: {FormatMultiplier(mult)}";
        }

        if (_pauseButton != null)
        {
            _pauseButton.Text = paused ? "▶" : "||";
            _pauseButton.AddThemeColorOverride("font_color", paused ? new Color("ffd700") : new Color("00e5ff"));
        }

        if (_slowDownButton != null)
        {
            bool atMin = mult <= TickManager.MinSpeedMultiplier + 0.001f;
            _slowDownButton.Disabled = atMin;
            _slowDownButton.AddThemeColorOverride("font_color", atMin ? new Color("556b82") : new Color("00e5ff"));
        }

        if (_speedUpButton != null)
        {
            bool atMax = mult >= TickManager.MaxSpeedMultiplier - 0.001f;
            _speedUpButton.Disabled = atMax;
            _speedUpButton.AddThemeColorOverride("font_color", atMax ? new Color("556b82") : new Color("00e5ff"));
        }
    }

    private static string FormatMultiplier(float mult)
    {
        if (Mathf.IsEqualApprox(mult, 0.25f)) return "0.25x";
        if (Mathf.IsEqualApprox(mult, 0.5f)) return "0.5x";
        if (Mathf.IsEqualApprox(mult, 1.0f)) return "1x";
        if (Mathf.IsEqualApprox(mult, 2.0f)) return "2x";
        if (Mathf.IsEqualApprox(mult, 4.0f)) return "4x";
        return $"{mult:0.##}x";
    }
}

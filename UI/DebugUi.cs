using System;
using Godot;

public partial class DebugUi : Control
{
    private Label _gameSpeedLabel;
    private Button _pauseButton;
    private Button _slowButton;
    private Button _normalButton;
    private Button _fastButton;

    public override void _Ready()
    {
        _gameSpeedLabel = GetNodeOrNull<Label>("%SpeedStatusLabel") ?? GetNodeOrNull<Label>("VBoxContainer/Label");
        _pauseButton = GetNodeOrNull<Button>("%PauseButton");
        _slowButton = GetNodeOrNull<Button>("%SlowButton");
        _normalButton = GetNodeOrNull<Button>("%NormalButton");
        _fastButton = GetNodeOrNull<Button>("%FastButton");

        if (_pauseButton != null)
            _pauseButton.Pressed += OnPausePressed;
        if (_slowButton != null)
            _slowButton.Pressed += OnSlowPressed;
        if (_normalButton != null)
            _normalButton.Pressed += OnNormalPressed;
        if (_fastButton != null)
            _fastButton.Pressed += OnFastPressed;

        ApplyButtonStyles(_pauseButton);
        ApplyButtonStyles(_slowButton);
        ApplyButtonStyles(_normalButton);
        ApplyButtonStyles(_fastButton);

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

    private void OnPausePressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.TogglePause();
        RefreshSpeedDisplay();
    }

    private void OnSlowPressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.SetSpeedMultiplier(0.25f);
        RefreshSpeedDisplay();
    }

    private void OnNormalPressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.SetSpeedMultiplier(1.0f);
        RefreshSpeedDisplay();
    }

    private void OnFastPressed()
    {
        if (!TickManager.HasInstance) return;
        TickManager.Instance.SetSpeedMultiplier(4.0f);
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
            _pauseButton.Text = paused ? "RESUME" : "PAUSE";
            _pauseButton.AddThemeColorOverride("font_color", paused ? new Color("ffd700") : new Color("00e5ff"));
        }

        UpdateSpeedButtonHighlight(_slowButton, !paused && Mathf.IsEqualApprox(mult, 0.25f));
        UpdateSpeedButtonHighlight(_normalButton, !paused && Mathf.IsEqualApprox(mult, 1.0f));
        UpdateSpeedButtonHighlight(_fastButton, !paused && Mathf.IsEqualApprox(mult, 4.0f));
    }

    private static string FormatMultiplier(float mult)
    {
        if (Mathf.IsEqualApprox(mult, 0.25f)) return "0.25x";
        if (Mathf.IsEqualApprox(mult, 1.0f)) return "1x";
        if (Mathf.IsEqualApprox(mult, 4.0f)) return "4x";
        return $"{mult:0.##}x";
    }

    private static void UpdateSpeedButtonHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        btn.AddThemeColorOverride("font_color", active ? new Color("ffd700") : new Color("8fa3bf"));
    }
}

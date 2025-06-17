using System;
using Godot;

public partial class DebugUi : Control
{
    private HSlider _gameSpeedSlider;
    private Label _gameSpeedLabel; // Optional: To display the current speed

    private const string GameSpeedSliderPath = "VBoxContainer/Slider";
    private const string GameSpeedLabelPath = "VBoxContainer/Label"; // Optional

    public override void _Ready()
    {
        _gameSpeedSlider = GetNodeOrNull<HSlider>(GameSpeedSliderPath);
        _gameSpeedLabel = GetNodeOrNull<Label>(GameSpeedLabelPath); // Optional

        if (_gameSpeedSlider != null)
        {
            // Initialize slider value from TickManager
            if (TickManager.Instance != null)
            {
                _gameSpeedSlider.Value = TickManager.Instance.TickUpdateInterval;
                if (_gameSpeedLabel != null)
                    _gameSpeedLabel.Text =
                        $"Game Speed: {TickManager.Instance.TickUpdateInterval:F2}s/tick"; // Optional
            }
            _gameSpeedSlider.ValueChanged += OnGameSpeedSliderChanged;
        }
        else
        {
            GD.PushError($"DebugUi: GameSpeedSlider not found at path '{GameSpeedSliderPath}'.");
        }
    }

    private void OnGameSpeedSliderChanged(double value)
    {
        if (TickManager.Instance != null)
        {
            TickManager.Instance.TickUpdateInterval = (float)value;
            if (_gameSpeedLabel != null)
                _gameSpeedLabel.Text = $"Game Speed: {(float)value:F2}s/tick"; // Optional
            GD.Print($"Game speed changed to: {(float)value}");
        }
    }
}

using System;
using Godot;

public partial class StarInfoBox : Control
{
    public override void _Ready()
    {
        // Get the CloseButton node.
        // Make sure the path "PanelContainer/VBoxContainer/CloseButton" is correct for your scene structure.
        Button closeButton = GetNode<Button>("PanelContainer/VBoxContainer/CloseButton");

        if (closeButton != null)
        {
            // Connect the button's "pressed" signal to a new method.
            closeButton.Pressed += OnCloseButtonPressed;
        }
        else
        {
            GD.PushWarning(
                "CloseButton not found in StarInfoBox. Please check the path: PanelContainer/VBoxContainer/CloseButton"
            );
        }
    }

    private void OnCloseButtonPressed()
    {
        // QueueFree safely removes the node from the scene tree and deletes it.
        QueueFree();
        GD.Print("StarInfoBox closed.");
    }
}

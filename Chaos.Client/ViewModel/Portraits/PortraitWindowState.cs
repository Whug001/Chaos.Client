using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.Portraits;

public enum PortraitTool
{
    Pen,
    Fill,
    Pick
}

/// <summary>The portrait window's headless view state: the canvas, the current tool and colour, and whether it changed.</summary>
public sealed class PortraitWindowState
{
    public PortraitCanvas Canvas { get; } = PortraitCanvas.Blank();
    public PortraitTool Tool { get; set; } = PortraitTool.Pen;
    public Color Current { get; private set; } = Color.White;
    public bool IsDirty => Canvas.IsDirty;

    /// <summary>Takes a colour from the canvas or a swatch. Picking from the canvas returns to the pen.</summary>
    public void Pick(Color color)
    {
        Current = color;

        if (Tool == PortraitTool.Pick)
            Tool = PortraitTool.Pen;
    }

    public void MarkClean()
        => Canvas.MarkClean();

    /// <summary>Loads the own-portrait bytes onto the canvas and refills the swatches; empty or null loads a blank canvas.</summary>
    public void Open(byte[]? picture)
    {
        if (picture is { Length: > 0 } && (PortraitCanvas.TryFromBytes(picture, out var decoded) == string.Empty) && (decoded is not null))
        {
            Canvas.CopyFrom(decoded);
            Canvas.FillSwatchesFrom();
        } else
            Canvas.CopyFrom(PortraitCanvas.Blank());

        Canvas.MarkClean();
    }
}

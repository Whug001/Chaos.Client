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
    public string? PendingSetHash { get; private set; }
    public bool PendingRemove { get; private set; }

    private const string RemovedMessage = "Portrait removed.";

    /// <summary>Takes a colour from the canvas or a swatch. Picking from the canvas returns to the pen.</summary>
    public void Pick(Color color)
    {
        Current = color;

        if (Tool == PortraitTool.Pick)
            Tool = PortraitTool.Pen;
    }

    public void MarkClean()
        => Canvas.MarkClean();

    public void BeginSet(string hash)
    {
        PendingSetHash = hash;
        PendingRemove = false;
    }

    public void BeginRemove()
    {
        PendingSetHash = null;
        PendingRemove = true;
    }

    /// <summary>Answers a returning own picture: the status text, or null when the status stays as it is.</summary>
    public string? SettleOwnPicture(string hash, int pictureLength)
    {
        if (PendingSetHash is not null)
        {
            var set = (pictureLength > 0) && string.Equals(hash, PendingSetHash, StringComparison.Ordinal);
            PendingSetHash = null;

            return set ? "Portrait set." : string.Empty;
        }

        if (!PendingRemove)
            return null;

        //a non-empty picture here is an earlier Set landing after Remove was clicked; the Remove is still due
        if (pictureLength > 0)
            return null;

        PendingRemove = false;

        return RemovedMessage;
    }

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
        PendingSetHash = null;
        PendingRemove = false;

        //a fresh session starts from swatch 0 with the pen, matching the window's swatch highlight
        Tool = PortraitTool.Pen;
        Current = Canvas.Swatches[0];
    }
}

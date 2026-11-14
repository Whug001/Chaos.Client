namespace Chaos.Client.ViewModel;

/// <summary>
///     One animation frame as plain numbers: its texture size and the texel the world draws on the tile centre
///     (<c>CenterX - Min(0, Left)</c>, <c>CenterY - Min(0, Top)</c>, as <c>CreatureRenderer.Draw</c> works it out).
///     <see cref="Flip" /> is whether the frame is drawn mirrored.
/// </summary>
public readonly record struct TamerSpriteFrameBox(int Width, int Height, int AnchorX, int AnchorY, bool Flip = false)
{
    /// <summary>The anchor's column as drawn: mirrored when the frame is flipped.</summary>
    public int DrawnAnchorX => Flip ? Width - AnchorX : AnchorX;
}

/// <summary>Where a sprite's anchor sits in its box.</summary>
public enum TamerSpriteAnchor
{
    /// <summary>The bounds over all frames are centred in the box (the portrait).</summary>
    Centre,

    /// <summary>The anchor is on the box's centre column and the lowest pixel of any frame on its bottom edge (a board cell).</summary>
    Feet
}

/// <summary>Where one frame is drawn, relative to the box's top-left corner.</summary>
public readonly record struct TamerSpritePlacement(int X, int Y, int Width, int Height);

/// <summary>
///     One scale for every frame of a sprite in a box, and the point of the box every frame's anchor is drawn on.
///     Plain numbers: safe to keep while the renderer drops and reloads its textures.
/// </summary>
public readonly record struct TamerSpriteLayout(float Scale, int AnchorX, int AnchorY)
{
    /// <summary>Nothing to draw: there were no frames or no room.</summary>
    public bool IsEmpty => Scale <= 0f;

    /// <summary>The frame's rectangle in the box: scaled by <see cref="Scale" /> with its anchor on the fixed point.</summary>
    public TamerSpritePlacement Place(TamerSpriteFrameBox frame)
        => new(
            AnchorX - (int)MathF.Round(frame.DrawnAnchorX * Scale),
            AnchorY - (int)MathF.Round(frame.AnchorY * Scale),
            Math.Max(1, (int)MathF.Round(frame.Width * Scale)),
            Math.Max(1, (int)MathF.Round(frame.Height * Scale)));
}

/// <summary>
///     Sizes a creature for a box once for all its frames, so it does not change size or shift as it animates:
///     the scale comes from the bounds over every frame measured around the anchor, and every frame is then drawn
///     with its anchor on the same point. Pure: no textures.
/// </summary>
public static class TamerSpriteFit
{
    /// <summary>
    ///     The layout for <paramref name="frames" /> in a box. A sprite smaller than the box grows in whole multiples;
    ///     a larger one shrinks to fit. Empty when there are no frames with a size or the box has no room.
    /// </summary>
    public static TamerSpriteLayout Fit(
        IReadOnlyList<TamerSpriteFrameBox> frames,
        int boxWidth,
        int boxHeight,
        TamerSpriteAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if ((boxWidth <= 0) || (boxHeight <= 0))
            return default;

        //how far any frame reaches from the anchor in each direction
        var left = int.MinValue;
        var right = int.MinValue;
        var up = int.MinValue;
        var down = int.MinValue;

        foreach (var frame in frames)
        {
            if ((frame.Width <= 0) || (frame.Height <= 0))
                continue;

            left = Math.Max(left, frame.DrawnAnchorX);
            right = Math.Max(right, frame.Width - frame.DrawnAnchorX);
            up = Math.Max(up, frame.AnchorY);
            down = Math.Max(down, frame.Height - frame.AnchorY);
        }

        if (left == int.MinValue)
            return default;

        //on the board the anchor stays on the centre column whichever way the unit faces, so both sides get the wider reach
        if (anchor == TamerSpriteAnchor.Feet)
            left = right = Math.Max(left, right);

        var width = left + right;
        var height = up + down;

        if ((width <= 0) || (height <= 0))
            return default;

        var fit = Math.Min(boxWidth / (float)width, boxHeight / (float)height);
        var scale = fit >= 1f ? MathF.Floor(fit) : fit;

        var anchorX = anchor == TamerSpriteAnchor.Feet
            ? boxWidth / 2
            : (int)MathF.Round(((boxWidth - (width * scale)) / 2f) + (left * scale));

        var anchorY = anchor == TamerSpriteAnchor.Feet
            ? boxHeight - (int)MathF.Round(down * scale)
            : (int)MathF.Round(((boxHeight - (height * scale)) / 2f) + (up * scale));

        return new TamerSpriteLayout(scale, anchorX, anchorY);
    }
}

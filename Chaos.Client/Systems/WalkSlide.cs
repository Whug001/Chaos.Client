namespace Chaos.Client.Systems;

/// <summary>
///     How the walk slide between two tiles moves.
/// </summary>
public enum WalkSlide
{
    /// <summary>
    ///     Classic: one jump per walk frame (4 per tile).
    /// </summary>
    Stepped,

    /// <summary>
    ///     Smooth, snapped to whole game pixels.
    /// </summary>
    Pixel,

    /// <summary>
    ///     Smooth with fractions kept, for the full-resolution world target.
    /// </summary>
    Exact
}

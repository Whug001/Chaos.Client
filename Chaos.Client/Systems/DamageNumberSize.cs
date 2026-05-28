namespace Chaos.Client.Systems;

/// <summary>
///     Render size for floating damage/heal numbers, chosen from the F4 Damage Numbers dropdown. The enum
///     ordinal IS the dropdown index and the int persisted to Darkages.cfg. Each tier maps to a distinct
///     hand-authored glyph set in <see cref="Chaos.Client.Rendering.DamageNumberFont" /> (no draw-time scaling).
/// </summary>
public enum DamageNumberSize
{
    Compact,
    Normal,
    Large
}

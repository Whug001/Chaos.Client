namespace Chaos.Client.Rendering.Definitions;

/// <summary>
///     The type of a visible entity in the game world. Painter's draw order (back-to-front) is ground items first
///     (underneath), then aislings, then creatures (on top); note this differs from this enum's numeric order.
/// </summary>
public enum ClientEntityType : byte
{
    GroundItem,
    Creature,
    Aisling
}

/// <summary>
///     Specifies the highlight tint to apply when drawing an entity.
/// </summary>
public enum EntityTintType
{
    /// <summary>
    ///     No tint, draw normally.
    /// </summary>
    None,

    /// <summary>
    ///     Mouse hover highlight tint.
    /// </summary>
    Highlight,

    /// <summary>
    ///     Group member highlight tint.
    /// </summary>
    Group,

    /// <summary>
    ///     Projectile impact hit flash (red 50% blend).
    /// </summary>
    HitTint,

    /// <summary>
    ///     Server-assigned status tint from <see cref="WorldEntity.TintColor" />.
    /// </summary>
    Status
}

/// <summary>
///     Layer slots for aisling composite ordering. Each slot is one visual layer.
/// </summary>
public enum LayerSlot
{
    BodyB,
    Body,
    Pants,
    Face,
    Boots,
    HeadH,
    HeadE,
    HeadF,
    Armor,
    Arms,
    WeaponW,
    WeaponP,
    Shield,
    Acc1C,
    Acc1G,
    Acc2C,
    Acc2G,
    Acc3C,
    Acc3G,
    Acc4C,
    Acc4G,
    Emotion,
    Count
}

/// <summary>
///     Packing mode for a texture atlas.
/// </summary>
public enum PackingMode
{
    /// <summary>
    ///     Fixed-size cells in a grid. All entries must be the same size. Zero wasted space.
    /// </summary>
    Grid,

    /// <summary>
    ///     Variable-size entries packed left-to-right in rows (shelves). Entries sorted by height descending.
    /// </summary>
    Shelf
}
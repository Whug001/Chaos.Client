#region
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     A single floating damage/heal number. Owns its arc + fade timing. <see cref="Rendering.EntityOverlayManager" />
///     anchors it to a fixed spawn world-position at construction (while the entity is still alive — a killing blow removes
///     the entity in the same packet batch), projects it to the snapped screen position each frame, and draws it.
///     World-anchored: it does not follow the creature and keeps animating if the creature despawns. Textureless — it stores
///     the bare magnitude digits and a heal flag, and draws glyphs from the shared <see cref="DamageNumberFont" /> atlas;
///     color alone signals heal (green) vs damage (red).
/// </summary>
public sealed class DamageNumber(
    uint entityId,
    string digits,
    bool isHeal,
    float dir,
    float peak,
    float travel,
    Vector2 spawnWorld,
    DamageNumberSize size)
{
    public uint EntityId { get; } = entityId;
    public bool IsHeal { get; } = isHeal;
    public string Digits { get; } = digits;

    //the world-space point the number's bottom-center arcs from; fixed at spawn so removal of the entity can't drop it
    public Vector2 SpawnWorld { get; } = spawnWorld;
    public DamageNumberSize Size { get; } = size;

    private readonly float Dir = dir;
    private readonly float Peak = peak;
    private readonly float Travel = travel;
    private float ElapsedMs;

    //projected screen position, set by the manager each frame
    public int X { get; set; }
    public int Y { get; set; }

    public int Width => DamageNumberFont.MeasureWidth(Digits, Size);
    public int Height => DamageNumberFont.GlyphHeight(Size);

    private float T
    {
        get
        {
            var lifetime = GlobalSettings.DamageNumberLifetimeMs;

            return lifetime <= 0f ? 1f : Math.Clamp(ElapsedMs / lifetime, 0f, 1f);
        }
    }

    public bool IsExpired => ElapsedMs >= GlobalSettings.DamageNumberLifetimeMs;

    //parabola: 0 -> -peak -> 0 (up is negative); constant sideways drift
    public Vector2 WorldOffset
    {
        get
        {
            var t = T;

            return new Vector2(Dir * Travel * t, -Peak * 4f * t * (1f - t));
        }
    }

    public float Alpha
    {
        get
        {
            var t = T;
            var fadeStart = GlobalSettings.DamageNumberFadeStart;

            if (t < fadeStart)
                return 1f;

            var fadeSpan = 1f - fadeStart;

            return fadeSpan <= 0f ? 0f : 1f - ((t - fadeStart) / fadeSpan);
        }
    }

    public void Update(GameTime gameTime) => ElapsedMs += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

    public void Draw(SpriteBatch spriteBatch) => DamageNumberFont.Draw(
        spriteBatch,
        Digits,
        IsHeal,
        X,
        Y,
        Alpha,
        Size);
}

#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Systems;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     A creature drawn by sprite id in a recessed box, standing and facing down-left with its idle cycle, the way
///     <see cref="Tcg.TcgCardFace" /> draws its picture: the pose is resolved once per sprite from
///     <see cref="CreatureRenderer" /> and the frame comes from its cache every draw, so nothing is kept across frames
///     and a sprite the cache has cleared just draws the empty box. Sub-project 4 reuses this in the arena scene.
/// </summary>
public sealed class TamerCreaturePortrait : UIElement
{
    public const int HEIGHT = 110;
    public const int WIDTH = 110;

    private const int MIN_FRAME_MS = 120;

    private static readonly Color BoxFill = new(28, 20, 12);

    private readonly CreatureRenderer Creatures;
    private int AnimCount = 1;
    private double AnimMs;
    private int AnimStart;
    private bool AnimFlip;
    private int IdleFrame;
    private bool IdleFlip;
    private int IntervalMs = MIN_FRAME_MS;
    private int PoseSpriteId = -1;

    public TamerCreaturePortrait(CreatureRenderer creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Creatures = creatures;
        Width = WIDTH;
        Height = HEIGHT;
    }

    /// <summary>Play the creature's idle animation; otherwise it holds its idle pose.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>The species sprite to draw. 0 with no art draws the empty box.</summary>
    public ushort SpriteId { get; set; }

    public override void Update(GameTime gameTime)
    {
        if (Animate)
            AnimMs += gameTime.ElapsedGameTime.TotalMilliseconds;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);

        DrawRectClipped(spriteBatch, bounds, BoxFill);
        DrawBorder(spriteBatch, bounds, LegendColors.DimGray);

        Texture2D? texture = null;
        var flip = false;

        if (SpriteId != 0)
        {
            ResolvePose(SpriteId);

            var frameIndex = IdleFrame;
            flip = IdleFlip;

            if (Animate && (AnimCount > 1))
            {
                frameIndex = AnimStart + ((int)(AnimMs / IntervalMs) % AnimCount);
                flip = AnimFlip;
            }

            var frame = Creatures.GetFrame(SpriteId, frameIndex) ?? Creatures.GetFrame(SpriteId, IdleFrame);

            if (frame is { } spriteFrame)
                texture = spriteFrame.Texture;
        }

        if (texture is null || (texture.Width <= 0) || (texture.Height <= 0))
            return;

        //the sprite grows in whole multiples to fit the box; one larger than the box shrinks to fit instead
        var fit = Math.Min(Width / (float)texture.Width, Height / (float)texture.Height);
        var scale = fit >= 1f ? Math.Max(1f, Math.Floor(fit)) : fit;
        var width = Math.Max(1, (int)(texture.Width * scale));
        var height = Math.Max(1, (int)(texture.Height * scale));
        var dest = new Rectangle(bounds.X + ((Width - width) / 2), bounds.Y + ((Height - height) / 2), width, height);

        DrawTextureFitted(spriteBatch, texture, dest, Color.White, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
    }

    //looked up once per sprite, not per frame: GetAnimInfo reads the creature archive
    private void ResolvePose(int spriteId)
    {
        if (spriteId == PoseSpriteId)
            return;

        PoseSpriteId = spriteId;
        IdleFrame = 0;
        IdleFlip = false;
        AnimStart = 0;
        AnimCount = 1;
        AnimFlip = false;
        AnimMs = 0;
        IntervalMs = MIN_FRAME_MS;

        if (Creatures.GetAnimInfo(spriteId) is not { } info)
            return;

        //facing the viewer, as the slot machine reels draw creatures
        (IdleFrame, IdleFlip) = AnimationSystem.GetCreatureIdleFrame(in info, Direction.Down);
        IntervalMs = Math.Max(MIN_FRAME_MS, info.AnimationIntervalMs);

        if (info.OptionalAnimationFrameCount > 1)
        {
            AnimStart = IdleFrame;
            AnimCount = info.OptionalAnimationFrameCount;
            AnimFlip = IdleFlip;

            return;
        }

        //many creatures define no idle animation; their walk cycle in place reads as idle at this size
        (var walkStart, var walkCount, var walkFlip) = AnimationSystem.GetCreatureWalkCycle(in info, Direction.Down);

        if (walkCount > 1)
        {
            AnimStart = walkStart;
            AnimCount = walkCount;
            AnimFlip = walkFlip;
        }
    }
}

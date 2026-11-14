#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering.Models;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     A creature drawn by sprite id in a recessed box, standing and facing down-left with its idle cycle, the way
///     <see cref="Tcg.TcgCardFace" /> draws its picture: the pose is resolved once per sprite from
///     <see cref="CreatureRenderer" /> and the frame comes from its cache every draw, so nothing is kept across frames
///     and a sprite the cache has cleared just draws the empty box. The creature keeps one size and one standing
///     point while it animates: <see cref="TamerSpriteFit" /> sizes it once from the bounds over the cycle's frames
///     and every frame is drawn by its anchor, as <see cref="CreatureRenderer.Draw" /> anchors them in the world.
///     <see cref="GetBattleFrame" /> is the same per-draw fetch for the battle board: walk and attack cycles and
///     facing, driven by the playback's action and progress instead of a wall clock.
/// </summary>
public sealed class TamerCreaturePortrait : UIElement
{
    public const int HEIGHT = 110;
    public const int WIDTH = 110;

    private const int MIN_FRAME_MS = 120;

    //more sprites than the game has creatures on screen at once; past it the numbers are simply worked out again
    private const int MAX_LAYOUTS = 512;

    private static readonly Color BoxFill = new(28, 20, 12);

    //plain numbers per (sprite, box, anchoring): never textures, so the renderer may drop its frames at any time
    private static readonly Dictionary<(int SpriteId, int Width, int Height, TamerSpriteAnchor Anchor), TamerSpriteLayout> Layouts = [];

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

        if (SpriteId == 0)
            return;

        ResolvePose(SpriteId);

        var frameIndex = IdleFrame;
        var flip = IdleFlip;

        if (Animate && (AnimCount > 1))
        {
            frameIndex = AnimStart + ((int)(AnimMs / IntervalMs) % AnimCount);
            flip = AnimFlip;
        }

        var frame = Creatures.GetFrame(SpriteId, frameIndex);

        if (frame is null)
        {
            frame = Creatures.GetFrame(SpriteId, IdleFrame);
            flip = IdleFlip;
        }

        if (frame is not { } spriteFrame || spriteFrame.Texture is not { Width: > 0, Height: > 0 } texture)
            return;

        //one scale for the whole cycle and every frame on the same anchor point, so the creature neither pops between
        //sizes nor shifts as its frames change shape
        var layout = PortraitLayout();

        if (layout.IsEmpty)
            return;

        var placed = layout.Place(BoxOf(in spriteFrame, flip));
        var dest = new Rectangle(bounds.X + placed.X, bounds.Y + placed.Y, placed.Width, placed.Height);

        DrawTextureFitted(spriteBatch, texture, dest, Color.White, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
    }

    /// <summary>A frame as the plain numbers <see cref="TamerSpriteFit" /> works with, anchored as the world anchors it.</summary>
    public static TamerSpriteFrameBox BoxOf(in SpriteFrame frame, bool flip)
        => new(
            frame.Texture.Width,
            frame.Texture.Height,
            frame.CenterX - Math.Min(0, (int)frame.Left),
            frame.CenterY - Math.Min(0, (int)frame.Top),
            flip);

    //the bounds over the frames this portrait plays (its idle frame and its cycle), centred in the box
    private TamerSpriteLayout PortraitLayout()
    {
        var key = (PoseSpriteId, Width, Height, TamerSpriteAnchor.Centre);

        if (Layouts.TryGetValue(key, out var cached))
            return cached;

        var frames = new List<TamerSpriteFrameBox>(AnimCount + 1);

        AddFrame(Creatures, frames, PoseSpriteId, IdleFrame, IdleFlip);

        for (var i = 0; i < AnimCount; i++)
            AddFrame(Creatures, frames, PoseSpriteId, AnimStart + i, AnimFlip);

        return Remember(key, TamerSpriteFit.Fit(frames, Width, Height, TamerSpriteAnchor.Centre));
    }

    /// <summary>
    ///     The one layout a sprite uses in a board cell, whatever it is doing and whichever way it faces: sized from
    ///     the bounds over its idle frames and its walk and attack cycles in all four directions, standing with its
    ///     feet on the bottom-centre of the cell. Worked out once per sprite and box and kept as plain numbers.
    /// </summary>
    public static TamerSpriteLayout GetBattleLayout(CreatureRenderer creatures, ushort spriteId, int boxWidth, int boxHeight)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        if (spriteId == 0)
            return default;

        var key = ((int)spriteId, boxWidth, boxHeight, TamerSpriteAnchor.Feet);

        if (Layouts.TryGetValue(key, out var cached))
            return cached;

        if (creatures.GetAnimInfo(spriteId) is not { } info)
            return default;

        var frames = new List<TamerSpriteFrameBox>();

        foreach (var facing in BattleFacings)
        {
            var (idleFrame, idleFlip) = AnimationSystem.GetCreatureIdleFrame(in info, facing);
            AddFrame(creatures, frames, spriteId, idleFrame, idleFlip);

            var (walkStart, walkCount, walkFlip) = AnimationSystem.GetCreatureWalkCycle(in info, facing);

            for (var i = 0; i < walkCount; i++)
                AddFrame(creatures, frames, spriteId, walkStart + i, walkFlip);

            var (attackStart, attackCount, attackFlip) = AnimationSystem.GetCreatureAttackCycle(in info, facing);

            for (var i = 0; i < attackCount; i++)
                AddFrame(creatures, frames, spriteId, attackStart + i, attackFlip);
        }

        return Remember(key, TamerSpriteFit.Fit(frames, boxWidth, boxHeight, TamerSpriteAnchor.Feet));
    }

    private static readonly Direction[] BattleFacings = [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    private static void AddFrame(CreatureRenderer creatures, List<TamerSpriteFrameBox> frames, int spriteId, int frameIndex, bool flip)
    {
        if (creatures.GetFrame(spriteId, frameIndex) is { Texture: { Width: > 0, Height: > 0 } } frame)
            frames.Add(BoxOf(in frame, flip));
    }

    //a sprite with no frames yet is not remembered, so it is measured again once its art can be read
    private static TamerSpriteLayout Remember(
        (int SpriteId, int Width, int Height, TamerSpriteAnchor Anchor) key,
        TamerSpriteLayout layout)
    {
        if (layout.IsEmpty)
            return layout;

        if (Layouts.Count >= MAX_LAYOUTS)
            Layouts.Clear();

        Layouts[key] = layout;

        return layout;
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

    /// <summary>
    ///     The battle board's frame for one unit: walk and attack cycles and facing, driven by the playback's
    ///     action and 0-to-1 progress. Fetched from <see cref="CreatureRenderer" /> on every call; the frame's texture
    ///     is the renderer's cached one and must never be kept or disposed by the caller. The idle frame is the
    ///     fallback where a cycle is missing. A missing sprite or frame returns null, and the board draws a
    ///     coloured marker with the unit's initial instead so the fight stays readable.
    /// </summary>
    public static (SpriteFrame? Frame, bool Flip) GetBattleFrame(
        CreatureRenderer creatures,
        ushort spriteId,
        Direction facing,
        TamerBattleAction action,
        double progress)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        if (spriteId == 0)
            return (null, false);

        if (creatures.GetAnimInfo(spriteId) is not { } info)
            return (null, false);

        var (idleFrame, idleFlip) = AnimationSystem.GetCreatureIdleFrame(in info, facing);
        var frameIndex = idleFrame;
        var flip = idleFlip;

        switch (action)
        {
            case TamerBattleAction.Walk:
            case TamerBattleAction.Leap:
            {
                var (start, count, walkFlip) = AnimationSystem.GetCreatureWalkCycle(in info, facing);

                if (count > 0)
                {
                    frameIndex = start + Math.Clamp((int)(Math.Clamp(progress, 0, 1) * count), 0, count - 1);
                    flip = walkFlip;
                }

                break;
            }
            case TamerBattleAction.Attack:
            {
                var (start, count, attackFlip) = AnimationSystem.GetCreatureAttackCycle(in info, facing);

                if (count > 0)
                {
                    frameIndex = start + Math.Clamp((int)(Math.Clamp(progress, 0, 1) * count), 0, count - 1);
                    flip = attackFlip;
                }

                break;
            }
            default:
                break;
        }

        var frame = creatures.GetFrame(spriteId, frameIndex);

        if (frame is null && frameIndex != idleFrame)
        {
            frame = creatures.GetFrame(spriteId, idleFrame);
            flip = idleFlip;
        }

        if (frame is null)
            return (null, false);

        return (frame, flip);
    }
}

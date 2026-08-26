#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Slots;

/// <summary>
///     A single reel. Scrolls continuously while spinning, then eases onto the stop index the server chose.
///     The reel never decides its own outcome — it only animates toward one it was given.
/// </summary>
public sealed class ReelControl : UIPanel
{
    private const int CELL_H = 52;
    private const int CELL_W = 56;

    //breathing room so a fitted sprite never touches the cell border it sits inside
    private const int CELL_PADDING = 2;
    private const float SPIN_SPEED = 900f; //pixels per second
    private const float SETTLE_SECONDS = 0.45f;

    //floor for a sprite's own AnimationIntervalMs. Some creature MPFs declare 0, which would divide by zero in
    //the celebration frame math; others declare intervals fast enough to strobe at this size.
    private const float MIN_CELEBRATION_FRAME_MS = 90f;

    //which way the symbols face. Creature MPFs store two direction groups per animation block and the FIRST one
    //-- where a naive frame 0 lands -- is the Up/away-facing art, which is why every symbol on these reels was
    //showing the player its back. Down is the front-facing pose, at the cost of a mirror (see the Flip flags
    //AnimationSystem returns): in that layout one direction's art literally is another's, mirrored.
    private const Direction SYMBOL_FACING = Direction.Down;

    /// <summary>
    ///     Shared creature sprite renderer (per-frame texture cache), injected rather than looked up statically —
    ///     mirrors how <c>CharacterCreationControl</c> receives its <c>AislingRenderer</c>.
    /// </summary>
    private readonly CreatureRenderer CreatureRenderer;

    //owned copies -- SetStrip receives collections owned by the slot machine view model (which may mutate or
    //replace them on the next Open/SpinResult) and must not be aliased into this control. See SetStrip.
    private byte[] Strip = [];
    private int[] SpriteIds = [];

    //front-facing idle pose per entry in SpriteIds, resolved once in SetStrip. The machine's symbol set is fixed
    //for as long as the panel shows it, so this never belongs in Draw -- doing it there cost an archive lookup
    //per visible cell per frame (fifteen a frame, every frame, throughout a spin).
    private (int FrameIndex, bool Flip)[] IdlePoses = [];

    private float Offset; //pixels scrolled, wraps at Strip.Length * CELL_H
    private bool Spinning;
    private bool Settling;
    private float SettleElapsed;
    private float SettleFrom;
    private float SettleTo;

    //── payline celebration ──
    //the centre row is the only row that pays, so it is the only row that ever animates. The other two stay on
    //their frozen idle frame -- nine creatures animating at once is the noise Draw's frame-0 rule exists to
    //prevent, and animation only means something here if it means "this is what paid".
    private bool Celebrating;
    private float CelebrationElapsed;
    private int CelebrationStartFrame;
    private int CelebrationFrameCount;
    private float CelebrationFrameSeconds;
    private bool CelebrationFlip;

    /// <summary>
    ///     True once the reel is neither spinning nor easing into its landing position. Task 12 polls this on every
    ///     reel to know when all three have finished animating.
    /// </summary>
    public bool IsSettled => !Spinning && !Settling;

    public ReelControl(CreatureRenderer creatureRenderer)
    {
        ArgumentNullException.ThrowIfNull(creatureRenderer);

        CreatureRenderer = creatureRenderer;
        Width = CELL_W;
        Height = CELL_H * 3;
    }

    /// <summary>
    ///     Replaces the reel's symbol strip and sprite lookup table, and resets scroll/animation state. Copies both
    ///     collections defensively -- <paramref name="strip" /> and <paramref name="spriteIds" /> are owned by the
    ///     slot machine view model, which may mutate or replace its own copies on a later Open/SpinResult. Aliasing
    ///     them here would let a later view-model update silently change what this reel is mid-animation over.
    /// </summary>
    public void SetStrip(IReadOnlyList<byte> strip, IReadOnlyList<int> spriteIds)
    {
        ArgumentNullException.ThrowIfNull(strip);
        ArgumentNullException.ThrowIfNull(spriteIds);

        Strip = [..strip];
        SpriteIds = [..spriteIds];
        IdlePoses = SpriteIds.Select(ResolveIdlePose)
                             .ToArray();
        Offset = 0f;

        //a fresh strip invalidates any in-flight spin/settle -- stale SettleFrom/SettleTo pixel offsets from the
        //previous strip would otherwise be reinterpreted against the new one next Update.
        Spinning = false;
        Settling = false;
        StopCelebration();
    }

    /// <summary>
    ///     Animates this reel's payline (centre) symbol — the payout signal <see cref="Draw" />'s frame-0 rule
    ///     reserves. Resolves the symbol's animation cycle once, here, rather than per frame.
    /// </summary>
    /// <remarks>
    ///     A symbol whose sprite declares no usable multi-frame cycle simply does not animate: this is emphasis on
    ///     a result the message line and paytable already state plainly, never the thing that tells the player what
    ///     they won, so degrading to a still frame costs nothing. The walk cycle is used because it is the most
    ///     reliably multi-frame block across the creature archives — many sprites define no idle animation at all —
    ///     and it is taken from the <see cref="SYMBOL_FACING" /> direction group, so a celebrating symbol keeps
    ///     facing the player instead of turning its back the moment it pays.
    /// </remarks>
    public void StartCelebration()
    {
        StopCelebration();

        if (Strip.Length == 0)
            return;

        //the payline symbol is whatever sits at the centre index -- the same index Draw resolves for row 1. This
        //is only ever called at rest, where Offset is an exact multiple of CELL_H, so the centre row is exact.
        var symbolIndex = Strip[Mod((int)Math.Floor(Offset / CELL_H), Strip.Length)];

        if (symbolIndex >= SpriteIds.Length)
            return;

        if (CreatureRenderer.GetAnimInfo(SpriteIds[symbolIndex]) is not { } info)
            return;

        var (startFrame, frameCount, flip) = AnimationSystem.GetCreatureWalkCycle(in info, SYMBOL_FACING);

        if (frameCount <= 1)
            return;

        CelebrationStartFrame = startFrame;
        CelebrationFrameCount = frameCount;
        CelebrationFlip = flip;
        CelebrationFrameSeconds = Math.Max(info.AnimationIntervalMs, MIN_CELEBRATION_FRAME_MS) / 1000f;
        Celebrating = true;
    }

    /// <summary>
    ///     Returns the payline symbol to its frozen idle frame. Safe to call when not celebrating.
    /// </summary>
    public void StopCelebration()
    {
        Celebrating = false;
        CelebrationElapsed = 0f;
    }

    /// <summary>
    ///     Begins continuous scrolling. No-op until <see cref="SetStrip" /> has supplied a non-empty strip.
    /// </summary>
    public void StartSpin()
    {
        if (Strip.Length == 0)
            return;

        Spinning = true;
        Settling = false;
        StopCelebration(); //whatever last paid is over the moment the reels move again
    }

    /// <summary>
    ///     Stops the reel with <paramref name="stop" /> on the centre row. Always travels forward at least one
    ///     full revolution so the stop never appears to jump backwards. No-op until <see cref="SetStrip" /> has
    ///     supplied a non-empty strip.
    /// </summary>
    public void LandOn(int stop)
    {
        if (Strip.Length == 0)
            return;

        ArgumentOutOfRangeException.ThrowIfNegative(stop);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(stop, Strip.Length);

        var stripPixels = Strip.Length * CELL_H;
        float target = stop * CELL_H;

        while (target <= Offset)
            target += stripPixels;

        //idempotent guard, defense-in-depth: the caller (SlotMachineControl) is expected to call this exactly
        //once per stop -- see its ReelLanded latch -- but a duplicate call for the stop already reached or
        //already in flight must not restart the ease. Resetting SettleElapsed unconditionally on every call was
        //the actual bug (SettleElapsed could never reach SETTLE_SECONDS, so IsSettled never became true); this
        //guard means a second, redundant LandOn(sameStop) is now harmless rather than merely rare.
        if (Settling && (Math.Abs(target - SettleTo) < 0.01f))
            return;

        if (IsSettled && (Math.Abs((Offset % stripPixels) - (stop * CELL_H)) < 0.01f))
            return;

        Spinning = false;
        Settling = true;
        SettleElapsed = 0f;
        SettleFrom = Offset;
        SettleTo = target;
    }

    public void Update(float deltaSeconds)
    {
        if (Strip.Length == 0)
            return;

        //ticked before the motion branches below return early -- a celebrating reel is by definition at rest, so
        //it takes the !Settling early-out and would otherwise never advance a frame.
        if (Celebrating)
            CelebrationElapsed += deltaSeconds;

        var stripPixels = Strip.Length * CELL_H;

        if (Spinning)
        {
            Offset = (Offset + (SPIN_SPEED * deltaSeconds)) % stripPixels;

            return;
        }

        if (!Settling)
            return;

        SettleElapsed += deltaSeconds;
        var t = Math.Clamp(SettleElapsed / SETTLE_SECONDS, 0f, 1f);
        var eased = 1f - ((1f - t) * (1f - t) * (1f - t)); //cubic ease-out

        Offset = SettleFrom + ((SettleTo - SettleFrom) * eased);

        if (t < 1f)
            return;

        Offset %= stripPixels;
        Settling = false;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //UIPanel.Draw is not called (this control replaces its background/border/children handling entirely with
        //its own strip rendering), so ClipRect must still be refreshed here -- it is otherwise never populated for
        //this element and every DrawTexture call below would clip against an empty rectangle.
        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        if (Strip.Length == 0)
            return;

        var centerIndex = (int)Math.Floor(Offset / CELL_H);
        var pixelShift = (int)(Offset % CELL_H);

        //draw one extra row above and below so the scroll has no visible seam
        for (var row = -1; row <= 3; row++)
        {
            var stripIndex = Mod(centerIndex + row - 1, Strip.Length);
            var symbolIndex = Strip[stripIndex];

            if (symbolIndex >= SpriteIds.Length)
                continue;

            var spriteId = SpriteIds[symbolIndex];

            //a single frozen idle pose at rest -- three creature sprites animating side by side would read as
            //noise. Animation is the payout signal, and only on the payline (row 1), the only row that pays --
            //see StartCelebration. Neither is frame 0: that lands in the away-facing direction group, which had
            //every symbol on these reels showing the player its back.
            var (idleFrame, idleFlip) = IdlePoses[symbolIndex];

            var animating = Celebrating && (row == 1);

            var frameIndex = animating
                ? CelebrationStartFrame + ((int)(CelebrationElapsed / CelebrationFrameSeconds) % CelebrationFrameCount)
                : idleFrame;

            var flip = animating ? CelebrationFlip : idleFlip;

            //a celebration frame that fails to resolve falls back to the idle pose rather than dropping the
            //symbol: the payline must never blink out of existence at the exact moment it is being celebrated.
            var frame = CreatureRenderer.GetFrame(spriteId, frameIndex);

            if ((frame is null) && (frameIndex != idleFrame))
            {
                frame = CreatureRenderer.GetFrame(spriteId, idleFrame);
                flip = idleFlip;
            }

            if (frame is null)
                continue;

            var spriteFrame = frame.Value;

            //Fit the sprite's IMAGE into the cell rather than centering on the creature's world anchor.
            //CreatureRenderer's CenterX/CenterY is where a creature's feet stand on an isometric tile -- correct
            //in the world, wrong inside a box: it pushes the body upward until symbols straddle cell borders
            //instead of sitting on the payline. DrawTextureFitted centers and clamps in one step, the same way
            //PaytableIconRow already draws these sprites.
            var cellTop = ScreenY + (row * CELL_H) - pixelShift;

            var destRect = new Rectangle(
                ScreenX + CELL_PADDING,
                cellTop + CELL_PADDING,
                CELL_W - (CELL_PADDING * 2),
                CELL_H - (CELL_PADDING * 2));

            DrawSymbolClipped(
                spriteBatch,
                spriteFrame.Texture,
                destRect,
                flip);
        }
    }

    /// <summary>
    ///     Resolves a sprite's front-facing idle pose — frame index plus whether it must be drawn mirrored. Falls
    ///     back to frame 0 unflipped only when the sprite declares no animation metadata at all.
    /// </summary>
    private (int FrameIndex, bool Flip) ResolveIdlePose(int spriteId)
        => CreatureRenderer.GetAnimInfo(spriteId) is { } info
            ? AnimationSystem.GetCreatureIdleFrame(in info, SYMBOL_FACING)
            : (0, false);

    /// <summary>
    ///     Draws a symbol scaled into <paramref name="destRect" />, cropped to this reel's visible window.
    /// </summary>
    /// <remarks>
    ///     The inherited <c>DrawTextureFitted</c> only <i>culls</i> — it skips a destination that misses ClipRect
    ///     entirely, but draws a partially-overlapping one in full. This reel deliberately renders one extra row
    ///     above and below so scrolling has no seam, so during a spin those rows overlap the window partially and
    ///     would spill over the jackpot banner and out the bottom. Crop instead: intersect in screen space, then
    ///     map that slice back into source space so the sprite is cut rather than squashed.
    /// </remarks>
    private void DrawSymbolClipped(
        SpriteBatch spriteBatch,
        Texture2D? texture,
        Rectangle destRect,
        bool flip)
    {
        if ((texture is null) || (destRect is { Width: <= 0 } or { Height: <= 0 }))
            return;

        var visible = Rectangle.Intersect(destRect, ClipRect);

        if (visible is not { Width: > 0, Height: > 0 })
            return;

        Texture2D actualTexture;
        Rectangle sourceRect;

        if (texture is CachedTexture2D { AtlasRegion: { } region })
        {
            actualTexture = region.Atlas;
            sourceRect = region.SourceRect;
        } else
        {
            actualTexture = texture;
            sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        var scaleX = sourceRect.Width / (float)destRect.Width;
        var scaleY = sourceRect.Height / (float)destRect.Height;

        //a mirrored draw reads the source right-to-left, so a horizontally cropped slice has to be taken from the
        //OPPOSITE edge -- measuring the inset from destRect's left as usual would show the wrong strip of the
        //sprite. The reel window is never narrower than a cell in practice, so this is defensive rather than a
        //path exercised every spin; the vertical crop below is the one that runs constantly.
        var leftInset = flip ? destRect.Right - visible.Right : visible.X - destRect.X;

        var croppedSource = new Rectangle(
            sourceRect.X + (int)(leftInset * scaleX),
            sourceRect.Y + (int)((visible.Y - destRect.Y) * scaleY),
            Math.Max(1, (int)(visible.Width * scaleX)),
            Math.Max(1, (int)(visible.Height * scaleY)));

        spriteBatch.Draw(
            actualTexture,
            visible,
            croppedSource,
            Color.White,
            0f,
            Vector2.Zero,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }

    private static int Mod(int value, int length)
    {
        var result = value % length;

        return result < 0 ? result + length : result;
    }
}

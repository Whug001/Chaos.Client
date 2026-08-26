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
///     Renders up to three small creature-sprite icons for one paytable row -- the "which creature(s) does this
///     row mean" illustration that replaces the old plain-text label. A slot holding the wildcard sentinel (-1)
///     renders a dim placeholder square instead of a creature, so a rule like "two matching, third irrelevant"
///     is never drawn as a specific creature it does not actually require. See
///     <c>SlotMachineScript.BuildOpenPayload</c>'s symbol-index derivation for how each paytable rule kind maps
///     to this row's slot contents.
/// </summary>
/// <remarks>
///     Reuses <see cref="CreatureRenderer.GetFrame" /> the same way <see cref="ReelControl" /> does, but scales
///     every frame down into a small fixed <see cref="ICON_SIZE" /> box (via <c>DrawTextureFitted</c>) instead of
///     drawing at native size -- the reel window can afford full-size sprites; a paytable rail with up to
///     <c>SlotMachineControl.MAX_PAYTABLE_ROWS</c> rows cannot. <see cref="SetSymbols" /> resolves every slot's
///     sprite frame eagerly (not lazily in <see cref="Draw" />) so <see cref="AllResolved" /> is accurate the
///     moment it returns -- <c>SlotMachineControl.RefreshPaytableRows</c> uses it to decide whether to show this
///     control or fall back to the row's plain-text label, rather than silently drawing a blank row when a
///     sprite id fails to resolve.
/// </remarks>
public sealed class PaytableIconRow : UIPanel
{
    /// <summary>
    ///     The on-screen size (both dimensions) of each icon slot. Small enough that
    ///     <c>SlotMachineControl.MAX_PAYTABLE_ROWS</c> rows fit inside the panel -- see
    ///     <c>SlotMachineControl.PAYTABLE_ROW_HEIGHT</c>'s remarks for the arithmetic.
    /// </summary>
    public const int ICON_SIZE = 16;

    private const int ICON_GAP = 3;
    public const int SLOT_COUNT = 3;

    //dim, desaturated fill -- reads as "not a creature" at a glance rather than a mystery blank/dark square.
    private static readonly Color WildcardColor = new(90, 84, 74, 180);

    private static Texture2D? SharedWildcardTexture;

    private readonly CreatureRenderer CreatureRenderer;

    //null = empty slot (this row's SymbolIndices had fewer than SLOT_COUNT entries)
    private readonly int?[] SpriteIds = new int?[SLOT_COUNT];
    private readonly bool[] IsWildcard = new bool[SLOT_COUNT];

    //the front-facing pose each slot draws, resolved once in SetSymbols alongside the sprite ids themselves --
    //both so Draw stays free of archive lookups, and so AllResolved can validate the frame that will actually
    //be drawn rather than a different one.
    private readonly (int FrameIndex, bool Flip)[] IdlePoses = new (int FrameIndex, bool Flip)[SLOT_COUNT];

    private int ActiveSlotCount;

    /// <summary>
    ///     True once every non-empty, non-wildcard slot resolved to a real sprite frame. False the instant any
    ///     slot's sprite id fails to resolve (a bad/missing sprite id in the catalog) -- the caller must not show
    ///     this control in that case, since an unresolved slot draws nothing (a silent blank row) rather than
    ///     visibly failing.
    /// </summary>
    public bool AllResolved { get; private set; }

    public PaytableIconRow(CreatureRenderer creatureRenderer)
    {
        ArgumentNullException.ThrowIfNull(creatureRenderer);

        CreatureRenderer = creatureRenderer;
        Width = (ICON_SIZE * SLOT_COUNT) + (ICON_GAP * (SLOT_COUNT - 1));
        Height = ICON_SIZE;
        IsHitTestVisible = false;
    }

    /// <summary>
    ///     Sets this row's symbol indices (server-authored, up to <see cref="SLOT_COUNT" />, -1 = wildcard) and
    ///     resolves each to a sprite frame immediately via <paramref name="spriteIdsBySymbolIndex" /> (the Open
    ///     payload's Symbols table, by index) -- so <see cref="AllResolved" /> reflects reality as soon as this
    ///     returns, not lazily on the next <see cref="Draw" />.
    /// </summary>
    public void SetSymbols(IReadOnlyList<int> symbolIndices, IReadOnlyList<int> spriteIdsBySymbolIndex)
    {
        ArgumentNullException.ThrowIfNull(symbolIndices);
        ArgumentNullException.ThrowIfNull(spriteIdsBySymbolIndex);

        ActiveSlotCount = Math.Min(symbolIndices.Count, SLOT_COUNT);
        AllResolved = ActiveSlotCount > 0;

        for (var i = 0; i < SLOT_COUNT; i++)
        {
            if (i >= ActiveSlotCount)
            {
                SpriteIds[i] = null;
                IsWildcard[i] = false;

                continue;
            }

            var symbolIndex = symbolIndices[i];

            //-1 (or any other out-of-range value) is the wildcard sentinel -- render a placeholder, never a
            //creature we'd have to guess at.
            if ((symbolIndex < 0) || (symbolIndex >= spriteIdsBySymbolIndex.Count))
            {
                IsWildcard[i] = true;
                SpriteIds[i] = null;

                continue;
            }

            IsWildcard[i] = false;
            var spriteId = spriteIdsBySymbolIndex[symbolIndex];
            SpriteIds[i] = spriteId;

            //validate the exact frame Draw will use. This checked frame 0 while Draw now draws the front-facing
            //frame instead -- a sprite whose frame 0 resolves but whose front-facing frame does not would have
            //passed this gate and then rendered nothing, which is the silent blank row AllResolved exists to
            //prevent.
            IdlePoses[i] = ResolveIdlePose(spriteId);

            if (CreatureRenderer.GetFrame(spriteId, IdlePoses[i].FrameIndex) is null)
                AllResolved = false;
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        for (var i = 0; i < ActiveSlotCount; i++)
        {
            var slotX = ScreenX + (i * (ICON_SIZE + ICON_GAP));
            var destRect = new Rectangle(slotX, ScreenY, ICON_SIZE, ICON_SIZE);

            if (IsWildcard[i])
            {
                DrawTextureFitted(spriteBatch, GetWildcardTexture(), destRect, Color.White);

                continue;
            }

            if (SpriteIds[i] is not { } spriteId)
                continue;

            //the front-facing idle pose, matching ReelControl's own at-rest convention. NOT frame 0: creature
            //MPFs store two direction groups per animation block, and the first -- where frame 0 lands -- is the
            //Up/away-facing one, so the whole paytable was illustrated with the backs of its creatures.
            var (frameIndex, flip) = IdlePoses[i];
            var frame = CreatureRenderer.GetFrame(spriteId, frameIndex);

            //AllResolved already told the caller not to show this control if any slot got here -- this is a
            //defense-in-depth no-op, not the primary guard.
            if (frame is null)
                continue;

            DrawTextureFitted(
                spriteBatch,
                frame.Value.Texture,
                destRect,
                Color.White,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        }
    }

    /// <summary>
    ///     Resolves a sprite's front-facing idle pose — the frame index plus whether it must be drawn mirrored.
    ///     Falls back to frame 0 unflipped only when the sprite declares no animation metadata at all, which is
    ///     the pre-existing behaviour and no worse than what it replaces.
    /// </summary>
    private (int FrameIndex, bool Flip) ResolveIdlePose(int spriteId)
        => CreatureRenderer.GetAnimInfo(spriteId) is { } info
            ? AnimationSystem.GetCreatureIdleFrame(in info, Direction.Down)
            : (0, false);

    /// <summary>
    ///     Lazily builds the shared dim placeholder texture used for every wildcard slot across every row --
    ///     one small solid-color texture, not one per row.
    /// </summary>
    private static Texture2D GetWildcardTexture()
    {
        if (SharedWildcardTexture is not null)
            return SharedWildcardTexture;

        var pixels = new Color[ICON_SIZE * ICON_SIZE];
        Array.Fill(pixels, WildcardColor);

        var texture = new Texture2D(TextureConverter.Device, ICON_SIZE, ICON_SIZE);
        texture.SetData(pixels);

        SharedWildcardTexture = texture;

        return texture;
    }
}

#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Mounts;

/// <summary>
///     A walking mounted figure for the mount window: the player on the selected mount, turning through four
///     facings. The Sleigh shows its creature sprite instead. Each walk step is rendered once and kept until the
///     figure or the facing changes.
/// </summary>
public sealed class MountPreview : UIElement
{
    public const int FACING_COUNT = 4;
    private const int STEP_MS = 160;

    //the world walks on frames 1-4 of each direction's five (frame 0 is standing); see AnimationSystem.GetAislingFrame
    private const int WALK_STEPS = 4;
    private const int FIRST_WALK_FRAME = 1;

    //room kept free under the figure, as in the beauty shop's pedestal
    private const int BOTTOM_MARGIN = 12;

    //(frame, flip, isFront): Down, Right, Up, Left. The walk sheet holds up (0-4) and right (5-9); down = right flipped,
    //left = up flipped. Matches AnimationSystem.GetAislingFrame and AislingRenderer.RenderPreview.
    private static readonly (int Frame, bool Flip, bool IsFront)[] Facings =
    [
        (5, true, true),
        (5, false, true),
        (0, false, false),
        (0, true, false)
    ];

    private readonly GuildCloakWalkCycle Cycle = new(WALK_STEPS, STEP_MS);
    private readonly Texture2D?[] Frames = new Texture2D?[WALK_STEPS];

    //a step the renderer could not draw, so Draw does not retry it every frame
    private readonly bool[] Failed = new bool[WALK_STEPS];
    private readonly Texture2D Pedestal;
    private readonly AislingRenderer Renderer;
    private readonly CreatureRenderer Creatures;
    private MountFigure? Figure;
    private int Facing;

    public MountPreview(AislingRenderer renderer, CreatureRenderer creatures, int width, int height)
    {
        Renderer = renderer;
        Creatures = creatures;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
        IsHitTestVisible = false;
    }

    public override void Dispose()
    {
        ReleaseFrames();
        Pedestal.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        if (Figure is { Look: null, CreatureSprite: > 0 } sleigh)
        {
            DrawCreature(spriteBatch, sleigh.CreatureSprite);

            return;
        }

        if (Figure?.Look is null)
            return;

        var step = Cycle.Step;

        if (!Failed[step] && Frames[step] is null)
        {
            Frames[step] = RenderStep(step);
            Failed[step] = Frames[step] is null;
        }

        if (Frames[step] is not { } figure)
            return;

        //2x when the figure's height fits. The composite is 111 px wide (the body plus 27 px of layer padding each side),
        //so at 2x only its middle columns fit: crop the source around the body's centre instead of dropping to 1x.
        var scale = (figure.Height * 2) <= (Height - BOTTOM_MARGIN) ? 2 : 1;
        var sourceWidth = Math.Min(figure.Width, Width / scale);
        var sourceX = Math.Clamp(AislingRenderer.CANVAS_CENTER_X - (sourceWidth / 2), 0, figure.Width - sourceWidth);
        var source = new Rectangle(sourceX, 0, sourceWidth, figure.Height);

        var destination = new Rectangle(
            ScreenX + (Width / 2) - ((AislingRenderer.CANVAS_CENTER_X - sourceX) * scale),
            ScreenY + (Height / 2) - (AislingRenderer.BODY_CENTER_Y * scale),
            sourceWidth * scale,
            figure.Height * scale);

        //Render returns a plain texture (never an atlas region), so the source rectangle is the texture's own
        if (destination.Intersects(ClipRect))
            spriteBatch.Draw(figure, destination, source, Color.White);
    }

    /// <summary>Drops the rendered steps. Called when the window hides, so a closed window holds no GPU memory.</summary>
    public void ReleaseFrames()
    {
        for (var i = 0; i < Frames.Length; i++)
        {
            Frames[i]?.Dispose();
            Frames[i] = null;
            Failed[i] = false;
        }
    }

    /// <summary>Shows a new mounted figure; null shows nothing.</summary>
    public void SetFigure(MountFigure? figure)
    {
        if (Nullable.Equals(figure, Figure))
            return;

        Figure = figure;
        ReleaseFrames();
    }

    /// <summary>Turns the preview to a new facing.</summary>
    /// <param name="facing">Down=0, Right=1, Up=2, Left=3.</param>
    public void SetFacing(int facing)
    {
        if (facing == Facing)
            return;

        Facing = facing;
        ReleaseFrames();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        Cycle.Advance(gameTime.ElapsedGameTime.TotalMilliseconds);
    }

    private static readonly Direction[] FacingDirections = [Direction.Down, Direction.Right, Direction.Up, Direction.Left];

    //the Sleigh is a creature sprite; its frames belong to the renderer's cache, so they are drawn, never disposed
    private void DrawCreature(SpriteBatch spriteBatch, ushort spriteId)
    {
        if (Creatures.GetAnimInfo(spriteId) is not { } info)
            return;

        (var start, var count, var flip) = AnimationSystem.GetCreatureWalkCycle(in info, FacingDirections[Facing]);
        var frameIndex = count > 0 ? start + (Cycle.Step % count) : start;

        if (Creatures.GetFrame(spriteId, frameIndex) is not { } frame)
            return;

        var texture = frame.Texture;
        var scale = (texture.Height * 2) <= (Height - BOTTOM_MARGIN) ? 2 : 1;

        var destination = new Rectangle(
            ScreenX + ((Width - (texture.Width * scale)) / 2),
            ScreenY + ((Height - (texture.Height * scale)) / 2),
            texture.Width * scale,
            texture.Height * scale);

        if (destination.Intersects(ClipRect))
            spriteBatch.Draw(texture, destination, null, Color.White, 0f, Vector2.Zero,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    private Texture2D? RenderStep(int step)
    {
        if (Figure?.Look is not { } look)
            return null;

        (var frame, var flip, var isFront) = Facings[Facing];
        frame += FIRST_WALK_FRAME + step;

        return Renderer.Render(in look, frame, AislingRenderer.WALK_ANIM, flip, isFront);
    }
}

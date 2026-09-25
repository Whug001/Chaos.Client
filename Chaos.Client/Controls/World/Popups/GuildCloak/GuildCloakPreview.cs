#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     A walking figure in a guild cloak for the editor and review windows, modeled on the beauty shop's preview. It turns
///     through four facings and switches body type. Each walk step is rendered once and kept until the design, the facing or
///     the body changes.
/// </summary>
public sealed class GuildCloakPreview : UIElement
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
    private Gender BodyGender = Gender.Male;
    private int DesignId;
    private int Facing;

    public GuildCloakPreview(AislingRenderer renderer, int width, int height)
    {
        Renderer = renderer;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
        IsHitTestVisible = false;
    }

    public bool IsMale => BodyGender == Gender.Male;
    public bool Paused => Cycle.Paused;

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

    public void SetDesignId(int designId)
    {
        if (designId == DesignId)
            return;

        DesignId = designId;
        ReleaseFrames();
    }

    /// <summary>Pauses and moves <paramref name="delta" /> walk steps. Turning and switching body keep the step.</summary>
    public void StepBy(int delta) => Cycle.StepBy(delta);

    public void ToggleBody()
    {
        BodyGender = IsMale ? Gender.Female : Gender.Male;
        ReleaseFrames();
    }

    public void TogglePause() => Cycle.TogglePause();

    public void Turn(int delta)
    {
        Facing = (Facing + delta + FACING_COUNT) % FACING_COUNT;
        ReleaseFrames();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        Cycle.Advance(gameTime.ElapsedGameTime.TotalMilliseconds);
    }

    private Texture2D? RenderStep(int step)
    {
        (var frame, var flip, var isFront) = Facings[Facing];

        var appearance = new AislingAppearance
        {
            Gender = BodyGender,
            BodySpriteId = AislingRenderer.BODY_ID,
            HeadSprite = 1,
            Accessory1Sprite = GuildCloakProtocol.SPRITE,
            GuildCloakDesignId = DesignId
        };

        return Renderer.Render(in appearance, frame + FIRST_WALK_FRAME + step, AislingRenderer.WALK_ANIM, flip, isFront);
    }
}

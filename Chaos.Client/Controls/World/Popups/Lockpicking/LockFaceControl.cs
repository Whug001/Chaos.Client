using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>
///     Draws the lock from the <c>lockpk0N.spf</c> sprites: the plate, the cylinder frame for the turn angle, the
///     wrench turning with the cylinder, and the pick at its angle, or its two halves falling after it snaps. Like
///     <see cref="Wheel.WheelControl" />, it replaces UIPanel's drawing with its own, and it is not hit-testable,
///     so mouse events reach the panel around it.
/// </summary>
public sealed class LockFaceControl : UIPanel
{
    public const int SIZE = 184;
    private const float WRENCH_REST_DEGREES = -35f;
    private const float WRENCH_DROP_PIXELS = 14f;
    private const float HANDLE_FALL_PIXELS_PER_SECOND_SQUARED = 240f;
    private const float TIP_FALL_PIXELS_PER_SECOND_SQUARED = 120f;

    private readonly LockpickAnimator Animator;

    public LockFaceControl(LockpickAnimator animator)
    {
        Animator = animator;
        Width = SIZE;
        Height = SIZE;
        IsHitTestVisible = false;
    }

    public Vector2 CenterScreen => new(ScreenX + (SIZE / 2f), ScreenY + (SIZE / 2f));

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var ui = UiRenderer.Instance!;
        var center = CenterScreen;

        DrawCentered(spriteBatch, ui.GetSpfTexture("lockpk01.spf"), center);
        DrawCentered(spriteBatch, ui.GetSpfTexture("lockpk02.spf", LockpickGeometry.CylinderFrame(Animator.CylinderDegrees)), center);

        var wrench = ui.GetSpfTexture("lockpk04.spf");
        var cylinderRadians = MathHelper.ToRadians(Animator.CylinderDegrees);
        var wrenchPivot = center + Vector2.Transform(new Vector2(0f, WRENCH_DROP_PIXELS), Matrix.CreateRotationZ(cylinderRadians));

        AtlasHelper.Draw(
            spriteBatch,
            wrench,
            wrenchPivot,
            null,
            Color.White,
            MathHelper.ToRadians(WRENCH_REST_DEGREES + Animator.CylinderDegrees),
            new Vector2(wrench.Width / 2f, 0f),
            1f,
            SpriteEffects.None,
            0f);

        var pickRotation = MathHelper.ToRadians(Animator.PickDegrees - 90f);
        var pickPivot = center + new Vector2(Animator.PickShakeOffset, 0f);

        if (Animator.State is LockpickAnimState.Snapped or LockpickAnimState.Finished)
        {
            var t = Animator.StateSeconds;
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk05.spf"), pickPivot + new Vector2(0f, TIP_FALL_PIXELS_PER_SECOND_SQUARED * t * t), pickRotation);
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk05.spf", 1), pickPivot + new Vector2(0f, HANDLE_FALL_PIXELS_PER_SECOND_SQUARED * t * t), pickRotation);
        } else
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk03.spf"), pickPivot, pickRotation);
    }

    private static void DrawCentered(SpriteBatch spriteBatch, Texture2D texture, Vector2 center)
        => AtlasHelper.Draw(spriteBatch, texture, new Vector2(center.X - (texture.Width / 2f), center.Y - (texture.Height / 2f)), Color.White);

    //the pick sprite points up with its tip at the bottom centre, which is the pivot in the keyhole
    private static void DrawPick(SpriteBatch spriteBatch, Texture2D texture, Vector2 pivot, float rotation)
        => AtlasHelper.Draw(
            spriteBatch,
            texture,
            pivot,
            null,
            Color.White,
            rotation,
            new Vector2(texture.Width / 2f, texture.Height - 1f),
            1f,
            SpriteEffects.None,
            0f);
}

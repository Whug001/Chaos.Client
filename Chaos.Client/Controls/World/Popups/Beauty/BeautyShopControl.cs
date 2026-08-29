#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Josephine's mirror: a full-sprite preview on the left, one row per appearance category on the right, a
///     running total underneath, and a single Apply. Reads everything from <see cref="WorldState.BeautyShop" />;
///     nothing here costs gold until the server answers an Apply.
/// </summary>
public sealed class BeautyShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 520;
    private const int PANEL_HEIGHT = 330;
    private const int TOP_MARGIN = 60;
    private const int TITLE_TOP = 12;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;

    //preview column
    private const int PREVIEW_LEFT = 24;
    private const int PREVIEW_TOP = 36;
    private const int PREVIEW_WIDTH = 150;
    private const int PREVIEW_HEIGHT = 170;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ROTATE_ROW_TOP = PREVIEW_TOP + PREVIEW_HEIGHT + 6;

    //rows column (Task 11)
    internal const int ROWS_LEFT = PREVIEW_LEFT + PREVIEW_WIDTH + 24;
    internal const int ROWS_TOP = 40;
    internal const int ROW_HEIGHT = 30;
    internal const int ROWS_WIDTH = PANEL_WIDTH - ROWS_LEFT - 24;

    private readonly AislingRenderer Renderer;
    private readonly PreviewView Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly UILabel TitleLabel;
    private int FacingIndex;

    /// <summary>The player closed the panel (button or Escape). Fires once per hide.</summary>
    public event Action? Closed;

    public BeautyShopControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Renderer = renderer;

        Name = "BeautyShop";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Josephine's Mirror"
        };
        AddChild(TitleLabel);

        Preview = new PreviewView(renderer)
        {
            X = PREVIEW_LEFT,
            Y = PREVIEW_TOP,
            Width = PREVIEW_WIDTH,
            Height = PREVIEW_HEIGHT
        };
        AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = ROTATE_ROW_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + ROTATE_BUTTON_WIDTH + 4,
            Y = ROTATE_ROW_TOP
        };
        rotateRight.Clicked += () => Rotate(+1);
        AddChild(rotateRight);

        GearToggle = new CustomCheckBox
        {
            X = PREVIEW_LEFT + (ROTATE_BUTTON_WIDTH * 2) + 16,
            Y = ROTATE_ROW_TOP + ((CustomButton.HEIGHT - CustomCheckBox.CHECKBOX_SIZE) / 2),
            Text = "Show gear",
            Checked = false
        };
        GearToggle.Clicked += () => RefreshPreview();
        AddChild(GearToggle);

        BuildRows();      //Task 11
        BuildFooter();    //Task 11
    }

    /// <summary>Repaints the preview and (Task 11) every row from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshRows();    //Task 11
    }

    public override void Show()
    {
        FacingIndex = 0;
        GearToggle.Checked = false;
        base.Show();
        Refresh();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Preview.Release();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestDismissal();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>The player's own close. Nothing is at stake before Apply, so no confirmation.</summary>
    private void RequestDismissal()
    {
        if (!Visible)
            return;

        Hide();
        Closed?.Invoke();
    }

    private void Rotate(int delta)
    {
        FacingIndex = (FacingIndex + delta + PreviewView.FACING_COUNT) % PreviewView.FACING_COUNT;
        RefreshPreview();
    }

    private void RefreshPreview() => Preview.Refresh(BuildAppearance(), FacingIndex);

    /// <summary>
    ///     Bare body + hair + face by default so every change is visible; with gear on, the player's live world
    ///     appearance (armor, helmet, weapon...) with the five editable fields overridden.
    /// </summary>
    private AislingAppearance BuildAppearance()
    {
        var vm = WorldState.BeautyShop;

        var bare = new AislingAppearance
        {
            Gender = vm.Gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            BodyColor = (int)vm.BodyColor,
            HeadSprite = vm.HairStyle,
            HeadColor = vm.HairColor,
            FaceSprite = vm.FaceSprite
        };

        if (!GearToggle.Checked)
            return bare;

        var live = WorldState.GetPlayerEntity()?.Appearance;

        if (live is null)
            return bare;

        return live.Value with
        {
            Gender = vm.Gender,
            BodyColor = (int)vm.BodyColor,
            HeadSprite = vm.HairStyle,
            HeadColor = vm.HairColor,
            FaceSprite = vm.FaceSprite
        };
    }

    public override void Dispose()
    {
        Preview.Dispose();
        base.Dispose();
    }

    /// <summary>
    ///     The sprite. Owns exactly one composited texture at a time -- <see cref="AislingRenderer.Render" />
    ///     allocates a fresh texture per call and caches only per world entity, so this view caches by
    ///     (appearance, facing) and disposes the previous texture on every re-render (see PokerTableControl's
    ///     PortraitView for the same reasoning).
    /// </summary>
    private sealed class PreviewView(AislingRenderer renderer) : UIElement
    {
        public const int FACING_COUNT = 4;

        //(frame, flip, isFront): Down, Right, Up, Left. Epfs hold up (0-4) and right (5-9); down = right flipped, left = up flipped.
        private static readonly (int Frame, bool Flip, bool IsFront)[] FACINGS =
        [
            (5, true, true),
            (5, false, true),
            (0, false, false),
            (0, true, false)
        ];

        private Texture2D? Figure;
        private AislingAppearance? RenderedAppearance;
        private int RenderedFacing = -1;

        public void Refresh(AislingAppearance appearance, int facingIndex)
        {
            if (Nullable.Equals(appearance, RenderedAppearance) && (facingIndex == RenderedFacing))
                return;

            RenderedAppearance = appearance;
            RenderedFacing = facingIndex;

            Figure?.Dispose();
            var (frame, flip, isFront) = FACINGS[facingIndex];
            Figure = renderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront);
        }

        /// <summary>Drops the texture on hide so a closed panel holds no GPU memory; the next Show re-renders.</summary>
        public void Release()
        {
            Figure?.Dispose();
            Figure = null;
            RenderedAppearance = null;
            RenderedFacing = -1;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (!Visible || (Figure is null))
                return;

            //centre the body (not the padded canvas) in the box, feet a little above the bottom edge
            var x = ScreenX + (Width / 2) - AislingRenderer.CANVAS_CENTER_X;
            var y = ScreenY + Height - AislingRenderer.COMPOSITE_HEIGHT - 12;

            DrawTexture(spriteBatch, Figure, new Vector2(x, y), Color.White);
        }

        public override void Dispose()
        {
            Release();
            base.Dispose();
        }
    }

    //── Task 11 fills these in ──
    private void BuildRows() { }
    private void BuildFooter() { }
    private void RefreshRows() { }
}

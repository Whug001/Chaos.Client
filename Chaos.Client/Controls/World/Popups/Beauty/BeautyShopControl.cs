#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Data;
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
    private const int GEAR_TOGGLE_WIDTH = CustomCheckBox.CHECKBOX_SIZE + CustomCheckBox.CAPTION_GAP + (9 * TextRenderer.CHAR_WIDTH);

    //rows column (Task 11)
    internal const int ROWS_LEFT = PREVIEW_LEFT + PREVIEW_WIDTH + 24;
    internal const int ROWS_TOP = 40;
    internal const int ROW_HEIGHT = 30;
    internal const int ROWS_WIDTH = PANEL_WIDTH - ROWS_LEFT - 24;

    //rows
    private const int ARROW_WIDTH = 26;
    private const int LABEL_WIDTH = 74;
    private const int VALUE_WIDTH = 94; //leaves the price column (ROWS_WIDTH - PRICE_WIDTH) clear of the right arrow
    private const int PRICE_WIDTH = 70;
    private const int SWATCH_SIZE = 14;
    private const int GENDER_BUTTON_WIDTH = 64;

    //footer
    private const int FOOTER_TOP = ROWS_TOP + (ROW_HEIGHT * 5) + 12;
    private const int FOOTER_BUTTON_WIDTH = 70;

    private readonly PreviewView Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly UILabel TitleLabel;
    private int FacingIndex;

    private CustomButton MaleButton = null!;
    private CustomButton FemaleButton = null!;
    private UILabel GenderPriceLabel = null!;
    private OptionRow HairstyleRow = null!;
    private OptionRow HairColorRow = null!;
    private OptionRow BodyColorRow = null!;
    private OptionRow FaceRow = null!;
    private UIPanel HairSwatch = null!;
    private UILabel TotalLabel = null!;
    private UILabel GoldLabel = null!;
    private UILabel StatusLabel = null!;
    private CustomButton ResetButton = null!;
    private CustomButton ApplyButton = null!;
    private OkPopupMessageControl ConfirmDialog = null!;
    private Texture2D? SwatchTexture;
    private DisplayColor? SwatchColor;

    /// <summary>The player closed the panel (button or Escape). Fires once per hide.</summary>
    public event Action? Closed;

    /// <summary>The player pressed Apply (and confirmed, when a gender change was involved).</summary>
    public event Action? ApplyRequested;

    public BeautyShopControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);

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
            Width = GEAR_TOGGLE_WIDTH,
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Show gear",
            Checked = false
        };

        GearToggle.Clicked += () =>
        {
            GearToggle.Checked = !GearToggle.Checked;
            RefreshPreview();
        };
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
        StatusLabel.Text = string.Empty;
        base.Show();
        Refresh();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        //a gender-reshape prompt still standing when the panel goes away -- the player's own X/Escape while it is
        //up, or the server closing the session out from under it -- must not be left visible or on the input
        //stack; see PokerTableControl.Hide for the same reasoning.
        ConfirmDialog.Hide();
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

            if (Figure is null)
                RenderedFacing = -1;
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
            var y = ScreenY + Height - Figure.Height - 12;

            DrawTexture(spriteBatch, Figure, new Vector2(x, y), Color.White);
        }

        public override void Dispose()
        {
            Release();
            base.Dispose();
        }
    }

    /// <summary>One "◀ value ▶  price *" line. The row owns no state; the control feeds it text on every refresh.</summary>
    private sealed class OptionRow : UIPanel
    {
        public readonly CustomButton Left;
        public readonly CustomButton Right;
        public readonly UILabel Caption;
        public readonly UILabel Value;
        public readonly UILabel Price;

        public OptionRow(string caption, Action<int> step, int width)
        {
            Width = width;
            Height = ROW_HEIGHT;
            Background = null;

            Caption = new UILabel
            {
                X = 0,
                Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = LABEL_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false,
                Text = caption
            };
            AddChild(Caption);

            Left = new CustomButton("<", ARROW_WIDTH) { X = LABEL_WIDTH, Y = (ROW_HEIGHT - CustomButton.HEIGHT) / 2 };
            Left.Clicked += () => step(-1);
            AddChild(Left);

            Value = new UILabel
            {
                X = LABEL_WIDTH + ARROW_WIDTH + 4,
                Y = Caption.Y,
                Width = VALUE_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };
            AddChild(Value);

            Right = new CustomButton(">", ARROW_WIDTH) { X = Value.X + VALUE_WIDTH + 4, Y = Left.Y };
            Right.Clicked += () => step(+1);
            AddChild(Right);

            Price = new UILabel
            {
                X = width - PRICE_WIDTH,
                Y = Caption.Y,
                Width = PRICE_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Gold,
                IsHitTestVisible = false
            };
            AddChild(Price);
        }

        public void Set(string value, int price, bool changed)
        {
            Value.Text = value;
            Price.Text = changed ? $"+{price:N0} *" : $"{price:N0}";
            Price.ForegroundColor = changed ? LegendColors.Gold : LegendColors.Gray;
        }
    }

    private void BuildRows()
    {
        var vm = WorldState.BeautyShop;
        var y = ROWS_TOP;

        //── gender: two buttons, the selected one greyed ──
        var genderCaption = new UILabel
        {
            X = ROWS_LEFT,
            Y = y + ((ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            Width = LABEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Gender"
        };
        AddChild(genderCaption);

        MaleButton = new CustomButton("Male", GENDER_BUTTON_WIDTH) { X = ROWS_LEFT + LABEL_WIDTH, Y = y + ((ROW_HEIGHT - CustomButton.HEIGHT) / 2) };
        MaleButton.Clicked += () => Select(v => v.SetGender(Gender.Male));
        AddChild(MaleButton);

        FemaleButton = new CustomButton("Female", GENDER_BUTTON_WIDTH) { X = MaleButton.X + GENDER_BUTTON_WIDTH + 6, Y = MaleButton.Y };
        FemaleButton.Clicked += () => Select(v => v.SetGender(Gender.Female));
        AddChild(FemaleButton);

        GenderPriceLabel = new UILabel
        {
            X = ROWS_LEFT + ROWS_WIDTH - PRICE_WIDTH,
            Y = genderCaption.Y,
            Width = PRICE_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        AddChild(GenderPriceLabel);
        y += ROW_HEIGHT;

        HairstyleRow = AddRow("Hairstyle", d => vm.StepHairstyle(d), ref y);
        HairColorRow = AddRow("Hair dye", d => vm.StepHairColor(d), ref y);

        //sits between the "Hair dye" caption and the left arrow -- the only gap wide enough for it
        HairSwatch = new UIPanel
        {
            X = HairColorRow.X + LABEL_WIDTH - SWATCH_SIZE - 4,
            Y = HairColorRow.Y + ((ROW_HEIGHT - SWATCH_SIZE) / 2),
            Width = SWATCH_SIZE,
            Height = SWATCH_SIZE,
            IsHitTestVisible = false,
            ZIndex = 1
        };
        AddChild(HairSwatch);

        BodyColorRow = AddRow("Skin", d => vm.StepBodyColor(d), ref y);
        FaceRow = AddRow("Face", d => vm.StepFace(d), ref y);
    }

    private OptionRow AddRow(string caption, Action<int> step, ref int y)
    {
        var row = new OptionRow(caption, delta => Select(_ => step(delta)), ROWS_WIDTH) { X = ROWS_LEFT, Y = y };
        AddChild(row);
        y += ROW_HEIGHT;

        return row;
    }

    /// <summary>Every selection change goes through here: mutate the view model, then repaint everything.</summary>
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        //any change while the gender-reshape prompt is up invalidates what it was about to confirm
        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        mutate(WorldState.BeautyShop);
        StatusLabel.Text = string.Empty;
        Refresh();
    }

    private void BuildFooter()
    {
        TotalLabel = FooterLabel(ROWS_LEFT, FOOTER_TOP, ROWS_WIDTH / 2);
        GoldLabel = FooterLabel(ROWS_LEFT + (ROWS_WIDTH / 2), FOOTER_TOP, ROWS_WIDTH / 2);
        GoldLabel.HorizontalAlignment = HorizontalAlignment.Right;
        StatusLabel = FooterLabel(ROWS_LEFT, FOOTER_TOP + TextRenderer.CHAR_HEIGHT + 4, ROWS_WIDTH);
        StatusLabel.ForegroundColor = LegendColors.Red;

        //kept clear of the frame's ornate bottom border rather than OK_BOTTOM_MARGIN (which is sized for the
        //small round close button, not this row of full-width buttons)
        var buttonsTop = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;

        ResetButton = new CustomButton("Reset", FOOTER_BUTTON_WIDTH) { X = ROWS_LEFT, Y = buttonsTop };
        ResetButton.Clicked += () => Select(v => v.Reset());
        AddChild(ResetButton);

        ApplyButton = new CustomButton("Apply", FOOTER_BUTTON_WIDTH) { X = ROWS_LEFT + FOOTER_BUTTON_WIDTH + 8, Y = buttonsTop, Enabled = false };
        ApplyButton.Clicked += OnApplyClicked;
        AddChild(ApplyButton);

        //parented to the panel like poker's leave confirm, drawn above everything else in it
        ConfirmDialog = new OkPopupMessageControl(true) { Name = "BeautyShopGenderConfirm", ZIndex = 100 };
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();

            //re-validate: a row change or Reset while the prompt was up must not sneak an apply through
            if (WorldState.BeautyShop.CanApply && WorldState.BeautyShop.GenderChanged)
                ApplyRequested?.Invoke();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    private UILabel FooterLabel(int x, int y, int width)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(label);

        return label;
    }

    private void OnApplyClicked()
    {
        var vm = WorldState.BeautyShop;

        if (!vm.CanApply)
            return;

        if (vm.GenderChanged)
        {
            ConfirmDialog.Show(
                $"This will reshape your Master and Grandmaster gear - equipped, banked and in inventory - for {vm.GenderPrice:N0} gold. Continue?");

            return;
        }

        ApplyRequested?.Invoke();
    }

    /// <summary>Server refused the Apply; keep the panel open and say why.</summary>
    public void OnRejected(BeautyShopRejectReason reason)
    {
        if (!Visible)
            return;

        StatusLabel.Text = reason switch
        {
            BeautyShopRejectReason.NothingChanged => "Nothing has changed.",
            BeautyShopRejectReason.InsufficientGold => "You can't afford that.",
            BeautyShopRejectReason.InvalidSelection => "Josephine can't do that one.",
            BeautyShopRejectReason.GenderSwapUnavailable => "Josephine can't reshape your class's gear.",
            BeautyShopRejectReason.NotNearShop => "Step closer to Josephine.",
            _ => "Josephine shakes her head."
        };
    }

    private void RefreshRows()
    {
        var vm = WorldState.BeautyShop;

        MaleButton.Enabled = vm.Gender != Gender.Male;
        FemaleButton.Enabled = vm.Gender != Gender.Female;
        GenderPriceLabel.Text = vm.GenderChanged ? $"+{vm.GenderPrice:N0} *" : $"{vm.GenderPrice:N0}";
        GenderPriceLabel.ForegroundColor = vm.GenderChanged ? LegendColors.Gold : LegendColors.Gray;

        HairstyleRow.Set($"Style {vm.HairStyle}", vm.HairstylePrice, vm.HairstyleChanged);
        HairColorRow.Set(vm.HairColor.ToString(), vm.HairDyePrice, vm.HairColorChanged);
        BodyColorRow.Set(vm.BodyColor.ToString(), vm.BodyDyePrice, vm.BodyColorChanged);

        var face = vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite);
        FaceRow.Set(face?.Name ?? $"Face {vm.FaceSprite}", vm.FacePrice, vm.FaceChanged);

        RefreshSwatch(vm.HairColor);

        TotalLabel.Text = $"Total: {vm.Total:N0} gold";
        TotalLabel.ForegroundColor = vm.CanAfford ? LegendColors.White : LegendColors.Red;
        GoldLabel.Text = $"You have: {vm.Gold:N0}";
        ApplyButton.Enabled = vm.CanApply;
    }

    /// <summary>A flat 14x14 tile of the dye's mid-tone, rebuilt only when the colour changes.</summary>
    private void RefreshSwatch(DisplayColor color)
    {
        if (SwatchColor == color)
            return;

        SwatchColor = color;
        var rgb = SwatchColorFor(color);

        SwatchTexture?.Dispose();
        var device = ChaosGame.Device;
        SwatchTexture = new Texture2D(device, SWATCH_SIZE, SWATCH_SIZE);
        SwatchTexture.SetData(Enumerable.Repeat(rgb, SWATCH_SIZE * SWATCH_SIZE).ToArray());
        HairSwatch.Background = SwatchTexture;
    }

    private static Color SwatchColorFor(DisplayColor color)
    {
        if (color == DisplayColor.Default)
            return LegendColors.DeepLavender;

        var table = DataContext.AislingDrawData.DyeColorTable;

        if (!table.Contains((int)color))
            return LegendColors.Gray;

        var colors = table[(int)color].Colors;
        var mid = colors[Math.Min(2, colors.Length - 1)];

        return new Color(mid.Red, mid.Green, mid.Blue);
    }

    public override void Dispose()
    {
        HairSwatch.Background = null; //detach so base doesn't double-dispose
        SwatchTexture?.Dispose();
        base.Dispose();
    }
}

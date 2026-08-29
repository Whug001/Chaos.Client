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
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Josephine's mirror: a full-sprite preview on a recessed pedestal on the left, gender/hairstyle/dye/skin/face
///     pickers on the right, and a purchase summary underneath. Reads everything from
///     <see cref="WorldState.BeautyShop" />; nothing here costs gold until the server answers an Apply.
/// </summary>
public sealed class BeautyShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 470;
    private const int TOP_MARGIN = 5;
    private const int TITLE_TOP = 10;
    private const int SUBTITLE_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;

    //preview column
    private const int PREVIEW_LEFT = 20;
    private const int PREVIEW_TOP = 44;
    private const int PREVIEW_WIDTH = 200;
    private const int PEDESTAL_WIDTH = 180;
    private const int PEDESTAL_HEIGHT = 200;
    private const int PEDESTAL_LEFT = PREVIEW_LEFT + ((PREVIEW_WIDTH - PEDESTAL_WIDTH) / 2);
    private const int PEDESTAL_TOP = PREVIEW_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PREVIEW_CONTROLS_TOP = PEDESTAL_TOP + PEDESTAL_HEIGHT + 6;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ZOOM_BUTTON_WIDTH = 60;
    private const int RANDOMIZE_BUTTON_WIDTH = 100;
    private const int GEAR_TOGGLE_WIDTH = CustomCheckBox.CHECKBOX_SIZE + CustomCheckBox.CAPTION_GAP + (9 * TextRenderer.CHAR_WIDTH);

    //appearance column
    private const int APPEARANCE_LEFT = 236;
    private const int APPEARANCE_TOP = 44;
    private const int APPEARANCE_WIDTH = PANEL_WIDTH - APPEARANCE_LEFT - 20;
    private const int SECTION_GAP = 6;
    private const int CAPTION_ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    //the dye grid's vertical budget is fixed at construction time (a design assumption -- 8 rows), not the live
    //color count, because every other row below it is placed once and never moves
    private const int DYE_GRID_ROWS = 8;
    private const int DYE_GRID_HEIGHT = (DYE_GRID_ROWS * SwatchGrid.SWATCH) + ((DYE_GRID_ROWS - 1) * SwatchGrid.GAP);

    //summary band
    private const int SUMMARY_TOP = 358;
    private const int SUMMARY_LINE2_TOP = 372;
    private const int SUMMARY_LINE3_TOP = 400;
    private const int SUMMARY_LEFT = PREVIEW_LEFT;
    private const int SUMMARY_WIDTH = PANEL_WIDTH - (PREVIEW_LEFT * 2);
    private const int SUMMARY_HALF_WIDTH = SUMMARY_WIDTH / 2;
    private const int SUMMARY_HOVER_WIDTH = 320;
    private const int DISCARD_BUTTON_WIDTH = 130;
    private const int DISCARD_BUTTON_LEFT = 352;
    private const int APPLY_BUTTON_WIDTH = 90;
    private const int APPLY_BUTTON_LEFT = 490;

    private readonly PreviewView Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly CustomButton ZoomButton;
    private readonly CustomButton RandomizeButton;
    private readonly GenderSelector GenderPicker;
    private readonly ThumbnailStrip<BeautyShopHairstyleEntry> HairstyleStrip;
    private readonly SwatchGrid DyeGrid;
    private readonly ThumbnailStrip<BodyColor> SkinStrip;
    private readonly ThumbnailStrip<BeautyShopFaceEntry> FaceStrip;
    private readonly HeadThumbnailRenderer Thumbnails;

    private readonly UILabel TitleLabel;
    private readonly UILabel SubtitleLabel;
    private readonly UILabel UnsavedLabel;
    private readonly UILabel PreviewCaption;
    private readonly UILabel HairstyleCaption;
    private readonly UILabel DyeCaption;
    private readonly UILabel SkinCaption;
    private readonly UILabel FaceCaption;
    private readonly UILabel SummaryLabel;
    private readonly UILabel TotalLabel;
    private readonly UILabel HoverTotalLabel;
    private readonly UILabel GoldLabel;
    private readonly UILabel StatusLabel;

    private readonly CustomButton DiscardButton;
    private readonly CustomButton ApplyButton;
    private readonly OkPopupMessageControl ConfirmDialog;

    private int FacingIndex;
    private bool Zoomed = true;

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

        Thumbnails = new HeadThumbnailRenderer(renderer);

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = Caption("JOSEPHINE'S MIRROR", 0, TITLE_TOP, PANEL_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        SubtitleLabel = Caption("Customize your appearance", PREVIEW_LEFT, SUBTITLE_TOP, 300, HorizontalAlignment.Left, LegendColors.LightGray);
        UnsavedLabel = Caption("● Unsaved changes", PANEL_WIDTH - 20 - 160, SUBTITLE_TOP, 160, HorizontalAlignment.Right, LegendColors.Gold);
        UnsavedLabel.Visible = false;

        //── preview column ──
        PreviewCaption = Caption("PREVIEW", PREVIEW_LEFT, PREVIEW_TOP, PREVIEW_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        Preview = new PreviewView(renderer)
        {
            X = PEDESTAL_LEFT,
            Y = PEDESTAL_TOP,
            Width = PEDESTAL_WIDTH,
            Height = PEDESTAL_HEIGHT
        };
        AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PEDESTAL_LEFT, Y = PREVIEW_CONTROLS_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH)
        {
            X = PEDESTAL_LEFT + ROTATE_BUTTON_WIDTH + 4,
            Y = PREVIEW_CONTROLS_TOP
        };
        rotateRight.Clicked += () => Rotate(+1);
        AddChild(rotateRight);

        ZoomButton = new CustomButton("2x", ZOOM_BUTTON_WIDTH)
        {
            X = PEDESTAL_LEFT + PEDESTAL_WIDTH - ZOOM_BUTTON_WIDTH,
            Y = PREVIEW_CONTROLS_TOP
        };
        ZoomButton.Clicked += () =>
        {
            Zoomed = !Zoomed;
            Preview.Zoomed = Zoomed;
            ZoomButton.Caption = Zoomed ? "2x" : "1x";
        };
        AddChild(ZoomButton);

        GearToggle = new CustomCheckBox
        {
            X = PEDESTAL_LEFT,
            Y = PREVIEW_CONTROLS_TOP + CustomButton.HEIGHT + 6,
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

        RandomizeButton = new CustomButton("Randomize", RANDOMIZE_BUTTON_WIDTH)
        {
            X = PEDESTAL_LEFT,
            Y = GearToggle.Y + CustomCheckBox.CHECKBOX_SIZE + 6
        };
        RandomizeButton.Clicked += () => Select(v => v.Randomize());
        AddChild(RandomizeButton);

        //── appearance column ──
        var y = APPEARANCE_TOP;

        Caption("Gender", APPEARANCE_LEFT, y, 80);
        GenderPicker = new GenderSelector { X = APPEARANCE_LEFT + 80, Y = y - 4 };
        GenderPicker.GenderChosen += g => Select(v => v.SetGender(g));
        AddChild(GenderPicker);
        y += GenderSelector.HEIGHT + SECTION_GAP;

        HairstyleCaption = Caption("Hairstyle", APPEARANCE_LEFT, y, APPEARANCE_WIDTH);
        y += CAPTION_ROW_HEIGHT;
        HairstyleStrip = new ThumbnailStrip<BeautyShopHairstyleEntry>(ViewModel.BeautyShop.HAIRSTYLE_PAGE_SIZE) { X = APPEARANCE_LEFT, Y = y };
        HairstyleStrip.Hovered += h => Hover(v => v.SetHoverHairStyle(h.Sprite));
        HairstyleStrip.HoverCleared += () => Hover(v => v.ClearHover());
        HairstyleStrip.Selected += h => Select(v => v.SelectHairStyle(h.Sprite));
        HairstyleStrip.PageStepped += d => Select(v => v.StepHairstylePage(d));
        AddChild(HairstyleStrip);
        y += ThumbnailStrip<BeautyShopHairstyleEntry>.CELL + SECTION_GAP;

        DyeCaption = Caption("Hair dye", APPEARANCE_LEFT, y, APPEARANCE_WIDTH);
        y += CAPTION_ROW_HEIGHT;
        DyeGrid = new SwatchGrid { X = APPEARANCE_LEFT, Y = y };
        DyeGrid.Hovered += c => Hover(v => v.SetHoverHairColor(c));
        DyeGrid.HoverCleared += () => Hover(v => v.ClearHover());
        DyeGrid.Selected += c => Select(v => v.SelectHairColor(c));
        AddChild(DyeGrid);
        y += DYE_GRID_HEIGHT + SECTION_GAP;

        SkinCaption = Caption("Skin", APPEARANCE_LEFT, y, APPEARANCE_WIDTH);
        y += CAPTION_ROW_HEIGHT;
        SkinStrip = new ThumbnailStrip<BodyColor>(ViewModel.BeautyShop.BODY_COLOR_PAGE_SIZE) { X = APPEARANCE_LEFT, Y = y };
        SkinStrip.Hovered += c => Hover(v => v.SetHoverBodyColor(c));
        SkinStrip.HoverCleared += () => Hover(v => v.ClearHover());
        SkinStrip.Selected += c => Select(v => v.SelectBodyColor(c));
        SkinStrip.PageStepped += d => Select(v => v.StepBodyColorPage(d));
        AddChild(SkinStrip);
        y += ThumbnailStrip<BodyColor>.CELL + SECTION_GAP;

        FaceCaption = Caption("Face", APPEARANCE_LEFT, y, APPEARANCE_WIDTH);
        y += CAPTION_ROW_HEIGHT;
        FaceStrip = new ThumbnailStrip<BeautyShopFaceEntry>(ViewModel.BeautyShop.FACE_PAGE_SIZE) { X = APPEARANCE_LEFT, Y = y };
        FaceStrip.Hovered += f => Hover(v => v.SetHoverFace(f.Sprite));
        FaceStrip.HoverCleared += () => Hover(v => v.ClearHover());
        FaceStrip.Selected += f => Select(v => v.SelectFace(f.Sprite));
        FaceStrip.PageStepped += d => Select(v => v.StepFacePage(d));
        AddChild(FaceStrip);

        //── purchase summary band ──
        SummaryLabel = Caption("No changes", SUMMARY_LEFT, SUMMARY_TOP, SUMMARY_WIDTH);
        TotalLabel = Caption(string.Empty, SUMMARY_LEFT, SUMMARY_LINE2_TOP, SUMMARY_HALF_WIDTH);
        GoldLabel = Caption(
            string.Empty,
            SUMMARY_LEFT + SUMMARY_HALF_WIDTH,
            SUMMARY_LINE2_TOP,
            SUMMARY_WIDTH - SUMMARY_HALF_WIDTH,
            HorizontalAlignment.Right);
        HoverTotalLabel = Caption(string.Empty, SUMMARY_LEFT, SUMMARY_LINE3_TOP, SUMMARY_HOVER_WIDTH, HorizontalAlignment.Left, LegendColors.Gray);
        StatusLabel = Caption(string.Empty, SUMMARY_LEFT, SUMMARY_LINE3_TOP, SUMMARY_HOVER_WIDTH, HorizontalAlignment.Left, LegendColors.Red);

        //kept clear of the frame's ornate bottom border rather than OK_BOTTOM_MARGIN (which is sized for the
        //small round close button, not this row of full-width buttons)
        var buttonsTop = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;

        DiscardButton = new CustomButton("Discard Changes", DISCARD_BUTTON_WIDTH) { X = DISCARD_BUTTON_LEFT, Y = buttonsTop };
        DiscardButton.Clicked += () => Select(v => v.Reset());
        AddChild(DiscardButton);

        ApplyButton = new CustomButton("APPLY", APPLY_BUTTON_WIDTH) { X = APPLY_BUTTON_LEFT, Y = buttonsTop, Enabled = false };
        ApplyButton.Clicked += OnApplyClicked;
        AddChild(ApplyButton);

        //parented to the panel like poker's leave confirm, drawn above everything else in it
        ConfirmDialog = new OkPopupMessageControl(true) { Name = "BeautyShopGenderConfirm", ZIndex = 100 };
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();

            //re-validate: a picker change or Discard while the prompt was up must not sneak an apply through
            if (WorldState.BeautyShop.CanApply && WorldState.BeautyShop.GenderChanged)
                ApplyRequested?.Invoke();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    private UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };
        AddChild(label);

        return label;
    }

    /// <summary>Repaints the preview, every picker and the summary band from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshPickers();
        RefreshSummary();
        UnsavedLabel.Visible = WorldState.BeautyShop.HasUnsavedChanges;
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
        Thumbnails.Clear();
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
    ///     appearance (armor, helmet, weapon...) with the five editable fields overridden. Uses the
    ///     <b>effective</b> (hover-aware) values so the preview shows what the player is pointing at, not just
    ///     what they have chosen.
    /// </summary>
    private AislingAppearance BuildAppearance()
    {
        var vm = WorldState.BeautyShop;

        var bare = BareAppearance(vm.Gender, vm.EffectiveHairStyle, vm.EffectiveHairColor, vm.EffectiveBodyColor, vm.EffectiveFaceSprite);

        if (!GearToggle.Checked)
            return bare;

        var live = WorldState.GetPlayerEntity()?.Appearance;

        if (live is null)
            return bare;

        return live.Value with
        {
            Gender = vm.Gender,
            BodyColor = (int)vm.EffectiveBodyColor,
            HeadSprite = vm.EffectiveHairStyle,
            HeadColor = vm.EffectiveHairColor,
            FaceSprite = vm.EffectiveFaceSprite
        };
    }

    private static AislingAppearance BareAppearance(Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
        => new()
        {
            Gender = gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            BodyColor = (int)bodyColor,
            HeadSprite = hairStyle,
            HeadColor = hairColor,
            FaceSprite = faceSprite
        };

    /// <summary>
    ///     The sprite. Owns exactly one composited texture at a time -- <see cref="AislingRenderer.Render" />
    ///     allocates a fresh texture per call and caches only per world entity, so this view caches by
    ///     (appearance, facing) and disposes the previous texture on every re-render (see PokerTableControl's
    ///     PortraitView for the same reasoning). Zoom is a pure draw-time scale -- it never invalidates the cache.
    /// </summary>
    private sealed class PreviewView : UIElement
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

        private readonly AislingRenderer Renderer;
        private readonly Texture2D Pedestal;

        private Texture2D? Figure;
        private AislingAppearance? RenderedAppearance;
        private int RenderedFacing = -1;

        public bool Zoomed { get; set; } = true;

        public PreviewView(AislingRenderer renderer)
        {
            Renderer = renderer;
            Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), PEDESTAL_WIDTH, PEDESTAL_HEIGHT);
        }

        public void Refresh(AislingAppearance appearance, int facingIndex)
        {
            if (Nullable.Equals(appearance, RenderedAppearance) && (facingIndex == RenderedFacing))
                return;

            RenderedAppearance = appearance;
            RenderedFacing = facingIndex;

            Figure?.Dispose();
            var (frame, flip, isFront) = FACINGS[facingIndex];
            Figure = Renderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront);

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

            if (!Visible)
                return;

            DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

            if (Figure is null)
                return;

            var scale = Zoomed ? 2 : 1;
            var w = Figure.Width * scale;
            var h = Figure.Height * scale;

            //anchored on the body centre, not the pedestal centre, so the figure doesn't drift sideways between
            //poses whose padded canvases differ in width
            var x = ScreenX + (Width / 2) - (AislingRenderer.CANVAS_CENTER_X * scale);
            var y = ScreenY + Height - h - 12;

            DrawTextureFitted(spriteBatch, Figure, new Rectangle(x, y, w, h), Color.White);
        }

        public override void Dispose()
        {
            Release();
            Pedestal.Dispose();
            base.Dispose();
        }
    }

    /// <summary>Every selection/page change goes through here: tear down a stale confirm, mutate, repaint.</summary>
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        //any change while the gender-reshape prompt is up invalidates what it was about to confirm
        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        mutate(WorldState.BeautyShop);
        StatusLabel.Text = string.Empty;
        Thumbnails.Clear(); //the base look may have changed; visible cells re-render on Refresh
        Refresh();
    }

    /// <summary>Hover never touches the confirm dialog, the status line or the thumbnail cache -- only the preview and the hover total.</summary>
    private void Hover(Action<ViewModel.BeautyShop> mutate)
    {
        mutate(WorldState.BeautyShop);
        RefreshPreview();
        RefreshSummary();
    }

    private void RefreshPickers()
    {
        var vm = WorldState.BeautyShop;
        GenderPicker.SetSelected(vm.Gender);

        //keyed on the selected (not hovered) look so thumbnails never flicker while the player hovers other cells
        var baseLook = BareAppearance(vm.Gender, vm.HairStyle, vm.HairColor, vm.BodyColor, vm.FaceSprite);

        HairstyleCaption.Text = CaptionLine(
            $"Hairstyle   {IndexLabel(vm.Hairstyles, h => h.Sprite == vm.HairStyle)} / {vm.Hairstyles.Count}",
            $"page {vm.HairstylePage + 1}/{vm.HairstylePageCount}");
        HairstyleStrip.SetItems(
            vm.VisibleHairstyles,
            vm.Hairstyles.FirstOrDefault(h => h.Sprite == vm.HairStyle),
            h => Thumbnails.Get(baseLook with { HeadSprite = h.Sprite }));

        DyeGrid.SetColors(vm.HairColors, vm.HairColor, SwatchColorFor);

        SkinCaption.Text = CaptionLine(
            $"Skin   {IndexLabel(vm.BodyColors, c => c == vm.BodyColor)} / {vm.BodyColors.Count}",
            $"page {vm.BodyColorPage + 1}/{vm.BodyColorPageCount}");
        SkinStrip.SetItems(
            vm.VisibleBodyColors,
            vm.BodyColor,
            c => Thumbnails.Get(baseLook with { BodyColor = (int)c }));

        FaceCaption.Text = CaptionLine(
            $"Face   {IndexLabel(vm.AvailableFaces, f => f.Sprite == vm.FaceSprite)} / {vm.AvailableFaces.Count}",
            $"page {vm.FacePage + 1}/{vm.FacePageCount}");
        FaceStrip.SetItems(
            vm.VisibleFaces,
            vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite),
            f => Thumbnails.Get(baseLook with { FaceSprite = f.Sprite }));
    }

    private void RefreshSummary()
    {
        var vm = WorldState.BeautyShop;

        var parts = new List<string>();

        if (vm.GenderChanged)
            parts.Add($"Gender {vm.GenderPrice:N0}");

        if (vm.HairstyleChanged)
            parts.Add($"Hairstyle {vm.HairstylePrice:N0}");

        if (vm.HairColorChanged)
            parts.Add($"Hair dye {vm.HairDyePrice:N0}");

        if (vm.BodyColorChanged)
            parts.Add($"Skin {vm.BodyDyePrice:N0}");

        if (vm.FaceCharged)
            parts.Add($"Face {vm.FacePrice:N0}");

        SummaryLabel.Text = parts.Count == 0 ? "No changes" : string.Join("  ·  ", parts);

        TotalLabel.Text = $"TOTAL {vm.Total:N0}";
        TotalLabel.ForegroundColor = vm.CanAfford ? LegendColors.Gold : LegendColors.Red;
        GoldLabel.Text = $"You have {vm.Gold:N0}";

        //the third line shows at most one message -- a standing rejection/status always wins over the hover hint
        var hasStatus = !string.IsNullOrEmpty(StatusLabel.Text);
        StatusLabel.Visible = hasStatus;
        HoverTotalLabel.Text = (vm.IsHovering && (vm.HoverTotal != vm.Total)) ? $"if chosen: TOTAL {vm.HoverTotal:N0}" : string.Empty;
        HoverTotalLabel.Visible = !hasStatus;

        DiscardButton.Enabled = vm.HasUnsavedChanges;
        ApplyButton.Enabled = vm.CanApply;
    }

    /// <summary>Right-pads <paramref name="left" /> with spaces (the font is fixed-width) so <paramref name="right" /> lands near the caption row's right edge.</summary>
    private static string CaptionLine(string left, string right)
    {
        var totalChars = Math.Max(1, APPEARANCE_WIDTH / TextRenderer.CHAR_WIDTH);
        var padCount = Math.Max(1, totalChars - left.Length - right.Length);

        return left + new string(' ', padCount) + right;
    }

    /// <summary>1-based position of the matching entry in <paramref name="items" />, or "-" when none matches.</summary>
    private static string IndexLabel<T>(IReadOnlyList<T> items, Func<T, bool> isCurrent)
    {
        for (var i = 0; i < items.Count; i++)
            if (isCurrent(items[i]))
                return (i + 1).ToString();

        return "-";
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

        RefreshSummary();
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
        Thumbnails.Dispose();
        base.Dispose();
    }
}

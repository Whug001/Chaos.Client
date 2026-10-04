#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Beauty.Pages;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Josephine's mirror: a five-page guide (Gender, Hair, Skin, Face, Review). Pages 1-4 show the preview column on
///     the left and the page on the right; Review spans both. A footer of arrows that name the next page, page dots
///     and the running total sits at the bottom. Reads everything from <see cref="WorldState.BeautyShop" />; nothing
///     here costs gold until the server answers an Apply.
/// </summary>
public sealed class BeautyShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 470;
    private const int TOP_MARGIN = 5;
    private const int HEADER_TOP = 10;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //preview column -- the pedestal must fit a 111px-wide composite at 2x (222px)
    private const int PREVIEW_LEFT = 20;
    private const int PREVIEW_WIDTH = 230;
    private const int PEDESTAL_HEIGHT = 250;
    private const int PEDESTAL_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PREVIEW_CONTROLS_TOP = PEDESTAL_TOP + PEDESTAL_HEIGHT + 6;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ZOOM_BUTTON_WIDTH = 60;
    private const int RANDOMIZE_BUTTON_WIDTH = 100;

    //backdrop picker: a caption and one row of pictures between the Show gear row and the footer total
    private const int BACKDROP_CAPTION_TOP = PREVIEW_CONTROLS_TOP + (2 * (CustomButton.HEIGHT + 6));
    private const int BACKDROP_CAPTION_WIDTH = 10 * TextRenderer.CHAR_WIDTH;
    private const int BACKDROP_PICKER_TOP = BACKDROP_CAPTION_TOP + TextRenderer.CHAR_HEIGHT;
    private const int BACKDROP_CELL = 28;
    private const int BACKDROP_GAP = 4;

    //page column
    private const int PAGE_LEFT = PREVIEW_LEFT + PREVIEW_WIDTH + 12;
    private const int PAGE_WIDTH = PANEL_WIDTH - PAGE_LEFT - 20;
    private const int INSTRUCTION_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PAGE_TOP = INSTRUCTION_TOP + TextRenderer.CHAR_HEIGHT + 8;

    //footer -- kept clear of the frame's ornate bottom border
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int TOTAL_TOP = FOOTER_TOP - TextRenderer.CHAR_HEIGHT - 4;
    private const int PAGE_HEIGHT = TOTAL_TOP - 4 - PAGE_TOP;
    private const int NAV_BUTTON_WIDTH = 130;

    //the review page spans both columns
    private const int REVIEW_LEFT = PREVIEW_LEFT;
    private const int REVIEW_TOP = HEADER_TOP;
    private const int REVIEW_WIDTH = PANEL_WIDTH - (PREVIEW_LEFT * 2);
    private const int REVIEW_HEIGHT = TOTAL_TOP - REVIEW_TOP;

    //head crops are cached by the full look, so a selection never makes one wrong; they are only dropped when the
    //cache grows this large (each is a 30x30 texture)
    private const int THUMBNAIL_CACHE_LIMIT = 512;

    //the right button turns from "Review >" into APPLY in the same spot, so the second click of a
    //double-click would otherwise buy without the player ever seeing the receipt
    private const int APPLY_ARM_MS = 600;

    private readonly HeadThumbnailRenderer Thumbnails;
    private readonly MirrorBackdropCache Backdrops;
    private readonly UIPanel PreviewColumn;
    private readonly MirrorPreview Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly CustomButton ZoomButton;
    private readonly UILabel BackdropName;
    private readonly ThumbnailGrid<MirrorBackdrop> BackdropPicker;
    private readonly UILabel TitleLabel;
    private readonly UILabel PageNumberLabel;
    private readonly UILabel InstructionLabel;
    private readonly MirrorPageView[] Pages;
    private readonly ReviewPage Review;
    private readonly CustomButton BackButton;
    private readonly CustomButton NextButton;
    private readonly PageDots Dots;
    private readonly UILabel TotalLabel;
    private readonly OkPopupMessageControl ConfirmDialog;

    private MirrorPage CurrentPage = MirrorPages.First;
    private int FacingIndex;
    private bool ShowGear;
    private bool Zoomed = true;
    private long ReviewShownAt;

    //static so the choice outlives the window -- closing and reopening it, even logging out; it is not saved to settings
    private static MirrorBackdrop ChosenBackdrop = MirrorBackdrop.Plain;
    private MirrorBackdrop? HoveredBackdrop;

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
        Backdrops = new MirrorBackdropCache();

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        //── preview column (hidden on Review, which draws its own two figures) ──
        PreviewColumn = new UIPanel
        {
            Background = null,
            X = 0,
            Y = 0,
            Width = PREVIEW_LEFT + PREVIEW_WIDTH,
            Height = TOTAL_TOP
        };
        AddChild(PreviewColumn);

        var previewCaption = NewLabel(PREVIEW_LEFT, HEADER_TOP, PREVIEW_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        previewCaption.Text = "PREVIEW";
        PreviewColumn.AddChild(previewCaption);

        Preview = new MirrorPreview(renderer, Backdrops, PREVIEW_WIDTH, PEDESTAL_HEIGHT) { X = PREVIEW_LEFT, Y = PEDESTAL_TOP };
        PreviewColumn.AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = PREVIEW_CONTROLS_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        PreviewColumn.AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + ROTATE_BUTTON_WIDTH + 4,
            Y = PREVIEW_CONTROLS_TOP
        };
        rotateRight.Clicked += () => Rotate(+1);
        PreviewColumn.AddChild(rotateRight);

        ZoomButton = new CustomButton("2x", ZOOM_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + PREVIEW_WIDTH - ZOOM_BUTTON_WIDTH,
            Y = PREVIEW_CONTROLS_TOP
        };
        ZoomButton.Clicked += () =>
        {
            Zoomed = !Zoomed;
            Preview.Zoomed = Zoomed;
            ZoomButton.Caption = Zoomed ? "2x" : "1x";
        };
        PreviewColumn.AddChild(ZoomButton);

        GearToggle = MirrorPreview.CreateGearToggle(PREVIEW_LEFT, PREVIEW_CONTROLS_TOP + CustomButton.HEIGHT + 6);
        GearToggle.Clicked += ToggleGear;
        PreviewColumn.AddChild(GearToggle);

        var randomizeButton = new CustomButton("Randomize", RANDOMIZE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + PREVIEW_WIDTH - RANDOMIZE_BUTTON_WIDTH,
            Y = GearToggle.Y
        };
        randomizeButton.Clicked += () => Select(v => v.Randomize());
        PreviewColumn.AddChild(randomizeButton);

        var backdropCaption = NewLabel(PREVIEW_LEFT, BACKDROP_CAPTION_TOP, BACKDROP_CAPTION_WIDTH, LegendColors.Gray);
        backdropCaption.Text = "Backdrop";
        PreviewColumn.AddChild(backdropCaption);

        BackdropName = NewLabel(
            PREVIEW_LEFT + BACKDROP_CAPTION_WIDTH,
            BACKDROP_CAPTION_TOP,
            PREVIEW_WIDTH - BACKDROP_CAPTION_WIDTH,
            LegendColors.Gold);
        PreviewColumn.AddChild(BackdropName);

        BackdropPicker = new ThumbnailGrid<MirrorBackdrop>(MirrorBackdrops.All.Count, 1, BACKDROP_GAP, BACKDROP_GAP, BACKDROP_CELL, arrows: false)
        {
            X = PREVIEW_LEFT,
            Y = BACKDROP_PICKER_TOP
        };
        BackdropPicker.Hovered += place => HoverBackdrop(place);
        BackdropPicker.HoverCleared += () => HoverBackdrop(null);
        BackdropPicker.Selected += ChooseBackdrop;
        PreviewColumn.AddChild(BackdropPicker);

        //── page heading (pages 1-4; Review draws its own) ──
        TitleLabel = NewLabel(PAGE_LEFT, HEADER_TOP, PAGE_WIDTH, LegendColors.Gold);
        AddChild(TitleLabel);
        PageNumberLabel = NewLabel(PAGE_LEFT, HEADER_TOP, PAGE_WIDTH, LegendColors.Gray);
        AddChild(PageNumberLabel);
        InstructionLabel = NewLabel(PAGE_LEFT, INSTRUCTION_TOP, PAGE_WIDTH, LegendColors.Gray);
        AddChild(InstructionLabel);

        //── pages, in MirrorPage order ──
        var actions = new MirrorActions(Select, Hover, Thumbnails);
        Review = new ReviewPage(actions, renderer, Backdrops, Rotate, ToggleGear, REVIEW_WIDTH, REVIEW_HEIGHT) { X = REVIEW_LEFT, Y = REVIEW_TOP };

        Pages =
        [
            new GenderPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new HairPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new SkinPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new FacePage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            Review
        ];

        foreach (var page in Pages)
            AddChild(page);

        //── footer ──
        BackButton = new CustomButton(string.Empty, NAV_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = FOOTER_TOP };
        BackButton.Clicked += GoBack;
        AddChild(BackButton);

        Dots = new PageDots(MirrorPages.COUNT);
        Dots.X = (PANEL_WIDTH - Dots.Width) / 2;
        Dots.Y = FOOTER_TOP + ((CustomButton.HEIGHT - Dots.Height) / 2);
        Dots.PageChosen += index => GoTo((MirrorPage)index);
        AddChild(Dots);

        TotalLabel = NewLabel(
            PREVIEW_LEFT + NAV_BUTTON_WIDTH,
            TOTAL_TOP,
            PANEL_WIDTH - (2 * (PREVIEW_LEFT + NAV_BUTTON_WIDTH)),
            LegendColors.Gray,
            HorizontalAlignment.Center);
        AddChild(TotalLabel);

        NextButton = new CustomButton(string.Empty, NAV_BUTTON_WIDTH)
        {
            X = PANEL_WIDTH - PREVIEW_LEFT - NAV_BUTTON_WIDTH,
            Y = FOOTER_TOP
        };
        NextButton.Clicked += GoForward;
        AddChild(NextButton);

        //parented to the panel like poker's leave confirm, drawn above everything else in it
        ConfirmDialog = new OkPopupMessageControl(true) { Name = "BeautyShopGenderConfirm", ZIndex = 100 };
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();

            //re-validate: a picker change or Start over while the prompt was up must not sneak an apply through
            if (WorldState.BeautyShop.CanApply && WorldState.BeautyShop.GenderChanged)
                ApplyRequested?.Invoke();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    private static UILabel NewLabel(int x, int y, int width, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

    /// <summary>Repaints the preview, the backdrop picker, the heading, the visible page and the footer from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshBackdrop();
        RefreshHeading();
        Review.SetView(FacingIndex, ShowGear, ChosenBackdrop);
        Pages[(int)CurrentPage].Refresh();
        RefreshFooter();
    }

    public override void Show()
    {
        FacingIndex = 0;
        ShowGear = false;
        GearToggle.Checked = false;
        Zoomed = true;
        Preview.Zoomed = true;
        ZoomButton.Caption = "2x";
        Review.Status = string.Empty;
        base.Show();
        GoTo(MirrorPages.First);
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
        Review.Release();
        Thumbnails.Clear();
        Backdrops.Clear();
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

    /// <summary>Shows <paramref name="page" />. Turning a page clears hover and never changes a selection.</summary>
    private void GoTo(MirrorPage page)
    {
        if (!Visible)
            return;

        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        WorldState.BeautyShop.ClearHover();
        HoveredBackdrop = null;
        CurrentPage = page;

        for (var i = 0; i < Pages.Length; i++)
            Pages[i].Visible = i == (int)page;

        PreviewColumn.Visible = page != MirrorPage.Review;
        Refresh();

        if (page == MirrorPage.Review)
            ReviewShownAt = Environment.TickCount64;
    }

    private void GoBack()
    {
        if (MirrorPages.Previous(CurrentPage) is { } previous)
            GoTo(previous);
    }

    private void GoForward()
    {
        if (MirrorPages.Next(CurrentPage) is { } next)
            GoTo(next);
        else if ((Environment.TickCount64 - ReviewShownAt) >= APPLY_ARM_MS)
            OnApplyClicked();
    }

    private void Rotate(int delta)
    {
        FacingIndex = (FacingIndex + delta + MirrorPreview.FACING_COUNT) % MirrorPreview.FACING_COUNT;
        Refresh();
    }

    private void ToggleGear()
    {
        ShowGear = !ShowGear;
        GearToggle.Checked = ShowGear;
        Refresh();
    }

    /// <summary>
    ///     Pointing at a backdrop tries it on. Backdrops are UI-only: they skip the view model, the confirm dialog, the
    ///     money and the review status.
    /// </summary>
    private void HoverBackdrop(MirrorBackdrop? place)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        HoveredBackdrop = place;
        RefreshBackdrop();
    }

    private void ChooseBackdrop(MirrorBackdrop place)
    {
        if (!Visible)
            return;

        ChosenBackdrop = place;
        RefreshBackdrop();
    }

    private void RefreshBackdrop()
    {
        var shown = HoveredBackdrop ?? ChosenBackdrop;
        Preview.Backdrop = shown;
        BackdropName.Text = MirrorBackdrops.Name(shown);
        BackdropPicker.SetItems(MirrorBackdrops.All, ChosenBackdrop, 1, Backdrops.Thumbnail);
    }

    private void RefreshPreview()
    {
        //Review draws its own two figures; don't render one nobody can see
        if (CurrentPage != MirrorPage.Review)
            Preview.Refresh(MirrorLooks.New(WorldState.BeautyShop, ShowGear), FacingIndex);
    }

    private void RefreshHeading()
    {
        var visible = CurrentPage != MirrorPage.Review;
        TitleLabel.Visible = visible;
        PageNumberLabel.Visible = visible;
        InstructionLabel.Visible = visible;

        var title = MirrorPages.Title(CurrentPage);
        TitleLabel.Text = title;
        PageNumberLabel.X = PAGE_LEFT + ((title.Length + 3) * TextRenderer.CHAR_WIDTH);
        PageNumberLabel.Text = $"page {(int)CurrentPage + 1} of {MirrorPages.COUNT}";
        InstructionLabel.Text = MirrorPages.Instruction(CurrentPage);
    }

    private void RefreshFooter()
    {
        var vm = WorldState.BeautyShop;
        var onReview = CurrentPage == MirrorPage.Review;

        BackButton.Visible = MirrorPages.Previous(CurrentPage) is not null;
        BackButton.Caption = MirrorPages.BackLabel(CurrentPage);
        NextButton.Caption = MirrorPages.NextLabel(CurrentPage);
        NextButton.Enabled = !onReview || vm.CanApply;
        Dots.SetCurrent((int)CurrentPage);

        TotalLabel.Visible = !onReview;
        TotalLabel.Text = vm.HasUnsavedChanges ? $"TOTAL {vm.Total:N0}" : "No changes yet";
        TotalLabel.ForegroundColor = !vm.HasUnsavedChanges ? LegendColors.Gray
            : vm.CanAfford ? LegendColors.Gold
            : LegendColors.Red;
    }

    /// <summary>Every selection and page step goes through here: tear down a stale confirm, mutate, repaint.</summary>
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        //any change while the gender-reshape prompt is up invalidates what it was about to confirm
        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        mutate(WorldState.BeautyShop);
        Review.Status = string.Empty;

        if (Thumbnails.Count > THUMBNAIL_CACHE_LIMIT)
            Thumbnails.Clear();

        Refresh();
    }

    /// <summary>Hover never touches the confirm dialog, the status line, the thumbnail cache or the money -- only the preview and the page's captions.</summary>
    private void Hover(Action<ViewModel.BeautyShop> mutate)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        mutate(WorldState.BeautyShop);
        RefreshPreview();
        Pages[(int)CurrentPage].Refresh();
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

    /// <summary>Server refused the Apply; keep the panel open and say why on the review page.</summary>
    public void OnRejected(BeautyShopRejectReason reason)
    {
        if (!Visible)
            return;

        Review.Status = reason switch
        {
            BeautyShopRejectReason.NothingChanged        => "Nothing has changed.",
            BeautyShopRejectReason.InsufficientGold      => "You can't afford that.",
            BeautyShopRejectReason.InvalidSelection      => "Josephine can't do that one.",
            BeautyShopRejectReason.GenderSwapUnavailable => "Josephine can't reshape your class's gear.",
            BeautyShopRejectReason.NotNearShop           => "Step closer to Josephine.",
            _                                            => "Josephine shakes her head."
        };

        Refresh();
    }

    public override void Dispose()
    {
        Thumbnails.Dispose();
        Backdrops.Dispose();
        base.Dispose();
    }
}

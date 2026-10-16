#region
using System.Text;
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.Portraits;
using Chaos.Client.ViewModel.CommissionBoard;
using Chaos.Client.ViewModel.Portraits;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.CommissionBoard;

/// <summary>
///     The commission board window: Open requests, My requests and My commissions tabs with a row list, the selected
///     row's details and its actions, the post form, the buyer review pane and the deliver picker. Opens from the
///     server's CommissionBoardDisplay; every button sends a CommissionBoardAction, results arrive as whispers.
/// </summary>
public sealed class CommissionBoardControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int TITLE_TOP = 10;
    private const int TABS_TOP = 30;
    private const int TAB_GAP = 6;
    private const int OPEN_WIDTH = 120;
    private const int REQUESTS_WIDTH = 110;
    private const int COMMISSIONS_WIDTH = 132;
    private const int LIST_TOP = 56;
    private const int PAGE_SIZE = 5;
    private const int PAGE_TOP = 210;
    private const int SMALL_BUTTON = 28;
    private const int DETAIL_TOP = 236;
    private const int DETAIL_HEIGHT = 100;
    private const int ACTIONS_TOP = 340;
    private const int BUTTON_GAP = 6;
    private const int POST_CAPTION_TOP = 366;
    private const int DESC_TOP = 380;
    private const int DESC_HEIGHT = 32;
    private const int FORM_TOP = 414;
    private const int STATUS_TOP = 438;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int REVIEW_WIDTH = 320;
    private const int REVIEW_HEIGHT = 202;
    private const int REVIEW_PICTURE_TOP = 28;
    private const int REVIEW_NOTE_TOP = 106;
    private const int REVIEW_NOTE_HEIGHT = 44;
    private const int REVIEW_BUTTONS_TOP = 156;
    private const int REVIEW_STATUS_TOP = 182;

    private const int DELIVER_WIDTH = 340;
    private const int DELIVER_HEIGHT = 268;
    private const int DELIVER_LIST_TOP = 30;
    private const int DELIVER_PAGE_SIZE = 6;
    private const int DELIVER_PAGE_TOP = 214;
    private const int DELIVER_BUTTONS_TOP = 238;

    private const int ARTIST_BOX_WIDTH = 170;
    private const int GOLD_BOX_WIDTH = 100;
    private const int POST_WIDTH = 70;

    private readonly CustomButton CancelButton;
    private readonly CustomButton ClaimButton;
    private readonly CustomButton CollectGoldButton;
    private readonly CustomButton CollectItemButton;
    private readonly CustomButton DeliverButton;
    private readonly CustomButton SendDeliverButton;
    private readonly CustomButton DropButton;
    private readonly CustomButton NextDeliverPageButton;
    private readonly CustomButton NextPageButton;
    private readonly CustomButton PostButton;
    private readonly CustomButton PrevDeliverPageButton;
    private readonly CustomButton PrevPageButton;
    private readonly CustomButton RedoButton;
    private readonly CustomButton ReviewButton;
    private readonly CustomButton TabCommissions;
    private readonly CustomButton TabOpen;
    private readonly CustomButton TabRequests;
    private readonly CommissionBoardRowView[] DeliverRows = new CommissionBoardRowView[DELIVER_PAGE_SIZE];
    private readonly UILabel DeliverEmptyLabel;
    private readonly UIPanel DeliverPanel;
    private readonly UILabel DeliverPageLabel;
    private readonly CustomTextBox ArtistBox;
    private readonly UILabel CounterLabel;
    private readonly CustomTextBox DescBox;
    private readonly UILabel DetailLabel;
    private readonly UILabel EmptyLabel;
    private readonly CustomTextBox GoldBox;
    private readonly CustomTextBox NoteBox;
    private readonly UILabel PageLabel;
    private readonly UILabel PostCaption;
    private readonly UIPanel ReviewPanel;
    private readonly UILabel ReviewPainterLabel;
    private readonly UIImage ReviewPicture;
    private readonly UILabel ReviewStatusLabel;
    private readonly CommissionBoardRowView[] Rows = new CommissionBoardRowView[PAGE_SIZE];
    private readonly UILabel StatusLabel;
    private readonly PortraitPictureTransfers Transfers;

    private readonly CustomButton[] ActionButtons;
    private string CounterFor = string.Empty;
    private string? DeliverId;
    private int DeliverPage;
    private byte? DeliverSlot;
    private int Page;
    private bool ReviewHeld;
    private string? ReviewHash;
    private string? ReviewId;

    public CommissionBoardControl(PortraitPictureTransfers transfers)
        : base("_nsett", false)
    {
        Transfers = transfers;
        Name = "CommissionBoard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Commission Board", 0, TITLE_TOP, WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        TabOpen = AddButton("Open requests", OPEN_WIDTH, LEFT, TABS_TOP, () => SwitchTab(CommissionBoardTab.Open));
        TabRequests = AddButton(
            "My requests",
            REQUESTS_WIDTH,
            LEFT + OPEN_WIDTH + TAB_GAP,
            TABS_TOP,
            () => SwitchTab(CommissionBoardTab.MyRequests));
        TabCommissions = AddButton(
            "My commissions",
            COMMISSIONS_WIDTH,
            LEFT + OPEN_WIDTH + TAB_GAP + REQUESTS_WIDTH + TAB_GAP,
            TABS_TOP,
            () => SwitchTab(CommissionBoardTab.MyCommissions));

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;
            var row = new CommissionBoardRowView(INNER_WIDTH)
            {
                X = LEFT,
                Y = LIST_TOP + (i * CommissionBoardRowView.HEIGHT),
                Visible = false
            };

            row.Clicked += () => SelectRow((Page * PAGE_SIZE) + slot);
            AddChild(row);
            Rows[i] = row;
        }

        EmptyLabel = Caption(
            "Nothing here.",
            LEFT,
            LIST_TOP + 60,
            INNER_WIDTH,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, PAGE_TOP, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, PAGE_TOP, () => TurnPage(1));
        PageLabel = Caption(
            string.Empty,
            LEFT + ((2 * SMALL_BUTTON) + 10),
            PAGE_TOP + 5,
            200,
            color: LegendColors.Gray);

        DetailLabel = Caption(string.Empty, LEFT, DETAIL_TOP, INNER_WIDTH);
        DetailLabel.WordWrap = true;
        DetailLabel.VerticalAlignment = VerticalAlignment.Top;
        DetailLabel.Height = DETAIL_HEIGHT;

        var actionX = LEFT;
        ClaimButton = AddButton("Claim", 64, actionX, ACTIONS_TOP, () => SendSelected(CommissionBoardRowAction.Claim));
        actionX += 64 + BUTTON_GAP;
        CancelButton = AddButton("Cancel", 70, actionX, ACTIONS_TOP, () => SendSelected(CommissionBoardRowAction.Cancel));
        actionX += 70 + BUTTON_GAP;
        DropButton = AddButton("Drop", 60, actionX, ACTIONS_TOP, () => SendSelected(CommissionBoardRowAction.Drop));
        actionX += 60 + BUTTON_GAP;
        DeliverButton = AddButton("Deliver", 70, actionX, ACTIONS_TOP, OpenDeliver);
        actionX += 70 + BUTTON_GAP;
        ReviewButton = AddButton("Review", 70, actionX, ACTIONS_TOP, OpenReview);
        actionX += 70 + BUTTON_GAP;
        CollectGoldButton = AddButton(
            "Collect gold",
            108,
            actionX,
            ACTIONS_TOP,
            () => SendSelected(CommissionBoardRowAction.CollectGold));
        actionX += 108 + BUTTON_GAP;
        CollectItemButton = AddButton(
            "Collect item",
            106,
            actionX,
            ACTIONS_TOP,
            () => SendSelected(CommissionBoardRowAction.CollectItem));
        ActionButtons = [ClaimButton, CancelButton, DropButton, DeliverButton, ReviewButton, CollectGoldButton, CollectItemButton];

        PostCaption = Caption("New request", LEFT, POST_CAPTION_TOP, INNER_WIDTH, color: LegendColors.Gray);

        DescBox = new CustomTextBox
        {
            X = LEFT,
            Y = DESC_TOP,
            Width = INNER_WIDTH,
            Height = DESC_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CommissionBoardState.MAX_DESCRIPTION_CHARS,
            MaxByteCount = CommissionBoardState.MAX_PACKET_STRING_BYTES,
            ByteEncoding = Encoding.GetEncoding(949),
            HintText = "Describe the portrait"
        };
        AddChild(DescBox);

        GoldBox = new CustomTextBox
        {
            X = LEFT,
            Y = FORM_TOP,
            Width = GOLD_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = 10,
            HintText = "Gold",
            DigitsOnly = true
        };
        AddChild(GoldBox);

        ArtistBox = new CustomTextBox
        {
            X = LEFT + GOLD_BOX_WIDTH + BUTTON_GAP,
            Y = FORM_TOP,
            Width = ARTIST_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = 32,
            HintText = "Artist (optional)"
        };
        AddChild(ArtistBox);

        PostButton = AddButton(
            "Post",
            POST_WIDTH,
            LEFT + GOLD_BOX_WIDTH + BUTTON_GAP + ARTIST_BOX_WIDTH + BUTTON_GAP,
            FORM_TOP,
            OnPostClicked);
        CounterLabel = Caption(
            string.Empty,
            LEFT + GOLD_BOX_WIDTH + BUTTON_GAP + ARTIST_BOX_WIDTH + BUTTON_GAP + POST_WIDTH + BUTTON_GAP,
            FORM_TOP + 5,
            LEFT + INNER_WIDTH - (LEFT + GOLD_BOX_WIDTH + BUTTON_GAP + ARTIST_BOX_WIDTH + BUTTON_GAP + POST_WIDTH + BUTTON_GAP),
            HorizontalAlignment.Right,
            LegendColors.Gray);

        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH);

        ReviewPanel = BuildReviewPanel(out ReviewPicture, out ReviewPainterLabel, out NoteBox, out ReviewStatusLabel, out RedoButton);
        AddChild(ReviewPanel);

        DeliverPanel = BuildDeliverPanel(
            out DeliverEmptyLabel,
            out DeliverPageLabel,
            out PrevDeliverPageButton,
            out NextDeliverPageButton,
            out SendDeliverButton);
        AddChild(DeliverPanel);

        WorldState.CommissionBoard.Changed += Refresh;
        Refresh();
    }

    /// <summary>Raised for every board button: claim, cancel, drop, deliver, accept, redo and the collects.</summary>
    public event Action<CommissionBoardActionArgs>? ActionRequested;

    /// <summary>Shows the window, keeping the current tab and selection.</summary>
    public void Open()
    {
        Refresh();
        Show();
    }

    /// <summary>Rebuilds the tabs, the detail and the action buttons from the state.</summary>
    public void Refresh()
    {
        var state = WorldState.CommissionBoard;
        var rows = state.CurrentRows;
        var pages = Math.Max(1, (rows.Count + PAGE_SIZE - 1) / PAGE_SIZE);
        Page = Math.Clamp(Page, 0, pages - 1);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (Page * PAGE_SIZE) + i;
            var view = Rows[i];

            if (index >= rows.Count)
            {
                view.Visible = false;
                view.Selected = false;

                continue;
            }

            var row = rows[index];
            view.Visible = true;
            view.Bind(null, row.Description, CommissionBoardState.RowNote(row, state.SelectedTab));
            view.Selected = row.Id.Equals(state.SelectedId, StringComparison.Ordinal);
        }

        EmptyLabel.Visible = rows.Count == 0;
        PageLabel.Text = $"{Page + 1}/{pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);

        TabOpen.Selected = state.SelectedTab == CommissionBoardTab.Open;
        TabRequests.Selected = state.SelectedTab == CommissionBoardTab.MyRequests;
        TabCommissions.Selected = state.SelectedTab == CommissionBoardTab.MyCommissions;

        var selected = state.SelectedRow;
        DetailLabel.Text = selected is null ? string.Empty : state.DetailsText(selected);

        var actions = selected is null ? [] : state.ActionsFor(selected);
        ClaimButton.Visible = actions.Contains(CommissionBoardRowAction.Claim);
        CancelButton.Visible = actions.Contains(CommissionBoardRowAction.Cancel);
        DropButton.Visible = actions.Contains(CommissionBoardRowAction.Drop);
        DeliverButton.Visible = actions.Contains(CommissionBoardRowAction.Deliver);
        ReviewButton.Visible = actions.Contains(CommissionBoardRowAction.Review);
        CollectGoldButton.Visible = actions.Contains(CommissionBoardRowAction.CollectGold);
        CollectItemButton.Visible = actions.Contains(CommissionBoardRowAction.CollectItem);

        var actionX = LEFT;

        foreach (var button in ActionButtons)
        {
            if (!button.Visible)
                continue;

            button.X = actionX;
            actionX += button.Width + BUTTON_GAP;
        }

        var posting = state.SelectedTab == CommissionBoardTab.MyRequests;
        PostCaption.Visible = posting;
        DescBox.Visible = posting;
        GoldBox.Visible = posting;
        ArtistBox.Visible = posting;
        PostButton.Visible = posting;
        CounterLabel.Visible = posting;
        CounterFor = DescBox.Text;
        CounterLabel.Text = $"{DescBox.Text.Length}/{CommissionBoardState.MAX_DESCRIPTION_CHARS}";

        if ((selected is null) || !selected.Id.Equals(ReviewId, StringComparison.Ordinal) || (selected.State != CommissionBoardRowState.Delivered))
            CloseReview();

        if ((selected is null)
            || !selected.Id.Equals(DeliverId, StringComparison.Ordinal)
            || (selected.State != CommissionBoardRowState.Claimed)
            || !state.IsClaimant(selected))
            CloseDeliver();
        else
            RefreshDeliverPicker();
    }

    /// <summary>Rebuilds the deliver picker's rows after the inventory changed. No-op while it is closed.</summary>
    public void RefreshDeliverPicker()
    {
        if (!DeliverPanel.Visible)
            return;

        var choices = CommissionBoardState.DeliverChoices(WorldState.Inventory, WorldState.PlayerName);
        var pages = Math.Max(1, (choices.Count + DELIVER_PAGE_SIZE - 1) / DELIVER_PAGE_SIZE);
        DeliverPage = Math.Clamp(DeliverPage, 0, pages - 1);

        if (DeliverSlot is { } slot && choices.All(c => c.Slot != slot))
            DeliverSlot = null;

        for (var i = 0; i < DELIVER_PAGE_SIZE; i++)
        {
            var index = (DeliverPage * DELIVER_PAGE_SIZE) + i;
            var view = DeliverRows[i];

            if (index >= choices.Count)
            {
                view.Visible = false;
                view.Selected = false;

                continue;
            }

            var choice = choices[index];
            view.Visible = true;
            view.Bind(UiRenderer.Instance!.GetItemIcon(choice.Sprite, choice.Color), choice.Name, string.Empty);
            view.Selected = DeliverSlot == choice.Slot;
        }

        DeliverEmptyLabel.Visible = choices.Count == 0;
        DeliverPageLabel.Text = $"{DeliverPage + 1}/{pages}";
        PrevDeliverPageButton.Enabled = DeliverPage > 0;
        NextDeliverPageButton.Enabled = DeliverPage < (pages - 1);
        SendDeliverButton.Enabled = DeliverSlot is not null;
    }

    /// <summary>Shows the fetched review picture once it arrives.</summary>
    public void OnReviewPictureReady(string hash)
    {
        if (!ReviewPanel.Visible || (ReviewHash is null) || !hash.Equals(ReviewHash, StringComparison.Ordinal))
            return;

        if (Transfers.TryGetTexture(hash, out var texture))
        {
            ReviewPicture.Texture = texture;

            if (!ReviewHeld)
            {
                Transfers.Hold(hash);
                ReviewHeld = true;
            }

            ReviewStatusLabel.Text = string.Empty;
        } else
            ReviewStatusLabel.Text = "Picture unavailable.";
    }

    /// <summary>Answers a failed review fetch while the pane waits for it.</summary>
    public void OnReviewPictureFailed(string hash)
    {
        if (ReviewPanel.Visible && hash.Equals(ReviewHash, StringComparison.Ordinal) && (ReviewPicture.Texture is null))
            ReviewStatusLabel.Text = "Picture unavailable.";
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        DescBox.IsFocused = false;
        GoldBox.IsFocused = false;
        ArtistBox.IsFocused = false;
        CloseReview();
        CloseDeliver();
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (ReviewPanel.Visible)
                CloseReview();
            else if (DeliverPanel.Visible)
                CloseDeliver();
            else
                Hide();

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        if (!CounterFor.Equals(DescBox.Text, StringComparison.Ordinal))
        {
            CounterFor = DescBox.Text;
            CounterLabel.Text = $"{DescBox.Text.Length}/{CommissionBoardState.MAX_DESCRIPTION_CHARS}";
        }
    }

    public override void Dispose()
    {
        WorldState.CommissionBoard.Changed -= Refresh;
        ReviewPicture.Texture = null;
        base.Dispose();
    }

    private UIPanel BuildReviewPanel(
        out UIImage picture,
        out UILabel painter,
        out CustomTextBox note,
        out UILabel status,
        out CustomButton redo)
    {
        var panel = new UIPanel
        {
            X = (WIDTH - REVIEW_WIDTH) / 2,
            Y = (HEIGHT - REVIEW_HEIGHT) / 2,
            Width = REVIEW_WIDTH,
            Height = REVIEW_HEIGHT,
            BackgroundColor = new Color(10, 8, 5),
            BorderColor = Color.White,
            Visible = false,
            ZIndex = 1
        };

        var title = new UILabel
        {
            X = 0,
            Y = 8,
            Width = REVIEW_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Text = "Review"
        };
        panel.AddChild(title);

        var pictureFrame = new UIPanel
        {
            X = (REVIEW_WIDTH - PortraitCanvas.WIDTH - 2) / 2,
            Y = REVIEW_PICTURE_TOP,
            Width = PortraitCanvas.WIDTH + 2,
            Height = PortraitCanvas.HEIGHT + 2,
            BorderColor = Color.White,
            IsHitTestVisible = false
        };
        panel.AddChild(pictureFrame);

        picture = new UIImage
        {
            X = pictureFrame.X + 1,
            Y = pictureFrame.Y + 1,
            Width = PortraitCanvas.WIDTH,
            Height = PortraitCanvas.HEIGHT,
            IsHitTestVisible = false
        };
        panel.AddChild(picture);

        painter = new UILabel
        {
            X = LEFT,
            Y = REVIEW_PICTURE_TOP + PortraitCanvas.HEIGHT + 6,
            Width = REVIEW_WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        panel.AddChild(painter);

        note = new CustomTextBox
        {
            X = LEFT,
            Y = REVIEW_NOTE_TOP,
            Width = REVIEW_WIDTH - (2 * LEFT),
            Height = REVIEW_NOTE_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CommissionBoardState.MAX_NOTE_CHARS,
            MaxByteCount = CommissionBoardState.MAX_PACKET_STRING_BYTES,
            ByteEncoding = Encoding.GetEncoding(949),
            HintText = "Redo note (optional)"
        };
        panel.AddChild(note);

        status = new UILabel
        {
            X = LEFT,
            Y = REVIEW_STATUS_TOP,
            Width = REVIEW_WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        panel.AddChild(status);

        var buttonsWidth = 70 + BUTTON_GAP + 90 + BUTTON_GAP + 60;
        var x = (REVIEW_WIDTH - buttonsWidth) / 2;
        ReviewPaneButton(panel, "Accept", 70, x, OnAcceptClicked);
        x += 70 + BUTTON_GAP;
        redo = ReviewPaneButton(panel, "Send redo", 90, x, OnRedoClicked);
        x += 90 + BUTTON_GAP;
        ReviewPaneButton(panel, "Close", 60, x, CloseReview);

        return panel;

        static CustomButton ReviewPaneButton(UIPanel parent, string caption, int width, int x, Action onClick)
        {
            var button = new CustomButton(caption, width)
            {
                X = x,
                Y = REVIEW_BUTTONS_TOP
            };

            button.Clicked += () => onClick();
            parent.AddChild(button);

            return button;
        }
    }

    private UIPanel BuildDeliverPanel(
        out UILabel emptyLabel,
        out UILabel pageLabel,
        out CustomButton prev,
        out CustomButton next,
        out CustomButton send)
    {
        var panel = new UIPanel
        {
            X = (WIDTH - DELIVER_WIDTH) / 2,
            Y = (HEIGHT - DELIVER_HEIGHT) / 2,
            Width = DELIVER_WIDTH,
            Height = DELIVER_HEIGHT,
            BackgroundColor = new Color(10, 8, 5),
            BorderColor = Color.White,
            Visible = false,
            ZIndex = 1
        };

        var title = new UILabel
        {
            X = 0,
            Y = 8,
            Width = DELIVER_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Text = "Deliver a portrait"
        };
        panel.AddChild(title);

        for (var i = 0; i < DELIVER_PAGE_SIZE; i++)
        {
            var slot = i;
            var row = new CommissionBoardRowView(DELIVER_WIDTH - (2 * LEFT))
            {
                X = LEFT,
                Y = DELIVER_LIST_TOP + (i * CommissionBoardRowView.HEIGHT),
                Visible = false
            };

            row.Clicked += () => SelectDeliverRow((DeliverPage * DELIVER_PAGE_SIZE) + slot);
            panel.AddChild(row);
            DeliverRows[i] = row;
        }

        emptyLabel = new UILabel
        {
            X = LEFT,
            Y = DELIVER_LIST_TOP + 60,
            Width = DELIVER_WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false,
            Text = "No portraits you painted."
        };
        panel.AddChild(emptyLabel);

        prev = DeliverPanelButton(panel, "<", SMALL_BUTTON, LEFT, DELIVER_PAGE_TOP, () => TurnDeliverPage(-1));
        next = DeliverPanelButton(
            panel,
            ">",
            SMALL_BUTTON,
            LEFT + SMALL_BUTTON + 4,
            DELIVER_PAGE_TOP,
            () => TurnDeliverPage(1));
        pageLabel = new UILabel
        {
            X = LEFT + ((2 * SMALL_BUTTON) + 10),
            Y = DELIVER_PAGE_TOP + 5,
            Width = 120,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        panel.AddChild(pageLabel);

        var buttonsWidth = 80 + BUTTON_GAP + 70;
        var x = (DELIVER_WIDTH - buttonsWidth) / 2;
        send = DeliverPanelButton(panel, "Deliver", 80, x, DELIVER_BUTTONS_TOP, OnSendDeliverClicked);
        DeliverPanelButton(panel, "Cancel", 70, x + 80 + BUTTON_GAP, DELIVER_BUTTONS_TOP, CloseDeliver);

        return panel;

        static CustomButton DeliverPanelButton(UIPanel parent, string caption, int width, int x, int y, Action onClick)
        {
            var button = new CustomButton(caption, width)
            {
                X = x,
                Y = y
            };

            button.Clicked += () => onClick();
            parent.AddChild(button);

            return button;
        }
    }

    private void SwitchTab(CommissionBoardTab tab)
    {
        Page = 0;
        WorldState.CommissionBoard.SelectTab(tab);
    }

    private void TurnPage(int delta)
    {
        Page += delta;
        Refresh();
    }

    private void SelectRow(int index)
    {
        var rows = WorldState.CommissionBoard.CurrentRows;

        if ((index < 0) || (index >= rows.Count))
            return;

        WorldState.CommissionBoard.Select(rows[index].Id);
    }

    private void SendSelected(CommissionBoardRowAction action)
    {
        if (WorldState.CommissionBoard.SelectedId is { } id)
            ActionRequested?.Invoke(CommissionBoardState.ActionArgs(action, id));
    }

    private void OnPostClicked()
    {
        if (!CommissionBoardState.TryBuildPost(DescBox.Text, GoldBox.Text, ArtistBox.Text, out var args, out var error))
        {
            StatusLabel.Text = error;

            return;
        }

        StatusLabel.Text = string.Empty;
        ActionRequested?.Invoke(args);
        DescBox.Text = string.Empty;
        GoldBox.Text = string.Empty;
    }

    private void OpenReview()
    {
        var row = WorldState.CommissionBoard.SelectedRow;

        if ((row is null) || (row.State != CommissionBoardRowState.Delivered))
            return;

        CloseReview();
        ReviewId = row.Id;
        ReviewHash = row.PortraitHash;
        ReviewHeld = false;
        ReviewPicture.Texture = null;
        NoteBox.Text = string.Empty;
        ReviewStatusLabel.Text = string.Empty;
        ReviewPainterLabel.Text = string.IsNullOrEmpty(row.PainterName) ? "Painter unknown" : $"Painted by {row.PainterName}";

        var redo = WorldState.CommissionBoard.CanRedo(row);
        NoteBox.Visible = redo;
        RedoButton.Visible = redo;

        ReviewPanel.Visible = true;

        if (string.IsNullOrEmpty(ReviewHash))
        {
            ReviewStatusLabel.Text = "Picture unavailable.";

            return;
        }

        if (Transfers.HasFailed(ReviewHash))
        {
            ReviewStatusLabel.Text = "Picture unavailable.";

            return;
        }

        if (Transfers.TryGetTexture(ReviewHash, out var texture))
        {
            ReviewPicture.Texture = texture;
            Transfers.Hold(ReviewHash);
            ReviewHeld = true;
        } else
            ReviewStatusLabel.Text = "Loading...";
    }

    private void OnAcceptClicked()
    {
        if (ReviewId is null)
            return;

        ActionRequested?.Invoke(CommissionBoardState.ActionArgs(CommissionBoardRowAction.Accept, ReviewId));
        CloseReview();
    }

    private void OnRedoClicked()
    {
        if (ReviewId is null)
            return;

        if (!CommissionBoardState.TryBuildRedo(ReviewId, NoteBox.Text, out var args, out var error))
        {
            ReviewStatusLabel.Text = error;

            return;
        }

        ActionRequested?.Invoke(args);
        CloseReview();
    }

    private void CloseReview()
    {
        if (!ReviewPanel.Visible)
            return;

        if (ReviewHeld && (ReviewHash is not null))
        {
            Transfers.Release(ReviewHash);
            ReviewHeld = false;
        }

        NoteBox.IsFocused = false;
        ReviewPicture.Texture = null;
        ReviewId = null;
        ReviewHash = null;
        ReviewPanel.Visible = false;
    }

    private void OpenDeliver()
    {
        var row = WorldState.CommissionBoard.SelectedRow;

        if (row is null)
            return;

        DeliverId = row.Id;
        DeliverSlot = null;
        DeliverPage = 0;
        DeliverPanel.Visible = true;
        RefreshDeliverPicker();
    }

    private void SelectDeliverRow(int index)
    {
        var choices = CommissionBoardState.DeliverChoices(WorldState.Inventory, WorldState.PlayerName);

        if ((index < 0) || (index >= choices.Count))
            return;

        DeliverSlot = choices[index].Slot;
        RefreshDeliverPicker();
    }

    private void TurnDeliverPage(int delta)
    {
        DeliverPage += delta;
        RefreshDeliverPicker();
    }

    private void OnSendDeliverClicked()
    {
        if ((DeliverId is null) || (DeliverSlot is not { } slot))
            return;

        ActionRequested?.Invoke(CommissionBoardState.ActionArgs(CommissionBoardRowAction.Deliver, DeliverId, slot));
        CloseDeliver();
    }

    private void CloseDeliver()
    {
        if (!DeliverPanel.Visible)
            return;

        DeliverId = null;
        DeliverSlot = null;
        DeliverPanel.Visible = false;
    }
}

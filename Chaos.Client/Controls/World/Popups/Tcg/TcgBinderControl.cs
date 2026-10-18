#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     The card binder (layout B): a 4 x 3 card grid with tabs and paging on the left, the clicked card drawn large with
///     its counts and Pull out / Break / Craft on the right, and the Decks tab listing saved decks. Opened by a
///     TcgCollectionDisplay snapshot with <c>OpenWindow</c> when a TCG Binder is used. Every button only asks: the server
///     answers with a fresh snapshot, which <see cref="Refresh" /> shows. Borrows the <c>_nsett</c> prefab for its OK
///     button only, as the slot machine does; the frame is drawn from the live size.
/// </summary>
public sealed class TcgBinderControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 420;
    public const int PANEL_WIDTH = 620;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    private const int BUTTON_HEIGHT = CustomButton.HEIGHT;
    private const int CELL_STEP_X = 68;
    private const int CELL_STEP_Y = 94;
    private const int COLUMNS = 4;
    private const int CONTENT_WIDTH = PANEL_WIDTH - (2 * LEFT);
    private const int DECK_BUTTON_STEP = 96;
    private const int DECK_BUTTON_WIDTH = 90;
    private const int DECK_BUTTONS_TOP = GRID_TOP + (TcgCollectionState.MAX_DECKS * DECK_ROW_HEIGHT) + 6;
    private const int DECK_ROW_HEIGHT = 20;
    private const int DETAIL_LEFT = 300;
    private const int GRID_TOP = 54;
    private const int INFO_LEFT = 430;
    private const int INFO_WIDTH = 170;
    private const int LEFT = 20;
    private const int MESSAGE_TOP = 358;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int PAGER_TOP = 334;
    private const int RENAME_TOP = DECK_BUTTONS_TOP + BUTTON_HEIGHT + 8;
    private const int TAB_STEP = 52;
    private const int TAB_TOP = 26;
    private const int TAB_WIDTH = 48;
    private const int TITLE_TOP = 8;
    private const int STYLE_STEP = BUTTON_HEIGHT + 2;
    private const int STYLE_TOP = 36;

    private static readonly string[] TabNames = ["All", "Fire", "Water", "Wind", "Earth", "Neutral", "Decks", "Ranked"];

    private readonly CustomButton BreakButton;
    private readonly TcgChoicePanel Choice;
    private readonly CustomButton CraftButton;
    private readonly CustomButton[] DeckButtons;
    private readonly DeckRow[] DeckRows = new DeckRow[TcgCollectionState.MAX_DECKS];
    private readonly UILabel DecksHint;
    private readonly CustomButton DeleteDeckButton;
    private readonly TcgCardFace DetailFace;
    private readonly UILabel DustLabel;
    private readonly CustomButton EditDeckButton;
    private readonly CustomButton FindMatchButton;
    private readonly TcgCardFace[] Faces = new TcgCardFace[TcgCollectionState.PAGE_SIZE];
    private readonly CustomButton[] StyleButtons = new CustomButton[3];
    private readonly UILabel HintLabel;
    private readonly UILabel MessageLabel;
    private readonly UILabel NameLabel;
    private readonly CustomButton NewDeckButton;
    private readonly CustomButton NextButton;
    private static readonly Color MythicColor = new(185, 140, 255);
    private static readonly TcgCardStyle[] SwitchStyles = [TcgCardStyle.Normal, TcgCardStyle.Extended, TcgCardStyle.Full];
    private readonly CustomCheckBox OwnedOnlyBox;
    private readonly UILabel PageLabel;
    private readonly CustomButton PreviousButton;
    private readonly CustomButton PullOutButton;
    private readonly UILabel RarityLabel;
    private readonly CustomTextBox RenameBox;
    private readonly CustomButton RenameCancelButton;
    private readonly CustomButton RenameDeckButton;
    private readonly UILabel RenameLabel;
    private readonly CustomButton RenameSaveButton;
    private readonly UILabel RankedBestLabel;
    private readonly UIPanel RankedPointsBar;
    private readonly UIPanel RankedPointsFill;
    private readonly UILabel RankedPointsLabel;
    private readonly UILabel RankedRecentLabel;
    private readonly UILabel RankedSeasonLabel;
    private readonly UILabel RankedStepLabel;
    private readonly UILabel RankedTopTitle;
    private readonly UILabel[] RankedTopRows = new UILabel[TcgRankedState.LEADERBOARD_SIZE];
    private readonly CustomButton[] TabButtons = new CustomButton[TabNames.Length];
    private bool ClosedByServer;
    private string? RenamingDeck;
    private string? SelectedDeck;

    public TcgBinderControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Name = "TcgBinder";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        AddChild(Label(0, TITLE_TOP, PANEL_WIDTH, "Card Binder", LegendColors.Gold, HorizontalAlignment.Center));

        for (var i = 0; i < TabButtons.Length; i++)
        {
            var tab = (TcgBinderTab)i;

            TabButtons[i] = new CustomButton(TabNames[i], TAB_WIDTH)
            {
                X = LEFT + (i * TAB_STEP),
                Y = TAB_TOP
            };

            TabButtons[i].Clicked += () => SelectTab(tab);
            AddChild(TabButtons[i]);
        }

        OwnedOnlyBox = new CustomCheckBox
        {
            X = LEFT + (TabNames.Length * TAB_STEP) + 2,
            Y = TAB_TOP + 2,
            Width = 86,
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Owned only"
        };

        OwnedOnlyBox.Clicked += () =>
        {
            OwnedOnlyBox.Checked = !OwnedOnlyBox.Checked;
            State.SetOwnedOnly(OwnedOnlyBox.Checked);
            Refresh();
        };

        AddChild(OwnedOnlyBox);

        DustLabel = Label(524, TAB_TOP + 5, 76, string.Empty, LegendColors.Gold, HorizontalAlignment.Right);
        AddChild(DustLabel);

        for (var i = 0; i < Faces.Length; i++)
        {
            Faces[i] = new TcgCardFace(creatures, 1)
            {
                X = LEFT + ((i % COLUMNS) * CELL_STEP_X),
                Y = GRID_TOP + ((i / COLUMNS) * CELL_STEP_Y)
            };

            Faces[i].Clicked += OnCardClicked;
            AddChild(Faces[i]);
        }

        PreviousButton = new CustomButton("<", 24)
        {
            X = LEFT,
            Y = PAGER_TOP
        };

        PreviousButton.Clicked += () =>
        {
            State.PreviousPage();
            Refresh();
        };

        AddChild(PreviousButton);

        PageLabel = Label(LEFT + 28, PAGER_TOP + 5, 208, string.Empty, LegendColors.White, HorizontalAlignment.Center);
        AddChild(PageLabel);

        NextButton = new CustomButton(">", 24)
        {
            X = LEFT + 240,
            Y = PAGER_TOP
        };

        NextButton.Clicked += () =>
        {
            State.NextPage();
            Refresh();
        };

        AddChild(NextButton);

        DetailFace = new TcgCardFace(creatures, 2)
        {
            X = DETAIL_LEFT,
            Y = GRID_TOP,
            Animate = true
        };

        AddChild(DetailFace);

        NameLabel = Label(INFO_LEFT, GRID_TOP, INFO_WIDTH, string.Empty, LegendColors.Gold);
        RarityLabel = Label(INFO_LEFT, GRID_TOP + 16, INFO_WIDTH, string.Empty, LegendColors.Silver);
        for (var i = 0; i < StyleButtons.Length; i++)
        {
            var style = SwitchStyles[i];

            StyleButtons[i] = Button(
                string.Empty,
                INFO_LEFT,
                GRID_TOP + STYLE_TOP + (i * STYLE_STEP),
                INFO_WIDTH - 10,
                () =>
                {
                    State.SetDetailStyle(style);
                    Refresh();
                });
        }

        HintLabel = Label(DETAIL_LEFT, GRID_TOP + 120, CONTENT_WIDTH - DETAIL_LEFT + LEFT, "Click a card to see it here.", LegendColors.Silver, HorizontalAlignment.Center);
        AddChild(NameLabel);
        AddChild(RarityLabel);
        AddChild(HintLabel);

        PullOutButton = Button("Pull out", INFO_LEFT, GRID_TOP + 116, 120, OnPullOut);
        BreakButton = Button("Break", INFO_LEFT, GRID_TOP + 144, 120, OnBreak);
        CraftButton = Button("Craft", INFO_LEFT, GRID_TOP + 172, 120, OnCraft);

        for (var i = 0; i < DeckRows.Length; i++)
        {
            DeckRows[i] = new DeckRow(CONTENT_WIDTH)
            {
                X = LEFT,
                Y = GRID_TOP + (i * DECK_ROW_HEIGHT)
            };

            DeckRows[i].Clicked += OnDeckRowClicked;
            DeckRows[i].DoubleClicked += OnDeckRowDoubleClicked;
            AddChild(DeckRows[i]);
        }

        DecksHint = Label(LEFT, GRID_TOP + 4, CONTENT_WIDTH, "You have no saved decks. Press New deck to build one.", LegendColors.Silver);
        AddChild(DecksHint);

        NewDeckButton = Button("New deck", LEFT, DECK_BUTTONS_TOP, DECK_BUTTON_WIDTH, NewDeck);
        EditDeckButton = Button("Edit", LEFT + DECK_BUTTON_STEP, DECK_BUTTONS_TOP, DECK_BUTTON_WIDTH, EditSelectedDeck);
        RenameDeckButton = Button("Rename", LEFT + (2 * DECK_BUTTON_STEP), DECK_BUTTONS_TOP, DECK_BUTTON_WIDTH, StartRename);
        DeleteDeckButton = Button("Delete", LEFT + (3 * DECK_BUTTON_STEP), DECK_BUTTONS_TOP, DECK_BUTTON_WIDTH, DeleteSelectedDeck);
        FindMatchButton = Button("Find match", LEFT + (4 * DECK_BUTTON_STEP), DECK_BUTTONS_TOP, DECK_BUTTON_WIDTH, FindMatch);
        DeckButtons = [NewDeckButton, EditDeckButton, RenameDeckButton, DeleteDeckButton, FindMatchButton];

        RenameLabel = Label(LEFT, RENAME_TOP + 5, 60, "New name", LegendColors.White);
        AddChild(RenameLabel);

        RenameBox = new CustomTextBox
        {
            X = LEFT + 60,
            Y = RENAME_TOP,
            Width = 200,
            Height = BUTTON_HEIGHT,
            MaxLength = TcgDeckDraft.MAX_NAME_LENGTH,
            IsSelectable = true,
            IsTabStop = true
        };

        AddChild(RenameBox);

        RenameSaveButton = Button("Save", LEFT + 268, RENAME_TOP, 70, SaveRename);
        RenameCancelButton = Button("Cancel", LEFT + 344, RENAME_TOP, 70, EndRename);

        MessageLabel = Label(LEFT, MESSAGE_TOP, CONTENT_WIDTH, string.Empty, LegendColors.Orange);
        AddChild(MessageLabel);

        RankedStepLabel = Label(LEFT, GRID_TOP, CONTENT_WIDTH, string.Empty, LegendColors.Gold);
        AddChild(RankedStepLabel);
        RankedPointsBar = new UIPanel { X = LEFT, Y = GRID_TOP + 18, Width = CONTENT_WIDTH / 2, Height = 10, BackgroundColor = new Color(28, 20, 12), BorderColor = LegendColors.Gold, IsHitTestVisible = false };
        RankedPointsFill = new UIPanel { X = 1, Y = 1, Width = 0, Height = 8, BackgroundColor = LegendColors.Gold, IsHitTestVisible = false };
        RankedPointsLabel = Label(LEFT + (CONTENT_WIDTH / 2) + 6, GRID_TOP + 18, CONTENT_WIDTH - (CONTENT_WIDTH / 2) - 6, string.Empty, LegendColors.Gold);
        RankedPointsBar.AddChild(RankedPointsFill);
        AddChild(RankedPointsBar);
        AddChild(RankedPointsLabel);
        RankedBestLabel = Label(LEFT, GRID_TOP + 38, CONTENT_WIDTH, string.Empty, LegendColors.White);
        AddChild(RankedBestLabel);
        RankedSeasonLabel = Label(LEFT, GRID_TOP + 56, CONTENT_WIDTH, string.Empty, LegendColors.White);
        AddChild(RankedSeasonLabel);
        RankedRecentLabel = Label(LEFT, GRID_TOP + 74, CONTENT_WIDTH, string.Empty, LegendColors.White);
        AddChild(RankedRecentLabel);
        RankedTopTitle = Label(LEFT, GRID_TOP + 98, CONTENT_WIDTH, "Top 10", LegendColors.Gold);
        AddChild(RankedTopTitle);

        for (var i = 0; i < RankedTopRows.Length; i++)
        {
            RankedTopRows[i] = Label(LEFT, GRID_TOP + 116 + (i * 18), CONTENT_WIDTH, string.Empty, LegendColors.White);
            AddChild(RankedTopRows[i]);
        }

        //last, so it draws over everything and swallows clicks meant for what is behind it
        Choice = new TcgChoicePanel(PANEL_WIDTH, PANEL_HEIGHT);
        AddChild(Choice);
    }

    private static TcgCollectionState State => WorldState.TcgCollection;

    /// <summary>Every request to send, including Close when the player closes the window.</summary>
    public event Action<TcgCollectionActionArgs>? ActionRequested;

    /// <summary>Find match on the Decks tab: the name of the deck to queue with.</summary>
    public event Action<string>? FindMatchClicked;

    /// <summary>The Ranked tab was opened: the server ladder info is needed.</summary>
    public event Action? RankedTabOpened;

    /// <summary>The window was hidden, by the player or the server. The deck builder closes with it.</summary>
    public event Action? Closed;

    /// <summary>New deck or Edit: open the deck builder on this draft.</summary>
    public event Action<TcgDeckDraft>? EditDeckRequested;

    /// <summary>The keyword under the cursor on the detail card, for the hover box. Null when nothing qualifies.</summary>
    public string? HoveredKeyword(int mouseX, int mouseY) => Visible ? DetailFace.KeywordAt(mouseX, mouseY) : null;

    /// <summary>True while a question is asked over the window; hovers behind it stay hidden.</summary>
    public bool HasOpenQuestion => Choice.Visible;

    /// <summary>The server closed the card windows: hide without sending Close back.</summary>
    public void CloseFromServer()
    {
        ClosedByServer = true;
        Hide();
        ClosedByServer = false;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        Choice.Dismiss();
        EndRename();
        base.Hide();

        if (!ClosedByServer)
            ActionRequested?.Invoke(TcgCollectionState.CloseAction());

        Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (Choice.Visible)
                Choice.Cancel();
            else if (RenamingDeck is not null)
                EndRename();
            else
                Hide();

            e.Handled = true;

            return;
        }

        if ((e.Keycode == Keycode.Enter) && (RenamingDeck is not null))
        {
            SaveRename();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public void Open()
    {
        RenamingDeck = null;
        Show();
        Refresh();
    }

    /// <summary>Shows the latest snapshot. Called after every snapshot while the window is open.</summary>
    public void Refresh()
    {
        if (!Visible)
            return;

        var decksTab = State.Tab == TcgBinderTab.Decks;
        var rankedTab = State.Tab == TcgBinderTab.Ranked;
        var cardsTab = !decksTab && !rankedTab;

        for (var i = 0; i < TabButtons.Length; i++)
            TabButtons[i].Selected = (TcgBinderTab)i == State.Tab;

        OwnedOnlyBox.Visible = cardsTab;
        OwnedOnlyBox.Checked = State.OwnedOnly;
        DustLabel.Text = $"Dust: {State.Dust:N0}";
        MessageLabel.Text = State.Message;
        MessageLabel.Visible = !rankedTab;

        RefreshGrid(decksTab || rankedTab);
        RefreshDetail(decksTab || rankedTab);
        RefreshDecks(decksTab);
        RefreshRanked(rankedTab);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        //the queue mode changes outside the binder, so re-check the button every frame
        if (Visible && (State.Tab == TcgBinderTab.Decks))
            FindMatchButton.Enabled = (SelectedDeckView() is { } selected) && WorldState.TcgRanked.CanFindMatch(selected.Status);
    }

    private CustomButton Button(string caption, int x, int y, int width, ClickedHandler onClick)
    {
        var button = new CustomButton(caption, width)
        {
            X = x,
            Y = y
        };

        button.Clicked += onClick;
        AddChild(button);

        return button;
    }

    private void ConfirmPullOut(TcgCatalogCardView card, TcgPrinting printing)
    {
        var request = TcgCollectionState.PullOutAction(card.Key, printing);

        if (State.PullOutWarning(card.Key) is { } warning)
            Choice.Ask(
                $"{warning} Pull out {TcgCollectionState.CardName(card, printing)}?",
                ["Yes", "No"],
                answer =>
                {
                    if (answer == 0)
                        Request(request);
                });
        else
            Request(request);
    }

    private void DeleteSelectedDeck()
    {
        if (SelectedDeckView() is not { } deck)
            return;

        Choice.Ask(
            $"Delete the deck {deck.Name}?",
            ["Yes", "No"],
            answer =>
            {
                if (answer != 0)
                    return;

                SelectedDeck = null;
                Request(TcgCollectionState.DeleteAction(deck.Name));
            });
    }

    private void FindMatch()
    {
        if (SelectedDeckView() is { } deck)
            FindMatchClicked?.Invoke(deck.Name);
    }

    private void EditSelectedDeck()
    {
        if (SelectedDeckView() is { } deck)
            EditDeckRequested?.Invoke(TcgDeckDraft.Edit(deck));
    }

    private void EndRename()
    {
        RenamingDeck = null;
        RenameBox.IsFocused = false;

        if (Visible)
            RefreshDecks(State.Tab == TcgBinderTab.Decks);
    }

    private static UILabel Label(
        int x,
        int y,
        int width,
        string text,
        Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            IsHitTestVisible = false
        };

    private void NewDeck()
    {
        if (State.Decks.Count >= TcgCollectionState.MAX_DECKS)
            return;

        EditDeckRequested?.Invoke(TcgDeckDraft.New(State.Decks.Select(deck => deck.Name)));
    }

    private void OnBreak()
    {
        if (State.Selected is not { } card)
            return;

        var plain = new TcgPrinting(State.DetailStyle, false);
        var foil = new TcgPrinting(State.DetailStyle, true);
        var canPlain = State.CanBreak(card.Key, plain);
        var canFoil = State.CanBreak(card.Key, foil);

        if (canPlain && canFoil)
            Choice.Ask(
                $"Break which copy of {card.Name}?",
                [
                    $"Plain ({TcgCollectionState.BreakValue(card.Rarity, plain)})",
                    $"Foil ({TcgCollectionState.BreakValue(card.Rarity, foil)})",
                    "Cancel"
                ],
                answer =>
                {
                    if (answer is 0 or 1)
                        Request(TcgCollectionState.BreakAction(card.Key, answer == 1 ? foil : plain));
                });
        else if (canPlain || canFoil)
        {
            var printing = canFoil ? foil : plain;

            Choice.Ask(
                State.BreakQuestion(card, printing),
                ["Yes", "No"],
                answer =>
                {
                    if (answer == 0)
                        Request(TcgCollectionState.BreakAction(card.Key, printing));
                });
        }
    }

    private void OnCardClicked(TcgCardFace face)
    {
        if (face.Card is not { } card)
            return;

        State.Select(card.Key);
        Refresh();
    }

    private void OnCraft()
    {
        if ((State.Selected is not { } card) || !State.CanCraft(card.Key))
            return;

        Choice.Ask(
            State.CraftQuestion(card),
            ["Yes", "No"],
            answer =>
            {
                if (answer == 0)
                    Request(TcgCollectionState.CraftAction(card.Key));
            });
    }

    private void OnDeckRowClicked(DeckRow row)
    {
        RenamingDeck = null;
        RenameBox.IsFocused = false;
        SelectedDeck = row.DeckName;
        Refresh();
    }

    private void OnDeckRowDoubleClicked(DeckRow row)
    {
        SelectedDeck = row.DeckName;
        EditSelectedDeck();
    }

    private void OnPullOut()
    {
        if (State.Selected is not { } card)
            return;

        var plain = new TcgPrinting(State.DetailStyle, false);
        var foil = new TcgPrinting(State.DetailStyle, true);
        var hasPlain = State.CanPullOut(card.Key, plain);
        var hasFoil = State.CanPullOut(card.Key, foil);

        if (hasPlain && hasFoil)
            Choice.Ask(
                $"Pull out which copy of {card.Name}?",
                ["Plain", "Foil", "Cancel"],
                answer =>
                {
                    if (answer is 0 or 1)
                        ConfirmPullOut(card, answer == 1 ? foil : plain);
                });
        else if (hasPlain || hasFoil)
            ConfirmPullOut(card, hasFoil ? foil : plain);
    }

    private static Color RarityColor(TcgRarity rarity)
        => rarity switch
        {
            TcgRarity.Common   => LegendColors.Silver,
            TcgRarity.Uncommon => LegendColors.Lime,
            TcgRarity.Rare     => LegendColors.Gold,
            TcgRarity.Legendary => LegendColors.Orange,
            _                  => MythicColor
        };

    private void RefreshDecks(bool shown)
    {
        var decks = State.Decks;

        if ((SelectedDeck is not null) && !decks.Any(deck => deck.Name.Equals(SelectedDeck, StringComparison.OrdinalIgnoreCase)))
            SelectedDeck = null;

        if ((RenamingDeck is not null) && !decks.Any(deck => deck.Name.Equals(RenamingDeck, StringComparison.OrdinalIgnoreCase)))
        {
            RenamingDeck = null;
            RenameBox.IsFocused = false;
        }

        for (var i = 0; i < DeckRows.Length; i++)
        {
            var deck = shown && (i < decks.Count) ? decks[i] : null;
            DeckRows[i].Bind(deck, (deck is not null) && deck.Name.Equals(SelectedDeck, StringComparison.OrdinalIgnoreCase));
        }

        DecksHint.Visible = shown && (decks.Count == 0);

        foreach (var button in DeckButtons)
            button.Visible = shown;

        NewDeckButton.Enabled = decks.Count < TcgCollectionState.MAX_DECKS;
        EditDeckButton.Enabled = SelectedDeck is not null;
        RenameDeckButton.Enabled = SelectedDeck is not null;
        DeleteDeckButton.Enabled = SelectedDeck is not null;
        FindMatchButton.Enabled = (SelectedDeckView() is { } selected) && WorldState.TcgRanked.CanFindMatch(selected.Status);

        var renaming = shown && (RenamingDeck is not null);
        RenameLabel.Visible = renaming;
        RenameBox.Visible = renaming;
        RenameSaveButton.Visible = renaming;
        RenameCancelButton.Visible = renaming;
    }

    private void RefreshRanked(bool shown)
    {
        var info = WorldState.TcgRanked.Info;

        RankedStepLabel.Visible = shown;
        RankedPointsLabel.Visible = shown && (info is not null);
        RankedPointsBar.Visible = shown && (info is not null);
        RankedBestLabel.Visible = shown && (info is not null);
        RankedSeasonLabel.Visible = shown && (info is not null);
        RankedRecentLabel.Visible = shown && (info is not null);
        RankedTopTitle.Visible = shown && (info is not null);

        foreach (var row in RankedTopRows)
            row.Visible = false;

        if (!shown)
            return;

        if (info is null)
        {
            RankedStepLabel.Text = "Loading...";

            return;
        }

        RankedStepLabel.Text = TcgRankedState.StepName(info.Step);
        RankedPointsLabel.Text = TcgRankedState.PointsText(info.Step, info.Points, info.LeaderboardPosition);
        var full = (info.Step >= TcgRankedState.TOP_STEP) && (info.Points >= TcgRankedState.STEP_POINTS);
        var fraction = full ? 1.0 : Math.Clamp((double)info.Points / TcgRankedState.STEP_POINTS, 0.0, 1.0);
        RankedPointsFill.Width = (int)Math.Round((RankedPointsBar.Width - 2) * fraction);
        RankedBestLabel.Text = $"Best this season: {TcgRankedState.StepName(info.BestStep)}";
        RankedSeasonLabel.Text = $"Season {info.Season} ends {DateTimeOffset.FromUnixTimeSeconds(info.SeasonEndUnix).UtcDateTime:yyyy-MM-dd}";
        RankedRecentLabel.Text = string.IsNullOrEmpty(info.Recent) ? "Recent: -" : $"Recent: {string.Join(" ", info.Recent.ToCharArray())}";

        for (var i = 0; i < RankedTopRows.Length; i++)
        {
            if (i >= info.Top.Count)
                break;

            RankedTopRows[i].Visible = true;
            RankedTopRows[i].Text = $"#{i + 1} {info.Top[i]}";
        }
    }

    private void RefreshDetail(bool decksTab)
    {
        var card = decksTab ? null : State.Selected;
        var shown = card is not null;

        DetailFace.Visible = shown;
        NameLabel.Visible = shown;
        RarityLabel.Visible = shown;
        foreach (var button in StyleButtons)
            button.Visible = false;
        PullOutButton.Visible = shown;
        BreakButton.Visible = shown;
        CraftButton.Visible = shown;
        HintLabel.Visible = !decksTab && !shown;

        if (card is null)
            return;

        var styles = TcgCollectionState.Styles(card);
        for (var i = 0; i < styles.Count; i++)
        {
            StyleButtons[i].Visible = true;
            StyleButtons[i].Caption = State.StyleText(card.Key, styles[i]);
            StyleButtons[i].Selected = styles[i] == State.DetailStyle;
        }

        var plain = new TcgPrinting(State.DetailStyle, false);
        var foil = new TcgPrinting(State.DetailStyle, true);

        DetailFace.Card = card;
        DetailFace.Style = State.DetailStyle;
        DetailFace.Owned = (State.Count(card.Key, plain) + State.Count(card.Key, foil)) > 0;
        DetailFace.Foil = State.Count(card.Key, foil) > 0;
        NameLabel.Text = card.Name;
        RarityLabel.Text = card.Rarity.ToString();
        RarityLabel.ForegroundColor = RarityColor(card.Rarity);
        PullOutButton.Enabled = State.CanPullOut(card.Key, plain) || State.CanPullOut(card.Key, foil);
        BreakButton.Enabled = State.CanBreak(card.Key, plain) || State.CanBreak(card.Key, foil);
        CraftButton.Caption = TcgCollectionState.Craftable(card.Rarity) ? $"Craft ({TcgCollectionState.CraftCost(card.Rarity)})" : "Can't craft";
        CraftButton.Enabled = State.CanCraft(card.Key);
    }

    private void RefreshGrid(bool decksTab)
    {
        var page = State.PageCards;

        for (var i = 0; i < Faces.Length; i++)
        {
            var face = Faces[i];
            var card = decksTab || (i >= page.Count) ? null : page[i];
            face.Visible = card is not null;

            if (card is null)
                continue;

            var copies = State.Copies(card.Key);
            face.Card = card;
            face.Owned = copies > 0;
            var best = State.BestPrinting(card.Key) ?? TcgPrinting.Normal;
            face.Style = best.Style;
            face.Foil = best.Foil;
            face.Badge = copies > 0 ? copies.ToString() : string.Empty;
            face.Selected = card.Key.Equals(State.SelectedKey, StringComparison.OrdinalIgnoreCase);
        }

        PreviousButton.Visible = !decksTab;
        NextButton.Visible = !decksTab;
        PageLabel.Visible = !decksTab;
        PreviousButton.Enabled = State.Page > 0;
        NextButton.Enabled = State.Page < (State.PageCount - 1);
        PageLabel.Text = State.PageText;
    }

    private void Request(TcgCollectionActionArgs args)
    {
        State.ClearMessage();
        MessageLabel.Text = string.Empty;
        ActionRequested?.Invoke(args);
    }

    private void SaveRename()
    {
        if (RenamingDeck is not { } old)
            return;

        var name = RenameBox.Text.Trim();

        if (name.Length is 0 or > TcgDeckDraft.MAX_NAME_LENGTH)
        {
            MessageLabel.Text = $"A deck name is 1 to {TcgDeckDraft.MAX_NAME_LENGTH} characters.";

            return;
        }

        EndRename();

        if (name.Equals(old, StringComparison.Ordinal))
            return;

        SelectedDeck = name;
        Request(TcgCollectionState.RenameAction(old, name));
    }

    private TcgDeckView? SelectedDeckView()
        => SelectedDeck is null ? null : State.Decks.FirstOrDefault(deck => deck.Name.Equals(SelectedDeck, StringComparison.OrdinalIgnoreCase));

    private void SelectTab(TcgBinderTab tab)
    {
        RenamingDeck = null;
        RenameBox.IsFocused = false;
        State.SetTab(tab);
        Refresh();

        if (tab == TcgBinderTab.Ranked)
            RankedTabOpened?.Invoke();
    }

    private void StartRename()
    {
        if (SelectedDeckView() is not { } deck)
            return;

        RenamingDeck = deck.Name;
        RenameBox.Text = deck.Name;
        RenameBox.IsFocused = true;
        Refresh();
    }

    /// <summary>One saved deck: its name and status. Click selects, double-click edits.</summary>
    private sealed class DeckRow : UIPanel
    {
        private static readonly Color SelectedBackground = new(156, 96, 12, 180);
        private readonly UILabel NameText;
        private readonly UILabel StatusText;

        public DeckRow(int width)
        {
            Width = width;
            Height = DECK_ROW_HEIGHT;

            NameText = Label(4, (DECK_ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2, width - 140, string.Empty, LegendColors.White);
            StatusText = Label(width - 132, (DECK_ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2, 128, string.Empty, LegendColors.Silver, HorizontalAlignment.Right);
            AddChild(NameText);
            AddChild(StatusText);
        }

        public string DeckName { get; private set; } = string.Empty;

        public event Action<DeckRow>? Clicked;
        public event Action<DeckRow>? DoubleClicked;

        public void Bind(TcgDeckView? deck, bool selected)
        {
            Visible = deck is not null;

            if (deck is null)
                return;

            DeckName = deck.Name;
            NameText.Text = deck.Name;
            StatusText.Text = TcgCollectionState.StatusText(deck);
            StatusText.ForegroundColor = deck.Status == TcgDeckStatus.Ready ? LegendColors.Lime : LegendColors.Silver;
            BackgroundColor = selected ? SelectedBackground : null;
        }

        public override void OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Left)
                Clicked?.Invoke(this);

            e.Handled = true;
        }

        public override void OnDoubleClick(DoubleClickEvent e)
        {
            if (e.Button == MouseButton.Left)
                DoubleClicked?.Invoke(this);

            e.Handled = true;
        }
    }
}

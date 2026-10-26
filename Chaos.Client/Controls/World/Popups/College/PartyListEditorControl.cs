using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The party-list editor. Page 1 has two tabs: Mine (the Teacher's saved lists) and Party bank (shared lists to
///     preview or copy, with a kind picker). Page 2 edits one list: Quip and Sketch entries carry one prompt, Bluff
///     entries a question, an answer and three decoys. It also serves as the read-only preview of a bank list. The
///     server's PartyLists, PartyBank, PartyListEdit and PartyListResult feed it; every save goes to the server, which
///     checks the limits again.
/// </summary>
public sealed class PartyListEditorControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - 4;
    private const int BUTTONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int STATUS_X = LEFT + 276;
    private const int TABS_TOP = 26;
    private const int LIST_HEADER_TOP = TABS_TOP + CustomButton.HEIGHT + 4;
    private const int LIST_ROWS_TOP = LIST_HEADER_TOP + 14;
    private const int KIND_X = 250;
    private const int COUNT_X = 424;
    private const int SIDE_WIDTH = 170;
    private const int SIDE_ROWS = 18;
    private const int TITLE_TOP = 28;
    private const int ENTRIES_LABEL_TOP = 54;
    private const int ENTRY_ROWS_TOP = 70;
    private const int EDIT_X = 200;
    private const int EDIT_WIDTH = LEFT + INNER_WIDTH - EDIT_X;
    private const int TEXT_TOP = 44;
    private const int ANSWER_LABEL_TOP = 80;
    private const int ANSWER_TOP = 96;
    private const int DECOY_TOP_1 = 148;
    private const int DECOY_TOP_2 = 200;
    private const int DECOY_TOP_3 = 252;
    private const int EDIT_BUTTONS_SINGLE_TOP = ANSWER_TOP + CustomButton.HEIGHT + 12;
    private const int EDIT_BUTTONS_BLUFF_TOP = DECOY_TOP_3 + CustomButton.HEIGHT + 12;
    private const int RULES_SINGLE_TOP = EDIT_BUTTONS_SINGLE_TOP + CustomButton.HEIGHT + 12;
    private const int RULES_BLUFF_TOP = EDIT_BUTTONS_BLUFF_TOP + CustomButton.HEIGHT + 12;

    private static readonly int[] DecoyTops = [DECOY_TOP_1, DECOY_TOP_2, DECOY_TOP_3];

    private readonly CustomButton AddEntryButton;
    private readonly CustomTextBox AnswerBox;
    private readonly UILabel AnswerLabel;
    private readonly CustomButton BackButton;
    private readonly List<UIElement> BankKindElements = [];
    private readonly CustomButton[] BankKindButtons = new CustomButton[3];
    private readonly UILabel BankPageLabel;
    private readonly List<UIElement> BankPageElements = [];
    private readonly CustomButton BankTab;
    private readonly UILabel CaptionLabel;
    private readonly CustomButton CopyBankButton;
    private readonly CustomButton CopyButton;
    private readonly CustomButton CopyToMineButton;
    private readonly CustomTextBox[] DecoyBoxes = new CustomTextBox[CollegeProtocol.BLUFF_MAX_DECOYS];
    private readonly UILabel[] DecoyLabels = new UILabel[CollegeProtocol.BLUFF_MAX_DECOYS];
    private readonly CustomButton DeleteButton;
    private readonly CustomButton DownEntryButton;
    private readonly List<UIElement> EditPage = [];
    private readonly UILabel EmptyLabel;
    private readonly UILabel EntriesLabel;
    private readonly CollegeListRow[] EntryRows = new CollegeListRow[SIDE_ROWS];
    private readonly List<UIElement> KindAskElements = [];
    private readonly List<UIElement> ListPage = [];
    private readonly UILabel MiddleHeader;
    private readonly List<UIElement> MineElements = [];
    private readonly CustomButton MineTab;
    private readonly CustomButton NextButton;
    private readonly CustomButton OpenButton;
    private readonly CustomButton PreviewButton;
    private readonly CustomButton PrevButton;
    private readonly CustomButton RemoveEntryButton;
    private readonly CollegeListRow[] Rows = new CollegeListRow[CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER];
    private readonly UILabel RulesLabel;
    private readonly CustomButton SaveButton;
    private readonly UILabel Status;
    private readonly CustomTextBox TextBox;
    private readonly UILabel TextLabel;
    private readonly CustomTextBox TitleBox;
    private readonly CustomButton UpEntryButton;

    private bool AskingKind;
    private bool AwaitingSave;
    private bool BackAfterSave;
    private PartyListKind BankKind;
    private List<CollegePartyListRowInfo> BankRows = [];
    private int BankPage;
    private int BankPageCount = 1;
    private bool BankTabShown;
    private string DeleteArmed = string.Empty;
    private bool EditingPage;
    private List<CollegePartyListRowInfo> MineRows = [];
    private int RefusedVersion = -1;
    private int ScrollTop;
    private int SelectedRow = -1;
    private int SentVersion;
    private int ShownVersion = -1;

    public PartyListDocument Document { get; } = new();

    public event Action<CollegeActionArgs>? Send;

    public PartyListEditorControl()
        : base("_nsett", false)
    {
        Name = "PartyListEditor";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - STATUS_X);

        //page 1: the tabs and their list
        MineTab = AddButton("Mine", 70, LEFT, TABS_TOP, () => ShowTab(false));
        BankTab = AddButton("Party bank", 90, LEFT + 76, TABS_TOP, () => ShowTab(true));
        ListPage.Add(MineTab);
        ListPage.Add(BankTab);

        var kinds = new[] { PartyListKind.Quip, PartyListKind.Bluff, PartyListKind.Sketch };
        var widths = new[] { 60, 60, 70 };
        var x = LEFT + INNER_WIDTH;

        for (var i = 2; i >= 0; i--)
        {
            var kind = kinds[i];
            x -= widths[i];
            BankKindButtons[i] = AddButton(KindName(kind), widths[i], x, TABS_TOP, () => PickKind(kind));
            BankKindElements.Add(BankKindButtons[i]);
            ListPage.Add(BankKindButtons[i]);
            x -= 6;
        }

        ListPage.Add(Caption("Title", LEFT, LIST_HEADER_TOP, 200, color: LegendColors.Gold));
        MiddleHeader = Caption("Kind", LEFT + KIND_X, LIST_HEADER_TOP, 120, color: LegendColors.Gold);
        ListPage.Add(MiddleHeader);
        ListPage.Add(Caption("Entries", LEFT + COUNT_X, LIST_HEADER_TOP, 100, color: LegendColors.Gold));

        for (var i = 0; i < Rows.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(INNER_WIDTH, [0, KIND_X, COUNT_X]) { X = LEFT, Y = LIST_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectRow(index);

            row.DoubleClicked += () =>
            {
                SelectRow(index);
                OpenSelected();
            };

            AddChild(row);
            Rows[i] = row;
            ListPage.Add(row);
        }

        EmptyLabel = Caption(string.Empty, LEFT, LIST_ROWS_TOP, INNER_WIDTH, color: LegendColors.Gray);
        ListPage.Add(EmptyLabel);

        OpenButton = AddButton("Open", 60, LEFT, BUTTONS_TOP, OpenSelected);
        MineElements.Add(OpenButton);
        MineElements.Add(AddButton("New list", 80, LEFT + 66, BUTTONS_TOP, NewList));
        CopyButton = AddButton("Copy", 50, LEFT + 152, BUTTONS_TOP, CopySelected);
        MineElements.Add(CopyButton);
        DeleteButton = AddButton("Delete", 60, LEFT + 208, BUTTONS_TOP, DeleteSelected);
        MineElements.Add(DeleteButton);

        KindAskElements.Add(AddButton("Quip", 60, LEFT, BUTTONS_TOP, () => StartNew(PartyListKind.Quip)));
        KindAskElements.Add(AddButton("Bluff", 60, LEFT + 66, BUTTONS_TOP, () => StartNew(PartyListKind.Bluff)));
        KindAskElements.Add(AddButton("Sketch", 70, LEFT + 132, BUTTONS_TOP, () => StartNew(PartyListKind.Sketch)));
        KindAskElements.Add(AddButton("Cancel", 60, LEFT + 208, BUTTONS_TOP, CancelNew));

        PreviewButton = AddButton("Preview", 60, LEFT, BUTTONS_TOP, OpenSelected);
        BankPageElements.Add(PreviewButton);
        CopyBankButton = AddButton("Copy to mine", 96, LEFT + 66, BUTTONS_TOP, CopyBankSelected);
        BankPageElements.Add(CopyBankButton);
        PrevButton = AddButton("<", 30, LEFT + 168, BUTTONS_TOP, () => RequestBankPage(BankPage - 1));
        BankPageElements.Add(PrevButton);
        BankPageLabel = Caption(string.Empty, LEFT + 200, footerTextY, 40, HorizontalAlignment.Center);
        BankPageElements.Add(BankPageLabel);
        NextButton = AddButton(">", 30, LEFT + 242, BUTTONS_TOP, () => RequestBankPage(BankPage + 1));
        BankPageElements.Add(NextButton);

        ListPage.AddRange(MineElements);
        ListPage.AddRange(KindAskElements);
        ListPage.AddRange(BankPageElements);

        //page 2: one list
        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = SIDE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_WORD_LIST_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "List title"
        };

        AddChild(TitleBox);
        EditPage.Add(TitleBox);

        EntriesLabel = Caption(string.Empty, LEFT, ENTRIES_LABEL_TOP, SIDE_WIDTH, color: LegendColors.Gold);
        EditPage.Add(EntriesLabel);

        for (var i = 0; i < EntryRows.Length; i++)
        {
            var slot = i;
            var row = new CollegeListRow(SIDE_WIDTH, [0]) { X = LEFT, Y = ENTRY_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectEntry(slot);
            AddChild(row);
            EntryRows[i] = row;
            EditPage.Add(row);
        }

        TextLabel = Caption(string.Empty, EDIT_X, TITLE_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(TextLabel);

        TextBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = TEXT_TOP,
            Width = EDIT_WIDTH,
            Height = CustomButton.HEIGHT,
            IsSelectable = true,
            IsTabStop = true
        };

        AddChild(TextBox);
        EditPage.Add(TextBox);

        AnswerLabel = Caption(string.Empty, EDIT_X, ANSWER_LABEL_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(AnswerLabel);

        AnswerBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = ANSWER_TOP,
            Width = EDIT_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.BLUFF_MAX_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Answer"
        };

        AddChild(AnswerBox);
        EditPage.Add(AnswerBox);

        for (var i = 0; i < DecoyBoxes.Length; i++)
        {
            var number = i + 1;
            DecoyLabels[i] = Caption(string.Empty, EDIT_X, DECOY_TOP_1 + (i * 52) - 16, EDIT_WIDTH, color: LegendColors.Gold);
            EditPage.Add(DecoyLabels[i]);

            DecoyBoxes[i] = new CustomTextBox
            {
                X = EDIT_X,
                Y = DecoyTops[i],
                Width = EDIT_WIDTH,
                Height = CustomButton.HEIGHT,
                MaxLength = CollegeProtocol.BLUFF_MAX_CHARS,
                IsSelectable = true,
                IsTabStop = true,
                HintText = $"Decoy {number}"
            };

            AddChild(DecoyBoxes[i]);
            EditPage.Add(DecoyBoxes[i]);
        }

        AddEntryButton = AddButton("Add", 50, EDIT_X, EDIT_BUTTONS_SINGLE_TOP, () => Edit(Document.Add));
        EditPage.Add(AddEntryButton);
        UpEntryButton = AddButton("Up", 40, EDIT_X + 56, EDIT_BUTTONS_SINGLE_TOP, () => Edit(Document.MoveUp));
        EditPage.Add(UpEntryButton);
        DownEntryButton = AddButton("Down", 50, EDIT_X + 102, EDIT_BUTTONS_SINGLE_TOP, () => Edit(Document.MoveDown));
        EditPage.Add(DownEntryButton);
        RemoveEntryButton = AddButton("Remove", 70, EDIT_X + 158, EDIT_BUTTONS_SINGLE_TOP, () => Edit(Document.Remove));
        EditPage.Add(RemoveEntryButton);

        RulesLabel = Caption(string.Empty, EDIT_X, RULES_SINGLE_TOP, EDIT_WIDTH, color: LegendColors.Gray);
        EditPage.Add(RulesLabel);

        BackButton = AddButton("Back", 50, LEFT, BUTTONS_TOP, OnBackClicked);
        SaveButton = AddButton("Save", 50, LEFT + 56, BUTTONS_TOP, () => Save(false));
        CopyToMineButton = AddButton("Copy to mine", 100, LEFT + 56, BUTTONS_TOP, CopyPreviewToMine);
        EditPage.Add(BackButton);
        EditPage.Add(SaveButton);
        EditPage.Add(CopyToMineButton);
    }

    private bool IsPreview => Document.IsPreview;

    private IReadOnlyList<CollegePartyListRowInfo> ShownRows => BankTabShown ? BankRows : MineRows;

    private CollegePartyListRowInfo? SelectedList
        => (SelectedRow >= 0) && (SelectedRow < ShownRows.Count) ? ShownRows[SelectedRow] : null;

    /// <summary>Shows the Teacher's saved lists on the Mine tab. A save still waiting on page 2 goes out first.</summary>
    public void ShowList(IReadOnlyList<CollegePartyListRowInfo> rows)
    {
        MineRows = [..rows];
        DeleteArmed = string.Empty;

        //the server sends the list after every save; a visible page 2 stays put, and Back or a pending save-then-back leaves it itself
        if (EditingPage && Visible)
            return;

        //unsaved edits of a closed editor: the save goes out first and its result returns to the list, so nothing is dropped
        if (EditingPage && !IsPreview)
        {
            Pull();

            if (Document.IsDirty)
            {
                if (Document.Version != RefusedVersion)
                    Save(true);

                Show();

                return;
            }
        }

        BankTabShown = false;
        AskingKind = false;
        SelectedRow = MineRows.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, MineRows.Count - 1);
        ShowPage(false);

        if (!Visible)
        {
            Status.Text = string.Empty;
            Show();
        }
    }

    /// <summary>Fills the Party bank tab with one page of shared lists of one kind.</summary>
    public void ShowBank(PartyListKind kind, byte page, byte pageCount, IReadOnlyList<CollegePartyListRowInfo> rows)
    {
        BankKind = kind;
        BankRows = [..rows];
        BankPage = page;
        BankPageCount = Math.Max(1, (int)pageCount);

        if (BankTabShown)
            SelectedRow = BankRows.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, BankRows.Count - 1);

        if (!EditingPage)
            RefreshList();
    }

    /// <summary>Opens page 2 on a list: a saved one, a new one (no entries) or a read-only preview.</summary>
    public void ShowEdit(string id, WordListEditMode mode, CollegePartyListDraftInfo? draft, string message)
    {
        if (draft is null)
        {
            Status.Text = message;

            return;
        }

        if (EditingPage && !IsPreview)
        {
            Pull();

            if (Document.IsDirty)
                Save(false);
        }

        Document.Load(id, draft, mode == WordListEditMode.Preview);
        AwaitingSave = false;
        BackAfterSave = false;
        RefusedVersion = -1;
        DeleteArmed = string.Empty;
        AskingKind = false;
        ScrollTop = 0;
        Status.Text = message;
        ShowPage(true);

        if (!Visible)
            Show();
    }

    /// <summary>Answers a PartyListResult. True when a refused save showed the hidden editor again, so the caller brings it to the front.</summary>
    public bool ShowResult(string id, WordListResultCode code, string message)
    {
        Status.Text = message;

        //Copy and Delete results only need the status line; the list that follows refreshes page 1
        if (!AwaitingSave)
            return false;

        AwaitingSave = false;

        if (code == WordListResultCode.Saved)
        {
            if (Document.Id.Length == 0)
                Document.SetId(id);

            //typing after Save was pressed is still unsaved
            if (Document.Version == SentVersion)
                Document.MarkClean();

            //typing after Back was pressed stays on page 2, still unsaved
            if (BackAfterSave && (Document.Version == SentVersion))
            {
                BackAfterSave = false;
                GoToList();
            }

            BackAfterSave = false;

            //the player closed while this save was in flight, so edits made since were never sent
            if (!Visible && Document.IsDirty)
                Save(false);

            return false;
        }

        BackAfterSave = false;
        RefusedVersion = SentVersion;

        if (Visible)
            return false;

        ShowPage(true);
        Show();

        return true;
    }

    /// <summary>Logging out takes the editor away without its Close, so a dirty list is saved here.</summary>
    public void SaveIfDirty()
    {
        if (!EditingPage || IsPreview)
            return;

        Pull();

        if (Document.IsDirty)
            Save(false);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && EditingPage)
        {
            Pull();

            if (Document.Version != ShownVersion)
                RefreshEdit();
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            OnCloseClicked();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (!EditingPage || (Document.Entries.Count <= SIDE_ROWS))
            return;

        ScrollTop = Math.Clamp(ScrollTop - Math.Sign(e.Delta), 0, Document.Entries.Count - SIDE_ROWS);
        RefreshEdit();
        e.Handled = true;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        TextBox.IsFocused = false;
        AnswerBox.IsFocused = false;

        foreach (var box in DecoyBoxes)
            box.IsFocused = false;

        base.Hide();
    }

    private void OnCloseClicked()
    {
        if (EditingPage && !IsPreview)
        {
            Pull();

            //a save the server refused is not retried until something changes, or the window could never close
            if (Document.IsDirty && (Document.Version != RefusedVersion))
                Save(false);
        }

        Hide();
    }

    private void OnBackClicked()
    {
        if (IsPreview)
        {
            BankTabShown = true;
            SelectedRow = BankRows.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, BankRows.Count - 1);
            ShowPage(false);

            return;
        }

        Pull();

        if (Document.IsDirty)
            Save(true);
        else
            GoToList();
    }

    private void GoToList()
    {
        BankTabShown = false;
        AskingKind = false;
        ShowPage(false);

        //asks for the list again, so a saved title or entry count shows
        Raise(new CollegeActionArgs { Type = CollegeActionType.PartyListOpen });
    }

    private void Save(bool thenBack)
    {
        Pull();

        //a second save while one is in flight could make a second list, or let an older save land last
        if (AwaitingSave)
            return;

        AwaitingSave = true;
        BackAfterSave = thenBack;
        SentVersion = Document.Version;
        Status.Text = "Saving...";

        Raise(
            new CollegeActionArgs
            {
                Type = CollegeActionType.PartyListSave,
                PartyListId = Document.Id,
                PartyListDraft = Document.ToInfo()
            });
    }

    private void CopyPreviewToMine()
    {
        if (Document.Id.Length > 0)
            Raise(new CollegeActionArgs { Type = CollegeActionType.PartyBankCopy, PartyListId = Document.Id });
    }

    private void ShowTab(bool bank)
    {
        if (EditingPage || (BankTabShown == bank))
            return;

        BankTabShown = bank;
        AskingKind = false;
        DeleteArmed = string.Empty;
        SelectedRow = ShownRows.Count == 0 ? -1 : 0;
        Status.Text = string.Empty;
        RefreshList();

        if (bank)
            RequestBankPage(0);
    }

    private void PickKind(PartyListKind kind)
    {
        if (EditingPage || !BankTabShown || (BankKind == kind))
            return;

        BankKind = kind;
        BankRows = [];
        SelectedRow = -1;
        Status.Text = string.Empty;
        RefreshList();
        RequestBankPage(0);
    }

    private void RequestBankPage(int page)
    {
        if ((page < 0) || (page >= BankPageCount))
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.PartyBankPage, Page = (byte)page, PartyKind = BankKind });
    }

    private void NewList()
    {
        DeleteArmed = string.Empty;

        if (MineRows.Count >= CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER)
        {
            Status.Text = $"You can keep {CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER} lists.";

            return;
        }

        AskingKind = true;
        Status.Text = "New list: pick a kind.";
        RefreshList();
    }

    private void StartNew(PartyListKind kind)
    {
        AskingKind = false;
        DeleteArmed = string.Empty;

        if (MineRows.Count >= CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER)
        {
            Status.Text = $"You can keep {CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER} lists.";
            RefreshList();

            return;
        }

        Document.New(kind);
        AwaitingSave = false;
        BackAfterSave = false;
        RefusedVersion = -1;
        ScrollTop = 0;
        Status.Text = string.Empty;
        ShowPage(true);
        TitleBox.IsFocused = true;
    }

    private void CancelNew()
    {
        AskingKind = false;
        Status.Text = string.Empty;
        RefreshList();
    }

    private void OpenSelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.PartyListOpen, PartyListId = list.Id });
    }

    private void CopySelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.PartyListCopy, PartyListId = list.Id });
    }

    private void CopyBankSelected()
    {
        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.PartyBankCopy, PartyListId = list.Id });
    }

    private void DeleteSelected()
    {
        if (SelectedList is not { } list)
            return;

        if (DeleteArmed != list.Id)
        {
            DeleteArmed = list.Id;
            Status.Text = $"Delete {list.Title}? Click Delete again.";

            return;
        }

        DeleteArmed = string.Empty;
        Raise(new CollegeActionArgs { Type = CollegeActionType.PartyListDelete, PartyListId = list.Id });
    }

    private void SelectRow(int index)
    {
        if (index >= ShownRows.Count)
            return;

        if (index != SelectedRow)
            DeleteArmed = string.Empty;

        AskingKind = false;
        SelectedRow = index;
        RefreshList();
    }

    private void SelectEntry(int slot)
    {
        var index = ScrollTop + slot;

        if (index >= Document.Entries.Count)
            return;

        Pull();
        Document.Select(index);
        Push();
    }

    private void Edit(Func<bool> change)
    {
        Pull();
        change();
        KeepSelectedInView();
        Push();
    }

    /// <summary>Copies the boxes into the document, growing the decoy list when a later box is typed in first.</summary>
    private void Pull()
    {
        if (!EditingPage || IsPreview)
            return;

        Document.SetTitle(TitleBox.Text);
        Document.SetText(TextBox.Text);

        if ((Document.Kind != PartyListKind.Bluff) || (Document.Current is not { } entry))
            return;

        Document.SetAnswer(AnswerBox.Text);

        for (var i = 0; i < DecoyBoxes.Length; i++)
        {
            var text = DecoyBoxes[i].Text;

            if (i < entry.Decoys.Count)
                Document.SetDecoy(i, text);
            else if (text.Length > 0)
            {
                while (entry.Decoys.Count <= i)
                    Document.AddDecoy();

                Document.SetDecoy(i, text);
            }
        }

        for (var i = entry.Decoys.Count - 1; i >= 0; i--)
            if (entry.Decoys[i].Length == 0)
                Document.RemoveDecoy(i);
    }

    /// <summary>Copies the document's title and selected entry into the boxes.</summary>
    private void Push()
    {
        TitleBox.Text = Document.Title;
        TextBox.MaxLength = Document.MaxTextChars;
        var current = Document.Current;
        TextBox.Text = current?.Text ?? string.Empty;
        AnswerBox.Text = current?.Answer ?? string.Empty;

        for (var i = 0; i < DecoyBoxes.Length; i++)
            DecoyBoxes[i].Text = (current is not null) && (i < current.Decoys.Count) ? current.Decoys[i] : string.Empty;

        RefreshEdit();
    }

    private void ShowPage(bool edit)
    {
        EditingPage = edit;

        foreach (var element in ListPage)
            element.Visible = !edit;

        foreach (var element in EditPage)
            element.Visible = edit;

        if (edit)
            Push();
        else
            RefreshList();
    }

    private void KeepSelectedInView()
    {
        if (Document.Selected < ScrollTop)
            ScrollTop = Document.Selected;
        else if (Document.Selected >= ScrollTop + SIDE_ROWS)
            ScrollTop = Document.Selected - SIDE_ROWS + 1;

        ScrollTop = Math.Clamp(ScrollTop, 0, Math.Max(0, Document.Entries.Count - SIDE_ROWS));
    }

    private void RefreshList()
    {
        if (EditingPage)
            return;

        var rows = ShownRows;
        CaptionLabel.Text = BankTabShown ? "Party bank" : $"My party lists ({MineRows.Count}/{CollegeProtocol.MAX_PARTY_LISTS_PER_OWNER})";
        MineTab.Enabled = BankTabShown;
        BankTab.Enabled = !BankTabShown;
        MiddleHeader.Text = BankTabShown ? "Owner" : "Kind";

        var kinds = new[] { PartyListKind.Quip, PartyListKind.Bluff, PartyListKind.Sketch };

        for (var i = 0; i < BankKindButtons.Length; i++)
            BankKindButtons[i].Enabled = BankKind != kinds[i];

        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            row.Visible = i < rows.Count;

            if (!row.Visible)
                continue;

            var info = rows[i];
            var middle = BankTabShown ? info.Owner : KindName(info.Kind);

            row.Set(
                i + 1,
                (Fit(info.Title, KIND_X - 8), LegendColors.White),
                (Fit(middle, COUNT_X - KIND_X - 8), LegendColors.Gray),
                (info.Count == 1 ? "1 entry" : $"{info.Count} entries", LegendColors.Gray));

            row.Selected = i == SelectedRow;
        }

        EmptyLabel.Visible = rows.Count == 0;
        EmptyLabel.Text = BankTabShown ? "No shared lists yet." : "No party lists yet. Press New list to write one.";

        foreach (var element in MineElements)
            element.Visible = !BankTabShown && !AskingKind;

        foreach (var element in KindAskElements)
            element.Visible = !BankTabShown && AskingKind;

        foreach (var element in BankPageElements)
            element.Visible = BankTabShown;

        foreach (var element in BankKindElements)
            element.Visible = BankTabShown;

        var selected = SelectedList is not null;
        OpenButton.Enabled = selected;
        CopyButton.Enabled = selected;
        DeleteButton.Enabled = selected;
        PreviewButton.Enabled = selected;
        CopyBankButton.Enabled = selected;
        PrevButton.Enabled = BankPage > 0;
        NextButton.Enabled = BankPage < BankPageCount - 1;
        BankPageLabel.Text = $"{BankPage + 1}/{BankPageCount}";
    }

    private void RefreshEdit()
    {
        if (!EditingPage)
            return;

        ShownVersion = Document.Version;
        var readOnly = Document.IsReadOnly;
        var bluff = Document.Kind == PartyListKind.Bluff;

        CaptionLabel.Text = IsPreview ? $"Preview: {Document.Title}"
            : Document.Title.Length == 0 ? $"New {KindName(Document.Kind)} list" : $"{KindName(Document.Kind)} list: {Document.Title}";

        KeepSelectedInView();
        EntriesLabel.Text = $"Entries ({Document.Entries.Count}/{Document.MaxEntries})";

        for (var i = 0; i < EntryRows.Length; i++)
        {
            var row = EntryRows[i];
            var index = ScrollTop + i;
            row.Visible = index < Document.Entries.Count;

            if (!row.Visible)
                continue;

            row.Set(index + 1, (Fit(Document.Label(index), SIDE_WIDTH - 4), LegendColors.White));
            row.Selected = index == Document.Selected;
        }

        TextLabel.Text = bluff ? $"Question ({TextBox.Text.Length}/{Document.MaxTextChars})" : $"Prompt ({TextBox.Text.Length}/{Document.MaxTextChars})";
        TextBox.HintText = bluff ? "Question" : "Prompt";
        AnswerLabel.Text = $"Answer ({AnswerBox.Text.Length}/{CollegeProtocol.BLUFF_MAX_CHARS})";

        for (var i = 0; i < DecoyBoxes.Length; i++)
            DecoyLabels[i].Text = $"Decoy {i + 1} ({DecoyBoxes[i].Text.Length}/{CollegeProtocol.BLUFF_MAX_CHARS})";

        AnswerLabel.Visible = bluff;
        AnswerBox.Visible = bluff;

        for (var i = 0; i < DecoyBoxes.Length; i++)
        {
            DecoyLabels[i].Visible = bluff;
            DecoyBoxes[i].Visible = bluff;
        }

        TitleBox.IsReadOnly = readOnly;
        TextBox.IsReadOnly = readOnly;
        AnswerBox.IsReadOnly = readOnly;

        foreach (var box in DecoyBoxes)
            box.IsReadOnly = readOnly;

        BackButton.Visible = true;
        SaveButton.Visible = !IsPreview;
        CopyToMineButton.Visible = IsPreview;

        var buttonsTop = bluff ? EDIT_BUTTONS_BLUFF_TOP : EDIT_BUTTONS_SINGLE_TOP;
        AddEntryButton.Y = buttonsTop;
        UpEntryButton.Y = buttonsTop;
        DownEntryButton.Y = buttonsTop;
        RemoveEntryButton.Y = buttonsTop;
        RulesLabel.Y = bluff ? RULES_BLUFF_TOP : RULES_SINGLE_TOP;

        RulesLabel.Text = Document.Kind switch
        {
            PartyListKind.Quip => $"Prompts up to {CollegeProtocol.QUIP_PROMPT_MAX_CHARS} characters.",
            PartyListKind.Bluff => $"Questions up to {CollegeProtocol.BLUFF_QUESTION_MAX_CHARS}, answers and decoys up to {CollegeProtocol.BLUFF_MAX_CHARS}.",
            _ => $"Prompts up to {CollegeProtocol.SKETCH_PROMPT_MAX_CHARS} characters."
        };

        AddEntryButton.Enabled = !readOnly;
        UpEntryButton.Enabled = !readOnly;
        DownEntryButton.Enabled = !readOnly;
        RemoveEntryButton.Enabled = !readOnly;
    }

    private static string KindName(PartyListKind kind)
        => kind switch
        {
            PartyListKind.Quip => "Quip",
            PartyListKind.Bluff => "Bluff",
            _ => "Sketch"
        };

    private static string Fit(string text, int width)
    {
        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        while ((text.Length > 0) && (TextRenderer.MeasureWidth(text + "...") > width))
            text = text[..^1];

        return text + "...";
    }

    private void Raise(CollegeActionArgs args) => Send?.Invoke(args);
}

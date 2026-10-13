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
///     The word-list editor. Page 1 has two tabs: Mine (the Teacher's saved lists) and Word bank (shared lists to preview or
///     copy). Page 2 edits one list. It also serves as the Quick list window (no title, Start and Cancel) and as the
///     read-only preview of a bank list. The server's WordLists, WordBank, WordListEdit and WordListResult feed it; every
///     save goes to the server, which checks the limits again.
/// </summary>
public sealed class WordListEditorControl : GuildCloakDialogBase
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
    private const int OWNER_X = 250;
    private const int COUNT_X = 424;
    private const int SIDE_WIDTH = 170;
    private const int SIDE_ROWS = 18;
    private const int TITLE_TOP = 28;
    private const int WORDS_LABEL_TOP = 54;
    private const int WORD_ROWS_TOP = 70;
    private const int EDIT_X = 200;
    private const int EDIT_WIDTH = LEFT + INNER_WIDTH - EDIT_X;
    private const int WORD_TOP = 44;
    private const int HINT_LABEL_TOP = 80;
    private const int HINT_TOP = 96;
    private const int EDIT_BUTTONS_TOP = HINT_TOP + CustomButton.HEIGHT + 12;
    private const int RULES_TOP = EDIT_BUTTONS_TOP + CustomButton.HEIGHT + 12;

    private readonly UILabel BankPageLabel;
    private readonly List<UIElement> BankPageElements = [];
    private readonly CustomButton CancelButton;
    private readonly UILabel CaptionLabel;
    private readonly CustomButton CopyBankButton;
    private readonly CustomButton CopyButton;
    private readonly CustomButton CopyToMineButton;
    private readonly CustomButton DeleteButton;
    private readonly List<UIElement> EditPage = [];
    private readonly UILabel EmptyLabel;
    private readonly CustomTextBox HintBox;
    private readonly UILabel HintLabel;
    private readonly List<UIElement> ListPage = [];
    private readonly List<UIElement> MineElements = [];
    private readonly CustomButton MineTab;
    private readonly CustomButton NextButton;
    private readonly CustomButton OpenButton;
    private readonly UILabel OwnerHeader;
    private readonly CustomButton PreviewButton;
    private readonly CustomButton PrevButton;
    private readonly CollegeListRow[] Rows = new CollegeListRow[CollegeProtocol.MAX_WORD_LISTS_PER_OWNER];
    private readonly CustomButton SaveButton;
    private readonly CustomButton StartButton;
    private readonly UILabel Status;
    private readonly CustomButton BackButton;
    private readonly CustomButton BankTab;
    private readonly CustomTextBox TitleBox;
    private readonly CollegeListRow[] WordRows = new CollegeListRow[SIDE_ROWS];
    private readonly CustomTextBox WordBox;
    private readonly UILabel WordLabel;
    private readonly UILabel WordsLabel;

    private bool AwaitingSave;
    private bool BackAfterSave;
    private List<CollegeWordListRowInfo> BankRows = [];
    private int BankPage;
    private int BankPageCount = 1;
    private bool BankTabShown;
    private string DeleteArmed = string.Empty;
    private bool EditingPage;
    private List<CollegeWordListRowInfo> MineRows = [];
    private int RefusedVersion = -1;
    private int ScrollTop;
    private int SelectedRow = -1;
    private int SentVersion;
    private int ShownVersion = -1;

    public WordListDocument Document { get; } = new();

    public event Action<CollegeActionArgs>? ActionRequested;

    public WordListEditorControl()
        : base("_nsett", false)
    {
        Name = "WordListEditor";
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
        BankTab = AddButton("Word bank", 90, LEFT + 76, TABS_TOP, () => ShowTab(true));
        ListPage.Add(MineTab);
        ListPage.Add(BankTab);
        ListPage.Add(Caption("Title", LEFT, LIST_HEADER_TOP, 200, color: LegendColors.Gold));
        OwnerHeader = Caption("Owner", LEFT + OWNER_X, LIST_HEADER_TOP, 120, color: LegendColors.Gold);
        ListPage.Add(OwnerHeader);
        ListPage.Add(Caption("Words", LEFT + COUNT_X, LIST_HEADER_TOP, 100, color: LegendColors.Gold));

        for (var i = 0; i < Rows.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(INNER_WIDTH, [0, OWNER_X, COUNT_X]) { X = LEFT, Y = LIST_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
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

        WordsLabel = Caption(string.Empty, LEFT, WORDS_LABEL_TOP, SIDE_WIDTH, color: LegendColors.Gold);
        EditPage.Add(WordsLabel);

        for (var i = 0; i < WordRows.Length; i++)
        {
            var slot = i;
            var row = new CollegeListRow(SIDE_WIDTH, [0]) { X = LEFT, Y = WORD_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectWord(slot);
            AddChild(row);
            WordRows[i] = row;
            EditPage.Add(row);
        }

        WordLabel = Caption(string.Empty, EDIT_X, TITLE_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(WordLabel);

        WordBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = WORD_TOP,
            Width = EDIT_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_WORD_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Word"
        };

        AddChild(WordBox);
        EditPage.Add(WordBox);

        HintLabel = Caption(string.Empty, EDIT_X, HINT_LABEL_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(HintLabel);

        HintBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = HINT_TOP,
            Width = EDIT_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_HINT_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Hint (optional)"
        };

        AddChild(HintBox);
        EditPage.Add(HintBox);

        EditPage.Add(AddButton("Add", 50, EDIT_X, EDIT_BUTTONS_TOP, () => Edit(Document.Add)));
        EditPage.Add(AddButton("Up", 40, EDIT_X + 56, EDIT_BUTTONS_TOP, () => Edit(Document.MoveUp)));
        EditPage.Add(AddButton("Down", 50, EDIT_X + 102, EDIT_BUTTONS_TOP, () => Edit(Document.MoveDown)));
        EditPage.Add(AddButton("Remove", 70, EDIT_X + 158, EDIT_BUTTONS_TOP, () => Edit(Document.Remove)));

        EditPage.Add(
            Caption(
                $"Letters and spaces, {CollegeProtocol.MIN_WORD_CHARS}-{CollegeProtocol.MAX_WORD_CHARS} characters.",
                EDIT_X,
                RULES_TOP,
                EDIT_WIDTH,
                color: LegendColors.Gray));

        BackButton = AddButton("Back", 50, LEFT, BUTTONS_TOP, OnBackClicked);
        SaveButton = AddButton("Save", 50, LEFT + 56, BUTTONS_TOP, () => Save(false));
        CopyToMineButton = AddButton("Copy to mine", 100, LEFT + 56, BUTTONS_TOP, CopyPreviewToMine);
        StartButton = AddButton("Start", 60, LEFT, BUTTONS_TOP, StartQuick);
        CancelButton = AddButton("Cancel", 60, LEFT + 66, BUTTONS_TOP, CancelQuick);
        EditPage.Add(BackButton);
        EditPage.Add(SaveButton);
        EditPage.Add(CopyToMineButton);
        EditPage.Add(StartButton);
        EditPage.Add(CancelButton);
    }

    private bool IsQuick => Document.Mode == WordListEditMode.Quick;

    private bool IsPreview => Document.Mode == WordListEditMode.Preview;

    private IReadOnlyList<CollegeWordListRowInfo> ShownRows => BankTabShown ? BankRows : MineRows;

    private CollegeWordListRowInfo? SelectedList
        => (SelectedRow >= 0) && (SelectedRow < ShownRows.Count) ? ShownRows[SelectedRow] : null;

    /// <summary>Shows the Teacher's saved lists on the Mine tab. A save still waiting on page 2 goes out first.</summary>
    public void ShowList(IReadOnlyList<CollegeWordListRowInfo> rows)
    {
        MineRows = [..rows];
        DeleteArmed = string.Empty;

        //the server sends the list after every save; a visible page 2 stays put, and Back or a pending save-then-back leaves it itself
        if (EditingPage && Visible)
            return;

        //unsaved edits of a closed editor: the save goes out first and its result returns to the list, so nothing is dropped
        if (EditingPage && !IsQuick && !IsPreview)
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
        SelectedRow = MineRows.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, MineRows.Count - 1);
        ShowPage(false);

        if (!Visible)
        {
            Status.Text = string.Empty;
            Show();
        }
    }

    /// <summary>Fills the Word bank tab with one page of shared lists.</summary>
    public void ShowBank(byte page, byte pageCount, IReadOnlyList<CollegeWordListRowInfo> rows)
    {
        BankRows = [..rows];
        BankPage = page;
        BankPageCount = Math.Max(1, (int)pageCount);

        if (BankTabShown)
            SelectedRow = BankRows.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, BankRows.Count - 1);

        if (!EditingPage)
            RefreshList();
    }

    /// <summary>Opens page 2 on a list: a saved one, a new one (no words), a read-only preview or the Quick list.</summary>
    public void ShowEdit(string id, WordListEditMode mode, CollegeWordListDraftInfo? draft, string message)
    {
        if (draft is null)
            return;

        Document.Load(id, draft, mode);
        AwaitingSave = false;
        BackAfterSave = false;
        RefusedVersion = -1;
        DeleteArmed = string.Empty;
        ScrollTop = 0;
        Status.Text = message;
        ShowPage(true);

        if (!Visible)
            Show();

        if (IsQuick)
            WordBox.IsFocused = true;
    }

    /// <summary>Answers a WordListResult. True when a refused save showed the hidden editor again, so the caller brings it to the front.</summary>
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
        if (!EditingPage || IsQuick || IsPreview)
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
        if (!EditingPage || (Document.Words.Count <= SIDE_ROWS))
            return;

        ScrollTop = Math.Clamp(ScrollTop - Math.Sign(e.Delta), 0, Document.Words.Count - SIDE_ROWS);
        RefreshEdit();
        e.Handled = true;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        WordBox.IsFocused = false;
        HintBox.IsFocused = false;
        base.Hide();
    }

    private void OnCloseClicked()
    {
        if (EditingPage && IsQuick)
        {
            CancelQuick();

            return;
        }

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
        ShowPage(false);

        //asks for the list again, so a saved title or word count shows
        Raise(new CollegeActionArgs { Type = CollegeActionType.WordListOpen });
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
                Type = CollegeActionType.WordListSave,
                WordListId = Document.Id,
                WordListDraft = Document.ToInfo()
            });
    }

    private void StartQuick()
    {
        Pull();

        Raise(new CollegeActionArgs { Type = CollegeActionType.QuickListStart, WordListDraft = Document.ToInfo() });
    }

    private void CancelQuick()
    {
        Raise(new CollegeActionArgs { Type = CollegeActionType.WordGameCancel });
        Hide();
    }

    private void CopyPreviewToMine()
    {
        if (Document.Id.Length > 0)
            Raise(new CollegeActionArgs { Type = CollegeActionType.WordBankCopy, WordListId = Document.Id });
    }

    private void ShowTab(bool bank)
    {
        if (EditingPage || (BankTabShown == bank))
            return;

        BankTabShown = bank;
        DeleteArmed = string.Empty;
        SelectedRow = ShownRows.Count == 0 ? -1 : 0;
        Status.Text = string.Empty;
        RefreshList();

        if (bank)
            RequestBankPage(0);
    }

    private void RequestBankPage(int page)
    {
        if ((page < 0) || (page >= BankPageCount))
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.WordBankPage, Page = (byte)page });
    }

    private void NewList()
    {
        DeleteArmed = string.Empty;

        if (MineRows.Count >= CollegeProtocol.MAX_WORD_LISTS_PER_OWNER)
        {
            Status.Text = $"You can keep {CollegeProtocol.MAX_WORD_LISTS_PER_OWNER} lists.";

            return;
        }

        Document.New(WordListEditMode.Saved);
        AwaitingSave = false;
        BackAfterSave = false;
        RefusedVersion = -1;
        ScrollTop = 0;
        Status.Text = string.Empty;
        ShowPage(true);
        TitleBox.IsFocused = true;
    }

    private void OpenSelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.WordListOpen, WordListId = list.Id });
    }

    private void CopySelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.WordListCopy, WordListId = list.Id });
    }

    private void CopyBankSelected()
    {
        if (SelectedList is { } list)
            Raise(new CollegeActionArgs { Type = CollegeActionType.WordBankCopy, WordListId = list.Id });
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
        Raise(new CollegeActionArgs { Type = CollegeActionType.WordListDelete, WordListId = list.Id });
    }

    private void SelectRow(int index)
    {
        if (index >= ShownRows.Count)
            return;

        if (index != SelectedRow)
            DeleteArmed = string.Empty;

        SelectedRow = index;
        RefreshList();
    }

    private void SelectWord(int slot)
    {
        var index = ScrollTop + slot;

        if (index >= Document.Words.Count)
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

    /// <summary>Copies the boxes into the document. The word box takes back the filtered text when the document changed it.</summary>
    private void Pull()
    {
        if (!EditingPage || IsPreview)
            return;

        if (!IsQuick)
            Document.SetTitle(TitleBox.Text);

        Document.SetText(WordBox.Text);
        Document.SetHint(HintBox.Text);

        var filtered = Document.Current?.Text ?? string.Empty;

        if (WordBox.Text != filtered)
            WordBox.Text = filtered;
    }

    /// <summary>Copies the document's title and selected word into the boxes.</summary>
    private void Push()
    {
        TitleBox.Text = Document.Title;
        var current = Document.Current;
        WordBox.Text = current?.Text ?? string.Empty;
        HintBox.Text = current?.Hint ?? string.Empty;
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

        ScrollTop = Math.Clamp(ScrollTop, 0, Math.Max(0, Document.Words.Count - SIDE_ROWS));
    }

    private void RefreshList()
    {
        if (EditingPage)
            return;

        var rows = ShownRows;
        CaptionLabel.Text = BankTabShown ? "Word bank" : $"My word lists ({MineRows.Count}/{CollegeProtocol.MAX_WORD_LISTS_PER_OWNER})";
        MineTab.Enabled = BankTabShown;
        BankTab.Enabled = !BankTabShown;
        OwnerHeader.Visible = BankTabShown;

        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            row.Visible = i < rows.Count;

            if (!row.Visible)
                continue;

            var info = rows[i];
            var owner = BankTabShown ? Fit(info.Owner, COUNT_X - OWNER_X - 8) : string.Empty;

            row.Set(
                i + 1,
                (Fit(info.Title, (BankTabShown ? OWNER_X : COUNT_X) - 8), LegendColors.White),
                (owner, LegendColors.Gray),
                (info.Count == 1 ? "1 word" : $"{info.Count} words", LegendColors.Gray));

            row.Selected = i == SelectedRow;
        }

        EmptyLabel.Visible = rows.Count == 0;
        EmptyLabel.Text = BankTabShown ? "No shared lists yet." : "No word lists yet. Press New list to write one.";

        foreach (var element in MineElements)
            element.Visible = !BankTabShown;

        foreach (var element in BankPageElements)
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

        CaptionLabel.Text = IsQuick ? "Quick list"
            : IsPreview ? $"Preview: {Document.Title}"
            : Document.Title.Length == 0 ? "New word list" : $"Word list: {Document.Title}";

        KeepSelectedInView();
        WordsLabel.Text = $"Words ({Document.Words.Count}/{Document.MaxWords})";

        for (var i = 0; i < WordRows.Length; i++)
        {
            var row = WordRows[i];
            var index = ScrollTop + i;
            row.Visible = index < Document.Words.Count;

            if (!row.Visible)
                continue;

            row.Set(index + 1, (Fit(Document.Label(index), SIDE_WIDTH - 4), LegendColors.White));
            row.Selected = index == Document.Selected;
        }

        WordLabel.Text = $"Word {Document.Selected + 1} ({WordBox.Text.Length}/{CollegeProtocol.MAX_WORD_CHARS})";
        HintLabel.Text = $"Hint ({HintBox.Text.Length}/{CollegeProtocol.MAX_HINT_CHARS})";

        TitleBox.Visible = !IsQuick;
        TitleBox.IsReadOnly = readOnly;
        WordBox.IsReadOnly = readOnly;
        HintBox.IsReadOnly = readOnly;

        BackButton.Visible = !IsQuick;
        SaveButton.Visible = !IsQuick && !IsPreview;
        CopyToMineButton.Visible = IsPreview;
        StartButton.Visible = IsQuick;
        CancelButton.Visible = IsQuick;

        foreach (var element in EditPage)
            if (element is CustomButton { Caption: "Add" or "Up" or "Down" or "Remove" } edit)
                edit.Enabled = !readOnly;
    }

    private static string Fit(string text, int width)
    {
        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        while ((text.Length > 0) && (TextRenderer.MeasureWidth(text + "...") > width))
            text = text[..^1];

        return text + "...";
    }

    private void Raise(CollegeActionArgs args) => ActionRequested?.Invoke(args);
}

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
///     The quiz editor. Page 1 lists the Teacher's saved quizzes; page 2 edits one. The server's QuizList and QuizEdit open
///     it; every save goes to the server, which checks the limits again.
/// </summary>
public sealed class QuizEditorControl : GuildCloakDialogBase
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
    private const int LIST_HEADER_TOP = 30;
    private const int LIST_ROWS_TOP = 46;
    private const int COUNT_X = 424;
    private const int SIDE_WIDTH = 170;
    private const int TITLE_TOP = 28;
    private const int QUESTIONS_LABEL_TOP = 54;
    private const int QUESTION_ROWS_TOP = 70;
    private const int EDIT_X = 200;
    private const int EDIT_WIDTH = LEFT + INNER_WIDTH - EDIT_X;
    private const int QUESTION_TOP = 44;
    private const int QUESTION_HEIGHT = 50;
    private const int ANSWERS_LABEL_TOP = 100;
    private const int ANSWERS_TOP = 116;
    private const int ANSWER_STEP = 28;
    private const int ANSWER_BOX_X = EDIT_X + 24;
    private const int EDIT_BUTTONS_TOP = ANSWERS_TOP + (CollegeProtocol.MAX_ANSWERS * ANSWER_STEP) + 4;

    private readonly CustomTextBox[] AnswerBoxes = new CustomTextBox[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel CaptionLabel;
    private readonly CustomButton CopyButton;
    private readonly CustomButton DeleteButton;
    private readonly List<UIElement> EditPage = [];
    private readonly UILabel EmptyLabel;
    private readonly List<UIElement> ListPage = [];
    private readonly CustomButton OpenButton;
    private readonly CustomTextBox QuestionBox;
    private readonly UILabel QuestionLabel;
    private readonly CollegeListRow[] QuestionRows = new CollegeListRow[CollegeProtocol.MAX_QUIZ_QUESTIONS];
    private readonly UILabel QuestionsLabel;
    private readonly CollegeListRow[] QuizRowsShown = new CollegeListRow[CollegeProtocol.MAX_QUIZZES_PER_OWNER];
    private readonly CustomCheckBox[] RightBoxes = new CustomCheckBox[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel Status;
    private readonly CustomTextBox TitleBox;

    private bool AwaitingSave;
    private bool BackAfterSave;
    private string DeleteArmed = string.Empty;
    private bool EditingPage;
    private List<CollegeQuizRowInfo> Quizzes = [];
    private int RefusedVersion = -1;
    private int SelectedRow = -1;
    private int SentVersion;
    private int ShownVersion = -1;

    public QuizDocument Document { get; } = new();

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizEditorControl()
        : base("_nsett", false)
    {
        Name = "QuizEditor";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - STATUS_X);

        //page 1: the list
        ListPage.Add(Caption("Title", LEFT, LIST_HEADER_TOP, 200, color: LegendColors.Gold));
        ListPage.Add(Caption("Questions", LEFT + COUNT_X, LIST_HEADER_TOP, 100, color: LegendColors.Gold));

        for (var i = 0; i < QuizRowsShown.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(INNER_WIDTH, [0, COUNT_X]) { X = LEFT, Y = LIST_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectRow(index);

            row.DoubleClicked += () =>
            {
                SelectRow(index);
                OpenSelected();
            };

            AddChild(row);
            QuizRowsShown[i] = row;
            ListPage.Add(row);
        }

        EmptyLabel = Caption("No quizzes yet. Press New quiz to write one.", LEFT, LIST_ROWS_TOP, INNER_WIDTH, color: LegendColors.Gray);
        ListPage.Add(EmptyLabel);
        OpenButton = AddButton("Open", 60, LEFT, BUTTONS_TOP, OpenSelected);
        ListPage.Add(OpenButton);
        ListPage.Add(AddButton("New quiz", 80, LEFT + 66, BUTTONS_TOP, NewQuiz));
        CopyButton = AddButton("Copy", 50, LEFT + 152, BUTTONS_TOP, CopySelected);
        ListPage.Add(CopyButton);
        DeleteButton = AddButton("Delete", 60, LEFT + 208, BUTTONS_TOP, DeleteSelected);
        ListPage.Add(DeleteButton);

        //page 2: one quiz
        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = SIDE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_QUIZ_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Quiz title"
        };

        AddChild(TitleBox);
        EditPage.Add(TitleBox);

        QuestionsLabel = Caption(string.Empty, LEFT, QUESTIONS_LABEL_TOP, SIDE_WIDTH, color: LegendColors.Gold);
        EditPage.Add(QuestionsLabel);

        for (var i = 0; i < QuestionRows.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(SIDE_WIDTH, [0]) { X = LEFT, Y = QUESTION_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectQuestion(index);
            AddChild(row);
            QuestionRows[i] = row;
            EditPage.Add(row);
        }

        QuestionLabel = Caption(string.Empty, EDIT_X, TITLE_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(QuestionLabel);

        QuestionBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = QUESTION_TOP,
            Width = EDIT_WIDTH,
            Height = QUESTION_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_QUESTION_CHARS,
            HintText = "Question"
        };

        AddChild(QuestionBox);
        EditPage.Add(QuestionBox);
        EditPage.Add(Caption("Answers (tick the right one)", EDIT_X, ANSWERS_LABEL_TOP, EDIT_WIDTH, color: LegendColors.Gold));

        for (var i = 0; i < CollegeProtocol.MAX_ANSWERS; i++)
        {
            var slot = i;
            var y = ANSWERS_TOP + (i * ANSWER_STEP);

            var right = new CustomCheckBox
            {
                X = EDIT_X,
                Y = y + 2,
                Width = CustomCheckBox.CHECKBOX_SIZE,
                Height = CustomCheckBox.CHECKBOX_SIZE
            };

            right.Clicked += () => MarkRight(slot);
            AddChild(right);
            RightBoxes[i] = right;
            EditPage.Add(right);

            var box = new CustomTextBox
            {
                X = ANSWER_BOX_X,
                Y = y,
                Width = LEFT + INNER_WIDTH - ANSWER_BOX_X,
                Height = CustomButton.HEIGHT,
                MaxLength = CollegeProtocol.MAX_ANSWER_CHARS,
                IsSelectable = true,
                IsTabStop = true,
                HintText = $"Answer {(char)('A' + i)}"
            };

            AddChild(box);
            AnswerBoxes[i] = box;
            EditPage.Add(box);
        }

        EditPage.Add(AddButton("Add question", 100, EDIT_X, EDIT_BUTTONS_TOP, () => Edit(Document.Add)));
        EditPage.Add(AddButton("Up", 40, EDIT_X + 106, EDIT_BUTTONS_TOP, () => Edit(Document.MoveUp)));
        EditPage.Add(AddButton("Down", 50, EDIT_X + 152, EDIT_BUTTONS_TOP, () => Edit(Document.MoveDown)));
        EditPage.Add(AddButton("Remove question", 120, EDIT_X + 208, EDIT_BUTTONS_TOP, () => Edit(Document.Remove)));
        EditPage.Add(AddButton("Back", 50, LEFT, BUTTONS_TOP, OnBackClicked));
        EditPage.Add(AddButton("Save", 50, LEFT + 56, BUTTONS_TOP, () => Save(false)));
    }

    private CollegeQuizRowInfo? SelectedQuiz => (SelectedRow >= 0) && (SelectedRow < Quizzes.Count) ? Quizzes[SelectedRow] : null;

    public void OpenList(CollegeDisplayArgs args)
    {
        Quizzes = args.QuizRows;
        SelectedRow = Quizzes.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, Quizzes.Count - 1);
        DeleteArmed = string.Empty;

        //unsaved edits stay on page 2: the save goes out first and its result returns to the list, so nothing is dropped
        if (EditingPage)
        {
            Pull();

            if (Document.IsDirty)
            {
                if (Document.Version != RefusedVersion)
                    Save(true);

                if (!Visible)
                    Show();

                return;
            }
        }

        ShowPage(false);

        if (!Visible)
        {
            Status.Text = string.Empty;
            Show();
        }
    }

    public void OpenQuiz(CollegeDisplayArgs args)
    {
        if (args.QuizDraft is null)
            return;

        Document.Load(args.QuizId, args.QuizDraft);
        AwaitingSave = false;
        BackAfterSave = false;
        Status.Text = string.Empty;
        ShowPage(true);

        if (!Visible)
            Show();
    }

    /// <summary>Answers a QuizResult. True when a refused save showed the hidden editor again, so the caller brings it to the front.</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        Status.Text = args.Message;

        //Copy and Delete results only need the status line; the list that follows refreshes page 1
        if (!AwaitingSave)
            return false;

        AwaitingSave = false;

        if (args.QuizResult == QuizResultCode.Saved)
        {
            if (Document.Id.Length == 0)
                Document.SetId(args.QuizId);

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

    /// <summary>Logging out takes the editor away without its Close, so a dirty quiz is saved here.</summary>
    public void SaveIfDirty()
    {
        if (!EditingPage)
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

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        QuestionBox.IsFocused = false;

        foreach (var box in AnswerBoxes)
            box.IsFocused = false;

        base.Hide();
    }

    private void OnCloseClicked()
    {
        if (EditingPage)
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
        Pull();

        if (Document.IsDirty)
            Save(true);
        else
            GoToList();
    }

    private void GoToList()
    {
        ShowPage(false);

        //asks for the list again, so a saved title or question count shows
        Raise(new CollegeActionArgs { Type = CollegeActionType.QuizOpen });
    }

    private void Save(bool thenBack)
    {
        Pull();

        //a second save while one is in flight could make a second quiz, or let an older save land last
        if (AwaitingSave)
            return;

        AwaitingSave = true;
        BackAfterSave = thenBack;
        SentVersion = Document.Version;
        Status.Text = "Saving...";

        Raise(
            new CollegeActionArgs
            {
                Type = CollegeActionType.QuizSave,
                QuizId = Document.Id,
                QuizDraft = Document.ToInfo()
            });
    }

    private void NewQuiz()
    {
        DeleteArmed = string.Empty;

        if (Quizzes.Count >= CollegeProtocol.MAX_QUIZZES_PER_OWNER)
        {
            Status.Text = "You can keep 20 quizzes.";

            return;
        }

        Document.New();
        AwaitingSave = false;
        BackAfterSave = false;
        Status.Text = string.Empty;
        ShowPage(true);
        TitleBox.IsFocused = true;
    }

    private void OpenSelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedQuiz is { } quiz)
            Raise(new CollegeActionArgs { Type = CollegeActionType.QuizOpen, QuizId = quiz.Id });
    }

    private void CopySelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedQuiz is { } quiz)
            Raise(new CollegeActionArgs { Type = CollegeActionType.QuizCopy, QuizId = quiz.Id });
    }

    private void DeleteSelected()
    {
        if (SelectedQuiz is not { } quiz)
            return;

        if (DeleteArmed != quiz.Id)
        {
            DeleteArmed = quiz.Id;
            Status.Text = $"Delete {quiz.Title}? Click Delete again.";

            return;
        }

        DeleteArmed = string.Empty;
        Raise(new CollegeActionArgs { Type = CollegeActionType.QuizDelete, QuizId = quiz.Id });
    }

    private void SelectRow(int index)
    {
        if (index >= Quizzes.Count)
            return;

        if (index != SelectedRow)
            DeleteArmed = string.Empty;

        SelectedRow = index;
        RefreshList();
    }

    private void SelectQuestion(int index)
    {
        if (index >= Document.Questions.Count)
            return;

        Pull();
        Document.Select(index);
        Push();
    }

    private void MarkRight(int slot)
    {
        Pull();
        Document.MarkRight(slot);
        RefreshEdit();
    }

    private void Edit(Func<bool> change)
    {
        Pull();
        change();
        Push();
    }

    /// <summary>Copies the boxes into the document.</summary>
    private void Pull()
    {
        if (!EditingPage)
            return;

        Document.SetTitle(TitleBox.Text);
        Document.SetText(QuestionBox.Text);

        for (var i = 0; i < AnswerBoxes.Length; i++)
            Document.SetAnswer(i, AnswerBoxes[i].Text);
    }

    /// <summary>Copies the document's title and selected question into the boxes.</summary>
    private void Push()
    {
        TitleBox.Text = Document.Title;
        var current = Document.Current;
        QuestionBox.Text = current?.Text ?? string.Empty;

        for (var i = 0; i < AnswerBoxes.Length; i++)
            AnswerBoxes[i].Text = current?.Answers[i] ?? string.Empty;

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

    private void RefreshList()
    {
        CaptionLabel.Text = $"My quizzes ({Quizzes.Count}/{CollegeProtocol.MAX_QUIZZES_PER_OWNER})";

        for (var i = 0; i < QuizRowsShown.Length; i++)
        {
            var row = QuizRowsShown[i];
            row.Visible = !EditingPage && (i < Quizzes.Count);

            if (!row.Visible)
                continue;

            row.Set(i + 1, (Fit(Quizzes[i].Title, COUNT_X - 8), LegendColors.White), ($"{Quizzes[i].Questions} questions", LegendColors.Gray));
            row.Selected = i == SelectedRow;
        }

        EmptyLabel.Visible = !EditingPage && (Quizzes.Count == 0);
        var selected = SelectedQuiz is not null;
        OpenButton.Enabled = selected;
        CopyButton.Enabled = selected;
        DeleteButton.Enabled = selected;
    }

    private void RefreshEdit()
    {
        ShownVersion = Document.Version;
        CaptionLabel.Text = Document.Title.Length == 0 ? "New quiz" : $"Quiz: {Document.Title}";
        QuestionsLabel.Text = $"Questions ({Document.Questions.Count}/{CollegeProtocol.MAX_QUIZ_QUESTIONS})";

        for (var i = 0; i < QuestionRows.Length; i++)
        {
            var row = QuestionRows[i];
            row.Visible = EditingPage && (i < Document.Questions.Count);

            if (!row.Visible)
                continue;

            row.Set(i + 1, (Fit(Document.Label(i), SIDE_WIDTH - 4), LegendColors.White));
            row.Selected = i == Document.Selected;
        }

        QuestionLabel.Text = $"Question {Document.Selected + 1} ({QuestionBox.Text.Length}/{CollegeProtocol.MAX_QUESTION_CHARS})";
        var right = Document.Current?.Right ?? -1;

        for (var i = 0; i < RightBoxes.Length; i++)
            RightBoxes[i].Checked = i == right;
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

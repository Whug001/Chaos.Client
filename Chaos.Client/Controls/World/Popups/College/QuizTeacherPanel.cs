using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The class Teacher's quiz panel at the top right of the view: the question and its answer, how many have answered,
///     and the buttons that run the quiz. It never takes keyboard focus, so the Teacher can keep moving and talking.
/// </summary>
public sealed class QuizTeacherPanel : GuildCloakDialogBase
{
    private const int WIDTH = 250;
    private const int HEIGHT = 200;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int QUESTION_TOP = 28;
    private const int QUESTION_LINES = 2;
    private const int RIGHT_TOP = QUESTION_TOP + (QUESTION_LINES * LINE_HEIGHT) + 2;
    private const int STATUS_TOP = RIGHT_TOP + LINE_HEIGHT;
    private const int TOP_TOP = STATUS_TOP + LINE_HEIGHT;
    private const int ARMED_TOP = TOP_TOP + LINE_HEIGHT;
    private const int BUTTONS_TOP = HEIGHT - BORDER_BOTTOM_HEIGHT - 4 - CustomButton.HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string END_ARMED = "Click End quiz again to end it.";

    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton EndButton;
    private readonly CustomButton NextButton;
    private readonly UILabel[] QuestionLines = new UILabel[QUESTION_LINES];
    private readonly UILabel RightLabel;
    private readonly UILabel StatusLabel;
    private readonly UILabel TopLabel;
    private bool Dismissed;
    private bool EndArmed;
    private CollegeQuizInfo? Quiz;

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizTeacherPanel()
        : base("_nsett", false)
    {
        Name = "QuizTeacherPanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i] = Caption(string.Empty, LEFT, QUESTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        RightLabel = Caption(string.Empty, LEFT, RIGHT_TOP, INNER_WIDTH, color: LegendColors.Lime);
        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH, color: LegendColors.Gray);
        TopLabel = Caption(string.Empty, LEFT, TOP_TOP, INNER_WIDTH);
        ArmedLabel = Caption(string.Empty, LEFT, ARMED_TOP, INNER_WIDTH, color: LegendColors.Gold);
        NextButton = AddButton("Next question", 110, LEFT, BUTTONS_TOP, OnNext);
        EndButton = AddButton("End quiz", 80, LEFT + 116, BUTTONS_TOP, OnEnd);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A QuizTeacher message. <paramref name="reopen" /> shows a panel the Teacher closed.</summary>
    public void Apply(CollegeQuizInfo quiz, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (quiz.Phase == QuizPhase.Ended)
        {
            Close();

            return;
        }

        //a QuizTeacher comes with every answer, so only a new phase or question takes back a first End quiz click
        if (Quiz is not { } shown || (shown.Phase != quiz.Phase) || (shown.Number != quiz.Number))
            EndArmed = false;

        Quiz = quiz;
        Countdown.Start(quiz.SecondsLeft);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Quiz = null;
        Dismissed = false;
        EndArmed = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Quiz is { Phase: QuizPhase.Open } quiz)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            StatusLabel.Text = ClassToolText.AnsweredLine(quiz.AnsweredCount, quiz.Students, Countdown.Left);
        }

        base.Update(gameTime);
    }

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private bool IsLastRevealed(CollegeQuizInfo quiz) => (quiz.Phase == QuizPhase.Revealed) && (quiz.Number >= quiz.Count);

    private void OnNext()
    {
        if (Quiz is not { } quiz || (quiz.Phase == QuizPhase.Open))
            return;

        Raise(IsLastRevealed(quiz) ? QuizControlType.End : QuizControlType.Next);
    }

    private void OnEnd()
    {
        if (!EndArmed)
        {
            EndArmed = true;
            ArmedLabel.Text = END_ARMED;

            return;
        }

        EndArmed = false;
        ArmedLabel.Text = string.Empty;
        Raise(QuizControlType.End);
    }

    private void Raise(QuizControlType control)
        => ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.QuizControl, QuizControl = control });

    private void Refresh()
    {
        if (Quiz is not { } quiz)
            return;

        var waiting = quiz.Phase == QuizPhase.Waiting;
        CaptionLabel.Text = $"{quiz.Title} {quiz.Number}/{quiz.Count}";
        List<string> lines = waiting ? ["Ask the first question when you are ready."] : TextRenderer.WrapLines(quiz.Question, INNER_WIDTH);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == QUESTION_LINES - 1) && (lines.Count > QUESTION_LINES) ? lines[i] + "..."
                : lines[i];

        RightLabel.Text = !waiting && (quiz.Right < quiz.Answers.Count) ? $"{(char)('A' + quiz.Right)}. {quiz.Answers[quiz.Right]}" : string.Empty;

        StatusLabel.Text = quiz.Phase switch
        {
            QuizPhase.Open     => ClassToolText.AnsweredLine(quiz.AnsweredCount, quiz.Students, Countdown.Left),
            QuizPhase.Revealed => ClassToolText.RightLine(quiz.RightCount, quiz.AnsweredCount),
            _                  => string.Empty
        };

        TopLabel.Text = quiz.Phase == QuizPhase.Revealed ? ClassToolText.TopLine(quiz.Top, string.Empty) : string.Empty;
        ArmedLabel.Text = EndArmed ? END_ARMED : string.Empty;
        NextButton.Caption = waiting ? "First question" : IsLastRevealed(quiz) ? "Finish quiz" : "Next question";
        NextButton.Enabled = quiz.Phase != QuizPhase.Open;
    }
}

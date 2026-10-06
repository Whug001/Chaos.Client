using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The student's quiz card in the middle of the view: the question, a timer bar and up to four answers. After the
///     reveal it outlines the right answer and the student's pick and shows the class's top scores. OK or Esc hides it
///     until the next question.
/// </summary>
public sealed class QuizCardControl : GuildCloakDialogBase
{
    private const int WIDTH = 370;
    private const int HEIGHT = 300;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int QUESTION_TOP = 28;
    private const int QUESTION_LINES = 3;
    private const int BAR_TOP = QUESTION_TOP + (QUESTION_LINES * LINE_HEIGHT) + 4;
    private const int BAR_HEIGHT = 4;
    private const int TIMER_TOP = BAR_TOP + 8;
    private const int ANSWERS_TOP = TIMER_TOP + LINE_HEIGHT + 4;
    private const int ANSWER_STEP = CustomButton.HEIGHT + 4;
    private const int RESULT_TOP = ANSWERS_TOP + (CollegeProtocol.MAX_ANSWERS * ANSWER_STEP) + 2;
    private const int RESULT_LINES = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton[] AnswerButtons = new CustomButton[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel[] QuestionLines = new UILabel[QUESTION_LINES];
    private readonly UILabel[] ResultLines = new UILabel[RESULT_LINES];
    private readonly UILabel TimerLabel;
    private int DismissedNumber;
    private CollegeQuizInfo? Quiz;
    private string Viewer = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizCardControl()
        : base("_nsett", false)
    {
        Name = "QuizCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i] = Caption(string.Empty, LEFT, QUESTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        TimerLabel = Caption(string.Empty, LEFT, TIMER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < AnswerButtons.Length; i++)
        {
            var slot = i;
            AnswerButtons[i] = AddButton(string.Empty, INNER_WIDTH, LEFT, ANSWERS_TOP + (i * ANSWER_STEP), () => Pick(slot));
        }

        for (var i = 0; i < RESULT_LINES; i++)
            ResultLines[i] = Caption(string.Empty, LEFT, RESULT_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);
    }

    /// <summary>A QuizCard message. <paramref name="reopen" /> shows a card the student closed.</summary>
    public void Apply(CollegeQuizInfo quiz, string viewer, bool reopen)
    {
        Quiz = quiz;
        Viewer = viewer;
        Countdown.Start(quiz.SecondsLeft);

        if (reopen)
            DismissedNumber = 0;

        if (quiz.Phase is QuizPhase.Waiting or QuizPhase.Ended)
        {
            Hide();

            return;
        }

        Refresh();

        if (!Visible && (DismissedNumber != quiz.Number))
            Show();
    }

    public void Close()
    {
        Quiz = null;
        DismissedNumber = 0;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Quiz is { Phase: QuizPhase.Open })
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            RefreshTimer();
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Dismiss();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || Quiz is not { } quiz)
            return;

        var bar = new Rectangle(ScreenX + LEFT, ScreenY + BAR_TOP, INNER_WIDTH, BAR_HEIGHT);
        DrawRect(spriteBatch, bar, LegendColors.DimGray);

        if (quiz is { Phase: QuizPhase.Open, Seconds: > 0 })
            DrawRect(spriteBatch, bar with { Width = INNER_WIDTH * Countdown.Left / quiz.Seconds }, LegendColors.Gold);

        if (quiz.Phase != QuizPhase.Revealed)
            return;

        Outline(spriteBatch, quiz.Right, LegendColors.Lime);

        if (quiz.MyAnswer != quiz.Right)
            Outline(spriteBatch, quiz.MyAnswer, LegendColors.Red);
    }

    private void Dismiss()
    {
        if (Quiz is { } quiz)
            DismissedNumber = quiz.Number;

        Hide();
    }

    private void Pick(int slot)
    {
        if (Quiz is not { Phase: QuizPhase.Open } quiz || (Countdown.Left == 0))
            return;

        quiz.MyAnswer = (byte)slot;
        Refresh();

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.QuizAnswer,
                Number = quiz.Number,
                Answer = (byte)slot
            });
    }

    private void Refresh()
    {
        if (Quiz is not { } quiz)
            return;

        CaptionLabel.Text = ClassToolText.QuizCaption(quiz);
        var lines = TextRenderer.WrapLines(quiz.Question, INNER_WIDTH);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == QUESTION_LINES - 1) && (lines.Count > QUESTION_LINES) ? lines[i] + "..."
                : lines[i];

        for (var i = 0; i < AnswerButtons.Length; i++)
        {
            var button = AnswerButtons[i];
            button.Visible = i < quiz.Answers.Count;

            if (!button.Visible)
                continue;

            button.Caption = $"{(char)('A' + i)}. {quiz.Answers[i]}";
            button.Selected = quiz.MyAnswer == i;
        }

        var revealed = quiz.Phase == QuizPhase.Revealed;
        ResultLines[0].Text = revealed ? ClassToolText.MyResult(quiz.MyAnswer, quiz.Right, quiz.MyScore) : string.Empty;
        ResultLines[1].Text = revealed ? ClassToolText.RightLine(quiz.RightCount, quiz.AnsweredCount) : string.Empty;
        ResultLines[2].Text = revealed ? ClassToolText.TopLine(quiz.Top, Viewer) : string.Empty;
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (Quiz is not { } quiz)
            return;

        var answering = (quiz.Phase == QuizPhase.Open) && (Countdown.Left > 0);
        TimerLabel.Text = answering ? $"{Countdown.Left} seconds left. You can change your answer." : "Time is up.";

        foreach (var button in AnswerButtons)
            button.Enabled = answering;
    }

    private void Outline(SpriteBatch spriteBatch, byte slot, Color color)
    {
        if ((slot >= AnswerButtons.Length) || !AnswerButtons[slot].Visible)
            return;

        var button = AnswerButtons[slot];
        var outer = new Rectangle(button.ScreenX - 2, button.ScreenY - 2, button.Width + 4, button.Height + 4);
        DrawBorder(spriteBatch, outer, color);
        DrawBorder(spriteBatch, new Rectangle(outer.X + 1, outer.Y + 1, outer.Width - 2, outer.Height - 2), color);
    }
}

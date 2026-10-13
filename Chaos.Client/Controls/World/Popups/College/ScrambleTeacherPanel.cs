using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The class Teacher's Scramble panel at the top right of the view: the scrambled tiles, the answer, how many have
///     solved it and the clock, then after the reveal the solve order and top scores. It never takes keyboard focus.
/// </summary>
public sealed class ScrambleTeacherPanel : GuildCloakDialogBase
{
    private const int WIDTH = 250;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int TILES_TOP = 28;
    private const int TILE_HEIGHT = 18;
    private const int TILE_MAX_WIDTH = 14;
    private const int ANSWER_TOP = TILES_TOP + TILE_HEIGHT + 6;
    private const int STATUS_TOP = ANSWER_TOP + LINE_HEIGHT;
    private const int LINES_TOP = STATUS_TOP + LINE_HEIGHT;
    private const int LINE_COUNT = 4;
    private const int BUTTONS_TOP = LINES_TOP + (LINE_COUNT * LINE_HEIGHT) + 4;
    private const int HEIGHT = BUTTONS_TOP + CustomButton.HEIGHT + OrnateFrame.BORDER_BOTTOM_HEIGHT - 4;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string END_ARMED = "Click End game again to end it.";

    private readonly UILabel AnswerLabel;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton EndButton;
    private readonly UILabel[] Lines = new UILabel[LINE_COUNT];
    private readonly CustomButton NextButton;
    private readonly ScrambleCardState State = new();
    private readonly UILabel StatusLabel;
    private bool Dismissed;
    private bool EndArmed;
    private ScrambleTileLayout? Layout;
    private bool Started;

    public event Action<CollegeActionArgs>? ActionRequested;

    public ScrambleTeacherPanel()
        : base("_nsett", false)
    {
        Name = "ScrambleTeacherPanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        AnswerLabel = Caption(string.Empty, LEFT, ANSWER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Lime);
        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < LINE_COUNT; i++)
            Lines[i] = Caption(string.Empty, LEFT, LINES_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        NextButton = AddButton("Next word", 110, LEFT, BUTTONS_TOP, OnNext);
        EndButton = AddButton("End game", 80, LEFT + 116, BUTTONS_TOP, OnEnd);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A ScrambleCard message for the Teacher. <paramref name="reopen" /> shows a panel the Teacher closed.</summary>
    public void Apply(CollegeScrambleInfo info, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (info.Phase == ScramblePhase.Ended)
        {
            Close();

            return;
        }

        //the panel is sent again as students solve, so only a new phase or word takes back a first End game click
        if (!Started || (State.Info.Phase != info.Phase) || (State.Info.Number != info.Number))
            EndArmed = false;

        State.Apply(info);
        Started = true;
        Countdown.Start(info.SecondsLeft);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Started = false;
        Dismissed = false;
        EndArmed = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Started && (State.Info.Phase == ScramblePhase.Open))
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            StatusLabel.Text = ScrambleCardState.TeacherStatus(State.Info, Countdown.Left);
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (Visible && Started && Layout is { } layout)
            ScrambleTileDrawer.Draw(spriteBatch, layout, ScreenX + LEFT, ScreenY + TILES_TOP, TILE_HEIGHT, State.Revealed);
    }

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private bool IsLastRevealed(CollegeScrambleInfo info) => (info.Phase == ScramblePhase.Revealed) && (info.Number >= info.Count);

    private void OnNext()
    {
        if (!Started || (State.Info.Phase == ScramblePhase.Open))
            return;

        Raise(IsLastRevealed(State.Info) ? ScrambleControlType.End : ScrambleControlType.Next);
    }

    private void OnEnd()
    {
        if (!EndArmed)
        {
            EndArmed = true;
            Lines[LINE_COUNT - 1].Text = END_ARMED;
            Lines[LINE_COUNT - 1].ForegroundColor = LegendColors.Gold;

            return;
        }

        EndArmed = false;
        Raise(ScrambleControlType.End);
    }

    private void Raise(ScrambleControlType control)
        => ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.ScrambleControl, ScrambleControl = control });

    private void Refresh()
    {
        var info = State.Info;
        var waiting = info.Phase == ScramblePhase.Waiting;
        CaptionLabel.Text = ScrambleCardState.Title(info);
        Layout = waiting ? null : new ScrambleTileLayout(State.Tiles, INNER_WIDTH, TILE_MAX_WIDTH);
        AnswerLabel.Text = waiting ? string.Empty : info.Word;
        StatusLabel.Text = waiting ? "Start the first word when ready." : ScrambleCardState.TeacherStatus(info, Countdown.Left);

        foreach (var line in Lines)
        {
            line.Text = string.Empty;
            line.ForegroundColor = LegendColors.White;
        }

        if (info.Phase == ScramblePhase.Revealed)
        {
            var solvers = TextRenderer.WrapLines(ScrambleCardState.SolversLine(info, string.Empty), INNER_WIDTH);
            Lines[0].Text = solvers.Count > 0 ? solvers[0] : string.Empty;
            Lines[1].Text = solvers.Count > 1 ? solvers[1] : string.Empty;
            Lines[2].Text = ClassToolText.TopLine(info.Top, string.Empty);
        }

        if (EndArmed)
        {
            Lines[LINE_COUNT - 1].Text = END_ARMED;
            Lines[LINE_COUNT - 1].ForegroundColor = LegendColors.Gold;
        }

        NextButton.Caption = waiting ? "First word" : IsLastRevealed(info) ? "Finish game" : "Next word";
        NextButton.Enabled = info.Phase != ScramblePhase.Open;
    }
}

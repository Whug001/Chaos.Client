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
///     The student's Word Scramble card in the middle of the view: the scrambled letter tiles, the hint, a time bar, a typing
///     box and Guess. A wrong guess shows "Not quite" on the guesser's own card; a right one locks the box. After the reveal
///     the tiles turn green and the card lists who solved it and the top scores. OK or Esc hides it until the next word.
/// </summary>
public sealed class ScrambleCardControl : GuildCloakDialogBase
{
    private const int WIDTH = 370;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int SCORE_WIDTH = 60;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int TILES_TOP = 30;
    private const int TILE_HEIGHT = 22;
    private const int TILE_MAX_WIDTH = 18;
    private const int HINT_TOP = TILES_TOP + TILE_HEIGHT + 6;
    private const int BAR_TOP = HINT_TOP + (2 * LINE_HEIGHT) + 4;
    private const int BAR_HEIGHT = 4;
    private const int GUESS_TOP = BAR_TOP + 10;
    private const int GUESS_BUTTON_WIDTH = 70;
    private const int LINES_TOP = GUESS_TOP + CustomButton.HEIGHT + 6;
    private const int LINE_COUNT = 5;
    private const int HEIGHT = LINES_TOP + (LINE_COUNT * LINE_HEIGHT) + OrnateFrame.BORDER_BOTTOM_HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton GuessButton;
    private readonly CustomTextBox GuessBox;
    private readonly UILabel HintLabel;
    private readonly UILabel[] Lines = new UILabel[LINE_COUNT];
    private readonly UILabel ScoreLabel;
    private readonly ScrambleCardState State = new();
    private int DismissedNumber = -1;
    private bool GuessShown;
    private bool Hidden;
    private ScrambleTileLayout? Layout;
    private bool Started;
    private string Viewer = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    public ScrambleCardControl()
        : base("_nsett", false)
    {
        Name = "ScrambleCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH - SCORE_WIDTH, color: LegendColors.Gold);
        ScoreLabel = Caption(string.Empty, LEFT + INNER_WIDTH - SCORE_WIDTH, CAPTION_TOP, SCORE_WIDTH, HorizontalAlignment.Right);
        HintLabel = Caption(string.Empty, LEFT, HINT_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        HintLabel.WordWrap = true;
        HintLabel.Height = 2 * LINE_HEIGHT;

        GuessBox = new CustomTextBox
        {
            X = LEFT,
            Y = GUESS_TOP,
            Width = INNER_WIDTH - GUESS_BUTTON_WIDTH - 6,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_WORD_CHARS,
            IsSelectable = true,
            HintText = "Type the word"
        };

        AddChild(GuessBox);
        GuessButton = AddButton("Guess", GUESS_BUTTON_WIDTH, LEFT + INNER_WIDTH - GUESS_BUTTON_WIDTH, GUESS_TOP, Guess);

        for (var i = 0; i < LINE_COUNT; i++)
            Lines[i] = Caption(string.Empty, LEFT, LINES_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);

        GuessBox.Visible = false;
        GuessButton.Visible = false;
    }

    /// <summary>A ScrambleCard message. <paramref name="reopen" /> shows a card the student closed.</summary>
    public void Apply(CollegeScrambleInfo info, string viewer, bool reopen)
    {
        State.Apply(info);
        Viewer = viewer;
        Started = true;
        Countdown.Start(info.SecondsLeft);

        if (info.Phase == ScramblePhase.Waiting)
        {
            GuessShown = false;
            GuessBox.IsFocused = false;
            Hide();

            return;
        }

        if (reopen || (info.Number != DismissedNumber))
            Hidden = false;

        Refresh();

        if (!Visible && !Hidden)
            Show();
    }

    /// <summary>ToolClosed: the game is over and the card goes.</summary>
    public void Close()
    {
        Started = false;
        DismissedNumber = -1;
        Hidden = false;
        GuessShown = false;
        GuessBox.IsFocused = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Started && (State.Info.Phase == ScramblePhase.Open))
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            RefreshBox();
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

        if ((e.Keycode == Keycode.Enter) && GuessBox.Visible)
        {
            Guess();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || !Started)
            return;

        var info = State.Info;

        if (Layout is { } layout)
            ScrambleTileDrawer.Draw(spriteBatch, layout, ScreenX + LEFT, ScreenY + TILES_TOP, TILE_HEIGHT, State.Revealed);

        if (State.Revealed)
            return;

        var bar = new Rectangle(ScreenX + LEFT, ScreenY + BAR_TOP, INNER_WIDTH, BAR_HEIGHT);
        DrawRect(spriteBatch, bar, LegendColors.DimGray);

        if (info is { Phase: ScramblePhase.Open, Seconds: > 0 })
            DrawRect(spriteBatch, bar with { Width = INNER_WIDTH * Countdown.Left / info.Seconds }, LegendColors.Gold);
    }

    private void Dismiss()
    {
        DismissedNumber = State.Info.Number;
        GuessBox.IsFocused = false;
        GuessShown = false;
        Hidden = true;
        Hide();
    }

    private void Guess()
    {
        if (!GuessBox.Visible || !GuessBox.Enabled)
            return;

        var text = GuessBox.Text.Trim();

        if (text.Length == 0)
            return;

        State.NoteGuess();
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.ScrambleGuess, Text = text });
        GuessBox.Text = string.Empty;
        GuessBox.IsFocused = true;
        Refresh();
    }

    private void Refresh()
    {
        var info = State.Info;
        CaptionLabel.Text = ScrambleCardState.Title(info);
        ScoreLabel.Text = $"You: {info.MyScore}";
        Layout = new ScrambleTileLayout(State.Tiles, INNER_WIDTH, TILE_MAX_WIDTH);
        HintLabel.Text = State.Revealed ? info.Hint : $"Hint: {info.Hint}";

        foreach (var line in Lines)
        {
            line.Text = string.Empty;
            line.ForegroundColor = LegendColors.White;
        }

        var status = ClassToolText.ScrambleStatus(info);

        if (info.Phase == ScramblePhase.Ended)
        {
            Lines[0].Text = "Game over. Final scores:";
            Lines[0].ForegroundColor = LegendColors.Gold;

            for (var i = 0; (i < info.Top.Count) && (i + 1 < LINE_COUNT); i++)
            {
                Lines[i + 1].Text = $"{i + 1}. {info.Top[i].Name}  {info.Top[i].Score}";

                Lines[i + 1].ForegroundColor = info.Top[i].Name.Equals(Viewer, StringComparison.OrdinalIgnoreCase)
                    ? LegendColors.Gold
                    : LegendColors.White;
            }
        }
        else if (info.Phase == ScramblePhase.Revealed)
        {
            Lines[0].Text = status;
            Lines[0].ForegroundColor = info.MyPlace > 0 ? LegendColors.Lime : LegendColors.Gray;
            var solvers = TextRenderer.WrapLines(ScrambleCardState.SolversLine(info, Viewer), INNER_WIDTH);
            Lines[1].Text = solvers.Count > 0 ? solvers[0] : string.Empty;
            Lines[2].Text = solvers.Count > 1 ? solvers[1] : string.Empty;
            Lines[3].Text = ScrambleCardState.SolvedLine(info);
            Lines[4].Text = ClassToolText.TopLine(info.Top, Viewer);
        }
        else
        {
            if (State.ShowWrong)
            {
                Lines[0].Text = ScrambleCardState.WRONG_TEXT;
                Lines[0].ForegroundColor = LegendColors.Red;
            }

            Lines[1].Text = status;
            Lines[1].ForegroundColor = info.MyPlace > 0 ? LegendColors.Lime : LegendColors.Gray;
        }

        RefreshBox();
    }

    private void RefreshBox()
    {
        var show = State.Info.Phase == ScramblePhase.Open;
        var open = show && State.BoxOpen && (Countdown.Left > 0);
        GuessBox.Visible = GuessButton.Visible = show;
        GuessBox.Enabled = GuessButton.Enabled = open;

        if (open && !GuessShown && !Hidden)
        {
            GuessBox.Text = string.Empty;
            GuessBox.IsFocused = GuessBox.IsFocused || !UITextBox.IsAnyFocused;
            GuessShown = true;
        }
        else if (!open)
        {
            GuessBox.IsFocused = false;
            GuessShown = false;
        }
    }
}

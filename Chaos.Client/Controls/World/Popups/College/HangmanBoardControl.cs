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
///     The Hangman board in the middle of the view, for players and watchers: the title, the gallows (Survival) or the miss
///     line (Casual), the word with its hint, the turn strip, 26 letter buttons, then Buzz or the solve box, and the latest
///     event. After the reveal it shows the whole word and the top scores; when the game ends, the final scores until the
///     server closes it. OK or Esc hides it until the next word.
/// </summary>
public sealed class HangmanBoardControl : GuildCloakDialogBase
{
    private const int WIDTH = 380;
    private const int HEIGHT = 276;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int SCORE_WIDTH = 60;
    private const int GALLOWS_TOP = 30;
    private const int GALLOWS_SLOT = 56;
    private const int WORD_X = LEFT + GALLOWS_SLOT;
    private const int WORD_WIDTH = INNER_WIDTH - GALLOWS_SLOT;
    private const int MASK_TOP = 36;
    private const int MASK_SCALE = 2;
    private const int MASK_CELL = 14;
    private const int HINT_TOP = 68;
    private const int STRIP_TOP = 88;
    private const int STRIP_HEIGHT = 15;
    private const int STRIP_GAP = 4;
    private const int KEYS_TOP = 108;
    private const int KEY_STEP = CustomButton.HEIGHT + 2;
    private const int KEYS_PER_ROW = 13;
    private const int ACTION_TOP = KEYS_TOP + (2 * KEY_STEP) + 6;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int RESULT_LINES = 6;
    private const int SOLVE_BUTTON_WIDTH = 70;
    private const int EVENT_WIDTH = INNER_WIDTH - 80;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton BuzzButton;
    private readonly UILabel CaptionLabel;
    private readonly UILabel EventLabel;
    private readonly UILabel HintLabel;
    private readonly CustomButton[] LetterButtons = new CustomButton[26];
    private readonly UILabel MissLabel;
    private readonly UILabel[] ResultLines = new UILabel[RESULT_LINES];
    private readonly UILabel ScoreLabel;
    private readonly CustomButton SolveButton;
    private readonly CustomTextBox SolveBox;
    private readonly ToolCountdown Countdown = new();
    private int DismissedNumber = -1;
    private bool Hidden;
    private bool SolveShown;
    private HangmanBoardState? State;
    private string Viewer = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    public HangmanBoardControl()
        : base("_nsett", false)
    {
        Name = "HangmanBoard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH - SCORE_WIDTH, color: LegendColors.Gold);
        ScoreLabel = Caption(string.Empty, LEFT + INNER_WIDTH - SCORE_WIDTH, CAPTION_TOP, SCORE_WIDTH, HorizontalAlignment.Right);
        MissLabel = Caption(string.Empty, LEFT, GALLOWS_TOP + 14, GALLOWS_SLOT - 4, HorizontalAlignment.Left, LegendColors.Gray);
        HintLabel = Caption(string.Empty, WORD_X, HINT_TOP, WORD_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < LetterButtons.Length; i++)
        {
            var letter = (char)('A' + i);
            var column = i % KEYS_PER_ROW;
            var x = LEFT + (column * INNER_WIDTH / KEYS_PER_ROW);
            var width = ((column + 1) * INNER_WIDTH / KEYS_PER_ROW) - (column * INNER_WIDTH / KEYS_PER_ROW) - 2;
            LetterButtons[i] = AddButton(letter.ToString(), width, x, KEYS_TOP + ((i / KEYS_PER_ROW) * KEY_STEP), () => Pick(letter));
        }

        BuzzButton = AddButton("BUZZ to solve", INNER_WIDTH, LEFT, ACTION_TOP, Buzz);

        SolveBox = new CustomTextBox
        {
            X = LEFT,
            Y = ACTION_TOP,
            Width = INNER_WIDTH - SOLVE_BUTTON_WIDTH - 6,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_WORD_CHARS,
            IsSelectable = true,
            HintText = "Type the whole word"
        };

        AddChild(SolveBox);
        SolveButton = AddButton("Solve", SOLVE_BUTTON_WIDTH, LEFT + INNER_WIDTH - SOLVE_BUTTON_WIDTH, ACTION_TOP, Solve);

        for (var i = 0; i < RESULT_LINES; i++)
            ResultLines[i] = Caption(string.Empty, LEFT, ACTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);

        EventLabel = Caption(string.Empty, LEFT, ACTION_TOP + CustomButton.HEIGHT + 6, EVENT_WIDTH, color: LegendColors.Gray);
        BuzzButton.Visible = false;
        SolveBox.Visible = false;
        SolveButton.Visible = false;
    }

    /// <summary>A HangmanBoard message. <paramref name="reopen" /> shows a board the player closed.</summary>
    public void Apply(CollegeHangmanInfo info, string viewer, bool reopen)
    {
        State = new HangmanBoardState(info);
        Viewer = viewer;
        Countdown.Start(info.Phase == HangmanPhase.Solving ? info.SolveSecondsLeft : info.TurnSecondsLeft);

        if (reopen || info.ISolve || (info.Number != DismissedNumber))
            Hidden = false;

        Refresh();

        if (!Visible && !Hidden)
            Show();
    }

    /// <summary>ToolClosed: the game is over and the board goes.</summary>
    public void Close()
    {
        State = null;
        DismissedNumber = -1;
        Hidden = false;
        SolveShown = false;
        SolveBox.IsFocused = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && State is { Info.Phase: HangmanPhase.Open or HangmanPhase.Solving })
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

        if ((e.Keycode == Keycode.Enter) && SolveBox.Visible)
        {
            Solve();
            e.Handled = true;

            return;
        }

        var key = (int)e.Keycode;

        if (State is { LettersEnabled: true }
            && (key is >= 'a' and <= 'z')
            && !e.Ctrl
            && !e.Alt
            && !UITextBox.IsAnyFocused)
        {
            Pick((char)(key - 'a' + 'A'));
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || State is not { } state)
            return;

        var info = state.Info;

        if (state.ShowGallows)
            DrawGallows(spriteBatch, state.GallowsStage);

        DrawMask(spriteBatch, info);
        DrawStrip(spriteBatch, info);

        for (var i = 0; i < LetterButtons.Length; i++)
        {
            var button = LetterButtons[i];

            var tint = state.LetterState((char)('A' + i)) switch
            {
                HangmanLetterState.Right => LegendColors.Lime * 0.35f,
                HangmanLetterState.Wrong => LegendColors.Red * 0.35f,
                _                        => Color.Transparent
            };

            if (tint != Color.Transparent)
                DrawRect(spriteBatch, new Rectangle(button.ScreenX + 2, button.ScreenY + 2, button.Width - 4, button.Height - 4), tint);
        }

        if (BuzzButton.Visible)
            DrawRect(
                spriteBatch,
                new Rectangle(BuzzButton.ScreenX + 2, BuzzButton.ScreenY + 2, BuzzButton.Width - 4, BuzzButton.Height - 4),
                LegendColors.Red * 0.45f);
    }

    private void Buzz()
    {
        if (State is not { ShowBuzz: true })
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.HangmanBuzz });
    }

    private void Dismiss()
    {
        if (State is { } state)
            DismissedNumber = state.Info.Number;

        SolveBox.IsFocused = false;
        SolveShown = false;
        Hidden = true;
        Hide();
    }

    private void DrawGallows(SpriteBatch spriteBatch, int stage)
    {
        var originX = ScreenX + LEFT + ((GALLOWS_SLOT - HangmanGallows.WIDTH) / 2) - 4;
        var originY = ScreenY + GALLOWS_TOP;

        foreach (var s in HangmanGallows.Segments(stage))
        {
            var steps = Math.Max(Math.Abs(s.X2 - s.X1), Math.Abs(s.Y2 - s.Y1));

            for (var i = 0; i <= steps; i++)
            {
                var x = steps == 0 ? s.X1 : s.X1 + ((s.X2 - s.X1) * i / steps);
                var y = steps == 0 ? s.Y1 : s.Y1 + ((s.Y2 - s.Y1) * i / steps);
                DrawRect(spriteBatch, new Rectangle(originX + x, originY + y, 2, 2), LegendColors.White);
            }
        }
    }

    private void DrawMask(SpriteBatch spriteBatch, CollegeHangmanInfo info)
    {
        var shown = info.Phase is HangmanPhase.Revealed or HangmanPhase.Ended && (info.Word.Length > 0) ? info.Word : info.Mask;

        var color = info.Phase is HangmanPhase.Revealed or HangmanPhase.Ended
            ? info.Outcome switch
            {
                HangmanOutcome.Solved => LegendColors.Lime,
                HangmanOutcome.Lost   => LegendColors.Red,
                _                     => LegendColors.White
            }
            : LegendColors.White;

        var x = ScreenX + WORD_X + ((WORD_WIDTH - (shown.Length * MASK_CELL)) / 2);

        foreach (var c in shown)
        {
            if (c != ' ')
                TextRenderer.DrawText(
                    spriteBatch,
                    new Vector2(x + ((MASK_CELL - (TextRenderer.CHAR_WIDTH * MASK_SCALE)) / 2), ScreenY + MASK_TOP),
                    c.ToString(),
                    color,
                    false,
                    scale: MASK_SCALE);

            x += MASK_CELL;
        }
    }

    private void DrawStrip(SpriteBatch spriteBatch, CollegeHangmanInfo info)
    {
        var current = info.Phase == HangmanPhase.Solving ? info.Solver : string.Empty;
        var strip = new HangmanTurnStrip(info.Players, current, Viewer);
        var turnLive = info.Phase is HangmanPhase.Open or HangmanPhase.Solving;

        if (strip.Entries.Count == 0)
            return;

        var more = strip.More > 0 ? $"+{strip.More}" : string.Empty;
        var budget = INNER_WIDTH - (more.Length == 0 ? 0 : TextRenderer.MeasureWidth(more) + STRIP_GAP);
        var perBox = (budget - (STRIP_GAP * (strip.Entries.Count - 1))) / strip.Entries.Count;
        var clockChars = 5;
        var x = ScreenX + LEFT;
        var y = ScreenY + STRIP_TOP;

        foreach (var entry in strip.Entries)
        {
            var maxChars = Math.Max(3, ((perBox - 8) / TextRenderer.CHAR_WIDTH) - (entry.Current && turnLive ? clockChars : 0));
            var name = entry.Name.Length <= maxChars ? entry.Name : entry.Name[..(maxChars - 1)] + ".";
            var live = entry.Current && turnLive;
            var text = live ? ClassToolText.TurnLabel(name, Countdown.Left) : name;
            var width = TextRenderer.MeasureWidth(text) + 8;
            var box = new Rectangle(x, y, width, STRIP_HEIGHT);
            var line = live ? LegendColors.Gold : LegendColors.DimGray;

            if (entry.Viewer)
                DrawDashed(spriteBatch, box, line);
            else
                DrawBorder(spriteBatch, box, line);

            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(x + 4, y + ((STRIP_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2)),
                text,
                live ? LegendColors.Gold : LegendColors.Silver,
                false);

            x += width + STRIP_GAP;
        }

        if (more.Length > 0)
            TextRenderer.DrawText(spriteBatch, new Vector2(x, y + ((STRIP_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2)), more, LegendColors.Gray, false);
    }

    private void DrawDashed(SpriteBatch spriteBatch, Rectangle box, Color color)
    {
        const int DASH = 3;

        for (var dx = 0; dx < box.Width; dx += 2 * DASH)
        {
            var w = Math.Min(DASH, box.Width - dx);
            DrawRect(spriteBatch, new Rectangle(box.X + dx, box.Y, w, 1), color);
            DrawRect(spriteBatch, new Rectangle(box.X + dx, box.Bottom - 1, w, 1), color);
        }

        for (var dy = 0; dy < box.Height; dy += 2 * DASH)
        {
            var h = Math.Min(DASH, box.Height - dy);
            DrawRect(spriteBatch, new Rectangle(box.X, box.Y + dy, 1, h), color);
            DrawRect(spriteBatch, new Rectangle(box.Right - 1, box.Y + dy, 1, h), color);
        }
    }

    private void Pick(char letter)
    {
        if (State is not { LettersEnabled: true } state || (state.LetterState(letter) != HangmanLetterState.Untried))
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.HangmanPick, Letter = (byte)char.ToUpperInvariant(letter) });
    }

    private void Raise(CollegeActionArgs args) => ActionRequested?.Invoke(args);

    private void Refresh()
    {
        if (State is not { } state)
            return;

        var info = state.Info;
        var finished = info.Phase is HangmanPhase.Revealed or HangmanPhase.Ended;

        CaptionLabel.Text = ClassToolText.HangmanTitle(info);
        ScoreLabel.Text = $"You: {info.MyScore}";
        MissLabel.Text = state.ShowGallows ? string.Empty : ClassToolText.MissLine(info);

        for (var i = 0; i < LetterButtons.Length; i++)
            LetterButtons[i].Enabled = state.LettersEnabled && (state.LetterState((char)('A' + i)) == HangmanLetterState.Untried);

        BuzzButton.Visible = state.ShowBuzz && !finished;
        SolveBox.Visible = SolveButton.Visible = state.ShowSolveBox;

        if (state.ShowSolveBox && !SolveShown)
        {
            SolveBox.Text = string.Empty;
            SolveBox.IsFocused = true;
        }
        else if (!state.ShowSolveBox)
            SolveBox.IsFocused = false;

        SolveShown = state.ShowSolveBox;

        foreach (var line in ResultLines)
            line.Text = string.Empty;

        var resultCount = 0;

        if (info.Phase == HangmanPhase.Revealed)
        {
            ResultLines[0].Text = info.Outcome == HangmanOutcome.Solved ? "Solved!" : "Out of misses.";
            ResultLines[0].ForegroundColor = info.Outcome == HangmanOutcome.Solved ? LegendColors.Lime : LegendColors.Red;
            ResultLines[1].Text = ClassToolText.TopLine(info.Top.Take(3).ToList(), Viewer);
            ResultLines[1].ForegroundColor = LegendColors.White;
            resultCount = 2;
        }
        else if (info.Phase == HangmanPhase.Ended)
        {
            ResultLines[0].Text = "Game over. Final scores:";
            ResultLines[0].ForegroundColor = LegendColors.Gold;
            resultCount = 1;

            for (var i = 0; (i < info.Top.Count) && (resultCount < RESULT_LINES); i++)
            {
                var top = info.Top[i];
                ResultLines[resultCount].Text = $"{i + 1}. {top.Name}  {top.Score}";
                ResultLines[resultCount].ForegroundColor = top.Name.Equals(Viewer, StringComparison.OrdinalIgnoreCase) ? LegendColors.Gold : LegendColors.White;
                resultCount++;
            }
        }

        EventLabel.Y = ACTION_TOP + (finished ? (resultCount * LINE_HEIGHT) + 4 : CustomButton.HEIGHT + 6);
        EventLabel.Text = info.Events.Count > 0 ? info.Events[^1] : string.Empty;
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (State is not { } state)
            return;

        var info = state.Info;
        var miss = state.ShowGallows ? ClassToolText.MissLine(info) : string.Empty;

        HintLabel.Text = info.Phase switch
        {
            HangmanPhase.Waiting                         => "Waiting for the next word",
            HangmanPhase.Solving when info.ISolve        => $"You buzzed first! {ClassToolText.Clock(Countdown.Left)} to answer",
            HangmanPhase.Solving                         => $"{info.Solver} is solving: {ClassToolText.Clock(Countdown.Left)}",
            HangmanPhase.Revealed or HangmanPhase.Ended  => info.Hint,
            _ when info.Hint.Length == 0                 => miss,
            _ when miss.Length == 0                      => $"Hint: {info.Hint}",
            _                                            => $"Hint: {info.Hint} - {miss}"
        };
    }

    private void Solve()
    {
        if (State is not { ShowSolveBox: true })
            return;

        var text = SolveBox.Text.Trim();

        if (text.Length == 0)
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.HangmanSolve, Text = text });
        SolveBox.Text = string.Empty;
    }
}

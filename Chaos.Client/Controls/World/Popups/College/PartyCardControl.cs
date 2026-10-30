using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The student's party-game card in the middle of the view, in the quiz card's place and frame: the title, the
///     score, a timer bar, and the write, note, vote, reveal or final view for Quip Duel, Bluff and Sketch Guess.
///     OK or Esc hides it until the next step.
/// </summary>
public sealed class PartyCardControl : GuildCloakDialogBase
{
    private const int WIDTH = 370;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int SCORE_WIDTH = 60;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int BAR_TOP = 26;
    private const int BAR_HEIGHT = 4;
    private const int TIMER_TOP = BAR_TOP + BAR_HEIGHT + 2;
    private const int CONTENT_TOP = TIMER_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int DRAW_ZOOM = 2;
    private const int DRAW_WIDTH = CollegeProtocol.DRAWING_WIDTH * DRAW_ZOOM;
    private const int DRAW_HEIGHT = CollegeProtocol.DRAWING_HEIGHT * DRAW_ZOOM;
    private const int PROMPT_LINES = 3;
    private const int NOTE_LINES = 4;
    private const int VOTE_STEP = CustomButton.HEIGHT + 3;

    //Five vote rows keep the tallest vote card (Sketch Guess with the 2x drawing: chrome, drawing,
    //prompt, rows, pager, footer) under the 480 px screen when centred.
    private const int VOTE_PAGE_SIZE = 5;
    private const int INFO_STEP = TextRenderer.CHAR_HEIGHT + 3;
    private const int ARROW_WIDTH = 28;
    private const int PAGE_LABEL_WIDTH = 100;
    private const int REVEAL_WRAP_LINES = 3;
    private const int SEND_BUTTON_WIDTH = 70;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int FOOTER_PAD = 30;

    private readonly CustomTextBox AnswerBox;
    private readonly UILabel CaptionLabel;
    private readonly UILabel CountLabel;
    private readonly UILabel CounterLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly Dictionary<byte, byte[]> Drawings = [];
    private readonly DrawingImage Drawing;
    private readonly List<UILabel> InfoLines = [];
    private readonly UILabel NoteLabel;
    private readonly UILabel[] PromptLines = new UILabel[PROMPT_LINES];
    private readonly UILabel ScoreLabel;
    private readonly CustomButton SendButton;
    private readonly PartyCardState State = new();
    private readonly UILabel TimerLabel;
    private readonly List<CustomButton> VoteButtons = [];
    private readonly CustomButton PrevButton;
    private readonly CustomButton NextButton;
    private readonly UILabel PageLabel;
    private int VotePage;
    private (PartyPhase Phase, byte Round, byte Item)? VoteStep;
    private (PartyPhase Phase, byte Round, byte Item)? DismissedStep;
    private bool Hidden;
    private (PartyPhase Phase, byte Round, byte Item, int Shown)? LastWriteKey;
    private byte? LastSentIndex;
    private string LastSentText = string.Empty;

    public PartyCardControl()
        : base("_nsett", false)
    {
        Name = "PartyCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = 200;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH - SCORE_WIDTH, color: LegendColors.Gold);
        ScoreLabel = Caption(string.Empty, LEFT + INNER_WIDTH - SCORE_WIDTH, CAPTION_TOP, SCORE_WIDTH, HorizontalAlignment.Right);
        TimerLabel = Caption(string.Empty, LEFT, TIMER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        CounterLabel = Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < PROMPT_LINES; i++)
            PromptLines[i] = Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center);

        Drawing = new DrawingImage { Visible = false };
        AddChild(Drawing);

        AnswerBox = new CustomTextBox
        {
            X = LEFT,
            Y = 0,
            Width = INNER_WIDTH,
            Height = CustomButton.HEIGHT,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Type your answer"
        };

        AddChild(AnswerBox);
        SendButton = AddButton("Send", SEND_BUTTON_WIDTH, 0, 0, SendAnswer);
        PrevButton = AddButton("<", ARROW_WIDTH, LEFT, 0, () => TurnVotePage(-1));
        NextButton = AddButton(">", ARROW_WIDTH, LEFT + ARROW_WIDTH + 4, 0, () => TurnVotePage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * ARROW_WIDTH) + 8, 0, PAGE_LABEL_WIDTH, HorizontalAlignment.Left, LegendColors.Gray);
        CountLabel = Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        NoteLabel = Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center);
        NoteLabel.WordWrap = true;

        AnswerBox.Visible = false;
        SendButton.Visible = false;
        CountLabel.Visible = false;
        CounterLabel.Visible = false;
        NoteLabel.Visible = false;
        PrevButton.Visible = false;
        NextButton.Visible = false;
        PageLabel.Visible = false;
    }

    public event Action<CollegeActionArgs>? Send;

    /// <summary>A PartyCard message. <paramref name="reopen" /> shows a card the student closed.</summary>
    public void Apply(CollegePartyInfo info, bool reopen)
    {
        State.Apply(info);
        LastSentIndex = null;
        LastSentText = string.Empty;
        Countdown.Start(info.SecondsLeft);

        if (info.Phase is PartyPhase.Waiting or PartyPhase.Ended)
        {
            AnswerBox.IsFocused = false;
            Drawings.Clear();
            Hide();

            return;
        }

        if (reopen)
            DismissedStep = null;

        if (DismissedStep != StepOf(info))
            Hidden = false;

        Refresh();

        var wasVisible = Visible;

        if (!Visible && !Hidden)
            Show();

        if (!wasVisible && Visible && !Hidden && (State.View == PartyCardView.Write) && AnswerBox.Visible)
            AnswerBox.IsFocused = true;
    }

    /// <summary>A PartyDrawing message: keeps the pixels so the card can show the drawing above its text.</summary>
    public void ShowDrawing(byte id, byte[] pixels)
    {
        Drawings[id] = (byte[])pixels.Clone();

        if (Visible)
            Refresh();
    }

    /// <summary>ToolClosed: the game is over and the card goes.</summary>
    public void Close()
    {
        State.Apply(new CollegePartyInfo());
        Drawings.Clear();
        DismissedStep = null;
        Hidden = false;
        LastWriteKey = null;
        LastSentIndex = null;
        LastSentText = string.Empty;
        AnswerBox.Text = string.Empty;
        AnswerBox.IsFocused = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
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

        if ((e.Keycode == Keycode.Enter) && AnswerBox.Visible)
        {
            SendAnswer();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var info = State.Info;
        var bar = new Rectangle(ScreenX + LEFT, ScreenY + BAR_TOP, INNER_WIDTH, BAR_HEIGHT);
        DrawRect(spriteBatch, bar, LegendColors.DimGray);

        if (info.Seconds > 0)
            DrawRect(spriteBatch, bar with { Width = INNER_WIDTH * Countdown.Left / info.Seconds }, LegendColors.Gold);
    }

    private void Dismiss()
    {
        DismissedStep = StepOf(State.Info);
        AnswerBox.IsFocused = false;
        Hidden = true;
        Hide();
    }

    private void SendAnswer()
    {
        if (!AnswerBox.Visible || !AnswerBox.Enabled)
            return;

        var shown = State.ShownPrompt;

        if (shown is null)
            return;

        var text = AnswerBox.Text.Trim();

        if (text.Length == 0)
            return;

        if ((LastSentIndex == shown.Index) && (LastSentText == text))
            return;

        LastSentIndex = shown.Index;
        LastSentText = text;

        Send?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.PartyAnswer,
                PartyPrompt = shown.Index,
                Text = text,
                Round = State.Info.Round,
                Item = State.Info.Item,
            });
    }

    private void Pick(int slot)
    {
        var info = State.Info;
        var page = VotePageChoices(info);

        if ((slot < 0) || (slot >= page.Count))
            return;

        if (info.Paused || (Countdown.Left == 0))
            return;

        var choice = page[slot];

        if (choice.Mine)
            return;

        info.MyChoice = choice.Id;
        Refresh();

        Send?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.PartyVote,
                PartyChoice = choice.Id,
                Round = info.Round,
                Item = info.Item,
            });
    }

    private void Refresh()
    {
        var info = State.Info;
        CaptionLabel.Text = ClassToolText.PartyTitle(info);
        ScoreLabel.Text = $"You: {info.MyScore}";
        var view = State.View == PartyCardView.Draw ? PartyCardView.Note : State.View;
        var pixels = DrawingPixels();
        var y = CONTENT_TOP;

        CounterLabel.Visible = false;
        Drawing.Visible = false;

        foreach (var line in PromptLines)
        {
            line.Text = string.Empty;
            line.Visible = false;
        }

        AnswerBox.Visible = false;
        SendButton.Visible = false;
        CountLabel.Visible = false;
        NoteLabel.Visible = false;

        foreach (var button in VoteButtons)
            button.Visible = false;

        PrevButton.Visible = false;
        NextButton.Visible = false;
        PageLabel.Visible = false;

        foreach (var line in InfoLines)
            line.Visible = false;

        var step = StepOf(info);

        if (VoteStep != step)
        {
            VoteStep = step;
            VotePage = 0;
        }

        y = view switch
        {
            PartyCardView.Write  => RefreshWrite(info, pixels, y),
            PartyCardView.Vote   => RefreshVote(info, pixels, y),
            PartyCardView.Reveal => RefreshReveal(info, pixels, y),
            PartyCardView.Final  => RefreshFinal(info, y),
            _                    => RefreshNote(info, pixels, y)
        };

        if (view != PartyCardView.Write)
        {
            AnswerBox.Text = string.Empty;
            AnswerBox.IsFocused = false;
        }

        if (!AnswerBox.Visible)
            AnswerBox.IsFocused = false;

        Height = y + FOOTER_PAD;

        if (OkButton is not null)
        {
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }

        this.CenterOnScreen();

        if (Y < 0)
            Y = 0;

        RefreshTimer();
    }

    private int RefreshWrite(CollegePartyInfo info, byte[]? pixels, int y)
    {
        var shown = State.ShownPrompt;

        if (shown is null)
            return RefreshNote(info, pixels, y);

        if (LastWriteKey != State.WriteKey)
        {
            LastWriteKey = State.WriteKey;
            AnswerBox.Text = string.Empty;
            AnswerBox.IsFocused = !Hidden;
        }

        CounterLabel.Text = ClassToolText.PromptCounter(State.Shown + 1, info.MyPrompts.Count);
        CounterLabel.Y = y;
        CounterLabel.Visible = true;
        y += LINE_HEIGHT + 2;

        var sketch = info.Kind == CollegeTool.SketchGuess;

        if (sketch && (pixels is not null))
        {
            ShowPixels(info.DrawingId, pixels, y);
            y += DRAW_HEIGHT + 6;
        }

        y = ShowPrompt(sketch ? "What is this drawing?" : shown.Text, y);

        AnswerBox.MaxLength = info.MaxChars;
        AnswerBox.Y = y;
        AnswerBox.Visible = true;
        y += CustomButton.HEIGHT + 4;

        SendButton.X = LEFT + INNER_WIDTH - SEND_BUTTON_WIDTH;
        SendButton.Y = y;
        SendButton.Visible = true;
        y += CustomButton.HEIGHT + 4;

        CountLabel.Y = y;
        CountLabel.Visible = true;
        y += LINE_HEIGHT;

        return y;
    }

    private int RefreshVote(CollegePartyInfo info, byte[]? pixels, int y)
    {
        if (pixels is not null)
        {
            ShowPixels(info.DrawingId, pixels, y);
            y += DRAW_HEIGHT + 6;
        }

        y = ShowPrompt(info.Prompt, y);

        EnsureVoteButtons(VOTE_PAGE_SIZE);
        var pages = Math.Max(1, (info.Choices.Count + VOTE_PAGE_SIZE - 1) / VOTE_PAGE_SIZE);
        VotePage = Math.Clamp(VotePage, 0, pages - 1);
        var page = VotePageChoices(info);

        for (var i = 0; i < page.Count; i++)
        {
            var button = VoteButtons[i];
            button.Caption = page[i].Text;
            button.Y = y + (i * VOTE_STEP);
            button.Selected = info.MyChoice == page[i].Id;
            button.Visible = true;
        }

        y += page.Count * VOTE_STEP;

        return ShowPager(pages, y);
    }

    private int RefreshReveal(CollegePartyInfo info, byte[]? pixels, int y)
    {
        if (pixels is not null)
        {
            ShowPixels(info.DrawingId, pixels, y);
            y += DRAW_HEIGHT + 6;
        }

        y = ShowPrompt(info.Prompt, y);

        var lines = new List<(string Text, Color Color)>();

        if (info.Kind == CollegeTool.QuipDuel)
        {
            var best = 0;

            foreach (var choice in info.Choices)
                best = Math.Max(best, choice.Votes);

            foreach (var choice in info.Choices)
                lines.Add(
                    (
                        $"{Quoted(choice.Text)}  {Authors(choice.Authors)} +{choice.Votes}",
                        (best > 0) && (choice.Votes == best) ? LegendColors.Gold : LegendColors.White));
        }
        else
        {
            foreach (var choice in info.Choices)
            {
                if (choice.IsReal)
                {
                    var voters = choice.Voters.Count == 0 ? "no one" : string.Join(", ", choice.Voters);
                    lines.Add(($"{Quoted(choice.Text)}  TRUTH: {voters} +{choice.Votes}", LegendColors.Lime));
                }
                else if (choice.IsDecoy)
                    lines.Add(($"{Quoted(choice.Text)}  decoy", LegendColors.Gray));
                else
                    lines.Add(
                        (
                            $"{Quoted(choice.Text)}  {Authors(choice.Authors)} fooled {choice.Votes} +{choice.Votes}",
                            LegendColors.White));
            }
        }

        if (info.Event.Length > 0)
            lines.Add((info.Event, LegendColors.Gray));

        var pages = Math.Max(1, (lines.Count + VOTE_PAGE_SIZE - 1) / VOTE_PAGE_SIZE);
        VotePage = Math.Clamp(VotePage, 0, pages - 1);
        var page = lines.Skip(VotePage * VOTE_PAGE_SIZE).Take(VOTE_PAGE_SIZE).ToList();

        return ShowPager(pages, ShowLines(page, y));
    }

    private int RefreshFinal(CollegePartyInfo info, int y)
    {
        var lines = new List<(string Text, Color Color)>();

        for (var i = 0; i < info.Scores.Count; i++)
            lines.Add((ClassToolText.FinalLine(i + 1, info.Scores[i]), i == 0 ? LegendColors.Gold : LegendColors.White));

        if (info.Note.Length > 0)
            lines.Add((info.Note, LegendColors.Gray));

        return ShowLines(lines, y);
    }

    private int RefreshNote(CollegePartyInfo info, byte[]? pixels, int y)
    {
        if (pixels is not null)
        {
            ShowPixels(info.DrawingId, pixels, y);
            y += DRAW_HEIGHT + 6;
        }

        if (info.Note.Length == 0)
            return y;

        var shown = Math.Min(TextRenderer.WrapLines(info.Note, INNER_WIDTH - 4).Count, NOTE_LINES);
        NoteLabel.Text = info.Note;
        NoteLabel.Height = Math.Max(1, shown) * LINE_HEIGHT;
        NoteLabel.Y = y;
        NoteLabel.Visible = true;

        return y + (NoteLabel.Height + 2);
    }

    private void ShowPixels(byte id, byte[] pixels, int y)
    {
        Drawing.SetImage(id, pixels);
        Drawing.X = LEFT + ((INNER_WIDTH - DRAW_WIDTH) / 2);
        Drawing.Y = y;
        Drawing.Visible = true;
    }

    private int ShowPrompt(string text, int y)
    {
        if (text.Length == 0)
            return y;

        var lines = TextRenderer.WrapLines(text, INNER_WIDTH);
        var shown = Math.Min(lines.Count, PROMPT_LINES);

        for (var i = 0; i < shown; i++)
        {
            PromptLines[i].Text = lines[i];
            PromptLines[i].Y = y + (i * LINE_HEIGHT);
            PromptLines[i].Visible = true;
        }

        return y + (shown * LINE_HEIGHT) + 4;
    }

    private int ShowLines(List<(string Text, Color Color)> lines, int y)
    {
        var rows = new List<(string Text, Color Color)>();

        foreach (var line in lines)
        {
            var wrapped = TextRenderer.WrapLines(line.Text, INNER_WIDTH);

            if (wrapped.Count == 0)
            {
                rows.Add((string.Empty, line.Color));

                continue;
            }

            var shown = wrapped.Take(REVEAL_WRAP_LINES).ToList();

            if (wrapped.Count > shown.Count)
                shown[^1] = Cut(shown[^1], INNER_WIDTH);

            foreach (var row in shown)
                rows.Add((row, line.Color));
        }

        EnsureInfoLines(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            InfoLines[i].Text = rows[i].Text;
            InfoLines[i].ForegroundColor = rows[i].Color;
            InfoLines[i].Y = y + (i * INFO_STEP);
            InfoLines[i].Visible = true;
        }

        return y + (rows.Count * INFO_STEP);
    }

    private void EnsureVoteButtons(int count)
    {
        while (VoteButtons.Count < count)
        {
            CustomButton? slot = null;
            slot = AddButton(string.Empty, INNER_WIDTH, LEFT, 0, () => Pick(VoteButtons.IndexOf(slot!)));
            VoteButtons.Add(slot);
        }
    }

    private List<CollegePartyChoiceInfo> VotePageChoices(CollegePartyInfo info)
        => info.Choices.Skip(VotePage * VOTE_PAGE_SIZE).Take(VOTE_PAGE_SIZE).ToList();

    private void TurnVotePage(int by)
    {
        VotePage += by;
        Refresh();
    }

    private int ShowPager(int pages, int y)
    {
        if (pages <= 1)
            return y;

        PrevButton.Y = y;
        NextButton.Y = y;
        PrevButton.Enabled = VotePage > 0;
        NextButton.Enabled = VotePage < (pages - 1);
        PrevButton.Visible = true;
        NextButton.Visible = true;
        PageLabel.Text = $"Page {VotePage + 1} of {pages}";
        PageLabel.Y = y + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        PageLabel.Visible = true;

        return y + CustomButton.HEIGHT + 4;
    }

    private static string Cut(string text, int maxWidth)
    {
        while ((text.Length > 0) && (TextRenderer.MeasureWidth(text + "...") > maxWidth))
            text = text[..^1];

        return text + "...";
    }

    private void EnsureInfoLines(int count)
    {
        while (InfoLines.Count < count)
            InfoLines.Add(Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center));
    }

    private void RefreshTimer()
    {
        var info = State.Info;
        TimerLabel.Text = info.Paused ? ClassToolText.PartyPaused : ClassToolText.Clock(Countdown.Left);
        var open = !info.Paused && (Countdown.Left > 0);

        AnswerBox.Enabled = open;
        SendButton.Enabled = open;

        if (CountLabel.Visible)
            CountLabel.Text = ClassToolText.CharCount(AnswerBox.Text.Length, info.MaxChars);

        if (State.View == PartyCardView.Vote)
        {
            var page = VotePageChoices(info);

            for (var i = 0; (i < page.Count) && (i < VoteButtons.Count); i++)
                VoteButtons[i].Enabled = open && !page[i].Mine;
        }
    }

    private byte[]? DrawingPixels()
    {
        if (State.Info.DrawingId == 0)
            return null;

        return Drawings.TryGetValue(State.Info.DrawingId, out var pixels) ? pixels : null;
    }

    private static (PartyPhase Phase, byte Round, byte Item) StepOf(CollegePartyInfo info) => (info.Phase, info.Round, info.Item);

    private static string Quoted(string text) => $"\"{text}\"";

    private static string Authors(List<string> authors) => authors.Count == 0 ? "?" : string.Join(", ", authors);

    private sealed class DrawingImage : UIElement
    {
        private byte[] Current = [];
        private byte CurrentId;
        private bool HasImage;
        private Texture2D? Texture;

        public DrawingImage()
        {
            Width = DRAW_WIDTH;
            Height = DRAW_HEIGHT;
            IsHitTestVisible = false;
        }

        public void SetImage(byte id, byte[] pixels)
        {
            if (HasImage && (CurrentId == id) && Current.AsSpan().SequenceEqual(pixels))
                return;

            CurrentId = id;
            Current = (byte[])pixels.Clone();
            HasImage = true;

            Texture?.Dispose();
            Texture = new Texture2D(TextureConverter.Device, DRAW_WIDTH, DRAW_HEIGHT);

            Texture.SetData(
                DrawingTextures.ToColors(
                    SketchToolbox.Colours,
                    pixels,
                    DRAW_ZOOM,
                    CollegeProtocol.DRAWING_WIDTH,
                    CollegeProtocol.DRAWING_HEIGHT));
        }

        public override void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
            base.Dispose();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            base.Draw(spriteBatch);

            if (Texture is not null)
                DrawTexture(spriteBatch, Texture, new Vector2(ScreenX, ScreenY), Color.White);
        }
    }
}

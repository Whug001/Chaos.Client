using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The class Teacher's party-game panel at the top right of the view: who has answered while writing or drawing, the
///     vote with its authors and the running scores, the reveal and the final scores, with the buttons that run the
///     game. For Sketch Guess the drawing shows above the vote. It never takes keyboard focus, so the Teacher can keep
///     moving and talking.
/// </summary>
public sealed class PartyTeacherPanel : GuildCloakDialogBase
{
    private const int WIDTH = 250;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int STATUS_TOP = 28;
    private const int PLAYER_ROWS = 6;
    private const int CHOICE_ROWS = 6;
    private const int SCORE_ROWS = 6;
    private const int NAME_COLUMN = INNER_WIDTH - 68;
    private const int DRAW_ZOOM = 2;
    private const int DRAW_WIDTH = CollegeProtocol.DRAWING_WIDTH * DRAW_ZOOM;
    private const int DRAW_HEIGHT = CollegeProtocol.DRAWING_HEIGHT * DRAW_ZOOM;
    private const int BUTTON_GAP = 4;
    private const int ARROW_WIDTH = 28;
    private const int PAGE_X = LEFT + 66;
    private const int PAGE_WIDTH = 100;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string END_ARMED = "Click End game again to end it.";

    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly CollegeListRow[] ChoiceRows = new CollegeListRow[CHOICE_ROWS];
    private readonly CustomButton ChoiceNextButton;
    private readonly UILabel ChoicePageLabel;
    private readonly CustomButton ChoicePrevButton;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton EndButton;
    private readonly UILabel[] EventLines = new UILabel[2];
    private readonly CustomButton NextButton;
    private readonly UILabel PausedLabel;
    private readonly CollegeListRow[] PlayerRows = new CollegeListRow[PLAYER_ROWS];
    private readonly CustomButton PlayerNextButton;
    private readonly UILabel PlayerPageLabel;
    private readonly CustomButton PlayerPrevButton;
    private readonly UILabel[] PromptLines = new UILabel[2];
    private readonly UILabel[] ScoreLines = new UILabel[SCORE_ROWS];
    private readonly UILabel ScoresMoreLabel;
    private readonly CustomButton SkipButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton StopButton;
    private readonly CustomButton StrikeButton;
    private readonly CustomButton StrikeDrawingButton;
    private readonly UILabel[] TopLines = new UILabel[2];
    private int ChoicePage;
    private Texture2D? DrawingTexture;
    private int DrawingY;
    private bool DrawingShown;
    private bool Dismissed;
    private bool EndArmed;
    private CollegePartyInfo? Info;
    private PartyPhase LastPhase = PartyPhase.Waiting;
    private byte LastRound;
    private byte LastItem;
    private int PlayerPage;
    private byte SelectedChoice = CollegeProtocol.PARTY_NO_CHOICE;
    private byte ShownDrawingId;

    public event Action<CollegeActionArgs>? Send;

    public PartyTeacherPanel()
        : base("_nsett", false)
    {
        Name = "PartyTeacherPanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = 200;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        PausedLabel = Caption(ClassToolText.PartyPaused, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < PromptLines.Length; i++)
            PromptLines[i] = Caption(string.Empty, LEFT, 0, INNER_WIDTH);

        for (var i = 0; i < PLAYER_ROWS; i++)
        {
            var row = new CollegeListRow(INNER_WIDTH, [0, NAME_COLUMN]);
            AddChild(row);
            PlayerRows[i] = row;
        }

        for (var i = 0; i < CHOICE_ROWS; i++)
        {
            var slot = i;
            var row = new CollegeListRow(INNER_WIDTH, [0, NAME_COLUMN]) { X = LEFT };
            row.Clicked += () => SelectChoice(slot);
            AddChild(row);
            ChoiceRows[i] = row;
        }

        PlayerPrevButton = AddButton("<", ARROW_WIDTH, 0, 0, () => TurnPlayer(-1));
        PlayerNextButton = AddButton(">", ARROW_WIDTH, 0, 0, () => TurnPlayer(1));
        PlayerPageLabel = Caption(string.Empty, PAGE_X, 0, PAGE_WIDTH, color: LegendColors.Gray);
        ChoicePrevButton = AddButton("<", ARROW_WIDTH, 0, 0, () => TurnChoice(-1));
        ChoiceNextButton = AddButton(">", ARROW_WIDTH, 0, 0, () => TurnChoice(1));
        ChoicePageLabel = Caption(string.Empty, PAGE_X, 0, PAGE_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < EventLines.Length; i++)
            EventLines[i] = Caption(string.Empty, LEFT, 0, INNER_WIDTH, color: LegendColors.Lime);

        for (var i = 0; i < TopLines.Length; i++)
            TopLines[i] = Caption(string.Empty, LEFT, 0, INNER_WIDTH);

        for (var i = 0; i < SCORE_ROWS; i++)
            ScoreLines[i] = Caption(string.Empty, LEFT, 0, INNER_WIDTH);

        ScoresMoreLabel = Caption(string.Empty, LEFT, 0, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        ArmedLabel = Caption(string.Empty, LEFT, 0, INNER_WIDTH, color: LegendColors.Gold);

        StopButton = AddButton("Stop writing now", INNER_WIDTH, LEFT, 0, OnStop);
        NextButton = AddButton("Next round", INNER_WIDTH, LEFT, 0, OnNext);
        StrikeButton = AddButton("Strike selected answer", INNER_WIDTH, LEFT, 0, OnStrike);
        StrikeDrawingButton = AddButton("Strike drawing", INNER_WIDTH, LEFT, 0, OnStrikeDrawing);
        SkipButton = AddButton("Skip this", INNER_WIDTH, LEFT, 0, () => Raise(PartyControlType.Skip));
        EndButton = AddButton("End game", INNER_WIDTH, LEFT, 0, OnEnd);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A PartyCard message for the Teacher. <paramref name="reopen" /> shows a panel the Teacher closed.</summary>
    public void Apply(CollegePartyInfo info, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (info.Phase == PartyPhase.Ended)
        {
            Close();

            return;
        }

        if ((LastPhase != info.Phase) || (LastRound != info.Round) || (LastItem != info.Item))
        {
            EndArmed = false;
            PlayerPage = 0;
            ChoicePage = 0;
            SelectedChoice = CollegeProtocol.PARTY_NO_CHOICE;
        }

        LastPhase = info.Phase;
        LastRound = info.Round;
        LastItem = info.Item;
        Info = info;
        Countdown.Start(info.SecondsLeft);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    /// <summary>A PartyDrawing message: keeps the drawing whose id matches the card's, for the Sketch Guess vote.</summary>
    public void ShowDrawing(byte id, byte[] pixels)
    {
        DrawingTexture?.Dispose();
        DrawingTexture = null;
        ShownDrawingId = 0;

        if ((id != 0) && (pixels.Length == CollegeProtocol.DRAWING_PIXELS))
        {
            var clean = new byte[pixels.Length];

            for (var i = 0; i < clean.Length; i++)
                clean[i] = pixels[i] < SketchToolbox.Colours.Count ? pixels[i] : (byte)0;

            DrawingTexture = new Texture2D(TextureConverter.Device, DRAW_WIDTH, DRAW_HEIGHT);
            DrawingTexture.SetData(DrawingTextures.ToColors(SketchToolbox.Colours, clean, DRAW_ZOOM));
            ShownDrawingId = id;
        }

        Refresh();
    }

    public void Close()
    {
        Info = null;
        Dismissed = false;
        LastPhase = PartyPhase.Waiting;
        LastRound = 0;
        LastItem = 0;
        PlayerPage = 0;
        ChoicePage = 0;
        EndArmed = false;
        SelectedChoice = CollegeProtocol.PARTY_NO_CHOICE;
        DrawingTexture?.Dispose();
        DrawingTexture = null;
        ShownDrawingId = 0;
        DrawingShown = false;
        Hide();
    }

    public override void Dispose()
    {
        DrawingTexture?.Dispose();
        base.Dispose();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && (Info is { } info) && Timed(info))
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            StatusLabel.Text = StatusText(info);
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (Visible && DrawingShown && (DrawingTexture is not null))
        {
            var x = ScreenX + LEFT + ((INNER_WIDTH - DRAW_WIDTH) / 2);
            DrawRectClipped(spriteBatch, new Rectangle(x - 1, ScreenY + DrawingY - 1, DRAW_WIDTH + 2, DRAW_HEIGHT + 2), LegendColors.DimGray);
            DrawTexture(spriteBatch, DrawingTexture, new Vector2(x, ScreenY + DrawingY), Color.White);
        }
    }

    private static bool Timed(CollegePartyInfo info)
        => !info.Paused && (info.Phase is PartyPhase.Writing or PartyPhase.Drawing or PartyPhase.Voting
            || ((info.Phase == PartyPhase.Revealed) && (info.Kind == CollegeTool.QuipDuel)));

    private static string KindName(CollegePartyInfo info)
        => info.Kind switch
        {
            CollegeTool.QuipDuel => "Quip Duel",
            CollegeTool.Bluff    => "Bluff",
            _                    => "Sketch Guess"
        };

    private static string VoteNoun(CollegePartyInfo info)
        => info.Kind switch
        {
            CollegeTool.QuipDuel => "Duel",
            CollegeTool.Bluff    => "Question",
            _                    => "Drawing"
        };

    private string StatusText(CollegePartyInfo info)
    {
        var clock = ClassToolText.Clock(Countdown.Left);

        return info.Phase switch
        {
            PartyPhase.Waiting => "Waiting to start.",
            PartyPhase.Writing => $"Round {info.Round} of {info.Rounds}: writing {clock}",
            PartyPhase.Drawing => $"Drawing {info.Item} of {info.Items}: {clock}",
            PartyPhase.Voting  => $"{VoteNoun(info)} {info.Item} of {info.Items}: voting {clock}",
            PartyPhase.Revealed when info.Kind == CollegeTool.QuipDuel => $"Next duel in {clock}",
            PartyPhase.Revealed => $"{VoteNoun(info)} {info.Item} of {info.Items}: revealed",
            PartyPhase.Final   => "Final scores",
            _                  => string.Empty
        };
    }

    private static string Cut(string text, int maxWidth)
    {
        if (TextRenderer.MeasureWidth(text) <= maxWidth)
            return text;

        while ((text.Length > 0) && (TextRenderer.MeasureWidth(text + "...") > maxWidth))
            text = text[..^1];

        return text + "...";
    }

    private void SelectChoice(int slot)
    {
        if (Info is not { } info || (Info.Phase != PartyPhase.Voting))
            return;

        ChoicePage = Math.Clamp(ChoicePage, 0, Math.Max(0, (info.Choices.Count - 1) / CHOICE_ROWS));

        var shown = info.Choices.Skip(ChoicePage * CHOICE_ROWS).Take(CHOICE_ROWS).ToList();

        if ((slot < 0) || (slot >= shown.Count))
            return;

        SelectedChoice = shown[slot].Id;
        var at = info.Choices.FindIndex(choice => choice.Id == SelectedChoice);
        if (at >= 0)
            ChoicePage = at / CHOICE_ROWS;
        Refresh();
    }

    private bool Strikable(CollegePartyChoiceInfo? choice) => choice is { IsReal: false, IsDecoy: false };

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private void OnStop()
    {
        if (Info is { Phase: PartyPhase.Writing })
            Raise(PartyControlType.StopWriting);
    }

    private void OnNext()
    {
        if (Info is not { } info)
            return;

        if (info.Phase == PartyPhase.Waiting)
            Raise(PartyControlType.Next);
        else if ((info.Phase == PartyPhase.Revealed) && (info.Kind != CollegeTool.QuipDuel))
            Raise(PartyControlType.Next);
    }

    private void OnStrike()
    {
        if (Info is not { Phase: PartyPhase.Voting } info)
            return;

        if (Strikable(info.Choices.FirstOrDefault(choice => choice.Id == SelectedChoice)))
            Raise(PartyControlType.Strike, SelectedChoice);
    }

    private void OnStrikeDrawing()
    {
        if (Info is { Phase: PartyPhase.Voting, Kind: CollegeTool.SketchGuess } && DrawingShown)
            Raise(PartyControlType.Strike, CollegeProtocol.PARTY_STRIKE_DRAWING);
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
        Raise(PartyControlType.End);
    }

    private void Raise(PartyControlType control, byte choice = CollegeProtocol.PARTY_NO_CHOICE)
        => Send?.Invoke(new CollegeActionArgs { Type = CollegeActionType.PartyControl, PartyControl = control, PartyChoice = choice });

    private void HideAll()
    {
        PausedLabel.Visible = false;
        PlayerPrevButton.Visible = false;
        PlayerNextButton.Visible = false;
        PlayerPageLabel.Visible = false;
        ChoicePrevButton.Visible = false;
        ChoiceNextButton.Visible = false;
        ChoicePageLabel.Visible = false;
        ScoresMoreLabel.Visible = false;
        ArmedLabel.Text = EndArmed ? END_ARMED : string.Empty;
        DrawingShown = false;

        foreach (var line in PromptLines)
            line.Visible = false;

        foreach (var line in EventLines)
            line.Visible = false;

        foreach (var line in TopLines)
            line.Visible = false;

        foreach (var line in ScoreLines)
            line.Visible = false;

        foreach (var row in PlayerRows)
        {
            row.Visible = false;
            row.Selected = false;
        }

        foreach (var row in ChoiceRows)
        {
            row.Visible = false;
            row.Selected = false;
        }

        StopButton.Visible = false;
        NextButton.Visible = false;
        StrikeButton.Visible = false;
        StrikeDrawingButton.Visible = false;
        SkipButton.Visible = false;
        EndButton.Visible = false;
    }

    private int ShowLines(IReadOnlyList<UILabel> lines, IReadOnlyList<string> texts, int y)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].Visible = i < texts.Count;

            if (i < texts.Count)
            {
                lines[i].Y = y;
                y += LINE_HEIGHT;
            }
        }

        return y;
    }

    private int ShowPlayers(CollegePartyInfo info, int y)
    {
        PlayerPage = Math.Clamp(PlayerPage, 0, Math.Max(0, (info.Players.Count - 1) / PLAYER_ROWS));

        var shown = info.Players.Skip(PlayerPage * PLAYER_ROWS).Take(PLAYER_ROWS).ToList();

        for (var i = 0; i < PLAYER_ROWS; i++)
        {
            var row = PlayerRows[i];
            row.Visible = i < shown.Count;

            if (i < shown.Count)
            {
                var player = shown[i];
                var done = (player.Given > 0) && (player.Answered >= player.Given);
                var color = player.Away ? LegendColors.Gray : done ? LegendColors.Lime : LegendColors.White;
                row.X = LEFT;
                row.Y = y + (i * CollegeListRow.HEIGHT);
                row.Set(i, (Cut(player.Name, NAME_COLUMN - 8), color), (ClassToolText.PartyPlayerLine(player), color));
            }
        }

        y += (shown.Count * CollegeListRow.HEIGHT) + 2;

        return ShowPager(PlayerPrevButton, PlayerNextButton, PlayerPageLabel, PlayerPage, info.Players.Count, PLAYER_ROWS, y);
    }

    private int ShowDrawing(int y)
    {
        DrawingShown = true;
        DrawingY = y;

        return y + DRAW_HEIGHT + 4;
    }

    private int ShowChoices(CollegePartyInfo info, int y, bool revealed)
    {
        ChoicePage = Math.Clamp(ChoicePage, 0, Math.Max(0, (info.Choices.Count - 1) / CHOICE_ROWS));

        var shown = info.Choices.Skip(ChoicePage * CHOICE_ROWS).Take(CHOICE_ROWS).ToList();

        for (var i = 0; i < CHOICE_ROWS; i++)
        {
            var row = ChoiceRows[i];
            row.Visible = i < shown.Count;

            if (i < shown.Count)
            {
                var choice = shown[i];
                var color = choice.IsReal ? LegendColors.Gold : LegendColors.White;
                var text = Cut(choice.IsReal ? $"{choice.Text} (real)" : choice.Text, NAME_COLUMN - 8);
                var detail = revealed ? ResultDetail(choice) : Cut(string.Join(", ", choice.Authors), INNER_WIDTH - NAME_COLUMN - 8);
                row.Y = y + (i * CollegeListRow.HEIGHT);
                row.Set(choice.Id, (text, color), (detail, LegendColors.Gray));
                row.Selected = choice.Id == SelectedChoice;
            }
        }

        y += (shown.Count * CollegeListRow.HEIGHT) + 2;

        return ShowPager(ChoicePrevButton, ChoiceNextButton, ChoicePageLabel, ChoicePage, info.Choices.Count, CHOICE_ROWS, y);
    }

    private static string ResultDetail(CollegePartyChoiceInfo choice)
    {
        var votes = choice.Votes == 1 ? "1 vote" : $"{choice.Votes} votes";

        return choice.Voters.Count == 0 ? votes : $"{votes} ({string.Join(", ", choice.Voters)})";
    }

    private int ShowScores(CollegePartyInfo info, int y)
    {
        var shown = info.Scores.Take(SCORE_ROWS).ToList();

        for (var i = 0; i < SCORE_ROWS; i++)
        {
            ScoreLines[i].Visible = i < shown.Count;

            if (i < shown.Count)
            {
                ScoreLines[i].Text = ClassToolText.FinalLine(i + 1, shown[i]);
                ScoreLines[i].Y = y;
                y += LINE_HEIGHT;
            }
        }

        var hidden = info.Scores.Count - shown.Count;
        ScoresMoreLabel.Visible = hidden > 0;

        if (hidden > 0)
        {
            ScoresMoreLabel.Text = $"+{hidden} more";
            ScoresMoreLabel.Y = y;
            y += LINE_HEIGHT;
        }

        return y;
    }

    private int ShowPager(CustomButton prev, CustomButton next, UILabel label, int page, int count, int pageSize, int y)
    {
        var pages = Math.Max(1, (count + pageSize - 1) / pageSize);

        var multi = pages > 1;
        prev.Visible = multi;
        next.Visible = multi;
        label.Visible = multi;

        if (!multi)
            return y;

        prev.X = LEFT;
        prev.Y = y;
        prev.Enabled = page > 0;
        next.X = LEFT + ARROW_WIDTH + 4;
        next.Y = y;
        next.Enabled = page < (pages - 1);
        label.Text = $"Page {page + 1} of {pages}";
        label.Y = y + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);

        return y + CustomButton.HEIGHT + 2;
    }

    private void TurnPlayer(int by)
    {
        PlayerPage += by;
        Refresh();
    }

    private void TurnChoice(int by)
    {
        ChoicePage += by;
        Refresh();
    }

    private int Stack(CustomButton button, int y)
    {
        button.X = LEFT;
        button.Y = y;
        button.Visible = true;

        return y + CustomButton.HEIGHT + BUTTON_GAP;
    }

    private void Resize(int contentBottom)
    {
        var height = contentBottom - BUTTON_GAP + 4 + BORDER_BOTTOM_HEIGHT;

        if (Height != height)
        {
            if (OkButton is not null)
                OkButton.Y += height - Height;

            Height = height;
        }
    }

    private void Refresh()
    {
        if (Info is not { } info)
            return;

        CaptionLabel.Text = info.Phase == PartyPhase.Waiting ? KindName(info) : ClassToolText.PartyTitle(info);
        StatusLabel.Text = StatusText(info);

        if (!info.Choices.Any(choice => choice.Id == SelectedChoice))
            SelectedChoice = CollegeProtocol.PARTY_NO_CHOICE;

        HideAll();

        var y = STATUS_TOP + LINE_HEIGHT + 4;

        if (info.Paused)
        {
            PausedLabel.Visible = true;
            PausedLabel.Y = y;
            y += LINE_HEIGHT;
        }

        switch (info.Phase)
        {
            case PartyPhase.Waiting:
            case PartyPhase.Writing:
            case PartyPhase.Drawing:
                y = ShowPlayers(info, y);
                y = StackWriteButtons(info, y);

                break;
            case PartyPhase.Voting:
                y = ShowVoting(info, y);

                break;
            case PartyPhase.Revealed:
                y = ShowRevealed(info, y);

                break;
            case PartyPhase.Final:
                y = ShowScores(info, y);
                y = Stack(EndButton, y);

                break;
            default:
                break;
        }

        ArmedLabel.Y = y;
        y += LINE_HEIGHT;
        Resize(y);
    }

    private int StackWriteButtons(CollegePartyInfo info, int y)
    {
        var writing = info.Phase is PartyPhase.Writing or PartyPhase.Drawing;

        if (writing)
        {
            StopButton.Enabled = info.Phase == PartyPhase.Writing;
            y = Stack(StopButton, y);
        }

        NextButton.Caption = "Next round";
        NextButton.Enabled = info.Phase == PartyPhase.Waiting;
        y = Stack(NextButton, y);
        y = Stack(EndButton, y);

        return y;
    }

    private int ShowVoting(CollegePartyInfo info, int y)
    {
        if ((info.Kind == CollegeTool.SketchGuess) && (info.DrawingId != 0) && (info.DrawingId == ShownDrawingId) && (DrawingTexture is not null))
            y = ShowDrawing(y);

        if (info.Prompt.Length > 0)
            y = ShowLines(PromptLines, TextRenderer.WrapLines(info.Prompt, INNER_WIDTH).Take(PromptLines.Length).ToList(), y);

        y = ShowChoices(info, y, false);
        y = ShowLines(TopLines, TextRenderer.WrapLines(ClassToolText.TopLine(info.Scores.Take(5).ToList(), string.Empty), INNER_WIDTH).Take(TopLines.Length).ToList(), y);

        var selected = info.Choices.FirstOrDefault(choice => choice.Id == SelectedChoice);
        StrikeButton.Enabled = Strikable(selected);
        y = Stack(StrikeButton, y);

        if ((info.Kind == CollegeTool.SketchGuess) && DrawingShown)
        {
            StrikeDrawingButton.Enabled = true;
            y = Stack(StrikeDrawingButton, y);
        }

        SkipButton.Caption = info.Kind switch
        {
            CollegeTool.QuipDuel => "Skip this duel",
            CollegeTool.Bluff    => "Skip this question",
            _                    => "Skip this drawing"
        };
        SkipButton.Enabled = true;
        y = Stack(SkipButton, y);
        y = Stack(EndButton, y);

        return y;
    }

    private int ShowRevealed(CollegePartyInfo info, int y)
    {
        if ((info.Kind == CollegeTool.SketchGuess) && (info.DrawingId != 0) && (info.DrawingId == ShownDrawingId) && (DrawingTexture is not null))
            y = ShowDrawing(y);

        if (info.Prompt.Length > 0)
            y = ShowLines(PromptLines, TextRenderer.WrapLines(info.Prompt, INNER_WIDTH).Take(PromptLines.Length).ToList(), y);

        if (info.Event.Length > 0)
            y = ShowLines(EventLines, TextRenderer.WrapLines(info.Event, INNER_WIDTH).Take(EventLines.Length).ToList(), y);

        y = ShowChoices(info, y, true);
        y = ShowScores(info, y);

        if (info.Kind != CollegeTool.QuipDuel)
        {
            NextButton.Caption = info.Kind == CollegeTool.Bluff ? "Next question" : "Next drawing";
            NextButton.Enabled = true;
            y = Stack(NextButton, y);
        }

        y = Stack(EndButton, y);

        return y;
    }
}

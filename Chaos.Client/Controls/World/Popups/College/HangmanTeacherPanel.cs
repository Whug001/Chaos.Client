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
///     The class Teacher's Hangman panel at the top right of the view: the answer, the mask and misses, the player rows
///     (the turn in gold, removed players grey) and the buttons that run the game. It never takes keyboard focus, so the
///     Teacher can keep moving and talking.
/// </summary>
public sealed class HangmanTeacherPanel : GuildCloakDialogBase
{
    private const int WIDTH = 250;
    private const int HEIGHT = 306;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int ANSWER_TOP = 28;
    private const int MASK_TOP = ANSWER_TOP + LINE_HEIGHT;
    private const int ROWS_TOP = MASK_TOP + LINE_HEIGHT + 4;
    private const int ROWS = 8;
    private const int SCORE_X = INNER_WIDTH - 40;
    private const int MORE_TOP = ROWS_TOP + (ROWS * CollegeListRow.HEIGHT) + 2;
    private const int ARMED_TOP = MORE_TOP + LINE_HEIGHT;
    private const int BUTTONS_ROW_2 = HEIGHT - BORDER_BOTTOM_HEIGHT - 4 - CustomButton.HEIGHT;
    private const int BUTTONS_ROW_1 = BUTTONS_ROW_2 - CustomButton.HEIGHT - 4;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string END_ARMED = "Click End game again to end it.";

    private readonly CustomButton AddButtonView;
    private readonly CollegeListRow[] AddRows = new CollegeListRow[ROWS];
    private readonly UILabel AnswerLabel;
    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton EndButton;
    private readonly UILabel MaskLabel;
    private readonly UILabel MoreLabel;
    private readonly CustomButton NextButton;
    private readonly CustomButton RemoveButton;
    private readonly CollegeListRow[] Rows = new CollegeListRow[ROWS];
    private readonly CustomButton SkipButton;
    private bool AddOpen;
    private bool Dismissed;
    private bool EndArmed;
    private CollegeHangmanInfo? Hangman;
    private int AddScrollTop;
    private int ScrollTop;
    private string SelectedName = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    public HangmanTeacherPanel()
        : base("_nsett", false)
    {
        Name = "HangmanTeacherPanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        AnswerLabel = Caption(string.Empty, LEFT, ANSWER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Lime);
        MaskLabel = Caption(string.Empty, LEFT, MASK_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < ROWS; i++)
        {
            var slot = i;
            Rows[i] = NewRow(i, () => SelectRow(slot));
            AddRows[i] = NewRow(i, () => AddFrom(slot));
            AddRows[i].Visible = false;
        }

        MoreLabel = Caption(string.Empty, LEFT, MORE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        ArmedLabel = Caption(string.Empty, LEFT, ARMED_TOP, INNER_WIDTH, color: LegendColors.Gold);
        AddButtonView = AddButton("Add player", 80, LEFT, BUTTONS_ROW_1, ToggleAdd);
        RemoveButton = AddButton("Remove", 60, LEFT + 84, BUTTONS_ROW_1, OnRemove);
        SkipButton = AddButton("Skip turn", 70, LEFT + 148, BUTTONS_ROW_1, () => Raise(HangmanControlType.Skip));
        NextButton = AddButton("Next word", 90, LEFT, BUTTONS_ROW_2, OnNext);
        EndButton = AddButton("End game", 90, LEFT + 96, BUTTONS_ROW_2, OnEnd);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A HangmanBoard message for the Teacher. <paramref name="reopen" /> shows a panel the Teacher closed.</summary>
    public void Apply(CollegeHangmanInfo info, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (info.Phase == HangmanPhase.Ended)
        {
            Close();

            return;
        }

        if (Hangman is not { } shown || (shown.Phase != info.Phase) || (shown.Number != info.Number))
            EndArmed = false;

        Hangman = info;
        Countdown.Start(info.Phase == HangmanPhase.Solving ? info.SolveSecondsLeft : info.TurnSecondsLeft);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Hangman = null;
        Dismissed = false;
        EndArmed = false;
        AddOpen = false;
        SelectedName = string.Empty;
        ScrollTop = 0;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Hangman is { Phase: HangmanPhase.Open or HangmanPhase.Solving })
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            ShowRows();
        }

        base.Update(gameTime);
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (Hangman is not { } info)
            return;

        var count = AddOpen ? info.Addable.Count : info.Players.Count;

        if (count <= ROWS)
            return;

        if (AddOpen)
            AddScrollTop = Math.Clamp(AddScrollTop - Math.Sign(e.Delta), 0, count - ROWS);
        else
            ScrollTop = Math.Clamp(ScrollTop - Math.Sign(e.Delta), 0, count - ROWS);

        ShowRows();
        e.Handled = true;
    }

    private void AddFrom(int slot)
    {
        if (Hangman is not { } info || (AddScrollTop + slot >= info.Addable.Count))
            return;

        AddOpen = false;
        Raise(HangmanControlType.AddPlayer, info.Addable[AddScrollTop + slot]);
        Refresh();
    }

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private bool IsLastRevealed(CollegeHangmanInfo info) => (info.Phase == HangmanPhase.Revealed) && (info.Number >= info.Count);

    private CollegeListRow NewRow(int index, Action onClick)
    {
        var row = new CollegeListRow(INNER_WIDTH, [0, SCORE_X]) { X = LEFT, Y = ROWS_TOP + (index * CollegeListRow.HEIGHT) };
        row.Clicked += onClick;
        AddChild(row);

        return row;
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
        Raise(HangmanControlType.End);
    }

    private void OnNext()
    {
        if (Hangman is not { } info || (info.Phase is not (HangmanPhase.Waiting or HangmanPhase.Revealed)))
            return;

        Raise(IsLastRevealed(info) ? HangmanControlType.End : HangmanControlType.Next);
    }

    private void OnRemove()
    {
        if (Hangman is not { } info || (SelectedName.Length == 0))
            return;

        var player = info.Players.FirstOrDefault(p => p.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase));

        if (player is null or { Removed: true })
            return;

        Raise(HangmanControlType.RemovePlayer, player.Name);
    }

    private void Raise(HangmanControlType control, string name = "")
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.HangmanControl,
                HangmanControl = control,
                Names = name.Length == 0 ? [] : [name]
            });

    private void Refresh()
    {
        if (Hangman is not { } info)
            return;

        var waiting = info.Phase == HangmanPhase.Waiting;
        CaptionLabel.Text = $"Hangman {info.Number}/{info.Count} · {info.Mode}";
        AnswerLabel.Text = waiting ? string.Empty : info.Word;
        MaskLabel.Text = waiting ? "Start the first word when you are ready." : $"{info.Mask} · {ClassToolText.MissLine(info)}";
        ArmedLabel.Text = EndArmed ? END_ARMED : string.Empty;
        NextButton.Caption = waiting ? "First word" : IsLastRevealed(info) ? "Finish game" : "Next word";
        NextButton.Enabled = info.Phase is HangmanPhase.Waiting or HangmanPhase.Revealed;
        SkipButton.Enabled = info.Phase == HangmanPhase.Open;
        AddButtonView.Caption = AddOpen ? "Close list" : "Add player";
        ShowRows();
    }

    private void SelectRow(int slot)
    {
        if (Hangman is not { } info || (ScrollTop + slot >= info.Players.Count))
            return;

        SelectedName = info.Players[ScrollTop + slot].Name;
        ShowRows();
    }

    private void ShowRows()
    {
        if (Hangman is not { } info)
            return;

        ScrollTop = Math.Clamp(ScrollTop, 0, Math.Max(0, info.Players.Count - ROWS));
        AddScrollTop = Math.Clamp(AddScrollTop, 0, Math.Max(0, info.Addable.Count - ROWS));

        for (var i = 0; i < ROWS; i++)
        {
            Rows[i].Visible = !AddOpen;
            AddRows[i].Visible = AddOpen;

            var index = ScrollTop + i;

            if (index < info.Players.Count)
            {
                var player = info.Players[index];

                var color = player.Removed ? LegendColors.DimGray
                    : player.IsTurn && (info.Phase == HangmanPhase.Open) ? LegendColors.Gold
                    : player.LockedOut ? LegendColors.Gray
                    : LegendColors.White;

                var name = player.IsTurn && (info.Phase == HangmanPhase.Open) && !player.Removed ? ClassToolText.TurnLabel(player.Name, Countdown.Left) : player.Name;
                Rows[i].Set(index, (name, color), (player.Removed ? "-" : player.Score.ToString(), color));
                Rows[i].Selected = player.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Rows[i].Set(-1);
                Rows[i].Selected = false;
            }

            var addIndex = AddScrollTop + i;

            if (addIndex < info.Addable.Count)
                AddRows[i].Set(i, (info.Addable[addIndex], LegendColors.White));
            else if ((i == 0) && (info.Addable.Count == 0))
                AddRows[i].Set(-1, ("No one to add", LegendColors.Gray));
            else
                AddRows[i].Set(-1);

            AddRows[i].Selected = false;
        }

        var hidden = info.Players.Count - ROWS;
        MoreLabel.Text = AddOpen ? info.Addable.Count > ROWS ? "Click a name to add; scroll for more" : "Click a name to add them" : hidden > 0 ? $"{hidden} more - scroll the list" : "Click a name, then Remove";
    }

    private void ToggleAdd()
    {
        AddOpen = !AddOpen;
        AddScrollTop = 0;
        Refresh();
    }
}

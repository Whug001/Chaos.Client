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
///     The centred card for a debate's opening and final votes, for students only. In the final vote it also shows the
///     opening counts and who would win if the vote ended now. OK or Esc hides it for that vote.
/// </summary>
public sealed class DebateVoteCard : GuildCloakDialogBase
{
    private const int WIDTH = 300;
    private const int HEIGHT = 196;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int MOTION_TOP = 28;
    private const int MOTION_LINES = 3;
    private const int COUNTS_TOP = MOTION_TOP + (MOTION_LINES * LINE_HEIGHT) + 4;
    private const int OPENING_TOP = COUNTS_TOP + LINE_HEIGHT;
    private const int LEADING_TOP = OPENING_TOP + LINE_HEIGHT;
    private const int BUTTONS_TOP = LEADING_TOP + LINE_HEIGHT + 4;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton AgainstButton;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel CountsLabel;
    private readonly CustomButton ForButton;
    private readonly UILabel LeadingLabel;
    private readonly UILabel[] MotionLines = new UILabel[MOTION_LINES];
    private readonly UILabel OpeningLabel;
    private readonly CustomButton UndecidedButton;
    private CollegeDebateInfo? Debate;
    private DebatePhase? DismissedPhase;

    public event Action<CollegeActionArgs>? ActionRequested;

    public DebateVoteCard()
        : base("_nsett", false)
    {
        Name = "DebateVoteCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i] = Caption(string.Empty, LEFT, MOTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);

        CountsLabel = Caption(string.Empty, LEFT, COUNTS_TOP, INNER_WIDTH, HorizontalAlignment.Center);
        OpeningLabel = Caption(string.Empty, LEFT, OPENING_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        LeadingLabel = Caption(string.Empty, LEFT, LEADING_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        ForButton = AddButton("For", 70, LEFT, BUTTONS_TOP, () => PickSide(DebateSide.For));
        AgainstButton = AddButton("Against", 76, LEFT + 76, BUTTONS_TOP, () => PickSide(DebateSide.Against));
        UndecidedButton = AddButton("Undecided", 86, LEFT + 158, BUTTONS_TOP, () => PickSide(DebateSide.Undecided));
    }

    /// <summary>A DebateState message. Shows only to students, only during a vote.</summary>
    public void Apply(CollegeDebateInfo debate, bool reopen)
    {
        if (reopen)
            DismissedPhase = null;

        if (debate.IsTeacher || (debate.Phase is not (DebatePhase.Opening or DebatePhase.Final)))
        {
            Debate = null;
            DismissedPhase = null;
            Hide();

            return;
        }

        Debate = debate;
        Countdown.Start(debate.SecondsLeft);
        Refresh();

        if (!Visible && (DismissedPhase != debate.Phase))
            Show();
    }

    public void Close()
    {
        Debate = null;
        DismissedPhase = null;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Debate is { } debate)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            CaptionLabel.Text = ClassToolText.VoteCaption(debate.Phase, Countdown.Left);
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

    private void Dismiss()
    {
        DismissedPhase = Debate?.Phase;
        Hide();
    }

    private void PickSide(DebateSide side)
    {
        if (Debate is not { } debate)
            return;

        debate.MySide = side;
        Refresh();
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.DebateSide, Side = side });
    }

    private void Refresh()
    {
        if (Debate is not { } debate)
            return;

        CaptionLabel.Text = ClassToolText.VoteCaption(debate.Phase, Countdown.Left);
        var lines = TextRenderer.WrapLines($"\"{debate.Motion}\"", INNER_WIDTH);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == MOTION_LINES - 1) && (lines.Count > MOTION_LINES) ? lines[i] + "..."
                : lines[i];

        CountsLabel.Text = ClassToolText.Counts(debate);
        var final = debate.Phase == DebatePhase.Final;
        OpeningLabel.Text = final ? ClassToolText.Opening(debate) : string.Empty;
        LeadingLabel.Text = final ? "If it ended now: " + ClassToolText.ResultLine(ClassToolText.Leading(debate), debate) : string.Empty;
        ForButton.Selected = debate.MySide == DebateSide.For;
        AgainstButton.Selected = debate.MySide == DebateSide.Against;
        UndecidedButton.Selected = debate.MySide == DebateSide.Undecided;
    }
}

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
///     The debate panel at the top right of the view. Students pick a side and raise a hand; the Teacher's view lists the
///     raised hands, gives and takes back the floor, starts the final vote and picks the best speakers. It never takes
///     keyboard focus.
/// </summary>
public sealed class DebatePanel : GuildCloakDialogBase
{
    private const int WIDTH = 260;
    private const int STUDENT_HEIGHT = 242;
    private const int TEACHER_HEIGHT = 324;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int MOTION_TOP = 28;
    private const int MOTION_LINES = 4;
    private const int COUNTS_TOP = MOTION_TOP + (MOTION_LINES * LINE_HEIGHT) + 2;
    private const int FLOOR_TOP = COUNTS_TOP + LINE_HEIGHT + 4;
    private const int ROW_TOP = FLOOR_TOP + LINE_HEIGHT + 8;
    private const int HAND_TOP = ROW_TOP + CustomButton.HEIGHT + 4;
    private const int HINT_TOP = HAND_TOP + CustomButton.HEIGHT + 4;
    private const int HAND_ROWS = 3;
    private const int HAND_STEP = CustomButton.HEIGHT + 4;
    private const int HANDS_TOP = ROW_TOP + LINE_HEIGHT + 2;
    private const int MORE_TOP = HANDS_TOP + (HAND_ROWS * HAND_STEP);
    private const int SPEAKER_COLUMNS = 2;
    private const int SPEAKER_ROWS = 4;
    private const int SPEAKER_STEP = 22;
    private const int SPEAKER_WIDTH = INNER_WIDTH / SPEAKER_COLUMNS;
    private const int TEACHER_BUTTONS_TOP = TEACHER_HEIGHT - BORDER_BOTTOM_HEIGHT - 4 - CustomButton.HEIGHT;
    private const int ARMED_TOP = TEACHER_BUTTONS_TOP - LINE_HEIGHT - 2;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton AgainstButton;
    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly CustomButton ConfirmButton;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel CountsLabel;
    private readonly CustomButton EndButton;
    private readonly CustomButton FinalButton;
    private readonly UILabel FloorLabel;
    private readonly CustomButton ForButton;
    private readonly CustomButton HandButton;
    private readonly UILabel[] HandNames = new UILabel[HAND_ROWS];
    private readonly CustomButton[] GiveButtons = new CustomButton[HAND_ROWS];
    private readonly UILabel HandsLabel;
    private readonly UILabel HintLabel;
    private readonly UILabel MoreLabel;
    private readonly UILabel[] MotionLines = new UILabel[MOTION_LINES];
    private readonly CustomCheckBox[] SpeakerBoxes = new CustomCheckBox[SPEAKER_COLUMNS * SPEAKER_ROWS];
    private readonly List<UIElement> StudentView = [];
    private readonly CustomButton TakeBackButton;
    private readonly List<UIElement> TeacherView = [];
    private readonly CustomButton UndecidedButton;
    private CollegeDebateInfo? Debate;
    private bool Dismissed;
    private bool EndArmed;

    public event Action<CollegeActionArgs>? ActionRequested;

    public DebatePanel()
        : base("_nsett", false)
    {
        Name = "DebatePanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = TEACHER_HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i] = Caption(string.Empty, LEFT, MOTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        CountsLabel = Caption(string.Empty, LEFT, COUNTS_TOP, INNER_WIDTH);
        FloorLabel = Caption(string.Empty, LEFT, FLOOR_TOP, INNER_WIDTH - 72, color: LegendColors.Gold);

        //student view
        ForButton = AddButton("For", 66, LEFT, ROW_TOP, () => PickSide(DebateSide.For));
        AgainstButton = AddButton("Against", 72, LEFT + 70, ROW_TOP, () => PickSide(DebateSide.Against));
        UndecidedButton = AddButton("Undecided", 82, LEFT + 146, ROW_TOP, () => PickSide(DebateSide.Undecided));
        HandButton = AddButton("Raise hand", 100, LEFT, HAND_TOP, ToggleHand);
        HintLabel = Caption(string.Empty, LEFT, HINT_TOP, INNER_WIDTH, color: LegendColors.Gray);
        StudentView.AddRange([ForButton, AgainstButton, UndecidedButton, HandButton, HintLabel]);

        //teacher view
        TakeBackButton = AddButton("Take back", 68, LEFT + INNER_WIDTH - 68, FLOOR_TOP - 5, () => Raise(DebateControlType.TakeBack));
        HandsLabel = Caption("Hands up:", LEFT, ROW_TOP, INNER_WIDTH, color: LegendColors.Gold);
        TeacherView.AddRange([TakeBackButton, HandsLabel]);

        for (var i = 0; i < HAND_ROWS; i++)
        {
            var index = i;
            var y = HANDS_TOP + (i * HAND_STEP);
            HandNames[i] = Caption(string.Empty, LEFT, y + 5, INNER_WIDTH - 90);
            GiveButtons[i] = AddButton("Give floor", 84, LEFT + INNER_WIDTH - 84, y, () => GiveFloor(index));
            TeacherView.AddRange([HandNames[i], GiveButtons[i]]);
        }

        MoreLabel = Caption(string.Empty, LEFT, MORE_TOP, 100, color: LegendColors.Gray);
        ArmedLabel = Caption(string.Empty, LEFT, ARMED_TOP, INNER_WIDTH, color: LegendColors.Gold);
        TeacherView.AddRange([MoreLabel, ArmedLabel]);

        for (var i = 0; i < SpeakerBoxes.Length; i++)
        {
            var box = new CustomCheckBox
            {
                X = LEFT + ((i % SPEAKER_COLUMNS) * SPEAKER_WIDTH),
                Y = HANDS_TOP + ((i / SPEAKER_COLUMNS) * SPEAKER_STEP),
                Width = SPEAKER_WIDTH - 4,
                Height = CustomCheckBox.CHECKBOX_SIZE
            };

            box.Clicked += () => ToggleSpeaker(box);
            AddChild(box);
            SpeakerBoxes[i] = box;
            TeacherView.Add(box);
        }

        FinalButton = AddButton("Final vote", 90, LEFT, TEACHER_BUTTONS_TOP, () => Raise(DebateControlType.FinalVote));
        ConfirmButton = AddButton("Confirm", 90, LEFT, TEACHER_BUTTONS_TOP, ConfirmSpeakers);
        EndButton = AddButton("End debate", 90, LEFT + 96, TEACHER_BUTTONS_TOP, OnEnd);
        TeacherView.AddRange([FinalButton, ConfirmButton, EndButton]);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A DebateState message. <paramref name="reopen" /> shows a panel the player closed.</summary>
    public void Apply(CollegeDebateInfo debate, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (debate.Phase == DebatePhase.Ended)
        {
            Close();

            return;
        }

        var phaseChanged = Debate?.Phase != debate.Phase;
        Debate = debate;
        Countdown.Start(debate.Phase is DebatePhase.Opening or DebatePhase.Final ? debate.SecondsLeft : debate.FloorSecondsLeft);

        if (phaseChanged)
        {
            EndArmed = false;

            foreach (var box in SpeakerBoxes)
                box.Checked = false;
        }

        Resize(debate.IsTeacher ? TEACHER_HEIGHT : STUDENT_HEIGHT);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Debate = null;
        Dismissed = false;
        EndArmed = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Debate is { } debate)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            FloorLabel.Text = FloorText(debate);
        }

        base.Update(gameTime);
    }

    private string FloorText(CollegeDebateInfo debate)
        => debate.Phase switch
        {
            DebatePhase.Opening or DebatePhase.Final => ClassToolText.VoteCaption(debate.Phase, Countdown.Left),
            DebatePhase.Result                       => ClassToolText.ResultLine(debate.Result, debate),
            _                                        => ClassToolText.FloorLine(debate.Floor, Countdown.Left)
        };

    private void Resize(int height)
    {
        if (Height == height)
            return;

        //the OK button was placed from the bottom edge, so it moves with it
        if (OkButton is not null)
            OkButton.Y += height - Height;

        Height = height;
    }

    private void Refresh()
    {
        if (Debate is not { } debate)
            return;

        var teacher = debate.IsTeacher;
        CaptionLabel.Text = teacher ? "Debate (Teacher)" : "Debate";
        var lines = TextRenderer.WrapLines($"\"{debate.Motion}\"", INNER_WIDTH);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == MOTION_LINES - 1) && (lines.Count > MOTION_LINES) ? lines[i] + "..."
                : lines[i];

        CountsLabel.Text = teacher && (debate.Phase != DebatePhase.Opening) ? ClassToolText.CountsWithGains(debate) : ClassToolText.Counts(debate);
        FloorLabel.Text = FloorText(debate);

        foreach (var element in StudentView)
            element.Visible = !teacher;

        foreach (var element in TeacherView)
            element.Visible = teacher;

        if (teacher)
            RefreshTeacher(debate);
        else
            RefreshStudent(debate);
    }

    private void RefreshStudent(CollegeDebateInfo debate)
    {
        var voting = debate.Phase is DebatePhase.Opening or DebatePhase.Floor or DebatePhase.Final;
        ForButton.Selected = debate.MySide == DebateSide.For;
        AgainstButton.Selected = debate.MySide == DebateSide.Against;
        UndecidedButton.Selected = debate.MySide == DebateSide.Undecided;
        ForButton.Enabled = AgainstButton.Enabled = UndecidedButton.Enabled = voting;
        HandButton.Caption = debate.MyHand ? "Lower hand" : "Raise hand";
        HandButton.Enabled = debate.Phase == DebatePhase.Floor;
        HintLabel.Text = (debate.Phase == DebatePhase.Floor) && (debate.Floor.Length > 0) ? $"{debate.Floor} has the floor." : string.Empty;
    }

    private void RefreshTeacher(CollegeDebateInfo debate)
    {
        var onFloor = debate.Phase == DebatePhase.Floor;
        var picking = (debate.Phase == DebatePhase.Result) && debate.Bonus;
        TakeBackButton.Visible = onFloor && (debate.Floor.Length > 0);
        HandsLabel.Text = picking ? "Pick up to 3 best speakers:" : "Hands up:";
        HandsLabel.Visible = onFloor || picking;

        for (var i = 0; i < HAND_ROWS; i++)
        {
            var shown = onFloor && (i < debate.Hands.Count);
            HandNames[i].Visible = shown;
            GiveButtons[i].Visible = shown;
            HandNames[i].Text = shown ? debate.Hands[i] : string.Empty;
        }

        MoreLabel.Text = onFloor ? ClassToolText.HandsMore(HAND_ROWS, debate.Hands.Count) : string.Empty;

        for (var i = 0; i < SpeakerBoxes.Length; i++)
        {
            var shown = picking && (i < debate.Speakers.Count);
            SpeakerBoxes[i].Visible = shown;
            SpeakerBoxes[i].Text = shown ? debate.Speakers[i] : string.Empty;
        }

        FinalButton.Visible = !picking;
        FinalButton.Enabled = onFloor;
        ConfirmButton.Visible = picking;
        ArmedLabel.Text = EndArmed ? "Click again to end." : string.Empty;
    }

    private void Dismiss()
    {
        Dismissed = true;
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

    private void ToggleHand()
    {
        if (Debate is not { Phase: DebatePhase.Floor } debate)
            return;

        //the server refuses a hand from the floor holder, so the button waits for the next DebateState instead of guessing
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.DebateHand, Raised = !debate.MyHand });
    }

    private void GiveFloor(int index)
    {
        if (Debate is not { } debate || (index >= debate.Hands.Count))
            return;

        Raise(DebateControlType.GiveFloor, [debate.Hands[index]]);
    }

    private void ToggleSpeaker(CustomCheckBox box)
    {
        if (!box.Checked && (SpeakerBoxes.Count(b => b.Checked) >= CollegeProtocol.MAX_BONUS_STUDENTS))
            return;

        box.Checked = !box.Checked;
    }

    private void ConfirmSpeakers()
        => Raise(DebateControlType.PickSpeakers, SpeakerBoxes.Where(b => b.Visible && b.Checked).Select(b => b.Text).ToList());

    private void OnEnd()
    {
        if (!EndArmed)
        {
            EndArmed = true;
            ArmedLabel.Text = "Click again to end.";

            return;
        }

        EndArmed = false;
        ArmedLabel.Text = string.Empty;
        Raise(DebateControlType.End);
    }

    private void Raise(DebateControlType control, List<string>? names = null)
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.DebateControl,
                DebateControl = control,
                Names = names ?? []
            });
}

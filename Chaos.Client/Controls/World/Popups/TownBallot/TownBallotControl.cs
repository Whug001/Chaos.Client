#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.TownBallot;

/// <summary>
///     The ballot window ("Mileth Election"): the candidates on the left, and on the right the selected candidate's figure,
///     level and class, guild and emblem, citizen date, past terms and statement, with the two-click vote button (voting
///     stage) or the statement editor (candidacy stage, own entry). Opened by the server's TownBallot Open from the ballot
///     box or the clerk. Every rule is checked again on the server.
/// </summary>
/// <remarks>
///     Layout, top to bottom: title, stage line, then the list (left, 8 rows of 34 px) beside the detail column (right: the
///     figure with the detail lines to its right, then the statement). Under the taller column: the page row, the note line
///     and the action buttons, then the frame's bottom border with Close. The statement box shows 9 lines of text, which
///     holds 400 characters at this width.
/// </remarks>
public sealed class TownBallotControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int LEFT = 20;
    private const int GAP = 10;
    private const int TITLE_TOP = 10;
    private const int STAGE_TOP = 30;
    private const int LIST_TOP = 52;
    private const int LIST_WIDTH = 180;
    private const int ROW_HEIGHT = TownBallotRow.HEIGHT;
    private const int RIGHT_WIDTH = 360;
    private const int FIGURE_WIDTH = 96;
    private const int FIGURE_HEIGHT = 128;
    private const int STATEMENT_LINES = 9;
    private const int SMALL_BUTTON = 28;
    private const int ACTION_WIDTH = 170;
    private const int BORDER_GAP = 4;

    private readonly CustomButton CancelButton;
    private readonly UILabel CitizenLabel;
    private readonly UILabel ClassLabel;
    private readonly CustomButton EditButton;
    private readonly UITextBox Editor;
    private readonly EmblemIcon Emblem;
    private readonly UILabel EmptyLabel;
    private readonly UILabel GuildLabel;
    private readonly UILabel NameLabel;
    private readonly CustomButton NextPageButton;
    private readonly UILabel NoteLabel;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly GuildCloakPreview Preview;
    private readonly TownBallotRow[] Rows = new TownBallotRow[TownBallotState.PAGE_SIZE];
    private readonly CustomButton SaveButton;
    private readonly UILabel StageLabel;
    private readonly UILabel StatementLabel;
    private readonly TownBallotState State = new();
    private readonly UILabel TermsLabel;
    private readonly UILabel TitleLabel;
    private readonly CustomButton VoteButton;

    public TownBallotControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        Name = "TownBallot";
        Visible = false;
        UsesControlStack = true;

        var rightLeft = LEFT + LIST_WIDTH + GAP;
        var detailLeft = rightLeft + FIGURE_WIDTH + GAP;
        var detailWidth = RIGHT_WIDTH - FIGURE_WIDTH - GAP;
        var statementTop = LIST_TOP + FIGURE_HEIGHT + 8;
        var statementHeight = (STATEMENT_LINES * TextRenderer.CHAR_HEIGHT) + 2;
        var listBottom = LIST_TOP + (TownBallotState.PAGE_SIZE * ROW_HEIGHT);
        var contentBottom = Math.Max(listBottom, statementTop + statementHeight);
        var pageTop = contentBottom + 6;
        var noteTop = pageTop + CustomButton.HEIGHT + 6;
        var actionTop = noteTop + TextRenderer.CHAR_HEIGHT + 6;

        Width = rightLeft + RIGHT_WIDTH + LEFT;
        Height = actionTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        var fullWidth = Width - (2 * LEFT);
        TitleLabel = Caption("Election", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        StageLabel = Caption(string.Empty, LEFT, STAGE_TOP, fullWidth, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < TownBallotState.PAGE_SIZE; i++)
        {
            var slot = i;

            var row = new TownBallotRow(LIST_WIDTH)
            {
                X = LEFT,
                Y = LIST_TOP + (i * ROW_HEIGHT),
                Visible = false
            };

            row.Clicked += () => SelectSlot(slot);
            AddChild(row);
            Rows[i] = row;
        }

        EmptyLabel = Caption("No one is running.", LEFT, LIST_TOP + 60, LIST_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, pageTop, () => TurnPage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * SMALL_BUTTON) + 10, pageTop + 5, LIST_WIDTH - (2 * SMALL_BUTTON) - 10, color: LegendColors.Gray);

        Preview = new GuildCloakPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT)
        {
            X = rightLeft,
            Y = LIST_TOP
        };

        AddChild(Preview);

        var line = TextRenderer.CHAR_HEIGHT + 4;
        NameLabel = Caption(string.Empty, detailLeft, LIST_TOP, detailWidth, color: LegendColors.Gold);
        ClassLabel = Caption(string.Empty, detailLeft, LIST_TOP + line, detailWidth);

        Emblem = new EmblemIcon
        {
            X = detailLeft,
            Y = LIST_TOP + (2 * line),
            Width = 11,
            Height = TextRenderer.CHAR_HEIGHT
        };

        AddChild(Emblem);
        GuildLabel = Caption(string.Empty, detailLeft + 15, LIST_TOP + (2 * line), detailWidth - 15);
        CitizenLabel = Caption(string.Empty, detailLeft, LIST_TOP + (3 * line), detailWidth, color: LegendColors.Gray);
        TermsLabel = Caption(string.Empty, detailLeft, LIST_TOP + (4 * line), detailWidth, color: LegendColors.Gray);

        StatementLabel = new UILabel
        {
            X = rightLeft,
            Y = statementTop,
            Width = RIGHT_WIDTH,
            Height = statementHeight,
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        AddChild(StatementLabel);

        Editor = new UITextBox
        {
            X = rightLeft,
            Y = statementTop,
            Width = RIGHT_WIDTH,
            Height = statementHeight,
            IsMultiLine = true,
            IsSelectable = true,
            MaxLength = TownBallotProtocol.MAX_STATEMENT_CHARS,
            ForegroundColor = TextColors.Default,
            IsTabStop = true,
            Visible = false
        };

        AddChild(Editor);

        NoteLabel = Caption(string.Empty, LEFT, noteTop, fullWidth);

        var actionLeft = rightLeft + RIGHT_WIDTH - ACTION_WIDTH;
        VoteButton = AddButton(string.Empty, ACTION_WIDTH, actionLeft, actionTop, OnVoteClicked);
        EditButton = AddButton("Edit statement", ACTION_WIDTH, actionLeft, actionTop, OnEditClicked);
        SaveButton = AddButton("Save", (ACTION_WIDTH - 4) / 2, actionLeft, actionTop, OnSaveClicked);
        CancelButton = AddButton("Cancel", (ACTION_WIDTH - 4) / 2, actionLeft + ((ACTION_WIDTH + 4) / 2), actionTop, OnCancelClicked);
    }

    /// <summary>Raised with a vote (second click) or a statement to save.</summary>
    public event Action<TownBallotInteractionArgs>? InteractionRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        Editor.IsFocused = false;
        base.Hide();
        Preview.ReleaseFrames();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (State.Editing)
                OnCancelClicked();
            else
                Hide();

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Shows the window on the first page with the first candidate selected.</summary>
    public void Open(TownBallotArgs args)
    {
        State.Open(args);
        RefreshAll();
        Show();
    }

    /// <summary>Replaces the data after a vote or a save, if the window is open.</summary>
    public void Refresh(TownBallotArgs args)
    {
        if (!Visible)
            return;

        State.Apply(args);
        RefreshAll();
    }

    private void OnCancelClicked()
    {
        State.CancelEdit();
        RefreshAll();
    }

    private void OnEditClicked()
    {
        State.StartEdit();
        Editor.Text = State.Selected?.Statement ?? string.Empty;
        RefreshAll();
        Editor.IsFocused = true;
    }

    private void OnSaveClicked()
    {
        if (State.Save(Editor.Text) is { } save)
            InteractionRequested?.Invoke(save);
        else
            NoteLabel.Text = $"Write 1 to {TownBallotProtocol.MAX_STATEMENT_CHARS} characters.";
    }

    private void OnVoteClicked()
    {
        if (State.ClickVote() is { } vote)
            InteractionRequested?.Invoke(vote);

        RefreshActions();
    }

    private void RefreshActions()
    {
        var editing = State.Editing;

        VoteButton.Visible = State.ShowsVoteButton && !editing;
        VoteButton.Enabled = State.VoteEnabled;
        VoteButton.Caption = State.VoteCaption;

        EditButton.Visible = State.ShowsEditButton && !editing;
        SaveButton.Visible = editing;
        CancelButton.Visible = editing;

        Editor.Visible = editing;
        StatementLabel.Visible = !editing;

        if (!editing)
            Editor.IsFocused = false;
    }

    private void RefreshAll()
    {
        var args = State.Args;
        TitleLabel.Text = string.IsNullOrEmpty(args.TownName) ? "Election" : $"{args.TownName} Election";
        StageLabel.Text = args.StageLine;

        var items = State.PageItems;

        for (var i = 0; i < TownBallotState.PAGE_SIZE; i++)
        {
            var candidate = i < items.Count ? items[i] : null;
            Rows[i].SetCandidate(candidate);
            Rows[i].Selected = candidate is not null && (candidate.Name == State.Selected?.Name);
        }

        EmptyLabel.Text = args.Stage == TownBallotStage.Idle ? "No election is running." : "No one is running.";
        EmptyLabel.Visible = args.Candidates.Count == 0;
        PageLabel.Text = $"{State.Page + 1}/{State.Pages}";
        PrevPageButton.Enabled = State.Page > 0;
        NextPageButton.Enabled = State.Page < (State.Pages - 1);

        var selected = State.Selected;
        Preview.Visible = selected is not null;
        Preview.SetLook(selected is null ? null : TownBallotLook.ToAppearance(selected.Look));
        NameLabel.Text = selected?.Name ?? string.Empty;
        ClassLabel.Text = selected is null ? string.Empty : TownBallotState.ClassLine(selected);
        Emblem.GuildEmblemId = selected?.GuildEmblemId ?? 0;
        Emblem.Visible = (selected?.GuildEmblemId ?? 0) != 0;
        GuildLabel.Text = selected is null ? string.Empty : string.IsNullOrEmpty(selected.GuildName) ? "No guild" : selected.GuildName;
        CitizenLabel.Text = selected is null ? string.Empty : TownBallotState.CitizenLine(selected);
        TermsLabel.Text = selected is null ? string.Empty : TownBallotState.TermsLine(selected);
        StatementLabel.Text = selected is null ? string.Empty : string.IsNullOrEmpty(selected.Statement) ? "No statement yet." : selected.Statement;

        NoteLabel.Text = State.Note;
        RefreshActions();
    }

    private void SelectSlot(int slot)
    {
        var items = State.PageItems;

        if (slot < items.Count)
        {
            State.Select(items[slot].Name);
            RefreshAll();
        }
    }

    private void TurnPage(int delta)
    {
        State.TurnPage(delta);
        RefreshAll();
    }
}

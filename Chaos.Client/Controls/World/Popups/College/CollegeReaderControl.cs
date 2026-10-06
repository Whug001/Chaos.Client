#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The College reader: one piece, scrolling, with a footer that depends on who is reading (the server's Piece context):
///     a judge's vote, the Director's verdict, the gallery's author line, a Teacher's hand-in tools, or nothing.
/// </summary>
/// <remarks>
///     Layout, top to bottom: the title, the server's header line, the piece, then the footer, stacked up from the
///     frame's bottom border: the action row, the comment or note box and the tier row (Judge and Verdict only) and the
///     footer line. The piece takes whatever height the footer leaves.
/// </remarks>
public sealed class CollegeReaderControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 440;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int TITLE_TOP = 10;
    private const int HEADER_TOP = 26;
    private const int BODY_TOP = 44;
    private const int GAP = 4;
    private const int BUTTON_GAP = 6;
    private const int BORDER_GAP = 4;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - BORDER_GAP;
    private const int ACTIONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int TIER_WIDTH = 80;
    private const int COMMENT_LINES = 3;
    private const string REMOVE_CAPTION = "Remove entry";
    private const string CONFIRM_REMOVE_CAPTION = "Confirm remove";

    private readonly CustomTextBox CommentBox;
    private readonly List<CustomButton> FooterButtons = [];
    private readonly UILabel FooterLine;
    private readonly UILabel HeaderLabel;
    private readonly CustomButton[] TierButtons;
    private readonly UILabel TitleLabel;
    private readonly PieceView View;
    private readonly TextPopupControl VotesPopup;
    private readonly VoteDrafts VoteDrafts = new();

    private CollegeDisplayArgs? Current;
    private CustomButton? RemoveButton;
    private bool RemoveArmed;
    private CustomButton? SubmitButton;
    private byte SelectedTier = CollegeProtocol.NO_TIER;
    private int[] Siblings = [];

    //the vote the server has for the piece shown, so leaving it can tell whether the footer holds unsaved changes
    private string SavedComment = string.Empty;
    private byte SavedTier = CollegeProtocol.NO_TIER;

    public CollegeReaderControl(CollegePictureTransfers transfers, TextPopupControl votesPopup, TunePlayer player)
        : base("_nsett", false)
    {
        Name = "CollegeReader";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        VotesPopup = votesPopup;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        TitleLabel = Caption(string.Empty, LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        HeaderLabel = Caption(string.Empty, LEFT, HEADER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        View = new PieceView(transfers, player, INNER_WIDTH, ACTIONS_TOP - GAP - BODY_TOP)
        {
            X = LEFT,
            Y = BODY_TOP
        };

        AddChild(View);

        FooterLine = Caption(string.Empty, LEFT, ACTIONS_TOP, INNER_WIDTH);

        TierButtons = CollegeTiers.Votable
                                  .Select((tier, i) => AddButton(
                                      CollegeTiers.Name(tier),
                                      TIER_WIDTH,
                                      LEFT + (i * (TIER_WIDTH + GAP)),
                                      ACTIONS_TOP,
                                      () => SelectTier((byte)tier)))
                                  .ToArray();

        CommentBox = new CustomTextBox
        {
            X = LEFT,
            Y = ACTIONS_TOP,
            Width = INNER_WIDTH,
            Height = (COMMENT_LINES * TextRenderer.CHAR_HEIGHT) + 10,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_COMMENT_CHARS
        };

        AddChild(CommentBox);
    }

    /// <summary>Raised with a vote, a verdict, a removal, a "show to class" or a request to open the previous/next entry.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        CommentBox.IsFocused = false;
        View.StopTune();
        KeepUnsavedVote();
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Shows a piece with the footer for its context. Opening another piece replaces the one shown.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        //Prev, Next and a piece shown to the class replace the piece; a vote not yet saved waits for the judge to come back
        KeepUnsavedVote();
        Current = args;
        TitleLabel.Text = args.Piece?.Title is { Length: > 0 } title ? title : "Untitled";
        HeaderLabel.Text = args.Header;
        BuildFooter(args);
        View.Show(args.Piece ?? new CollegePieceInfo());
        Show();

        //a Teacher's "Show to class" plays the tune for the whole room as the piece arrives
        if (args.Context == CollegePieceContext.Shown)
            View.PlayTune();
    }

    /// <summary>The judging list's order, for Prev and Next.</summary>
    public void SetSiblings(IReadOnlyList<int> entryIds) => Siblings = entryIds.ToArray();

    private void BuildFooter(CollegeDisplayArgs args)
    {
        foreach (var button in FooterButtons)
        {
            Children.Remove(button);
            button.Dispose();
        }

        FooterButtons.Clear();
        SubmitButton = null;
        RemoveButton = null;
        RemoveArmed = false;
        CommentBox.IsFocused = false;

        switch (args.Context)
        {
            case CollegePieceContext.Judge:
                SubmitButton = FooterAction("Save vote", 100, () => Send(CollegeActionType.Vote));
                var at = Array.IndexOf(Siblings, args.Id);
                FooterAction("< Prev", 80, () => Step(-1)).Enabled = at > 0;
                FooterAction("Next >", 80, () => Step(1)).Enabled = (at >= 0) && (at < (Siblings.Length - 1));

                break;
            case CollegePieceContext.Verdict:
                SubmitButton = FooterAction("Post verdict", 110, () => Send(CollegeActionType.Verdict));
                FooterAction("Votes and comments", 150, ShowVotes);
                RemoveButton = FooterAction(REMOVE_CAPTION, 110, OnRemoveClicked);

                break;
            case CollegePieceContext.Gallery when args.CanModerate:
                RemoveButton = FooterAction(REMOVE_CAPTION, 110, OnRemoveClicked);

                break;
            case CollegePieceContext.HandIn:
                FooterAction("Show to class", 120, () => Send(CollegeActionType.ShowToClass));

                break;
        }

        var voting = args.Context is CollegePieceContext.Judge or CollegePieceContext.Verdict;
        var y = FooterButtons.Count > 0 ? ACTIONS_TOP - GAP : FOOTER_BOTTOM;

        CommentBox.Visible = voting;

        foreach (var button in TierButtons)
            button.Visible = voting;

        if (voting)
        {
            var verdict = args.Context == CollegePieceContext.Verdict;
            CommentBox.MaxLength = verdict ? CollegeProtocol.MAX_NOTE_CHARS : CollegeProtocol.MAX_COMMENT_CHARS;
            CommentBox.HintText = verdict ? "A note to the author (optional)" : "A comment for the Director (optional)";

            y -= CommentBox.Height;
            CommentBox.Y = y;
            y -= GAP + CustomButton.HEIGHT;

            foreach (var button in TierButtons)
                button.Y = y;

            y -= GAP;
        }

        SavedTier = args.MyTier;
        SavedComment = args.MyComment;

        if (voting)
        {
            var (tier, comment) = VoteDrafts.Restore(args.Context, args.Id, args.MyTier, args.MyComment);
            CommentBox.Text = comment;
            SelectTier(tier);
        } else
            SelectTier(CollegeProtocol.NO_TIER);

        FooterLine.Text = args.Context switch
        {
            CollegePieceContext.Judge   => "Your vote (comments go to the Director only):",
            CollegePieceContext.Verdict => $"Votes: {CollegeTiers.VoteSummary(args.Votes)}",
            CollegePieceContext.Gallery => $"by {args.Author}",
            _                           => string.Empty
        };

        FooterLine.Visible = FooterLine.Text.Length > 0;

        if (FooterLine.Visible)
        {
            y -= TextRenderer.CHAR_HEIGHT;
            FooterLine.Y = y;
            y -= GAP;
        }

        View.SetHeight(y - BODY_TOP);
    }

    private void KeepUnsavedVote()
    {
        if (Current is { Context: CollegePieceContext.Judge or CollegePieceContext.Verdict } current)
            VoteDrafts.Leave(current.Context, current.Id, SelectedTier, CommentBox.Text, SavedTier, SavedComment);
    }

    private CustomButton FooterAction(string caption, int width, Action onClick)
    {
        var x = LEFT + FooterButtons.Sum(b => b.Width + BUTTON_GAP);
        var button = AddButton(caption, width, x, ACTIONS_TOP, onClick);
        FooterButtons.Add(button);

        return button;
    }

    //removing an entry can't be undone from the window, so it takes a second click
    private void OnRemoveClicked()
    {
        if (!RemoveArmed)
        {
            RemoveArmed = true;
            RemoveButton!.Caption = CONFIRM_REMOVE_CAPTION;

            return;
        }

        Send(CollegeActionType.RemoveEntry);
    }

    private void SelectTier(byte tier)
    {
        SelectedTier = tier;

        for (var i = 0; i < TierButtons.Length; i++)
            TierButtons[i].Selected = (byte)CollegeTiers.Votable[i] == tier;

        SubmitButton?.Enabled = tier != CollegeProtocol.NO_TIER;
    }

    private void Send(CollegeActionType type)
    {
        if (Current is null)
            return;

        var voting = type is CollegeActionType.Vote or CollegeActionType.Verdict;

        if (voting && (SelectedTier == CollegeProtocol.NO_TIER))
            return;

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Id = Current.Id,
                Tier = voting ? (CollegeTierCode)SelectedTier : CollegeTierCode.None,
                Text = voting ? CommentBox.Text : string.Empty
            });

        if (voting)
        {
            VoteDrafts.Saved(Current.Context, Current.Id);
            SavedTier = SelectedTier;
            SavedComment = CommentBox.Text;
        }

        if (type is CollegeActionType.Verdict or CollegeActionType.RemoveEntry)
            Hide();
    }

    private void ShowVotes()
    {
        if (Current is not null)
            VotesPopup.Show(CollegeTiers.VotesText(Current.Votes));
    }

    private void Step(int by)
    {
        if (Current is null)
            return;

        var at = Array.IndexOf(Siblings, Current.Id);
        var next = at + by;

        if ((at < 0) || (next < 0) || (next >= Siblings.Length))
            return;

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.OpenPiece,
                Source = CollegePieceSource.Entry,
                Id = Siblings[next]
            });
    }
}

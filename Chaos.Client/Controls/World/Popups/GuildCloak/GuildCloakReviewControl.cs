#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The admins' guild cloak review window: the waiting designs (oldest first, 8 per page), the selected design's front and
///     back, a walking preview, and Approve / Reject with a reason. Opened by the server's GuildCloakReviewList Open from the
///     admin trinket.
/// </summary>
/// <remarks>
///     Layout, left to right: the waiting list, the front canvas, the back canvas, the preview column. Below all of that (the
///     list's page row and the preview's turn/body row are the tallest columns) sits the submission info line, the reject
///     reason box, and Approve / Reject; under that is the frame's ornate bottom border with the Close button. As in the
///     editor window (Task 8), the window's height follows the tallest column rather than a fixed constant, so it always
///     clears <see cref="FramedDialogPanelBase.BORDER_BOTTOM_HEIGHT" />.
/// </remarks>
public sealed class GuildCloakReviewControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;

    //20, not 16: matches the editor's fix for the same "_nsett" frame's ornate side edges (Task 8)
    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int CONTENT_TOP = 44;
    private const int LIST_WIDTH = 120;
    private const int PAGE_SIZE = 8;
    private const int ZOOM = 4;
    private const int PREVIEW_WIDTH = 120;
    private const int PREVIEW_HEIGHT = 180;
    private const int SMALL_BUTTON = 28;
    private const int DECIDE_WIDTH = 80;
    private const int BOTTOM_ROW_GAP = 8;
    private const int BORDER_GAP = 4;

    private readonly CustomButton ApproveButton;
    private readonly GuildCloakCanvas BackCanvas;
    private readonly UILabel EmptyLabel;
    private readonly CustomButton[] EntryButtons = new CustomButton[PAGE_SIZE];
    private readonly GuildCloakCanvas FrontCanvas;
    private readonly UILabel InfoLabel;
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly GuildCloakPreview Preview;
    private readonly CustomTextBox ReasonBox;
    private readonly CustomButton RejectButton;
    private readonly AislingRenderer Renderer;

    private IReadOnlyList<GuildCloakReviewEntry> Entries = [];
    private int Page;
    private int PreviewDesignId;
    private int SelectedIndex = -1;

    public GuildCloakReviewControl(AislingRenderer renderer, GuildCloakReferences references)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Name = "GuildCloakReview";
        Visible = false;
        UsesControlStack = true;

        FrontCanvas = new GuildCloakCanvas(references, GuildCloakView.Front, ZOOM)
        {
            X = LEFT + LIST_WIDTH + GAP,
            Y = CONTENT_TOP,
            ReadOnly = true
        };

        BackCanvas = new GuildCloakCanvas(references, GuildCloakView.Back, ZOOM)
        {
            X = FrontCanvas.X + FrontCanvas.Width + GAP,
            Y = CONTENT_TOP,
            ReadOnly = true
        };

        var previewLeft = BackCanvas.X + BackCanvas.Width + GAP;
        var pageTop = CONTENT_TOP + (PAGE_SIZE * (CustomButton.HEIGHT + 3)) + 4;
        var turnTop = CONTENT_TOP + PREVIEW_HEIGHT + 4;

        //the tallest of the three columns decides where the info/reason/decide row starts: the list (page row),
        //the canvases, or the preview (turn/body row). The brief placed this row right under the canvases alone,
        //which the list's page row and the preview's turn row both run past (see task-9-report.md).
        var contentBottom = Math.Max(
            Math.Max(pageTop + CustomButton.HEIGHT, turnTop + CustomButton.HEIGHT),
            Math.Max(FrontCanvas.Y + FrontCanvas.Height, BackCanvas.Y + BackCanvas.Height));

        var infoTop = contentBottom + BOTTOM_ROW_GAP;
        var reasonTop = infoTop + TextRenderer.CHAR_HEIGHT + 6;
        var decideTop = reasonTop + CustomButton.HEIGHT + 6;

        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = decideTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Guild Cloak Review", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("WAITING", LEFT, CAPTION_TOP, LIST_WIDTH, color: LegendColors.Gray);
        Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;

            EntryButtons[i] = AddButton(
                string.Empty,
                LIST_WIDTH,
                LEFT,
                CONTENT_TOP + (i * (CustomButton.HEIGHT + 3)),
                () => Select((Page * PAGE_SIZE) + slot));
        }

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, pageTop, () => TurnPage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * SMALL_BUTTON) + 10, pageTop + 5, LIST_WIDTH - (2 * SMALL_BUTTON) - 10, color: LegendColors.Gray);

        AddChild(FrontCanvas);
        AddChild(BackCanvas);

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = CONTENT_TOP
        };

        AddChild(Preview);

        AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", SMALL_BUTTON, previewLeft + SMALL_BUTTON + 4, turnTop, () => Preview.Turn(1));
        AddButton("Body", 50, previewLeft + PREVIEW_WIDTH - 50, turnTop, Preview.ToggleBody);

        EmptyLabel = Caption(
            "No designs are waiting.",
            FrontCanvas.X,
            CONTENT_TOP + 60,
            (BackCanvas.X + BackCanvas.Width) - FrontCanvas.X,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        var infoWidth = previewLeft - FrontCanvas.X - GAP;
        InfoLabel = Caption(string.Empty, FrontCanvas.X, infoTop, infoWidth);

        ReasonBox = new CustomTextBox
        {
            X = FrontCanvas.X,
            Y = reasonTop,
            Width = infoWidth,
            Height = CustomButton.HEIGHT,
            MaxLength = GuildCloakProtocol.MAX_REASON_CHARS,
            HintText = "Reason for rejecting"
        };

        AddChild(ReasonBox);

        ApproveButton = AddButton("Approve", DECIDE_WIDTH, FrontCanvas.X, decideTop, () => Decide(GuildCloakReviewAction.Approve));

        RejectButton = AddButton(
            "Reject",
            DECIDE_WIDTH,
            FrontCanvas.X + DECIDE_WIDTH + GAP,
            decideTop,
            () => Decide(GuildCloakReviewAction.Reject));
    }

    private GuildCloakReviewEntry? Selected
        => (SelectedIndex >= 0) && (SelectedIndex < Entries.Count) ? Entries[SelectedIndex] : null;

    /// <summary>Raised when the admin approves or rejects the selected design.</summary>
    public event Action<GuildCloakReviewInteractionArgs>? DecisionRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        //hiding resets the children's interaction state (UIPanel.ResetInteractionState cascades on Visible=false),
        //which drops the reason box's focus the same way the editor's picker and canvases reset on hide
        base.Hide();
        Preview.ReleaseFrames();
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

    /// <summary>Shows the window with a fresh list.</summary>
    public void Open(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        SetEntries(entries);
        Show();
    }

    /// <summary>Replaces the list after a decision, if the window is open.</summary>
    public void Refresh(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        if (Visible)
            SetEntries(entries);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var hasEntry = Selected is not null;
        ApproveButton.Enabled = hasEntry;
        RejectButton.Enabled = hasEntry && ReasonBox.Text.Trim().Length is >= 1 and <= GuildCloakProtocol.MAX_REASON_CHARS;
    }

    private void Decide(GuildCloakReviewAction action)
    {
        if (Selected is not { } entry)
            return;

        var reason = ReasonBox.Text.Trim();

        if ((action == GuildCloakReviewAction.Reject) && (reason.Length == 0))
            return;

        DecisionRequested?.Invoke(
            new GuildCloakReviewInteractionArgs
            {
                Action = action,
                SubmissionId = entry.SubmissionId,
                Reason = action == GuildCloakReviewAction.Reject ? reason : string.Empty
            });
    }

    private void RefreshList()
    {
        var pages = Math.Max(1, (Entries.Count + PAGE_SIZE - 1) / PAGE_SIZE);
        Page = Math.Clamp(Page, 0, pages - 1);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (Page * PAGE_SIZE) + i;
            var button = EntryButtons[i];
            button.Visible = index < Entries.Count;

            if (!button.Visible)
                continue;

            button.Caption = Entries[index].GuildName;
            button.Selected = index == SelectedIndex;
        }

        PageLabel.Text = $"{Page + 1}/{pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);
    }

    private void Select(int index)
    {
        if (index >= Entries.Count)
            return;

        SelectedIndex = index;
        ReasonBox.Text = string.Empty;

        var entry = Selected;
        FrontCanvas.Visible = entry is not null;
        BackCanvas.Visible = entry is not null;
        Preview.Visible = entry is not null;
        EmptyLabel.Visible = entry is null;
        InfoLabel.Text = entry is null ? string.Empty : $"{entry.GuildName}, by {entry.LeaderName}, {entry.SubmittedAtUtc:yyyy-MM-dd HH:mm} UTC";

        if (entry is not null)
        {
            FrontCanvas.SetDesign(entry.Design);
            BackCanvas.SetDesign(entry.Design);
            PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, entry.Design.DeepCopy());
            Preview.SetDesignId(PreviewDesignId);
        }

        RefreshList();
    }

    private void SetEntries(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        var keep = Selected?.SubmissionId;
        Entries = entries;
        var index = -1;

        for (var i = 0; i < entries.Count; i++)
            if (entries[i].SubmissionId == keep)
                index = i;

        if ((index < 0) && (entries.Count > 0))
            index = 0;

        Page = index < 0 ? 0 : index / PAGE_SIZE;
        Select(index);
    }

    private void TurnPage(int delta)
    {
        Page += delta;
        RefreshList();
    }
}

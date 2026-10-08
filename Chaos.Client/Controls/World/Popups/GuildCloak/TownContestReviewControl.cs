#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.GuildEmblem;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The town contest review window: the entries of a contest for the town's cape, banner or song. The mayor picks one
///     (Pick mode); an admin then approves or rejects that pick with a reason (Approve mode). A cape shows its front, back
///     and a walking preview, a banner shows enlarged with its small sizes, and a song plays. Opened by the server's
///     TownContestDisplay Review from the town clerk.
/// </summary>
/// <remarks>
///     The layout is the guild design review window's: the entry list on the left, the design in the middle and right, and
///     below them the entry line, the reason box (Approve mode) and the decision buttons.
/// </remarks>
public sealed class TownContestReviewControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int CONTENT_TOP = 44;
    private const int LIST_WIDTH = 140;
    private const int PAGE_SIZE = 8;
    private const int ZOOM = 4;
    private const int PREVIEW_WIDTH = 120;
    private const int PREVIEW_HEIGHT = 180;
    private const int SMALL_BUTTON = 28;
    private const int DECIDE_WIDTH = 80;
    private const int PLAY_WIDTH = 80;
    private const int BOTTOM_ROW_GAP = 8;
    private const int BORDER_GAP = 4;

    private readonly CustomButton ApproveButton;
    private readonly GuildCloakCanvas BackCanvas;
    private readonly UILabel BackCaption;
    private readonly UIElement[] CloakViews;
    private readonly GuildEmblemCanvas EmblemCanvas;
    private readonly GuildEmblemPreviewStrip EmblemPreview;
    private readonly UILabel EmptyLabel;
    private readonly CustomButton[] EntryButtons = new CustomButton[PAGE_SIZE];
    private readonly GuildCloakCanvas FrontCanvas;
    private readonly UILabel FrontCaption;
    private readonly UILabel InfoLabel;
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PickButton;
    private readonly CustomButton PlayButton;
    private readonly TunePlayer Player;
    private readonly CustomButton PrevPageButton;
    private readonly GuildCloakPreview Preview;
    private readonly UILabel PreviewCaption;
    private readonly CustomTextBox ReasonBox;
    private readonly CustomButton RejectButton;
    private readonly AislingRenderer Renderer;
    private readonly UILabel SongLabel;
    private readonly UILabel ThemeLabel;
    private readonly UILabel TitleLabel;

    private TownContestDisplayArgs? Contest;
    private IReadOnlyList<TownContestEntryInfo> Entries = [];
    private int EmblemPreviewId;
    private int Page;
    private int PreviewDesignId;
    private int SelectedIndex = -1;

    public TownContestReviewControl(AislingRenderer renderer, GuildCloakReferences references, TunePlayer player)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Player = player;
        Name = "TownContestReview";
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
        var stepTop = turnTop + CustomButton.HEIGHT + 4;

        var contentBottom = Math.Max(
            Math.Max(pageTop + CustomButton.HEIGHT, stepTop + CustomButton.HEIGHT),
            Math.Max(FrontCanvas.Y + FrontCanvas.Height, BackCanvas.Y + BackCanvas.Height));

        var infoTop = contentBottom + BOTTOM_ROW_GAP;
        var reasonTop = infoTop + TextRenderer.CHAR_HEIGHT + 6;
        var decideTop = reasonTop + CustomButton.HEIGHT + 6;

        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = decideTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = Caption("Town Contest", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("ENTRIES", LEFT, CAPTION_TOP, LIST_WIDTH, color: LegendColors.Gray);
        FrontCaption = Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        BackCaption = Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        PreviewCaption = Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);

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

        var turnLeft = AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        var turnRight = AddButton(">", SMALL_BUTTON, previewLeft + SMALL_BUTTON + 4, turnTop, () => Preview.Turn(1));
        var body = AddButton("Body", 50, previewLeft + PREVIEW_WIDTH - 50, turnTop, Preview.ToggleBody);

        CloakViews = [FrontCanvas, BackCanvas, Preview, turnLeft, turnRight, body, .. AddStepButtons(Preview, previewLeft, stepTop, PREVIEW_WIDTH)];

        EmblemCanvas = new GuildEmblemCanvas(12)
        {
            X = FrontCanvas.X,
            Y = CONTENT_TOP,
            ReadOnly = true,
            Visible = false
        };

        EmblemPreview = new GuildEmblemPreviewStrip
        {
            X = FrontCanvas.X,
            Y = CONTENT_TOP + EmblemCanvas.Height + 8,
            Visible = false
        };

        AddChild(EmblemCanvas);
        AddChild(EmblemPreview);

        var middleWidth = previewLeft + PREVIEW_WIDTH - FrontCanvas.X;

        SongLabel = Caption(string.Empty, FrontCanvas.X, CONTENT_TOP + 40, middleWidth, HorizontalAlignment.Center);
        SongLabel.Visible = false;

        PlayButton = AddButton("Play", PLAY_WIDTH, FrontCanvas.X + ((middleWidth - PLAY_WIDTH) / 2), CONTENT_TOP + 70, TogglePlay);
        PlayButton.Visible = false;

        ThemeLabel = Caption(string.Empty, FrontCanvas.X, CONTENT_TOP + 110, middleWidth, HorizontalAlignment.Center, LegendColors.Gray);
        ThemeLabel.Visible = false;

        EmptyLabel = Caption(
            "There are no entries.",
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
            MaxLength = TownContestProtocol.MAX_REASON_CHARS,
            HintText = "Reason for rejecting"
        };

        AddChild(ReasonBox);

        PickButton = AddButton("Pick this one", DECIDE_WIDTH + 20, FrontCanvas.X, decideTop, Pick);
        ApproveButton = AddButton("Approve", DECIDE_WIDTH, FrontCanvas.X, decideTop, () => Decide(TownContestActionType.Approve));

        RejectButton = AddButton(
            "Reject",
            DECIDE_WIDTH,
            FrontCanvas.X + DECIDE_WIDTH + GAP,
            decideTop,
            () => Decide(TownContestActionType.Reject));
    }

    private bool IsApproving => Contest?.Mode == TownContestReviewMode.Approve;

    private TownContestEntryInfo? Selected => (SelectedIndex >= 0) && (SelectedIndex < Entries.Count) ? Entries[SelectedIndex] : null;

    /// <summary>Raised with the mayor's pick or the admin's decision.</summary>
    public event Action<TownContestActionArgs>? ActionRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        Player.StopIfOwner(this);
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

    /// <summary>Shows the window for a contest's entries.</summary>
    public void Open(TownContestDisplayArgs args)
    {
        Player.StopIfOwner(this);
        Contest = args;
        Entries = args.Entries;
        TitleLabel.Text = IsApproving ? $"{args.Title}: approve the mayor's pick" : $"{args.Title}: pick the winner";
        ThemeLabel.Text = $"Theme: {args.Theme}";
        ReasonBox.Visible = IsApproving;
        ReasonBox.Text = string.Empty;
        PickButton.Visible = !IsApproving;
        ApproveButton.Visible = IsApproving;
        RejectButton.Visible = IsApproving;
        Page = 0;

        var picked = -1;

        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].EntryId == args.PickedEntryId)
                picked = i;

        Select(picked >= 0 ? picked : Entries.Count > 0 ? 0 : -1);
        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var hasEntry = Selected is not null;
        PickButton.Enabled = hasEntry;
        ApproveButton.Enabled = hasEntry;
        RejectButton.Enabled = hasEntry && ReasonBox.Text.Trim().Length is >= 1 and <= TownContestProtocol.MAX_REASON_CHARS;

        var playing = ReferenceEquals(Player.Owner, this) && (Player.IsPlaying || Player.IsRendering);
        var caption = playing ? "Stop" : "Play";

        if (PlayButton.Caption != caption)
            PlayButton.Caption = caption;
    }

    private void Pick()
    {
        if (Contest is null || Selected is not { } entry)
            return;

        ActionRequested?.Invoke(
            new TownContestActionArgs
            {
                Type = TownContestActionType.Pick,
                ContestId = Contest.ContestId,
                EntryId = entry.EntryId
            });

        Hide();
    }

    private void Decide(TownContestActionType type)
    {
        if (Contest is null || Selected is not { } entry)
            return;

        var reason = ReasonBox.Text.Trim();

        if ((type == TownContestActionType.Reject) && (reason.Length == 0))
            return;

        ActionRequested?.Invoke(
            new TownContestActionArgs
            {
                Type = type,
                ContestId = Contest.ContestId,
                EntryId = entry.EntryId,
                Reason = type == TownContestActionType.Reject ? reason : string.Empty
            });

        Hide();
    }

    private void TogglePlay()
    {
        if (Selected is not { } entry)
            return;

        if (ReferenceEquals(Player.Owner, this) && (Player.IsPlaying || Player.IsRendering))
        {
            Player.Stop();

            return;
        }

        Player.Play(TuneData.From(entry.Song), this);
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

            button.Caption = Entries[index].Name;
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

        Player.StopIfOwner(this);
        SelectedIndex = index;
        ReasonBox.Text = string.Empty;

        var entry = Selected;
        var kind = Contest?.Kind ?? TownContestKind.Cape;
        var cape = entry is not null && (kind == TownContestKind.Cape);
        var banner = entry is not null && (kind == TownContestKind.Banner);
        var song = entry is not null && (kind == TownContestKind.Song);

        foreach (var view in CloakViews)
            view.Visible = cape;

        EmblemCanvas.Visible = banner;
        EmblemPreview.Visible = banner;
        SongLabel.Visible = song;
        PlayButton.Visible = song;
        ThemeLabel.Visible = song;
        FrontCaption.Text = kind switch
        {
            TownContestKind.Banner => "BANNER",
            TownContestKind.Song   => "SONG",
            _                      => "FRONT"
        };

        BackCaption.Visible = kind == TownContestKind.Cape;
        PreviewCaption.Visible = kind == TownContestKind.Cape;
        EmptyLabel.Visible = entry is null;
        InfoLabel.Text = entry is null ? string.Empty : $"By {entry.Name}, {entry.SubmittedAtUtc:yyyy-MM-dd HH:mm} UTC";

        if (cape)
        {
            FrontCanvas.SetDesign(entry!.Cape);
            BackCanvas.SetDesign(entry.Cape);
            PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, entry.Cape.DeepCopy());
            Preview.SetDesignId(PreviewDesignId);
        } else if (banner)
        {
            EmblemCanvas.SetDesign(entry!.Banner);
            EmblemPreviewId = GuildEmblemTextures.SetLocal(EmblemPreviewId, entry.Banner.DeepCopy());
            EmblemPreview.DesignId = EmblemPreviewId;
        } else if (song)
        {
            var tune = TuneData.From(entry!.Song);

            SongLabel.Text = $"{TuneLabels.Scale(tune.Scale)}, {TuneLabels.Speed(tune.Speed)}, {TuneLabels.Instrument(tune.Instrument)}, "
                             + $"{tune.Notes.Count} notes";
        }

        RefreshList();
    }

    private void TurnPage(int delta)
    {
        Page += delta;
        RefreshList();
    }
}

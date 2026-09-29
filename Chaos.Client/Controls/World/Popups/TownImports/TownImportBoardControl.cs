#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.TownImports;

/// <summary>
///     The mayor's import window ("Mileth Imports"): the running import, the admin-chosen pool with a walking preview of the
///     selected item on the viewer's own figure, the last few imports, and the two-click pick. Opened by the server's
///     TownImportBoard Open from Aingeal's mayor desk; anyone may look, only the mayor with no import running may pick.
/// </summary>
/// <remarks>
///     Layout: the title and the two running-import lines across the top; under them the pool list (left) and the preview
///     column (right: the preview with its turn row, then the past imports). Under the taller of the two sits the status
///     line and the pick button; under that is the frame's ornate bottom border with the Close button. The list's 8 rows of
///     34 px leave no room for the past imports under the list inside 480 px, so they sit in the preview column, which is
///     widened to hold a whole past line. As in the review window, the height follows the lowest element so it always
///     clears <see cref="FramedDialogPanelBase.BORDER_BOTTOM_HEIGHT" />.
/// </remarks>
public sealed class TownImportBoardControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int RUNNING_TOP = 30;
    private const int RUNNING_SECOND_TOP = 44;
    private const int CAPTION_TOP = 62;
    private const int LIST_TOP = 76;
    private const int LIST_WIDTH = 220;
    private const int ROW_HEIGHT = TownImportRow.HEIGHT;
    private const int PAGE_SIZE = 8;

    //the preview column: wide enough for a whole past-import line, with the preview centred in it
    private const int RIGHT_WIDTH = 332;
    private const int PREVIEW_WIDTH = 140;
    private const int PREVIEW_HEIGHT = 180;
    private const int PAST_LINES = 5;
    private const int SMALL_BUTTON = 28;
    private const int PICK_WIDTH = 200;
    private const int BOTTOM_ROW_GAP = 8;
    private const int BORDER_GAP = 4;
    private const int CLOCK_MS = 1000;

    private readonly UILabel EmptyLabel;
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly UILabel[] PastLabels = new UILabel[PAST_LINES];
    private readonly CustomButton PickButton;
    private readonly CustomButton PrevPageButton;
    private readonly GuildCloakPreview Preview;
    private readonly UILabel PreviewCaption;
    private readonly TownImportRow[] Rows = new TownImportRow[PAGE_SIZE];
    private readonly UILabel RunningLabel;
    private readonly UILabel RunningSalesLabel;
    private readonly UILabel StatusLabel;
    private readonly UILabel TitleLabel;
    private readonly Func<AislingAppearance> ViewerLook;

    private TownImportBoardArgs Args = new() { Type = TownImportBoardType.Open };
    private double ClockElapsedMs;
    private bool Confirming;
    private int Page;
    private string? SelectedKey;

    public TownImportBoardControl(AislingRenderer renderer, Func<AislingAppearance> viewerLook)
        : base("_nsett", false)
    {
        ViewerLook = viewerLook;
        Name = "TownImportBoard";
        Visible = false;
        UsesControlStack = true;

        var rightLeft = LEFT + LIST_WIDTH + GAP;
        var previewLeft = rightLeft + ((RIGHT_WIDTH - PREVIEW_WIDTH) / 2);
        var pageTop = LIST_TOP + (PAGE_SIZE * ROW_HEIGHT) + 4;
        var turnTop = LIST_TOP + PREVIEW_HEIGHT + 4;
        var pastCaptionTop = turnTop + CustomButton.HEIGHT + GAP;
        var pastTop = pastCaptionTop + TextRenderer.CHAR_HEIGHT + 2;

        //the lower of the two columns decides where the status/pick rows start: the list's page row, or the last past line
        var contentBottom = Math.Max(pageTop + CustomButton.HEIGHT, pastTop + (PAST_LINES * TextRenderer.CHAR_HEIGHT));
        var statusTop = contentBottom + BOTTOM_ROW_GAP;
        var pickTop = statusTop + TextRenderer.CHAR_HEIGHT + 6;

        Width = rightLeft + RIGHT_WIDTH + LEFT;
        Height = pickTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        var fullWidth = Width - (2 * LEFT);
        TitleLabel = Caption("Imports", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        RunningLabel = Caption(string.Empty, LEFT, RUNNING_TOP, fullWidth);
        RunningSalesLabel = Caption(string.Empty, LEFT, RUNNING_SECOND_TOP, fullWidth, color: LegendColors.Gray);

        Caption("POOL", LEFT, CAPTION_TOP, LIST_WIDTH, color: LegendColors.Gray);
        PreviewCaption = Caption("PREVIEW", rightLeft, CAPTION_TOP, RIGHT_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;

            var row = new TownImportRow(LIST_WIDTH)
            {
                X = LEFT,
                Y = LIST_TOP + (i * ROW_HEIGHT),
                Visible = false
            };

            row.Clicked += () => Select((Page * PAGE_SIZE) + slot);
            AddChild(row);
            Rows[i] = row;
        }

        EmptyLabel = Caption("The pool is empty.", LEFT, LIST_TOP + 60, LIST_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, pageTop, () => TurnPage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * SMALL_BUTTON) + 10, pageTop + 5, LIST_WIDTH - (2 * SMALL_BUTTON) - 10, color: LegendColors.Gray);

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = LIST_TOP
        };

        AddChild(Preview);

        AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", SMALL_BUTTON, previewLeft + PREVIEW_WIDTH - SMALL_BUTTON, turnTop, () => Preview.Turn(1));

        Caption("PAST IMPORTS", rightLeft, pastCaptionTop, RIGHT_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < PAST_LINES; i++)
            PastLabels[i] = Caption(string.Empty, rightLeft, pastTop + (i * TextRenderer.CHAR_HEIGHT), RIGHT_WIDTH);

        StatusLabel = Caption(string.Empty, LEFT, statusTop, fullWidth);
        PickButton = AddButton(string.Empty, PICK_WIDTH, LEFT, pickTop, OnPickClicked);
    }

    private TownImportItemInfo? SelectedItem
        => SelectedKey is null ? null : Args.Pool.FirstOrDefault(item => item.TemplateKey == SelectedKey);

    /// <summary>Raised on the confirming second click of the pick button.</summary>
    public event Action<TownImportBoardInteractionArgs>? PickRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

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

    /// <summary>Shows the window with fresh data. A closed window opens on the first page with nothing selected.</summary>
    public void Open(TownImportBoardArgs args)
    {
        if (!Visible)
        {
            SelectedKey = null;
            Page = 0;
        }

        SetArgs(args);
        Show();
    }

    /// <summary>Replaces the data after a pick, if the window is open. The selection stays while its item is still in the pool.</summary>
    public void Refresh(TownImportBoardArgs args)
    {
        if (Visible)
            SetArgs(args);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        //the running import's time left counts down while the window is open
        ClockElapsedMs += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (ClockElapsedMs < CLOCK_MS)
            return;

        ClockElapsedMs = 0;
        RefreshRunning();
    }

    /// <summary>"Confirm: name?", with the name shortened so the whole caption fits the pick button.</summary>
    private static string ConfirmCaption(string name)
    {
        const string ELLIPSIS = "...";

        //the button's label has a pixel of padding each side, and a little air keeps the text off the frame
        var room = PICK_WIDTH - 8 - TextRenderer.MeasureWidth("Confirm: ?");

        if (TextRenderer.MeasureWidth(name) > room)
        {
            var length = name.Length;

            while ((length > 0) && (TextRenderer.MeasureWidth(name.AsSpan(0, length)) > (room - TextRenderer.MeasureWidth(ELLIPSIS))))
                length--;

            name = string.Concat(name.AsSpan(0, length), ELLIPSIS);
        }

        return $"Confirm: {name}?";
    }

    private void OnPickClicked()
    {
        if (!Args.CanPick || SelectedItem is not { } item)
            return;

        if (!Confirming)
        {
            Confirming = true;
            RefreshPick();

            return;
        }

        Confirming = false;
        RefreshPick();

        PickRequested?.Invoke(
            new TownImportBoardInteractionArgs
            {
                Action = TownImportBoardAction.Pick,
                TownKey = Args.TownKey,
                TemplateKey = item.TemplateKey
            });
    }

    private void RefreshList()
    {
        var pool = Args.Pool;
        var pages = Math.Max(1, (pool.Count + PAGE_SIZE - 1) / PAGE_SIZE);
        Page = Math.Clamp(Page, 0, pages - 1);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (Page * PAGE_SIZE) + i;
            var item = index < pool.Count ? pool[index] : null;
            var row = Rows[i];

            row.SetItem(item);
            row.Selected = item is not null && (item.TemplateKey == SelectedKey);
        }

        EmptyLabel.Visible = pool.Count == 0;
        PageLabel.Text = $"{Page + 1}/{pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);
    }

    private void RefreshPast()
    {
        var past = Args.Past;

        for (var i = 0; i < PAST_LINES; i++)
        {
            var label = PastLabels[i];

            if (i < past.Count)
            {
                label.Text = TownImportText.PastLine(past[i]);
                label.ForegroundColor = LegendColors.White;
            } else
            {
                label.Text = (i == 0) && (past.Count == 0) ? "No imports yet." : string.Empty;
                label.ForegroundColor = LegendColors.Gray;
            }
        }
    }

    private void RefreshPick()
    {
        var item = SelectedItem;
        PickButton.Enabled = Args.CanPick && item is not null;

        if (!PickButton.Enabled)
            Confirming = false;

        var days = Args.ImportDays;

        PickButton.Caption = Confirming && item is not null
            ? ConfirmCaption(item.Name)
            : $"Import for {days} {(days == 1 ? "day" : "days")}";
    }

    /// <summary>
    ///     Previews the selected pool item; with nothing selected, the running import; with neither, the viewer as they are.
    /// </summary>
    private void RefreshPreview()
    {
        var item = SelectedItem ?? Args.Running?.Item;
        var look = ViewerLook();

        Preview.SetLook(item is null ? look : TownImportPreviewLook.Apply(in look, item.Look));
        PreviewCaption.Text = item is null ? "PREVIEW" : $"PREVIEW: {item.Name}";
    }

    private void RefreshRunning()
    {
        if (Args.Running is not { } running)
        {
            RunningLabel.Text = "No import is running.";
            RunningSalesLabel.Text = string.Empty;

            return;
        }

        var timeLeft = TownImportText.TimeLeft(running.EndsUtc, DateTime.UtcNow);

        RunningLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Now: {running.Item.Name}, {running.Item.Price:N0} gold, {timeLeft} left");

        RunningSalesLabel.Text = string.Create(CultureInfo.InvariantCulture, $"Sold {running.Sold:N0} for {running.Gold:N0} gold");
    }

    private void Select(int index)
    {
        if ((index < 0) || (index >= Args.Pool.Count))
            return;

        var key = Args.Pool[index].TemplateKey;

        if (key == SelectedKey)
            return;

        SelectedKey = key;
        Confirming = false;

        RefreshList();
        RefreshPreview();
        RefreshPick();
    }

    private void SetArgs(TownImportBoardArgs args)
    {
        Args = args;

        if (SelectedItem is null)
            SelectedKey = null;

        Confirming = false;
        ClockElapsedMs = 0;

        TitleLabel.Text = string.IsNullOrEmpty(args.TownName) ? "Imports" : $"{args.TownName} Imports";
        StatusLabel.Text = args.Status;

        RefreshRunning();
        RefreshList();
        RefreshPast();
        RefreshPreview();
        RefreshPick();
    }

    private void TurnPage(int delta)
    {
        Page += delta;
        RefreshList();
    }
}

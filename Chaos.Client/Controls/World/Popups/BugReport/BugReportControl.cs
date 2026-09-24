#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.BugReport;

/// <summary>What the player chose to send. <see cref="Picture" /> is null when the box was unticked or no frame was captured.</summary>
public sealed record BugReportSubmission(uint ReportId, BugReportCategory Category, string Description, byte[]? Picture);

/// <summary>
///     The in-game bug report window (layout A2): a 4x2 category grid, a multi-line description, a preview of the picture
///     taken when the window opened, and an "Include it" box. Opened by the server's BugReportOpen through Terminus's
///     "Report a bug". Close and Escape cancel. Send Report stays dim until a category is picked and 10 or more
///     characters are typed.
/// </summary>
public sealed class BugReportControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 430;
    private const int PANEL_HEIGHT = 310;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;
    private const int LEFT = 22;
    private const int CONTENT_WIDTH = 374;
    private const int GAP = 4;

    private const int TITLE_TOP = 10;
    private const int CATEGORY_CAPTION_TOP = 28;
    private const int CATEGORY_GRID_TOP = 42;
    private const int CATEGORY_BUTTON_WIDTH = (CONTENT_WIDTH - (3 * GAP)) / 4;
    private const int DESCRIPTION_CAPTION_TOP = CATEGORY_GRID_TOP + (2 * CustomButton.HEIGHT) + GAP + 8;
    private const int DESCRIPTION_TOP = DESCRIPTION_CAPTION_TOP + 14;
    private const int DESCRIPTION_HEIGHT = 100;
    private const int PREVIEW_LEFT = LEFT + CONTENT_WIDTH - CapturedFrame.THUMBNAIL_WIDTH;
    private const int DESCRIPTION_WIDTH = PREVIEW_LEFT - LEFT - 10;
    private const int COUNTER_TOP = DESCRIPTION_TOP + DESCRIPTION_HEIGHT + 3;
    private const int CHECKBOX_TOP = DESCRIPTION_TOP + CapturedFrame.THUMBNAIL_HEIGHT + 6;
    private const int SEND_WIDTH = 80;
    private const int SEND_TOP = COUNTER_TOP + 14;
    private const int NOTE_TOP = SEND_TOP + 2;

    private static readonly BugReportCategory[] CategoryOrder =
    [
        BugReportCategory.SkillSpell,
        BugReportCategory.Item,
        BugReportCategory.MapWarp,
        BugReportCategory.Npc,
        BugReportCategory.Quest,
        BugReportCategory.MonsterCombat,
        BugReportCategory.ClientUi,
        BugReportCategory.Other
    ];

    private readonly Dictionary<BugReportCategory, CustomButton> CategoryButtons = [];
    private readonly UILabel CounterLabel;
    private readonly CustomTextBox DescriptionBox;
    private readonly CustomCheckBox IncludePicture;
    private readonly UILabel NoPictureLabel;
    private readonly UIImage Preview;
    private readonly CustomButton SendButton;

    private BugReportCategory? Category;
    private CapturedFrame? Frame;
    private uint ReportId;

    public BugReportControl()
        : base("_nsett", false)
    {
        Name = "BugReport";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Report a Bug", 0, TITLE_TOP, PANEL_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("WHAT KIND OF PROBLEM?", LEFT, CATEGORY_CAPTION_TOP, CONTENT_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < CategoryOrder.Length; i++)
        {
            var category = CategoryOrder[i];

            var button = new CustomButton(BugReportProtocol.DisplayName(category), CATEGORY_BUTTON_WIDTH)
            {
                X = LEFT + ((i % 4) * (CATEGORY_BUTTON_WIDTH + GAP)),
                Y = CATEGORY_GRID_TOP + ((i / 4) * (CustomButton.HEIGHT + GAP))
            };

            button.Clicked += () => SelectCategory(category);
            CategoryButtons[category] = button;
            AddChild(button);
        }

        Caption("WHAT HAPPENED? WHAT DID YOU EXPECT?", LEFT, DESCRIPTION_CAPTION_TOP, DESCRIPTION_WIDTH, color: LegendColors.Gray);

        DescriptionBox = new CustomTextBox
        {
            X = LEFT,
            Y = DESCRIPTION_TOP,
            Width = DESCRIPTION_WIDTH,
            Height = DESCRIPTION_HEIGHT,
            IsMultiLine = true,
            MaxLength = BugReportProtocol.MAX_DESCRIPTION_CHARS,
            HintText = "What were you doing? What went wrong?"
        };

        AddChild(DescriptionBox);

        CounterLabel = Caption(
            $"0 / {BugReportProtocol.MAX_DESCRIPTION_CHARS}",
            LEFT,
            COUNTER_TOP,
            DESCRIPTION_WIDTH,
            HorizontalAlignment.Right,
            LegendColors.Gray);

        Caption("SCREENSHOT", PREVIEW_LEFT, DESCRIPTION_CAPTION_TOP, CapturedFrame.THUMBNAIL_WIDTH, color: LegendColors.Gray);

        Preview = new UIImage
        {
            X = PREVIEW_LEFT,
            Y = DESCRIPTION_TOP,
            Width = CapturedFrame.THUMBNAIL_WIDTH,
            Height = CapturedFrame.THUMBNAIL_HEIGHT,
            IsHitTestVisible = false
        };

        AddChild(Preview);

        NoPictureLabel = Caption(
            "No picture",
            PREVIEW_LEFT,
            DESCRIPTION_TOP + ((CapturedFrame.THUMBNAIL_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            CapturedFrame.THUMBNAIL_WIDTH,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        IncludePicture = new CustomCheckBox
        {
            X = PREVIEW_LEFT,
            Y = CHECKBOX_TOP,
            Width = CapturedFrame.THUMBNAIL_WIDTH,
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Include it",
            Checked = true
        };

        IncludePicture.Clicked += () => IncludePicture.Checked = !IncludePicture.Checked;
        AddChild(IncludePicture);

        var note = Caption(
            "Your character, position and recent server events are attached for staff.",
            LEFT,
            NOTE_TOP,
            DESCRIPTION_WIDTH,
            color: LegendColors.Gray);

        note.WordWrap = true;
        note.Height = TextRenderer.CHAR_HEIGHT * 2;
        note.PaddingLeft = 0;
        note.PaddingRight = 0;
        note.PaddingTop = 0;
        note.PaddingBottom = 0;
        note.VerticalAlignment = VerticalAlignment.Top;

        SendButton = new CustomButton("Send Report", SEND_WIDTH)
        {
            X = LEFT + CONTENT_WIDTH - SEND_WIDTH,
            Y = SEND_TOP,
            Enabled = false
        };

        SendButton.Clicked += Send;
        AddChild(SendButton);
    }

    /// <summary>Raised when the player presses Send Report. The window has already hidden itself.</summary>
    public event Action<BugReportSubmission>? SendRequested;

    /// <summary>Raised with the report number when the player closes the window without sending.</summary>
    public event Action<uint>? Cancelled;

    /// <summary>Resets the window for a new report and shows it. Takes ownership of <paramref name="frame" />.</summary>
    public void Open(uint reportId, CapturedFrame? frame)
    {
        ReleaseFrame();
        ReportId = reportId;
        Frame = frame;
        Category = null;

        foreach (var button in CategoryButtons.Values)
            button.Selected = false;

        DescriptionBox.Text = string.Empty;
        Preview.Texture = frame is null ? null : TextureConverter.ToTexture2D(frame.Thumbnail);
        NoPictureLabel.Visible = frame is null;
        IncludePicture.Visible = frame is not null;
        IncludePicture.Checked = frame is not null;
        RefreshSendState();
        Show();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        ReleaseFrame();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            RefreshSendState();
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

    public override void Dispose()
    {
        ReleaseFrame();
        base.Dispose();
    }

    private UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }

    private void SelectCategory(BugReportCategory category)
    {
        Category = category;

        foreach ((var key, var button) in CategoryButtons)
            button.Selected = key == category;

        RefreshSendState();
    }

    private void RefreshSendState()
    {
        var counter = $"{DescriptionBox.Text.Length} / {BugReportProtocol.MAX_DESCRIPTION_CHARS}";

        if (CounterLabel.Text != counter)
            CounterLabel.Text = counter;

        SendButton.Enabled = BugReportUpload.CanSend(Category, DescriptionBox.Text);
    }

    private void Send()
    {
        if (!BugReportUpload.CanSend(Category, DescriptionBox.Text))
            return;

        var submission = new BugReportSubmission(
            ReportId,
            Category!.Value,
            DescriptionBox.Text.Trim(),
            IncludePicture.Checked ? Frame?.Png : null);

        Hide();
        SendRequested?.Invoke(submission);
    }

    private void Dismiss()
    {
        if (!Visible)
            return;

        var reportId = ReportId;
        Hide();
        Cancelled?.Invoke(reportId);
    }

    private void ReleaseFrame()
    {
        Preview.Texture?.Dispose();
        Preview.Texture = null;
        Frame?.Dispose();
        Frame = null;
    }
}

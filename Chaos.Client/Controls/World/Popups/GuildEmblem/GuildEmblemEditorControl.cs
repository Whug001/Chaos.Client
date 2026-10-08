#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     The guild leader's emblem editor (spec: docs/superpowers/specs/2026-09-28-guild-emblem-design.md, layout B): an
///     11 × 11 canvas, a see-through box and six colors with the Theatre color picker, pencil, fill, pick, mirror, undo and
///     redo, previews at world list and Emblem tab sizes, and Save draft / Submit. Opened by the server's GuildEmblemEditor
///     Open from Quill.
/// </summary>
/// <remarks>
///     Layout, top to bottom: title, canvas, color row, two rows of three tools, preview strip, status line, then Save draft
///     and Submit on the right; under that is the frame's ornate bottom border with the Close button.
/// </remarks>
public sealed class GuildEmblemEditorControl : GuildCloakDialogBase
{
    private const int BORDER_GAP = 4;
    private const string CLOSE_WARNING = "Unsaved changes. Press Close again to discard them.";
    private const int CONTENT_TOP = 32;
    private const int CONTENT_WIDTH = GuildEmblemProtocol.SIZE * ZOOM;
    private const int GAP = 6;
    private const int LEFT = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int SAVE_WIDTH = 76;
    private const long SEND_COOLDOWN_MS = 2_500;
    private const int STATUS_LINES = 3;
    private const int SUBMIT_WIDTH = 64;
    private const int TITLE_TOP = 10;
    private const int TOOL_SPACING = 3;
    private const int TOOL_WIDTH = (CONTENT_WIDTH - (2 * TOOL_SPACING)) / 3;
    private const int ZOOM = 16;

    private readonly GuildEmblemCanvas Canvas;
    private readonly CustomButton FillButton;
    private readonly CustomButton MirrorButton;
    private readonly GuildEmblemEditorModel Model = new();
    private readonly CustomButton PencilButton;
    private readonly CustomButton PickButton;
    private readonly StageColorPicker Picker;
    private readonly GuildEmblemPreviewStrip Preview;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton SubmitButton;
    private readonly UILabel TitleLabel;

    //opened for a town contest entry: Submit enters it, and there is no draft
    private bool ContestMode;

    //the contest the window was last opened for; empty for the guild's own design
    private string ContestTitle = string.Empty;
    private readonly CustomButton UndoButton;

    private bool CloseArmed;

    //true from the picker's first color change until its drag ends: the whole drag undoes as one step
    private bool ColorDragging;
    private int EditingColor;
    private long LastSendMs = long.MinValue / 2;
    private int PreviewId;
    private GuildEmblemDesign? SentDesign;
    private int ShownVersion = -1;
    private GuildCloakStatus Status;
    private string StatusReason = string.Empty;

    public GuildEmblemEditorControl()
        : base("_nsett", false)
    {
        Name = "GuildEmblemEditor";
        Visible = false;
        UsesControlStack = true;

        Canvas = new GuildEmblemCanvas(ZOOM)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        var swatchTop = Canvas.Y + Canvas.Height + GAP;
        var toolsTop = swatchTop + GuildEmblemSwatches.BOX + GAP;
        var secondToolRow = toolsTop + CustomButton.HEIGHT + TOOL_SPACING;
        var previewTop = secondToolRow + CustomButton.HEIGHT + GAP;
        var statusTop = previewTop + GuildEmblemPreviewStrip.TOTAL_HEIGHT + GAP;
        var buttonsTop = statusTop + (STATUS_LINES * TextRenderer.CHAR_HEIGHT) + GAP;

        Width = LEFT + CONTENT_WIDTH + LEFT;
        Height = buttonsTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        TitleLabel = Caption("Guild Emblem", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas.StrokeStarted += Model.BeginStroke;
        Canvas.CellPainted += Model.Apply;
        Canvas.StrokeEnded += Model.EndStroke;
        AddChild(Canvas);

        var swatches = new GuildEmblemSwatches(Model)
        {
            X = LEFT + ((CONTENT_WIDTH - GuildEmblemSwatches.TOTAL_WIDTH) / 2),
            Y = swatchTop
        };

        swatches.EditRequested += OpenPicker;
        AddChild(swatches);

        PencilButton = ToolButton("Pencil", 0, toolsTop, () => Model.Tool = GuildCloakTool.Pencil);
        FillButton = ToolButton("Fill", 1, toolsTop, () => Model.Tool = GuildCloakTool.Fill);
        PickButton = ToolButton("Pick", 2, toolsTop, () => Model.Tool = GuildCloakTool.Pick);
        MirrorButton = ToolButton("Mirror", 0, secondToolRow, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", 1, secondToolRow, () => StepHistory(Model.Undo));
        RedoButton = ToolButton("Redo", 2, secondToolRow, () => StepHistory(Model.Redo));

        Preview = new GuildEmblemPreviewStrip
        {
            X = LEFT + ((CONTENT_WIDTH - GuildEmblemPreviewStrip.TOTAL_WIDTH) / 2),
            Y = previewTop
        };

        AddChild(Preview);

        StatusLabel = Caption(string.Empty, LEFT, statusTop, CONTENT_WIDTH);
        StatusLabel.WordWrap = true;
        StatusLabel.Height = TextRenderer.CHAR_HEIGHT * STATUS_LINES;
        StatusLabel.VerticalAlignment = VerticalAlignment.Top;
        StatusLabel.PaddingLeft = 0;
        StatusLabel.PaddingRight = 0;
        StatusLabel.PaddingTop = 0;
        StatusLabel.PaddingBottom = 0;

        SubmitButton = AddButton(
            "Submit",
            SUBMIT_WIDTH,
            Width - LEFT - SUBMIT_WIDTH,
            buttonsTop,
            () => Send(GuildCloakEditorAction.Submit));

        SaveButton = AddButton(
            "Save draft",
            SAVE_WIDTH,
            SubmitButton.X - GAP - SAVE_WIDTH,
            buttonsTop,
            () => Send(GuildCloakEditorAction.SaveDraft));

        //added last so it draws over the canvas while open, and is hit-tested before it
        Picker = new StageColorPicker
        {
            Y = CONTENT_TOP
        };

        Picker.X = LEFT + ((CONTENT_WIDTH - Picker.Width) / 2);

        Picker.ColorChanged += color =>
        {
            if (!ColorDragging)
            {
                ColorDragging = true;
                Model.BeginStroke();
            }

            Model.SetColor(EditingColor, new GuildCloakColor(color.R, color.G, color.B));
        };

        Picker.DragEnded += EndColorDrag;
        Picker.Closed += EndColorDrag;
        AddChild(Picker);
    }

    /// <summary>Raised with the action and a copy of the emblem when the leader presses Save draft or Submit.</summary>
    public event Action<GuildCloakEditorAction, GuildEmblemDesign>? SaveRequested;

    /// <summary>
    ///     A status from the server. Local edits count as saved only when this client sent an emblem and the emblem is still
    ///     exactly that copy. The sent copy is used up here, so one reply cannot confirm a later emblem.
    /// </summary>
    public void ApplyStatus(GuildEmblemEditorArgs args)
    {
        if (SentDesign is not null && SentDesign.ContentEquals(Model.Design))
            Model.MarkSaved();

        SentDesign = null;
        SetStatus(args.Status, args.RejectionReason);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        //hiding resets the children's interaction state, which ends any stroke or color drag in progress
        base.Hide();
        Picker.Visible = false;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestClose();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Loads the server's emblem and status and shows the window. An open window with unsaved painting keeps it.</summary>
    public void Open(GuildEmblemEditorArgs args)
    {
        //the window switching between the guild's design and a contest (or between contests) drops unsaved work, so
        //one never goes in as the other
        var sameDesign = ContestTitle == args.ContestTitle;
        ContestTitle = args.ContestTitle;
        ContestMode = args.ContestTitle.Length > 0;
        TitleLabel.Text = ContestMode ? args.ContestTitle : $"Guild Emblem - {args.GuildName}";
        SaveButton.Visible = !ContestMode;

        //asking Quill again while painting keeps the unsaved work; only the status line follows the server
        if (!Model.LoadSaved(args.Design, Visible && sameDesign))
        {
            SetStatus(args.Status, args.RejectionReason);

            return;
        }

        CloseArmed = false;
        ColorDragging = false;
        SentDesign = null;
        Picker.Visible = false;
        SetStatus(args.Status, args.RejectionReason);
        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            CloseArmed = false;
            Canvas.SetDesign(Model.Design);
            PreviewId = GuildEmblemTextures.SetLocal(PreviewId, Model.Design.DeepCopy());
            Preview.DesignId = PreviewId;

            //the picker always edits the selected color: follow a new selection (a box click, the pick tool, an undo)
            if (Picker.Visible && !ColorDragging && (EditingColor != Model.SelectedColor))
                OpenPicker(Model.SelectedColor);

            RefreshControls();
        }

        var canSend = (Environment.TickCount64 - LastSendMs) >= SEND_COOLDOWN_MS;
        SaveButton.Enabled = canSend;
        SubmitButton.Enabled = canSend && Model.Design.HasPaint();
    }

    private void EndColorDrag()
    {
        if (!ColorDragging)
            return;

        ColorDragging = false;
        Model.EndStroke();
    }

    private void OpenPicker(int number)
    {
        //see-through has no shade to pick
        if ((number < 1) || (number > Model.Design.Colors.Count))
        {
            Picker.Visible = false;

            return;
        }

        EditingColor = number;
        var color = Model.Design.Colors[number - 1];
        Picker.Open(new Color(color.R, color.G, color.B));
    }

    private void RefreshControls()
    {
        PencilButton.Selected = Model.Tool == GuildCloakTool.Pencil;
        FillButton.Selected = Model.Tool == GuildCloakTool.Fill;
        PickButton.Selected = Model.Tool == GuildCloakTool.Pick;
        MirrorButton.Selected = Model.Mirror;
        UndoButton.Enabled = Model.CanUndo;
        RedoButton.Enabled = Model.CanRedo;
        StatusLabel.Text = CloseArmed ? CLOSE_WARNING
            : ContestMode ? GuildCloakEditorModel.ContestStatusText(Status, Model.IsDirty)
            : GuildCloakEditorModel.StatusText(Status, StatusReason, Model.IsDirty);
    }

    private void RequestClose()
    {
        if (Model.IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            StatusLabel.Text = CLOSE_WARNING;

            return;
        }

        Hide();
    }

    private void Send(GuildCloakEditorAction action)
    {
        var now = Environment.TickCount64;

        if ((now - LastSendMs) < SEND_COOLDOWN_MS)
            return;

        LastSendMs = now;
        CloseArmed = false;
        SentDesign = Model.Design.DeepCopy();
        SaveRequested?.Invoke(action, Model.Design.DeepCopy());
        RefreshControls();
    }

    private void SetStatus(GuildCloakStatus status, string? reason)
    {
        Status = status;
        StatusReason = reason ?? string.Empty;
        RefreshControls();
    }

    /// <summary>Undo or redo, then point an open picker at the color it now shows.</summary>
    private void StepHistory(Action step)
    {
        step();

        if (Picker.Visible && !ColorDragging)
            OpenPicker(Model.SelectedColor);
    }

    private CustomButton ToolButton(string caption, int column, int top, Action onClick)
        => AddButton(
            caption,
            TOOL_WIDTH,
            LEFT + (column * (TOOL_WIDTH + TOOL_SPACING)),
            top,
            () =>
            {
                onClick();
                RefreshControls();
            });
}

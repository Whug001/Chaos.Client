#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
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

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The guild leader's cloak editor (spec: Chaos.Client/docs/superpowers/specs/2026-09-25-guild-cloak-design.md): front and
///     back canvases, six colors with the Theatre color picker, pencil, fill, pick, mirror painting, undo and redo, a
///     walking preview, and Save draft / Submit. Opened by the server's GuildCloakEditor Open from Quill.
/// </summary>
/// <remarks>
///     Layout, left to right: the colors and tools column, the front canvas, the back canvas, the preview column. Below them
///     one row holds the status line (left) and Save draft / Submit (right); under that is the frame's ornate bottom border
///     with the Close button. The window's height follows the tallest column, so the art's real frame sizes decide it.
/// </remarks>
public sealed class GuildCloakEditorControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;
    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int CONTENT_TOP = 44;
    private const int ZOOM = 5;
    private const int TOOL_WIDTH = 84;
    private const int TOOL_SPACING = 3;
    private const int TOOL_COUNT = 6;
    private const int PREVIEW_WIDTH = 134;
    private const int PREVIEW_HEIGHT = 205;
    private const int TURN_WIDTH = 28;
    private const int BODY_WIDTH = 70;
    private const int SAVE_WIDTH = 76;
    private const int SUBMIT_WIDTH = 64;

    //a rejection reason is up to 200 characters; four lines of the status column hold "Rejected: ", the reason and
    //" - unsaved changes"
    private const int STATUS_LINES = 4;
    private const int BOTTOM_ROW_GAP = 8;
    private const int BORDER_GAP = 4;
    private const long SEND_COOLDOWN_MS = 2_500;
    private const long PREVIEW_INTERVAL_MS = 150;
    private const string CLOSE_WARNING = "Unsaved changes. Press Close again to discard them.";

    private readonly GuildCloakCanvas BackCanvas;
    private readonly CustomButton BodyButton;
    private readonly CustomButton FillButton;
    private readonly GuildCloakCanvas FrontCanvas;
    private readonly CustomButton MirrorButton;
    private readonly GuildCloakEditorModel Model;
    private readonly CustomButton PencilButton;
    private readonly CustomButton PickButton;
    private readonly StageColorPicker Picker;
    private readonly GuildCloakPreview Preview;
    private readonly CustomButton RedoButton;
    private readonly AislingRenderer Renderer;
    private readonly CustomButton SaveButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton SubmitButton;
    private readonly CustomButton UndoButton;

    private bool CloseArmed;

    //true from the picker's first color change until its drag ends: the whole drag undoes as one step
    private bool ColorDragging;
    private int EditingColor;
    private long LastPreviewMs;
    private long LastSendMs = long.MinValue / 2;
    private bool Painting;

    //the design the preview shows, as handed to the renderer's design store (never changed afterwards)
    private GuildCloakDesign? PreviewDesign;
    private int PreviewDesignId;
    private bool PreviewStale;
    private GuildCloakDesign? SentDesign;
    private int ShownVersion = -1;
    private GuildCloakStatus Status;
    private string StatusReason = string.Empty;

    public GuildCloakEditorControl(AislingRenderer renderer, GuildCloakReferences references)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Model = new GuildCloakEditorModel((part, x, y) => references.Grid(part).IsFilled(x, y));
        Name = "GuildCloakEditor";
        Visible = false;
        UsesControlStack = true;

        FrontCanvas = new GuildCloakCanvas(references, GuildCloakView.Front, ZOOM)
        {
            X = LEFT + TOOL_WIDTH + GAP,
            Y = CONTENT_TOP
        };

        BackCanvas = new GuildCloakCanvas(references, GuildCloakView.Back, ZOOM)
        {
            X = FrontCanvas.X + FrontCanvas.Width + GAP,
            Y = CONTENT_TOP
        };

        //the size follows the canvases, which follow the art's frame sizes
        var previewLeft = BackCanvas.X + BackCanvas.Width + GAP;
        var turnTop = CONTENT_TOP + PREVIEW_HEIGHT + 4;
        var toolsTop = CONTENT_TOP + GuildCloakSwatches.TOTAL_HEIGHT + 10;
        var toolsBottom = toolsTop + (TOOL_COUNT * (CustomButton.HEIGHT + TOOL_SPACING)) - TOOL_SPACING;

        var contentBottom = Math.Max(
            Math.Max(FrontCanvas.Y + FrontCanvas.Height, BackCanvas.Y + BackCanvas.Height),
            Math.Max(turnTop + CustomButton.HEIGHT, toolsBottom));

        var bottomRowTop = contentBottom + BOTTOM_ROW_GAP;
        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = bottomRowTop + (STATUS_LINES * TextRenderer.CHAR_HEIGHT) + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Guild Cloak", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("COLORS", LEFT, CAPTION_TOP, TOOL_WIDTH, color: LegendColors.Gray);
        Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);

        var swatches = new GuildCloakSwatches(Model)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        swatches.EditRequested += OpenPicker;
        AddChild(swatches);

        var toolTop = toolsTop;
        PencilButton = ToolButton("Pencil", ref toolTop, () => Model.Tool = GuildCloakTool.Pencil);
        FillButton = ToolButton("Fill", ref toolTop, () => Model.Tool = GuildCloakTool.Fill);
        PickButton = ToolButton("Pick color", ref toolTop, () => Model.Tool = GuildCloakTool.Pick);
        MirrorButton = ToolButton("Mirror", ref toolTop, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", ref toolTop, () => StepHistory(Model.Undo));
        RedoButton = ToolButton("Redo", ref toolTop, () => StepHistory(Model.Redo));

        foreach (var canvas in new[] { FrontCanvas, BackCanvas })
        {
            canvas.StrokeStarted += () =>
            {
                Painting = true;
                Model.BeginStroke();
            };

            canvas.CellPainted += Model.Apply;

            canvas.StrokeEnded += () =>
            {
                Painting = false;
                Model.EndStroke();
                PreviewStale = true;
            };

            AddChild(canvas);
        }

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = CONTENT_TOP
        };

        AddChild(Preview);

        AddButton("<", TURN_WIDTH, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", TURN_WIDTH, previewLeft + TURN_WIDTH + 4, turnTop, () => Preview.Turn(1));
        BodyButton = AddButton("Female", BODY_WIDTH, previewLeft + PREVIEW_WIDTH - BODY_WIDTH, turnTop, ToggleBody);

        SubmitButton = AddButton(
            "Submit",
            SUBMIT_WIDTH,
            Width - LEFT - SUBMIT_WIDTH,
            bottomRowTop,
            () => Send(GuildCloakEditorAction.Submit));

        SaveButton = AddButton(
            "Save draft",
            SAVE_WIDTH,
            SubmitButton.X - GAP - SAVE_WIDTH,
            bottomRowTop,
            () => Send(GuildCloakEditorAction.SaveDraft));

        StatusLabel = Caption(string.Empty, LEFT, bottomRowTop, SaveButton.X - GAP - LEFT);
        StatusLabel.WordWrap = true;
        StatusLabel.Height = TextRenderer.CHAR_HEIGHT * STATUS_LINES;
        StatusLabel.VerticalAlignment = VerticalAlignment.Top;
        StatusLabel.PaddingLeft = 0;
        StatusLabel.PaddingRight = 0;
        StatusLabel.PaddingTop = 0;
        StatusLabel.PaddingBottom = 0;

        //added last so it draws over the canvases while open, and is hit-tested before them. It sits on the front canvas,
        //just right of the tools column: next to the swatches it would cover half of the Pencil and Fill buttons.
        Picker = new StageColorPicker
        {
            X = FrontCanvas.X,
            Y = CONTENT_TOP
        };

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

    /// <summary>Raised with the action and a copy of the design when the leader presses Save draft or Submit.</summary>
    public event Action<GuildCloakEditorAction, GuildCloakDesign>? SaveRequested;

    /// <summary>
    ///     A status from the server. The status line always follows it. Local edits count as saved only when this client
    ///     sent a design and the design is still exactly that copy; any other status (one not answering this client's own
    ///     save, or one arriving after further edits) leaves "unsaved changes" as it is. The sent copy is used up here, so
    ///     one reply cannot confirm a later design.
    /// </summary>
    public void ApplyStatus(GuildCloakEditorArgs args)
    {
        if (SentDesign is not null && SameDesign(SentDesign, Model.Design))
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
        Preview.ReleaseFrames();
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

    /// <summary>Loads the server's design and status and shows the window.</summary>
    public void Open(GuildCloakEditorArgs args)
    {
        Model.Load(args.Design);
        CloseArmed = false;
        ColorDragging = false;
        Painting = false;
        SentDesign = null;
        Picker.Visible = false;
        PreviewStale = true;
        SetStatus(args.Status, args.RejectionReason);
        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var now = Environment.TickCount64;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            CloseArmed = false;
            FrontCanvas.SetDesign(Model.Design);
            BackCanvas.SetDesign(Model.Design);

            //the picker always edits the selected color: follow a new selection (a swatch click, the pick tool, an undo)
            if (Picker.Visible && !ColorDragging && (EditingColor != Model.SelectedColor))
                OpenPicker(Model.SelectedColor);

            RefreshControls();
            PreviewStale = true;
        }

        //while painting or dragging a color, redraw the walking preview at most every 150 ms
        var busy = Painting || ColorDragging;

        if (PreviewStale && (!busy || ((now - LastPreviewMs) >= PREVIEW_INTERVAL_MS)))
        {
            PreviewStale = false;

            //a new color selection, a tool change or a save leaves the paint as it was: keep the rendered steps
            if (PreviewDesign is null || !SameDesign(PreviewDesign, Model.Design))
            {
                LastPreviewMs = now;
                PreviewDesign = Model.Design.DeepCopy();
                PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, PreviewDesign);
                Preview.SetDesignId(PreviewDesignId);
            }
        }

        var canSend = (now - LastSendMs) >= SEND_COOLDOWN_MS;
        SaveButton.Enabled = canSend;
        SubmitButton.Enabled = canSend;
    }

    private void EndColorDrag()
    {
        if (!ColorDragging)
            return;

        ColorDragging = false;
        Model.EndStroke();
        PreviewStale = true;
    }

    private void OpenPicker(int number)
    {
        if ((number < 1) || (number > Model.Design.Colors.Count))
            return;

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
        StatusLabel.Text = CloseArmed ? CLOSE_WARNING : GuildCloakEditorModel.StatusText(Status, StatusReason, Model.IsDirty);
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

    private static bool SameDesign(GuildCloakDesign a, GuildCloakDesign b)
        => a.Colors.SequenceEqual(b.Colors)
           && a.Back.AsSpan().SequenceEqual(b.Back)
           && a.Lining.AsSpan().SequenceEqual(b.Lining)
           && a.Collar.AsSpan().SequenceEqual(b.Collar);

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

    private void ToggleBody()
    {
        Preview.ToggleBody();
        BodyButton.Caption = Preview.IsMale ? "Female" : "Male";
    }

    private CustomButton ToolButton(string caption, ref int top, Action onClick)
    {
        var button = AddButton(
            caption,
            TOOL_WIDTH,
            LEFT,
            top,
            () =>
            {
                onClick();
                RefreshControls();
            });

        top += CustomButton.HEIGHT + TOOL_SPACING;

        return button;
    }
}

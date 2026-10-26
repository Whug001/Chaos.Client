using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The Sketch Guess drawing card for the round's artists: the secret prompt, the 96 x 72 canvas at 2x, the 8
///     sketch colours, pen tools, undo, clear and Done. Done sends the drawing but leaves the canvas editable, so
///     the artist can change it and press Done again; the deadline sends a changed drawing once. OK or Esc hides
///     it without losing the drawing.
/// </summary>
public sealed class SketchDrawCard : GuildCloakDialogBase
{
    private const int WIDTH = 320;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TIMER_WIDTH = 60;
    private const int SECRET_TOP = 28;
    private const int SECRET_LINES = 2;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CANVAS_TOP = SECRET_TOP + (SECRET_LINES * LINE_HEIGHT) + 4;
    private const int CANVAS_WIDTH = CollegeProtocol.DRAWING_WIDTH * 2;
    private const int CANVAS_HEIGHT = CollegeProtocol.DRAWING_HEIGHT * 2;
    private const int CANVAS_X = LEFT + ((INNER_WIDTH - CANVAS_WIDTH) / 2);
    private const int SWATCH_TOP = CANVAS_TOP + CANVAS_HEIGHT + 6;
    private const int TOOLS_TOP = SWATCH_TOP + 14 + 6;
    private const int TOOL_GAP = 2;
    private const int DONE_TOP = TOOLS_TOP + CustomButton.HEIGHT + 6;
    private const int HEIGHT = DONE_TOP + CustomButton.HEIGHT + 30;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string SENT_TEXT = "Sent. You can still change it and press Done again.";

    private readonly CustomButton BigPenButton;
    private readonly ArtCanvas Canvas;
    private readonly ArtEditor CanvasEditor;
    private readonly CustomButton ClearButton;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton DoneButton;
    private readonly CustomButton EraseButton;
    private readonly CustomButton FillButton;
    private (PartyPhase Phase, byte Round, byte Item)? DismissedStep;
    private bool Hidden;
    private CollegePartyInfo Info = new();
    private readonly CustomButton PenButton;
    private readonly UILabel SecretLabel;
    private int SentVersion = -1;
    private (PartyPhase Phase, byte Round, byte Item)? Step;
    private readonly SwatchStrip Swatches;
    private readonly UILabel TimerLabel;
    private readonly UILabel StatusLabel;
    private SketchToolbox Toolbox = new();
    private readonly CustomButton UndoButton;

    public SketchDrawCard()
        : base("_nsett", false)
    {
        Name = "SketchDraw";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH - TIMER_WIDTH, color: LegendColors.Gold);
        TimerLabel = Caption(string.Empty, LEFT + INNER_WIDTH - TIMER_WIDTH, CAPTION_TOP, TIMER_WIDTH, HorizontalAlignment.Right);
        SecretLabel = Caption(string.Empty, LEFT, SECRET_TOP, INNER_WIDTH, HorizontalAlignment.Center);
        SecretLabel.WordWrap = true;
        SecretLabel.Height = SECRET_LINES * LINE_HEIGHT;

        Toolbox = new SketchToolbox();
        CanvasEditor = new ArtEditor(Toolbox.Drawing);
        CanvasEditor.Mirror = false;
        SyncEditor();

        Canvas = new ArtCanvas(CanvasEditor, 2)
        {
            X = CANVAS_X,
            Y = CANVAS_TOP,
            ShowGrid = false
        };

        Canvas.Changed += RefreshTools;
        AddChild(Canvas);

        Swatches = new SwatchStrip
        {
            X = LEFT + ((INNER_WIDTH - SwatchStrip.STRIP_WIDTH) / 2),
            Y = SWATCH_TOP
        };

        Swatches.Picked += PickColour;
        AddChild(Swatches);

        var x = LEFT;
        PenButton = ToolButton("Pen", 44, ref x, () => PickTool(SketchTool.Pen));
        BigPenButton = ToolButton("Big pen", 60, ref x, () => PickTool(SketchTool.BigPen));
        FillButton = ToolButton("Fill", 40, ref x, () => PickTool(SketchTool.Fill));
        EraseButton = ToolButton("Erase", 48, ref x, () => PickTool(SketchTool.Erase));
        UndoButton = ToolButton("Undo", 42, ref x, Undo);
        ClearButton = ToolButton("Clear", 42, ref x, Clear);
        DoneButton = AddButton("Done", INNER_WIDTH, LEFT, DONE_TOP, SubmitDrawing);

        StatusLabel = Caption(string.Empty, LEFT, DONE_TOP + CustomButton.HEIGHT + 4, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.White);
    }

    public event Action<CollegeActionArgs>? Send;

    /// <summary>A PartyCard message. Shows only while the viewer is drawing; <paramref name="reopen" /> shows a card the artist closed.</summary>
    public void Apply(CollegePartyInfo info, bool reopen)
    {
        Info = info;
        Countdown.Start(info.SecondsLeft);

        if ((info.Phase != PartyPhase.Drawing) || (info.MyPrompts.Count == 0))
        {
            Hide();

            return;
        }

        var step = (info.Phase, info.Round, info.Item);

        if (step != Step)
        {
            Step = step;
            Toolbox = new SketchToolbox();
            CanvasEditor.Load(Toolbox.Drawing);
            SyncEditor();
            SentVersion = -1;
        }

        if (reopen)
            DismissedStep = null;

        if (DismissedStep != step)
            Hidden = false;

        var prompt = info.MyPrompts.FirstOrDefault(p => !p.Answered) ?? info.MyPrompts[0];
        CaptionLabel.Text = ClassToolText.PartyTitle(info);
        SecretLabel.Text = $"Draw: {prompt.Text} (only you can see this)";
        TimerLabel.Text = ClassToolText.Clock(Countdown.Left);

        RefreshTools();

        if (!Visible && !Hidden)
            Show();
    }

    /// <summary>ToolClosed: the game is over and the card goes.</summary>
    public void Close()
    {
        Step = null;
        DismissedStep = null;
        Hidden = false;
        SentVersion = -1;
        StatusLabel.Text = string.Empty;
        Toolbox = new SketchToolbox();
        CanvasEditor.Load(Toolbox.Drawing);
        SyncEditor();
        Hide();
    }

    /// <summary>Moves the drawing clock on by <paramref name="elapsed" /> and sends a changed drawing once at the deadline. WorldScreen is the only caller and calls it every frame, visible or not, so the clock keeps running while hidden.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (!Step.HasValue || (Info.Phase != PartyPhase.Drawing))
            return;

        Countdown.Advance(elapsed.TotalSeconds);
        TimerLabel.Text = ClassToolText.Clock(Countdown.Left);
        RefreshTools();

        if (!Info.Paused && (Countdown.Left == 0) && (Toolbox.Drawing.Version != SentVersion) && (Toolbox.Drawing.DrawnPixels > 0))
            SubmitDrawing();
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
        if (Step is { } step)
            DismissedStep = step;

        Hidden = true;
        Hide();
    }

    private void PickColour(int colour)
    {
        Toolbox.Colour = (byte)colour;
        SyncEditor();
        RefreshTools();
    }

    private void PickTool(SketchTool tool)
    {
        Toolbox.Tool = tool;
        SyncEditor();
        RefreshTools();
    }

    private void Undo()
    {
        CanvasEditor.Undo();
        RefreshTools();
    }

    private void Clear()
    {
        CanvasEditor.ClearAll();
        RefreshTools();
    }

    private void SubmitDrawing()
    {
        if (!Step.HasValue || (Info.Phase != PartyPhase.Drawing))
            return;

        if (Toolbox.Drawing.Version == SentVersion)
            return;

        SentVersion = Toolbox.Drawing.Version;

        Send?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.PartyDrawingSubmit,
                PartyPixels = Toolbox.Pixels()
            });

        RefreshTools();
    }

    private void RefreshTools()
    {
        var open = !Info.Paused && (Countdown.Left > 0);
        PenButton.Selected = Toolbox.Tool == SketchTool.Pen;
        BigPenButton.Selected = Toolbox.Tool == SketchTool.BigPen;
        FillButton.Selected = Toolbox.Tool == SketchTool.Fill;
        EraseButton.Selected = Toolbox.Tool == SketchTool.Erase;
        Swatches.Selected = Toolbox.Colour;
        UndoButton.Enabled = open && Toolbox.Drawing.CanUndo;
        ClearButton.Enabled = open;
        PenButton.Enabled = open;
        BigPenButton.Enabled = open;
        FillButton.Enabled = open;
        EraseButton.Enabled = open;
        DoneButton.Enabled = open;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if ((SentVersion == Toolbox.Drawing.Version) && (Info.Note.Length == 0) && Info.MyPrompts.All(p => p.Answered))
            StatusLabel.Text = SENT_TEXT;
        else
            StatusLabel.Text = Info.Note;
    }

    private void SyncEditor()
    {
        var colour = Toolbox.Colour < CollegeProtocol.SKETCH_COLOURS ? Toolbox.Colour : (byte)0;

        switch (Toolbox.Tool)
        {
            case SketchTool.Pen:
                CanvasEditor.Tool = ArtTool.Pen;
                SetBrush(1);
                CanvasEditor.Current = colour;

                break;
            case SketchTool.BigPen:
                CanvasEditor.Tool = ArtTool.Pen;
                SetBrush(3);
                CanvasEditor.Current = colour;

                break;
            case SketchTool.Fill:
                CanvasEditor.Tool = ArtTool.Fill;
                CanvasEditor.Current = colour;

                break;
            default:
                CanvasEditor.Tool = ArtTool.Eraser;
                SetBrush(1);

                break;
        }
    }

    private void SetBrush(int size)
    {
        while (CanvasEditor.BrushSize != size)
            CanvasEditor.CycleBrush();
    }

    private CustomButton ToolButton(string caption, int width, ref int x, Action onClick)
    {
        var button = AddButton(caption, width, x, TOOLS_TOP, onClick);
        x += width + TOOL_GAP;

        return button;
    }

    private sealed class SwatchStrip : UIElement
    {
        private const int CELL_WIDTH = 20;
        private const int CELL_HEIGHT = 14;
        private const int GAP = 2;
        public const int STRIP_WIDTH = CollegeProtocol.SKETCH_COLOURS * (CELL_WIDTH + GAP) - GAP;

        public SwatchStrip()
        {
            Width = STRIP_WIDTH;
            Height = CELL_HEIGHT;
        }

        public int Selected { get; set; } = 1;

        public event Action<int>? Picked;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            base.Draw(spriteBatch);

            for (var i = 0; i < CollegeProtocol.SKETCH_COLOURS; i++)
            {
                var bounds = new Rectangle(ScreenX + (i * (CELL_WIDTH + GAP)), ScreenY, CELL_WIDTH, CELL_HEIGHT);
                DrawRectClipped(spriteBatch, bounds, SketchToolbox.Colours[i]);
                DrawBorder(spriteBatch, bounds, i == Selected ? Color.White : Color.Black);
            }
        }

        public override void OnClick(ClickEvent e)
        {
            e.Handled = true;

            if (e.Button != MouseButton.Left)
                return;

            var offset = e.ScreenX - ScreenX;
            var index = offset / (CELL_WIDTH + GAP);

            if ((index >= 0) && (index < CollegeProtocol.SKETCH_COLOURS) && ((offset % (CELL_WIDTH + GAP)) < CELL_WIDTH))
                Picked?.Invoke(index);
        }
    }
}

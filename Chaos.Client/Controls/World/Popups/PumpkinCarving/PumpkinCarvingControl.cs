#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.PumpkinCarving;

/// <summary>
///     The Pumpkin Carving window (spec: docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md, 5.1): the 22 × 14
///     grid, knife, eraser, mirror, undo, redo and clear, a lit preview of the pumpkin and the time left. Saves to the
///     server every 600 ms while changed, so everyone watches the carving live; Done and Close send a closing save.
///     Opened by the server's PumpkinCarvingDisplay Open.
/// </summary>
/// <remarks>Layout: title; the canvas with the preview and timer on its right; two rows of three tools; a status line; Done.</remarks>
public sealed class PumpkinCarvingControl : GuildCloakDialogBase
{
    private const int BORDER_GAP = 4;
    private const int CANVAS_HEIGHT = PumpkinGrid.HEIGHT * ZOOM;
    private const int CANVAS_WIDTH = PumpkinGrid.WIDTH * ZOOM;
    private const int CONTENT_TOP = 32;
    private const int DONE_WIDTH = 64;
    private const int GAP = 8;
    private const int LEFT = 20;
    //mirrors the server's PumpkinCarving.MIN_CUT_CELLS
    private const int MIN_CUTS = 6;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int PREVIEW_SIZE = 88;
    private const int TITLE_TOP = 10;
    private const int TOOL_SPACING = 3;
    private const int TOOL_WIDTH = (CANVAS_WIDTH - (2 * TOOL_SPACING)) / 3;
    private const int WARN_SECONDS = 30;
    private const int ZOOM = 10;

    private static readonly Color WarnColor = new(230, 70, 50);

    private readonly PumpkinCarvingCanvas Canvas;
    private readonly CreatureRenderer Creatures;
    private readonly CustomButton EraserButton;
    private readonly CustomButton KnifeButton;
    private readonly CustomButton MirrorButton;
    private readonly PumpkinCarvingModel Model = new();
    private readonly int PreviewX;
    private readonly CustomButton RedoButton;
    private readonly UILabel StatusLabel;
    private readonly UILabel TimerLabel;
    private readonly CustomButton UndoButton;

    private long DeadlineMs;
    private long LastSendMs = long.MinValue / 2;
    private Color[]? PlainPixels;
    private int PlainHeight;
    private int PlainLeft;
    private int PlainTop;
    private int PlainWidth;
    private Texture2D? Preview;
    private int PreviewVersion = -1;
    private Color[]? Scratch;
    private int ShownVersion = -1;

    public PumpkinCarvingControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Creatures = creatures;
        Name = "PumpkinCarving";
        Visible = false;
        UsesControlStack = true;

        Canvas = new PumpkinCarvingCanvas(ZOOM)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        var toolsTop = CONTENT_TOP + CANVAS_HEIGHT + GAP;
        var secondToolRow = toolsTop + CustomButton.HEIGHT + TOOL_SPACING;
        var statusTop = secondToolRow + CustomButton.HEIGHT + GAP;
        var buttonsTop = statusTop + TextRenderer.CHAR_HEIGHT + GAP;
        PreviewX = LEFT + CANVAS_WIDTH + GAP;

        Width = PreviewX + PREVIEW_SIZE + LEFT;
        Height = buttonsTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Carve your pumpkin", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas.StrokeStarted += Model.BeginStroke;
        Canvas.CellPainted += Model.Apply;
        Canvas.StrokeEnded += Model.EndStroke;
        AddChild(Canvas);

        KnifeButton = ToolButton("Knife", 0, toolsTop, () => Model.Tool = PumpkinTool.Knife);
        EraserButton = ToolButton("Eraser", 1, toolsTop, () => Model.Tool = PumpkinTool.Eraser);
        MirrorButton = ToolButton("Mirror", 2, toolsTop, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", 0, secondToolRow, Model.Undo);
        RedoButton = ToolButton("Redo", 1, secondToolRow, Model.Redo);
        ToolButton("Clear", 2, secondToolRow, Model.Clear);

        TimerLabel = Caption(string.Empty, PreviewX, CONTENT_TOP + PREVIEW_SIZE + GAP, PREVIEW_SIZE, HorizontalAlignment.Center);
        StatusLabel = Caption(string.Empty, LEFT, statusTop, CANVAS_WIDTH);
        AddButton("Done", DONE_WIDTH, Width - LEFT - DONE_WIDTH, buttonsTop, Finish);
    }

    /// <summary>Raised with a copy of the grid and whether the window is closing.</summary>
    public event Action<byte[], bool>? SaveRequested;

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        if (PreviewVersion != Model.Version)
            RebuildPreview();

        if (Preview is not null)
            DrawTextureFitted(
                spriteBatch,
                Preview,
                new Rectangle(ScreenX + PreviewX, ScreenY + CONTENT_TOP, Preview.Width * 2, Preview.Height * 2),
                Color.White);
    }

    /// <summary>
    ///     The server closed the window (time up, or the pumpkin was removed). Always sends a closing save of the whole
    ///     grid: the server drops a periodic save that arrives too soon after the last one, so the last strokes may not have
    ///     reached it yet, and at time up it waits for them before the reveal.
    /// </summary>
    public void OnServerClose()
    {
        if (Visible)
            Send(true);

        Hide();
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

    /// <summary>Shows the window with the server's grid. Reopening an open window keeps strokes not yet sent.</summary>
    public void Open(ushort secondsLeft, byte[] grid)
    {
        DeadlineMs = Environment.TickCount64 + (secondsLeft * 1000L);

        if (!Visible)
            Model.Load(grid);

        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var now = Environment.TickCount64;
        var secondsLeft = (int)Math.Max(0, (DeadlineMs - now + 999) / 1000);
        TimerLabel.Text = $"{secondsLeft / 60}:{secondsLeft % 60:00} left";
        TimerLabel.ForegroundColor = secondsLeft < WARN_SECONDS ? WarnColor : LegendColors.White;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            Canvas.SetGrid(Model.Grid);
            RefreshControls();
        }

        if (Model.SaveDue(now, LastSendMs))
            Send(closing: false);
    }

    private void Finish()
    {
        Send(closing: true);
        Hide();
    }

    private void RebuildPreview()
    {
        PreviewVersion = Model.Version;

        //no preview until the launcher patch with the pumpkin sprite is installed
        if (Creatures.GetFrame(PumpkinFaceMap.SPRITE_ID, PumpkinFaceMap.FRONT_FRAME) is not { } plain)
            return;

        if (PlainPixels is null)
        {
            PlainWidth = plain.Texture.Width;
            PlainHeight = plain.Texture.Height;
            PlainLeft = plain.Left;
            PlainTop = plain.Top;
            PlainPixels = new Color[PlainWidth * PlainHeight];
            plain.Texture.GetData(PlainPixels);
            Scratch = new Color[PlainPixels.Length];
        }

        PlainPixels.CopyTo(Scratch!, 0);

        PumpkinPainter.Paint(
            Scratch!,
            PlainWidth,
            PlainHeight,
            PlainLeft,
            PlainTop,
            PumpkinFaceMap.Shared.For(PumpkinFaceMap.FRONT_FRAME),
            Model.Grid,
            0);

        Preview ??= new Texture2D(TextureConverter.Device, PlainWidth, PlainHeight);
        Preview.SetData(Scratch!);
    }

    private void RefreshControls()
    {
        KnifeButton.Selected = Model.Tool == PumpkinTool.Knife;
        EraserButton.Selected = Model.Tool == PumpkinTool.Eraser;
        MirrorButton.Selected = Model.Mirror;
        UndoButton.Enabled = Model.CanUndo;
        RedoButton.Enabled = Model.CanRedo;

        StatusLabel.Text = Model.CutCount < MIN_CUTS
            ? $"Cuts: {Model.CutCount}. Cut at least {MIN_CUTS} to count."
            : $"Cuts: {Model.CutCount}. Right-click erases.";
    }

    private void RequestClose()
    {
        Send(closing: true);
        Hide();
    }

    private void Send(bool closing)
    {
        LastSendMs = Environment.TickCount64;
        SaveRequested?.Invoke(Model.TakeForSend(), closing);
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

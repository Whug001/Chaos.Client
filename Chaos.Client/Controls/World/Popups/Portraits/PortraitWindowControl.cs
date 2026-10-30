#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.Client.ViewModel.Portraits;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Portraits;

/// <summary>
///     The Portrait window: the 48 x 56 canvas at 4x with pen, fill, eyedropper, mirror and undo, 32 full-colour
///     swatches with the Stage Lighting colour picker, an actual-size preview, Upload/Clear/Set/Remove/Close and a
///     status line. Opens from the own status-book portrait; Plan 3 adds Make Painted Portrait in the reserved slot.
/// </summary>
public sealed class PortraitWindowControl : GuildCloakDialogBase
{
    private const int WIDTH = 400;
    private const int LEFT = 16;
    private const int TITLE_TOP = 10;
    private const int CONTENT_TOP = 40;
    private const int TOOL_WIDTH = 46;
    private const int TOOL_STEP = CustomButton.HEIGHT + 2;
    private const int GROUP_GAP = 6;
    private const int CANVAS_X = LEFT + TOOL_WIDTH + 6;
    private const int CANVAS_Y = CONTENT_TOP;
    private const int RIGHT_X = CANVAS_X + (PortraitCanvas.WIDTH * PortraitCanvasView.ZOOM) + 8;
    private const int CURRENT_SIZE = 24;
    private const int SWATCHES_Y = CANVAS_Y + CURRENT_SIZE + 21;
    private const int PREVIEW_LABEL_Y = SWATCHES_Y + PortraitSwatches.HEIGHT + 4;
    private const int PREVIEW_Y = PREVIEW_LABEL_Y + TextRenderer.CHAR_HEIGHT + 2;
    private const int BUTTONS_TOP = PREVIEW_Y + PortraitCanvas.HEIGHT + 8;
    private const int STATUS_TOP = BUTTONS_TOP + CustomButton.HEIGHT + 6;
    private const int STATUS_LINES = 2;

    //a wrapped status keeps its 1px top and bottom label padding
    private const int STATUS_HEIGHT = (STATUS_LINES * TextRenderer.CHAR_HEIGHT) + 2;
    private const int UPLOAD_WIDTH = 62;
    private const int CLEAR_WIDTH = 52;
    private const int SET_WIDTH = 48;

    //reserved for Plan 3's Make Painted Portrait button, between Set and Remove
    private const int MAKE_WIDTH = 62;
    private const int REMOVE_WIDTH = 62;
    private const int CLOSE_WIDTH = 52;
    private const int BUTTON_GAP = 6;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly PortraitCanvasView Canvas;
    private readonly UIPanel CurrentBox;
    private readonly UILabel CurrentHex;
    private readonly CustomButton FillButton;
    private readonly nint GameWindow;
    private readonly CustomButton MirrorButton;
    private readonly CustomButton PenButton;
    private readonly CustomButton PickButton;
    private readonly StageColorPicker Picker;
    private readonly CustomButton PickerCancel;
    private readonly UILabel PreviewLabel;
    private readonly CustomButton RedoButton;
    private readonly PortraitWindowState State = new();
    private readonly UILabel Status;
    private readonly PortraitSwatches Swatches;
    private readonly CustomButton UndoButton;

    private int EditingSwatch = -1;
    private int OpenGeneration;
    private int PickingGeneration;
    private Task<(byte[]? Bytes, string Error)>? Picking;
    private Texture2D? Preview;
    private Color[]? PreviewColors;
    private int PreviewVersion = -1;

    public PortraitWindowControl(nint gameWindow)
        : base("_nsett", false)
    {
        GameWindow = gameWindow;
        Name = "PortraitWindow";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = STATUS_TOP + STATUS_HEIGHT + 8 + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Portrait", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas = new PortraitCanvasView(State)
        {
            X = CANVAS_X,
            Y = CANVAS_Y
        };
        Canvas.Changed += RefreshControls;
        AddChild(Canvas);

        var y = CANVAS_Y;
        PenButton = ToolButton("Pen", y, () => State.Tool = PortraitTool.Pen);
        y += TOOL_STEP;
        FillButton = ToolButton("Fill", y, () => State.Tool = PortraitTool.Fill);
        y += TOOL_STEP;
        PickButton = ToolButton("Pick", y, () => State.Tool = PortraitTool.Pick);
        y += TOOL_STEP + GROUP_GAP;
        MirrorButton = ToolButton("Mirror", y, () => State.Canvas.Mirror = !State.Canvas.Mirror);
        y += TOOL_STEP + GROUP_GAP;
        UndoButton = ToolButton("Undo", y, () => State.Canvas.Undo());
        y += TOOL_STEP;
        RedoButton = ToolButton("Redo", y, () => State.Canvas.Redo());

        CurrentBox = new UIPanel
        {
            X = RIGHT_X,
            Y = CANVAS_Y,
            Width = CURRENT_SIZE,
            Height = CURRENT_SIZE,
            BorderColor = Color.White,
            IsHitTestVisible = false
        };
        AddChild(CurrentBox);
        CurrentHex = Caption(string.Empty, RIGHT_X, CANVAS_Y + CURRENT_SIZE + 3, WIDTH - LEFT - RIGHT_X, color: LegendColors.Gray);

        Swatches = new PortraitSwatches(State.Canvas, PickSwatch)
        {
            X = RIGHT_X,
            Y = SWATCHES_Y
        };
        Swatches.EditRequested += OpenPicker;
        AddChild(Swatches);

        PreviewLabel = Caption("Preview", RIGHT_X, PREVIEW_LABEL_Y, PortraitCanvas.WIDTH, color: LegendColors.Gray);

        var x = LEFT;
        AddButton("Upload", UPLOAD_WIDTH, x, BUTTONS_TOP, OnUploadClicked);
        x += UPLOAD_WIDTH + BUTTON_GAP;
        AddButton("Clear", CLEAR_WIDTH, x, BUTTONS_TOP, OnClearClicked);
        x += CLEAR_WIDTH + BUTTON_GAP;
        AddButton("Set", SET_WIDTH, x, BUTTONS_TOP, OnSetClicked);
        x += SET_WIDTH + BUTTON_GAP;
        AddButton("Make", MAKE_WIDTH, x, BUTTONS_TOP, OnMakeClicked);
        x += MAKE_WIDTH + BUTTON_GAP;
        AddButton("Remove", REMOVE_WIDTH, x, BUTTONS_TOP, OnRemoveClicked);
        x += REMOVE_WIDTH + BUTTON_GAP;
        AddButton("Close", CLOSE_WIDTH, x, BUTTONS_TOP, OnCloseClicked);

        Status = Caption(string.Empty, LEFT, STATUS_TOP, WIDTH - (2 * LEFT));
        Status.WordWrap = true;
        Status.VerticalAlignment = VerticalAlignment.Top;
        Status.Height = STATUS_HEIGHT;

        Picker = new StageColorPicker
        {
            X = WIDTH - LEFT - 126,
            Y = SWATCHES_Y,
            ZIndex = 1
        };
        Picker.Closed += ApplyPickedColour;
        AddChild(Picker);
        PickerCancel = AddButton("Cancel", 60, Picker.X, Picker.Y + Picker.Height + 2, ClosePicker);
        PickerCancel.ZIndex = 1;
        PickerCancel.Visible = false;
    }

    /// <summary>Raised with the canvas PNG bytes when Set is clicked.</summary>
    public event Action<byte[]>? SetRequested;

    /// <summary>Raised with the canvas PNG bytes when Make is clicked.</summary>
    public event Action<byte[]>? MakeRequested;

    /// <summary>Raised when Remove is clicked, after which the screen sends the empty set and deletes the local files.</summary>
    public event Action? RemoveRequested;

    /// <summary>Loads the own-portrait bytes onto the canvas and refills the swatches; empty or null loads a blank canvas.</summary>
    public void Open(byte[]? ownPicture)
    {
        OpenGeneration++;
        State.Open(ownPicture);
        Swatches.Selected = 0;
        ClosePicker();
        Status.Text = string.Empty;
        RefreshControls();
        Show();
    }

    /// <summary>Whether a Remove is still waiting for the server's empty echo; the screen skips the file sync while true.</summary>
    public bool IsRemovePending => State.PendingRemove;

    /// <summary>Answers the pending Set or Remove once the owner's portrait picture arrives back.</summary>
    public void OnOwnPicture(string hash, byte[] picture)
    {
        if (State.SettleOwnPicture(hash, picture.Length) is { } status)
            Status.Text = status;
    }

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        //the actual-size preview sits under the colour picker, so both hide while a swatch colour is picked
        PreviewLabel.Visible = !Picker.Visible;
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        if (PreviewVersion != State.Canvas.Version)
            RebuildPreview();

        if ((Preview is not null) && !Picker.Visible)
            DrawTexture(spriteBatch, Preview, new Vector2(ScreenX + RIGHT_X, ScreenY + PREVIEW_Y), Color.White);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        ClosePicker();
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (Picker.Visible)
                ClosePicker();
            else
                OnCloseClicked();

            e.Handled = true;

            return;
        }

        if (e.Accelerator && (e.Keycode is Keycode.Z or Keycode.Y))
        {
            if (e.Keycode == Keycode.Z)
                State.Canvas.Undo();
            else
                State.Canvas.Redo();

            RefreshControls();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        FinishPicking();
    }

    //the swatch grid owns its highlight: a pick only changes the current color and refreshes the tools
    private void PickSwatch(Color color)
    {
        State.Pick(color);
        RefreshControls();
    }

    private void RefreshControls()
    {
        PenButton.Selected = State.Tool == PortraitTool.Pen;
        FillButton.Selected = State.Tool == PortraitTool.Fill;
        PickButton.Selected = State.Tool == PortraitTool.Pick;
        MirrorButton.Selected = State.Canvas.Mirror;
        UndoButton.Enabled = State.Canvas.CanUndo;
        RedoButton.Enabled = State.Canvas.CanRedo;

        CurrentBox.BackgroundColor = ToPremultiplied(State.Current);
        var current = State.Current;
        CurrentHex.Text = current.A switch
        {
            0 => "Clear",
            255 => $"#{current.R:X2}{current.G:X2}{current.B:X2}",
            _ => $"#{current.R:X2}{current.G:X2}{current.B:X2} a{current.A}"
        };
    }

    private CustomButton ToolButton(string caption, int y, Action onClick)
        => AddButton(
            caption,
            TOOL_WIDTH,
            LEFT,
            y,
            () =>
            {
                ClearStatus();
                onClick();
                RefreshControls();
            });

    private void ClearStatus()
        => Status.Text = string.Empty;

    private void OnClearClicked()
    {
        ClearStatus();
        State.Canvas.Clear();
        RefreshControls();
    }

    private void OnSetClicked()
    {
        ClearStatus();
        var png = State.Canvas.ToPng();
        State.BeginSet(PortraitCanvas.HashOf(png));
        Status.Text = "Portrait sent.";
        SetRequested?.Invoke(png);
    }

    private void OnMakeClicked()
    {
        ClearStatus();
        var png = State.Canvas.ToPng();
        Status.Text = "Portrait sent.";
        MakeRequested?.Invoke(png);
    }

    private void OnRemoveClicked()
    {
        ClearStatus();
        State.BeginRemove();
        Status.Text = "Portrait sent.";
        RemoveRequested?.Invoke();
    }

    private void OnCloseClicked()
        => Hide();

    private void OnUploadClicked()
    {
        ClearStatus();

        if (Picking is not null)
            return;

        var owner = Sdl.GetWin32Window(GameWindow);
        PickingGeneration = OpenGeneration;
        Picking = Task.Run(() => PickAndReadAsync(owner));
    }

    //the file dialog and the file read run off the game thread; the result is consumed in Update
    private static async Task<(byte[]? Bytes, string Error)> PickAndReadAsync(nint owner)
    {
        var path = await PictureFilePicker.PickAsync(owner);

        if (path is null)
            return (null, string.Empty);

        try
        {
            if (new FileInfo(path).Length > PortraitCanvas.MAX_UPLOAD_FILE_BYTES)
                return (null, "That file is too large for a portrait.");

            return (await File.ReadAllBytesAsync(path), string.Empty);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, "Could not read that file.");
        }
    }

    private void FinishPicking()
    {
        if (Picking is not { IsCompleted: true } done)
            return;

        Picking = null;

        //reading Exception marks a fault observed
        var failed = done.Exception is not null || !done.IsCompletedSuccessfully;

        //a file chosen for a window that has since closed, or been reopened, is dropped
        if (!Visible || (PickingGeneration != OpenGeneration))
            return;

        if (failed)
        {
            Status.Text = "Could not read that file.";

            return;
        }

        var (bytes, error) = done.Result;

        if (bytes is null)
        {
            //an empty error is a cancelled dialog
            Status.Text = error;

            return;
        }

        Status.Text = State.Canvas.LoadUpload(bytes);

        if (Status.Text.Length == 0)
            RefreshControls();
    }

    private void OpenPicker(int swatch)
    {
        EditingSwatch = swatch;
        Picker.Open(State.Canvas.Swatches[swatch]);
        PickerCancel.Visible = true;
    }

    //StageColorPicker's OK hides the picker, then raises Closed
    private void ApplyPickedColour()
    {
        if (EditingSwatch >= 0)
        {
            State.Canvas.SetSwatch(EditingSwatch, Picker.Current);

            if (EditingSwatch == Swatches.Selected)
                State.Pick(Picker.Current);
        }

        EditingSwatch = -1;
        PickerCancel.Visible = false;
        RefreshControls();
    }

    private void ClosePicker()
    {
        EditingSwatch = -1;
        Picker.Visible = false;
        PickerCancel.Visible = false;
    }

    private void RebuildPreview()
    {
        PreviewVersion = State.Canvas.Version;
        Preview ??= new Texture2D(TextureConverter.Device, PortraitCanvas.WIDTH, PortraitCanvas.HEIGHT);
        PreviewColors ??= new Color[PortraitCanvas.WIDTH * PortraitCanvas.HEIGHT];

        var pixels = State.Canvas.Pixels;

        for (var i = 0; i < pixels.Length; i++)
            PreviewColors[i] = ToPremultiplied(pixels[i]);

        Preview.SetData(PreviewColors);
    }

    //the canvas stores plain RGBA but the UI batch blends premultiplied, so texture data and draw colours
    //are premultiplied here; opaque and transparent pixels come through unchanged
    private static Color ToPremultiplied(Color color)
        => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);

    /// <summary>The canvas at 4x with grid and mirror lines. Mouse input paints the state in canvas pixels.</summary>
    private sealed class PortraitCanvasView : UIElement
    {
        public const int ZOOM = 4;

        private static readonly Color GridLine = new(0, 0, 0, 50);
        private static readonly Color MirrorLine = Color.White * (150 / 255f);

        private readonly PortraitWindowState State;
        private bool Erasing;
        private Point Last;
        private bool Painting;
        private Texture2D? Texture;
        private Color[]? TextureColors;
        private int TextureVersion = -1;

        public PortraitCanvasView(PortraitWindowState state)
        {
            State = state;
            Width = PortraitCanvas.WIDTH * ZOOM;
            Height = PortraitCanvas.HEIGHT * ZOOM;
        }

        /// <summary>Raised after a press, a drag or a release, so the window can refresh its buttons.</summary>
        public event Action? Changed;

        public override void Dispose()
        {
            Texture?.Dispose();
            base.Dispose();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            base.Draw(spriteBatch);
            EnsureTexture();
            DrawTexture(spriteBatch, Texture, new Vector2(ScreenX, ScreenY), Color.White);

            for (var x = 1; x < PortraitCanvas.WIDTH; x++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * ZOOM), ScreenY, 1, Height), GridLine);

            for (var y = 1; y < PortraitCanvas.HEIGHT; y++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * ZOOM), Width, 1), GridLine);

            if (State.Canvas.Mirror)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (Width / 2), ScreenY, 1, Height), MirrorLine);
        }

        public override void OnMouseDown(MouseDownEvent e)
        {
            e.Handled = true;

            if (Painting || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
                return;

            var (x, y) = CellAt(e.ScreenX, e.ScreenY);

            if (!State.Canvas.Contains(x, y))
                return;

            Erasing = e.Button == MouseButton.Right;

            if (!Erasing && (State.Tool == PortraitTool.Pick))
            {
                State.Pick(State.Canvas.Pick(x, y));
                Changed?.Invoke();

                return;
            }

            State.Canvas.BeginStep();

            if (!Erasing && (State.Tool == PortraitTool.Fill))
            {
                State.Canvas.Fill(x, y, State.Current);
                State.Canvas.EndStep();
                Changed?.Invoke();

                return;
            }

            Painting = true;
            Last = new Point(x, y);
            Stamp(x, y);
        }

        public override void OnMouseMove(MouseMoveEvent e)
        {
            if (!Painting)
                return;

            e.Handled = true;
            var (x, y) = CellAt(e.ScreenX, e.ScreenY);

            //the button came up where no mouse-up reached this canvas (e.g. the window lost focus mid-stroke)
            if (!(Erasing ? InputBuffer.IsRightButtonHeld : InputBuffer.IsLeftButtonHeld))
            {
                Finish();
                return;
            }

            var to = new Point(x, y);

            if (to != Last)
            {
                foreach (var point in ArtEditor.LinePoints(Last, to).Skip(1))
                    Stamp(point.X, point.Y);

                Last = to;
            }
        }

        public override void OnMouseUp(MouseUpEvent e)
        {
            if (!Painting)
                return;

            e.Handled = true;
            Finish();
        }

        public override void ResetInteractionState()
        {
            base.ResetInteractionState();

            if (!Painting)
                return;

            Painting = false;
            State.Canvas.EndStep();
            Changed?.Invoke();
        }

        private void Stamp(int x, int y)
            => State.Canvas.Set(x, y, Erasing ? PortraitCanvas.Transparent : State.Current);

        private (int X, int Y) CellAt(int screenX, int screenY)
            => ((int)Math.Floor((screenX - ScreenX) / (double)ZOOM), (int)Math.Floor((screenY - ScreenY) / (double)ZOOM));

        private void Finish()
        {
            Painting = false;
            State.Canvas.EndStep();
            Changed?.Invoke();
        }

        private void EnsureTexture()
        {
            var canvas = State.Canvas;

            if ((Texture is not null) && (TextureVersion == canvas.Version))
                return;

            Texture ??= new Texture2D(TextureConverter.Device, Width, Height);
            TextureColors ??= new Color[Width * Height];

            for (var y = 0; y < PortraitCanvas.HEIGHT; y++)
                for (var x = 0; x < PortraitCanvas.WIDTH; x++)
                {
                    var color = ToPremultiplied(canvas.Pixels[(y * PortraitCanvas.WIDTH) + x]);

                    for (var dy = 0; dy < ZOOM; dy++)
                        Array.Fill(TextureColors, color, (((y * ZOOM) + dy) * Width) + (x * ZOOM), ZOOM);
                }

            Texture.SetData(TextureColors);
            TextureVersion = canvas.Version;
        }
    }

    /// <summary>The canvas's 32 swatches in four columns. A click picks a swatch; a double-click asks to change its colour.</summary>
    private sealed class PortraitSwatches : UIElement
    {
        public const int HEIGHT = (ROWS * STEP_Y) - 1;

        private const int CELL_WIDTH = 20;
        private const int CELL_HEIGHT = 14;
        private const int COLUMNS = 4;
        private const int ROWS = (PortraitCanvas.SWATCH_COUNT + COLUMNS - 1) / COLUMNS;
        private const int STEP_X = CELL_WIDTH + 2;
        private const int STEP_Y = CELL_HEIGHT + 1;

        private readonly PortraitCanvas Canvas;
        private readonly Action<Color> Pick;
        private int LastClick = -1;
        private int PreviousClick = -1;

        public PortraitSwatches(PortraitCanvas canvas, Action<Color> pick)
        {
            Canvas = canvas;
            Pick = pick;
            Width = (COLUMNS * STEP_X) - 2;
            Height = HEIGHT;
            Selected = 0;
        }

        public int Selected { get; set; }

        public event Action<int>? EditRequested;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            base.Draw(spriteBatch);

            for (var i = 0; i < PortraitCanvas.SWATCH_COUNT; i++)
            {
                var bounds = new Rectangle(ScreenX + ((i % COLUMNS) * STEP_X), ScreenY + ((i / COLUMNS) * STEP_Y), CELL_WIDTH, CELL_HEIGHT);
                DrawRectClipped(spriteBatch, bounds, ToPremultiplied(Canvas.Swatches[i]));
                DrawBorder(spriteBatch, bounds, i == Selected ? Color.White : Color.Black);
            }
        }

        public override void OnClick(ClickEvent e)
        {
            e.Handled = true;
            PreviousClick = LastClick;
            LastClick = -1;

            if (e.Button != MouseButton.Left)
                return;

            if (SwatchAt(e.ScreenX, e.ScreenY) is { } swatch)
            {
                LastClick = swatch;
                Selected = swatch;
                Pick(Canvas.Swatches[swatch]);
            }
        }

        //the dispatcher raises Click and then DoubleClick for the second release, and pairs any two clicks on this element
        public override void OnDoubleClick(DoubleClickEvent e)
        {
            e.Handled = true;

            if ((e.Button == MouseButton.Left) && (LastClick >= 0) && (PreviousClick == LastClick))
                EditRequested?.Invoke(LastClick);

            LastClick = -1;
            PreviousClick = -1;
        }

        private int? SwatchAt(int screenX, int screenY)
        {
            var x = screenX - ScreenX;
            var y = screenY - ScreenY;

            if ((x < 0) || (y < 0))
                return null;

            var column = x / STEP_X;
            var row = y / STEP_Y;

            if ((column >= COLUMNS) || (row >= ROWS))
                return null;

            var swatch = (row * COLUMNS) + column;

            return swatch < PortraitCanvas.SWATCH_COUNT ? swatch : null;
        }
    }
}

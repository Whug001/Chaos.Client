#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Converters;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The board editor (576 x 452): the Art tools plus a text tool over the 160 x 80 board at zoom 3. While a class runs it
///     sends the board at most every 600 ms while it changed, and once more on close; in prep mode it sends nothing.
/// </summary>
public sealed class BoardEditorControl : GuildCloakDialogBase
{
    private const int WIDTH = 576;
    private const int HEIGHT = 452;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TOOL_WIDTH = 42;
    private const int TOOL_STEP = CustomButton.HEIGHT + 2;
    private const int GROUP_GAP = 6;
    private const int CANVAS_X = LEFT + TOOL_WIDTH + 4;
    private const int CANVAS_Y = 30;
    private const int CANVAS_WIDTH = CollegeProtocol.BOARD_WIDTH * CollegeProtocol.BOARD_ZOOM;
    private const int CANVAS_HEIGHT = CollegeProtocol.BOARD_HEIGHT * CollegeProtocol.BOARD_ZOOM;
    private const int SWATCHES_Y = CANVAS_Y + CANVAS_HEIGHT + 6;
    private const int SWATCH_COLUMNS = 16;
    private const int TEXT_ROW_Y = SWATCHES_Y + 36;
    private const int TEXT_BOX_WIDTH = 260;
    private const int SCALE_X = CANVAS_X + TEXT_BOX_WIDTH + 6;
    private const int SCALE_WIDTH = 64;
    private const int STAMP_X = SCALE_X + SCALE_WIDTH + 6;
    private const int STAMP_WIDTH = 60;
    private const int STATUS_Y = TEXT_ROW_Y + CustomButton.HEIGHT + 6;
    private const int BUTTONS_TOP = HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int CLEAR_WIDTH = 60;
    private const int NAME_X = LEFT + CLEAR_WIDTH + 8;
    private const int NAME_WIDTH = 150;
    private const int SAVE_X = NAME_X + NAME_WIDTH + 4;
    private const int SAVE_WIDTH = 60;
    private const int BOARDS_X = SAVE_X + SAVE_WIDTH + 6;
    private const int BOARDS_WIDTH = 80;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string SAVING = "Saving...";

    private readonly CustomButton BoardsButton;
    private readonly ArtCanvas Canvas;
    private readonly ArtEditor Editor = new(BoardState.NewBoard());
    private readonly CustomButton MirrorButton;
    private readonly CustomTextBox NameBox;
    private readonly StageColorPicker Picker;
    private readonly CustomButton PickerCancel;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly CustomButton ScaleButton;
    private readonly CustomButton SizeButton;
    private readonly UILabel Status;
    private readonly ArtSwatches Swatches;
    private readonly BoardSync Sync = new();
    private readonly CustomTextBox TextBox;
    private readonly Dictionary<ArtTool, CustomButton> ToolButtons = new();
    private readonly CustomButton UndoButton;

    private bool CanSave;
    private int EditingSwatch = -1;

    public BoardEditorControl()
        : base("_nsett", false)
    {
        Name = "BoardEditor";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Close, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Board editor", LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas = new ArtCanvas(Editor, CollegeProtocol.BOARD_ZOOM)
        {
            X = CANVAS_X,
            Y = CANVAS_Y,
            ShowGrid = false
        };

        Canvas.Changed += RefreshControls;
        AddChild(Canvas);

        var y = CANVAS_Y;

        foreach (var (tool, caption) in new[]
                 {
                     (ArtTool.Pen, "Pen"),
                     (ArtTool.Eraser, "Erase"),
                     (ArtTool.Fill, "Fill"),
                     (ArtTool.Line, "Line"),
                     (ArtTool.Pick, "Pick"),
                     (ArtTool.Text, "Text")
                 })
        {
            ToolButtons[tool] = ToolButton(caption, y, () => Editor.Tool = tool);
            y += TOOL_STEP;
        }

        y += GROUP_GAP;
        SizeButton = ToolButton("Size 1", y, Editor.CycleBrush);
        y += TOOL_STEP;
        MirrorButton = ToolButton("Mirror", y, () => Editor.Mirror = !Editor.Mirror);
        y += TOOL_STEP + GROUP_GAP;
        UndoButton = ToolButton("Undo", y, Editor.Undo);
        y += TOOL_STEP;
        RedoButton = ToolButton("Redo", y, Editor.Redo);

        Swatches = new ArtSwatches(Editor, SWATCH_COLUMNS)
        {
            X = CANVAS_X,
            Y = SWATCHES_Y
        };

        Swatches.Picked += swatch =>
        {
            Editor.Current = (byte)swatch;

            if (Editor.Tool is ArtTool.Eraser or ArtTool.Pick)
                Editor.Tool = ArtTool.Pen;

            RefreshControls();
        };

        Swatches.EditRequested += OpenPicker;
        AddChild(Swatches);

        TextBox = new CustomTextBox
        {
            X = CANVAS_X,
            Y = TEXT_ROW_Y,
            Width = TEXT_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_BOARD_TEXT_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Text to stamp"
        };

        AddChild(TextBox);
        ScaleButton = AddButton("Small", SCALE_WIDTH, SCALE_X, TEXT_ROW_Y, OnScaleClicked);
        AddButton("Stamp", STAMP_WIDTH, STAMP_X, TEXT_ROW_Y, Stamp);
        Status = Caption(string.Empty, LEFT, STATUS_Y, INNER_WIDTH, color: LegendColors.Gray);

        AddButton(
            "Clear",
            CLEAR_WIDTH,
            LEFT,
            BUTTONS_TOP,
            () =>
            {
                Editor.ClearAll();
                RefreshControls();
            });

        NameBox = new CustomTextBox
        {
            X = NAME_X,
            Y = BUTTONS_TOP,
            Width = NAME_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_BOARD_NAME_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Board name"
        };

        AddChild(NameBox);
        SaveButton = AddButton("Save", SAVE_WIDTH, SAVE_X, BUTTONS_TOP, OnSaveClicked);
        BoardsButton = AddButton("Boards...", BOARDS_WIDTH, BOARDS_X, BUTTONS_TOP, () => SavedListRequested?.Invoke());

        Picker = new StageColorPicker
        {
            X = CANVAS_X + CANVAS_WIDTH - 140,
            Y = CANVAS_Y + 8,
            ZIndex = 1
        };

        Picker.Closed += ApplyPickedColour;
        AddChild(Picker);
        PickerCancel = AddButton("Cancel", 60, Picker.X, Picker.Y + Picker.Height + 2, ClosePicker);
        PickerCancel.ZIndex = 1;
        PickerCancel.Visible = false;
    }

    /// <summary>Raised with BoardUpdate and BoardSave.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    public event Action? SavedListRequested;

    public bool IsLive { get; private set; }

    /// <summary>Opens the editor on the room's board (a blank one when the state has none).</summary>
    public void Open(BoardState state)
    {
        IsLive = state.Info.Live;
        Load(state.Drawing);
        NameBox.Text = string.Empty;
        Status.Text = string.Empty;
        SetCanSave(state.Info.CanSave);
        ClosePicker();
        RefreshControls();

        if (!Visible)
            Show();
    }

    /// <summary>Applies a board the server sent while the editor is open.</summary>
    public void Apply(BoardState state)
    {
        var info = state.Info;

        if (!info.CanDraw || (IsLive && !info.Live))
        {
            Hide();

            return;
        }

        IsLive = info.Live;

        if (info.Replace && state.Drawing is not null)
        {
            Load(state.Drawing);
            RefreshControls();
        }

        SetCanSave(info.CanSave);
    }

    public void ShowStatus(string message) => Status.Text = message;

    public override void Hide()
    {
        if (!Visible)
            return;

        Editor.Cancel();
        TextBox.IsFocused = false;
        NameBox.IsFocused = false;
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
                Close();

            e.Handled = true;

            return;
        }

        if (TextBox.IsFocused && (e.Keycode == Keycode.Enter))
        {
            Stamp();
            e.Handled = true;

            return;
        }

        if (e.Accelerator && !TextBox.IsFocused && !NameBox.IsFocused && (e.Keycode is Keycode.Z or Keycode.Y))
        {
            if (e.Keycode == Keycode.Z)
                Editor.Undo();
            else
                Editor.Redo();

            RefreshControls();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            var filtered = BoardFont.FilterText(TextBox.Text);

            if (filtered != TextBox.Text)
                TextBox.Text = filtered;

            if (Editor.PendingText != filtered)
                Editor.PendingText = filtered;

            var now = Environment.TickCount64;

            if (IsLive && Sync.Due(Editor.Drawing.Version, now))
                SendBoard(now);
        }

        base.Update(gameTime);
    }

    private void Load(PixelDrawing? drawing)
    {
        Editor.Load(
            drawing is null
                ? BoardState.NewBoard()
                : PixelDrawing.From(drawing.PaletteBytes(), drawing.Pixels, drawing.Width, drawing.Height));

        TextBox.Text = string.Empty;
        Sync.Reset(Editor.Drawing.Version);
    }

    private void SetCanSave(bool canSave)
    {
        CanSave = canSave;
        SaveButton.Visible = canSave;
        NameBox.Visible = canSave;
        BoardsButton.Visible = canSave;
    }

    private void Close()
    {
        SendUnsent();
        Hide();
    }

    /// <summary>Sends a live board's strokes that the 600 ms timer has not sent yet.</summary>
    public void SendUnsent()
    {
        if (Visible && IsLive && Sync.Unsent(Editor.Drawing.Version))
            SendBoard(Environment.TickCount64);
    }

    private void SendBoard(long now)
    {
        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.BoardUpdate,
                Data = CollegeBoardCodec.Pack(Editor.Drawing.PaletteBytes(), Editor.Drawing.Pixels)
            });

        Sync.MarkSent(Editor.Drawing.Version, now);
    }

    private void OnSaveClicked()
    {
        if (!CanSave)
            return;

        Status.Text = SAVING;

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.BoardSave,
                Text = NameBox.Text.Trim(),
                Data = CollegeBoardCodec.Pack(Editor.Drawing.PaletteBytes(), Editor.Drawing.Pixels)
            });
    }

    private void OnScaleClicked()
    {
        Editor.TextScale = Editor.TextScale == 1 ? 2 : 1;
        RefreshControls();
    }

    private void Stamp()
    {
        Editor.PendingText = BoardFont.FilterText(TextBox.Text);
        Editor.StampText();
        TextBox.Text = string.Empty;
        RefreshControls();
    }

    private void RefreshControls()
    {
        foreach (var (tool, button) in ToolButtons)
            button.Selected = Editor.Tool == tool;

        SizeButton.Caption = $"Size {Editor.BrushSize}";
        MirrorButton.Selected = Editor.Mirror;
        ScaleButton.Caption = Editor.TextScale == 1 ? "Small" : "Large";
        UndoButton.Enabled = Editor.Drawing.CanUndo;
        RedoButton.Enabled = Editor.Drawing.CanRedo;
    }

    private CustomButton ToolButton(string caption, int y, Action onClick)
        => AddButton(
            caption,
            TOOL_WIDTH,
            LEFT,
            y,
            () =>
            {
                onClick();
                RefreshControls();
            });

    private void OpenPicker(int swatch)
    {
        EditingSwatch = swatch;
        Picker.Open(Editor.Drawing.Palette[swatch]);
        PickerCancel.Visible = true;
    }

    //StageColorPicker's OK hides the picker, then raises Closed
    private void ApplyPickedColour()
    {
        if (EditingSwatch >= 0)
            Editor.SetColour(EditingSwatch, Picker.Current);

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
}

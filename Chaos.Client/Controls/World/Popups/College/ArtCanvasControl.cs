#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.College.Library;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

public enum ArtCanvasMode
{
    Draft,
    HandIn,
    Picture,
    Library
}

/// <summary>
///     The Art canvas window (spec: docs/superpowers/specs/2026-10-05-mileth-college-art-design.md): title, tools, the
///     zoom-4 canvas, the 32 swatches, the artist's note and the footer. Draft and HandIn send the piece to the server;
///     Picture mode (from the writing window's Draw or Edit) raises <see cref="PictureMade" /> and sends nothing.
/// </summary>
public sealed class ArtCanvasControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TITLE_TOP = 26;
    private const int TOOL_WIDTH = 42;
    private const int TOOL_STEP = CustomButton.HEIGHT + 2;
    private const int GROUP_GAP = 6;
    private const int CANVAS_X = LEFT + TOOL_WIDTH + 4;
    private const int CANVAS_Y = 52;
    private const int CANVAS_WIDTH = PixelDrawing.WIDTH * CollegeProtocol.DRAWING_ZOOM;
    private const int CANVAS_HEIGHT = PixelDrawing.HEIGHT * CollegeProtocol.DRAWING_ZOOM;
    private const int PALETTE_X = CANVAS_X + CANVAS_WIDTH + 8;
    private const int CURRENT_SIZE = 24;
    private const int SWATCHES_Y = CANVAS_Y + CURRENT_SIZE + 18;
    private const int NOTE_TOP = CANVAS_Y + CANVAS_HEIGHT + 3;
    private const int NOTE_HEIGHT = (3 * TextRenderer.CHAR_HEIGHT) + 10;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - 4;
    private const int BUTTONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int SAVE_WIDTH = 60;
    private const int SUBMIT_X = LEFT + SAVE_WIDTH + 6;
    private const int SUBMIT_WIDTH = 130;
    private const int STATUS_X = SUBMIT_X + SUBMIT_WIDTH + 8;
    private const int COUNTER_WIDTH = 60;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string CONFIRM_SUBMIT_CAPTION = "Yes, submit";
    private const string LIBRARY_CONFIRM_SUBMIT_CAPTION = "Yes, send";
    private const string UPLOADING_COVER = "Uploading cover...";
    private const string DRAW_MORE = "Draw more before entering it.";
    private const string DRAW_MORE_HAND_IN = "Draw more before handing it in.";
    private const string SAVING = "Saving...";

    private readonly CustomButton CancelButton;
    private readonly ArtCanvas Canvas;
    private readonly UILabel CaptionLabel;
    private readonly UIPanel CurrentBox;
    private readonly UILabel CurrentHex;
    private readonly ArtEditor Editor = new(PixelDrawing.Blank());
    private readonly CustomButton GridButton;
    private readonly CustomButton InsertButton;
    private readonly CustomButton MirrorButton;
    private readonly CustomTextBox NoteBox;
    private readonly UILabel NoteCounter;
    private readonly StageColorPicker Picker;
    private readonly CustomButton PickerCancel;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly CustomButton SizeButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly ArtSwatches Swatches;
    private readonly CustomTextBox TitleBox;
    private readonly Dictionary<ArtTool, CustomButton> ToolButtons = new();
    private readonly CustomButton UndoButton;

    private bool CloseArmed;

    //Close sent a save and waits for its Saved before hiding
    private bool ClosingOnSave;
    private int EditingSwatch = -1;
    private string? ReplacingHash;
    private string SavedNote = string.Empty;
    private string SavedTitle = string.Empty;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;
    private int LibraryBookId;
    private int AwaitingLibraryReplies; //Library saves and sends waiting for a WriterResult; a late reply for the book open before this one is ignored
    private byte SavedSpine;
    private readonly LibraryBookOptions LibraryOptions;
    private string CoverHash = string.Empty;
    private string SavedCoverHash = string.Empty;
    private string CoverUpload = string.Empty;
    private string CoverBeforeUpload = string.Empty;
    private byte SentSpine;
    private string SentCoverHash = string.Empty;
    private Task<(PreparedPicture? Picture, string Error)>? CoverPicking;
    private CollegeDisplayArgs? CoverPickingSession;
    private bool CoverChoiceOpen;
    private readonly CustomButton CoverUploadButton;
    private readonly CustomButton CoverClearButton;
    private readonly CollegePictureTransfers? CoverTransfers;
    private readonly nint GameWindow;

    public ArtCanvasControl(CollegePictureTransfers? transfers = null, nint gameWindow = 0)
        : base("_nsett", false)
    {
        CoverTransfers = transfers;
        GameWindow = gameWindow;

        if (CoverTransfers is not null)
            CoverTransfers.UploadFinished += OnUploadFinished;

        Name = "ArtCanvas";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = INNER_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Title"
        };

        AddChild(TitleBox);

        Canvas = new ArtCanvas(Editor)
        {
            X = CANVAS_X,
            Y = CANVAS_Y
        };

        Canvas.Changed += () =>
        {
            DisarmSubmit();
            CloseArmed = false;
            RefreshControls();
        };

        AddChild(Canvas);

        var y = CANVAS_Y;

        foreach (var (tool, caption) in new[]
                 {
                     (ArtTool.Pen, "Pen"),
                     (ArtTool.Eraser, "Erase"),
                     (ArtTool.Fill, "Fill"),
                     (ArtTool.Line, "Line"),
                     (ArtTool.Pick, "Pick")
                 })
        {
            ToolButtons[tool] = ToolButton(caption, y, () => Editor.Tool = tool);
            y += TOOL_STEP;
        }

        y += GROUP_GAP;
        SizeButton = ToolButton("Size 1", y, Editor.CycleBrush);
        y += TOOL_STEP + GROUP_GAP;
        MirrorButton = ToolButton("Mirror", y, () => Editor.Mirror = !Editor.Mirror);
        y += TOOL_STEP;
        GridButton = ToolButton("Grid", y, () => Canvas.ShowGrid = !Canvas.ShowGrid);
        y += TOOL_STEP + GROUP_GAP;
        UndoButton = ToolButton("Undo", y, Editor.Undo);
        y += TOOL_STEP;
        RedoButton = ToolButton("Redo", y, Editor.Redo);

        CurrentBox = new UIPanel
        {
            X = PALETTE_X,
            Y = CANVAS_Y,
            Width = CURRENT_SIZE,
            Height = CURRENT_SIZE,
            BorderColor = Color.White,
            IsHitTestVisible = false
        };

        AddChild(CurrentBox);
        CurrentHex = Caption(string.Empty, PALETTE_X, CANVAS_Y + CURRENT_SIZE + 3, WIDTH - LEFT - PALETTE_X, color: LegendColors.Gray);

        Swatches = new ArtSwatches(Editor)
        {
            X = PALETTE_X,
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

        NoteBox = new CustomTextBox
        {
            X = LEFT,
            Y = NOTE_TOP,
            Width = INNER_WIDTH,
            Height = NOTE_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_ART_NOTE_CHARS,
            HintText = "Artist's note"
        };

        AddChild(NoteBox);

        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        SaveButton = AddButton("Save", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);
        InsertButton = AddButton("Insert", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnInsertClicked);
        CancelButton = AddButton("Cancel", SAVE_WIDTH, SUBMIT_X, BUTTONS_TOP, OnCloseClicked);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - COUNTER_WIDTH - 4 - STATUS_X);
        NoteCounter = Caption(string.Empty, LEFT + INNER_WIDTH - COUNTER_WIDTH, footerTextY, COUNTER_WIDTH, HorizontalAlignment.Right, LegendColors.Gray);

        LibraryOptions = new LibraryBookOptions
        {
            X = LEFT,
            Y = NOTE_TOP,
            Visible = false
        };

        LibraryOptions.Changed += () =>
        {
            DisarmSubmit();
            CloseArmed = false;
        };

        LibraryOptions.CoverRequested += ShowCoverChoice;
        AddChild(LibraryOptions);

        CoverUploadButton = AddButton("Upload", 60, LEFT, NOTE_TOP, OnCoverUploadClicked);
        CoverClearButton = AddButton("No cover", 70, LEFT + 66, NOTE_TOP, OnCoverClearClicked);
        HideCoverChoice();

        Picker = new StageColorPicker
        {
            X = PALETTE_X - 132,
            Y = SWATCHES_Y,
            ZIndex = 1
        };

        Picker.Closed += ApplyPickedColour;
        AddChild(Picker);
        PickerCancel = AddButton("Cancel", 60, Picker.X, Picker.Y + Picker.Height + 2, ClosePicker);
        PickerCancel.ZIndex = 1;
        PickerCancel.Visible = false;
    }

    /// <summary>Raised with SaveDraft, Submit and HandIn (Draft and HandIn modes).</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>Picture mode's Insert: the picture, and the hash of the picture it replaces (null for a new one).</summary>
    public event Action<PreparedPicture, string?>? PictureMade;

    public ArtCanvasMode Mode { get; private set; }

    public override void Dispose()
    {
        if (CoverTransfers is not null)
            CoverTransfers.UploadFinished -= OnUploadFinished;

        base.Dispose();
    }

    private bool IsDirty
        => Editor.Drawing.IsDirty || ((Mode != ArtCanvasMode.Picture) && ((TitleBox.Text != SavedTitle) || (NoteBox.Text != SavedNote)))
            || ((Mode == ArtCanvasMode.Library) && ((CoverHash != SavedCoverHash) || (LibraryOptions.Spine != SavedSpine)));

    private bool LibraryMode => Mode == ArtCanvasMode.Library;

    private string SubmitCaption
        => Mode == ArtCanvasMode.HandIn ? "Hand in"
            : Mode == ArtCanvasMode.Library ? "Send for review"
            : Session?.FreeEntries > 0 ? "Submit (free entry)"
            : $"Submit ({Session?.EntryCost} {Marks(Session?.EntryCost ?? 0)})";

    /// <summary>Opens an Art draft or hand-in (a null piece is a blank canvas).</summary>
    public void Open(CollegeDisplayArgs args)
    {
        var wasHandIn = Visible && (Mode == ArtCanvasMode.HandIn);
        Session = args;
        Mode = args.Mode switch
        {
            CollegeWriterMode.HandIn => ArtCanvasMode.HandIn,
            CollegeWriterMode.Library => ArtCanvasMode.Library,
            _ => ArtCanvasMode.Draft
        };

        var library = Mode == ArtCanvasMode.Library;

        if (library)
        {
            LibraryBookId = args.Id;
            CoverHash = args.Hash;
            AwaitingLibraryReplies = 0;
            SavedCoverHash = args.Hash;
            SavedSpine = args.Spine;
            CoverUpload = string.Empty;
            CoverBeforeUpload = string.Empty;
            LibraryOptions.Load(LibraryShelfCode.Artwork, args.Spine, args.Hash, true);
        }

        SaveButton.Enabled = true;
        SubmitButton.Enabled = true;
        HideCoverChoice();
        ReplacingHash = null;

        Editor.Load(
            args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Drawing) is { } block
                ? PixelDrawing.From(block.Palette, block.Pixels)
                : PixelDrawing.Blank());

        TitleBox.Text = args.Piece?.Title ?? string.Empty;
        NoteBox.Text = args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Text)?.Text ?? string.Empty;

        CaptionLabel.Text = Mode == ArtCanvasMode.HandIn
            ? OneLine(args.Prompt.Length > 0 ? $"Hand-in: {args.Prompt}" : "Art: class hand-in")
            : library ? $"Library: {LibraryLabels.ShelfName(LibraryOptions.Shelf)}"
            : "Art: draft";

        Begin();

        if (Mode == ArtCanvasMode.HandIn)
            SendHandInWindow(true);
        else if (wasHandIn)
            SendHandInWindow(false);
    }

    /// <summary>Tells the server the hand-in window is open, so the room shows "Drawing" over this player. Sent again on each map change.</summary>
    public void ReportHandInWindow()
    {
        if (Visible && (Mode == ArtCanvasMode.HandIn) && Session is not null)
            SendHandInWindow(true);
    }

    /// <summary>Opens Picture mode for the writing window: a blank canvas, or a drawing being edited.</summary>
    public void OpenPicture(PixelDrawing? drawing, string? replacing)
    {
        Session = null;
        Mode = ArtCanvasMode.Picture;
        ReplacingHash = replacing;
        Editor.Load(drawing ?? PixelDrawing.Blank());
        TitleBox.Text = string.Empty;
        NoteBox.Text = string.Empty;
        CaptionLabel.Text = replacing is null ? "Draw a picture" : "Edit the picture";
        Begin();
    }

    /// <summary>Applies a WriterResult for this window's session. True when a hidden window was shown again (a refused save).</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        if (Session is null || (Mode == ArtCanvasMode.Picture) || (args.Mode != Session.Mode) || (args.Subject != Session.Subject))
            return false;

        if (LibraryMode)
        {
            if (!LibraryReplyGuard.ShouldTake(AwaitingLibraryReplies, LibraryBookId, args.Id))
                return false;

            AwaitingLibraryReplies--;
        }

        Status.Text = args.Message;

        if (LibraryMode && (args.Id != 0))
            LibraryBookId = args.Id;

        if (LibraryMode && (args.Result is CollegeWriterResult.Saved or CollegeWriterResult.Submitted))
        {
            SavedCoverHash = SentCoverHash;
            SavedSpine = SentSpine;
        }

        switch (args.Result)
        {
            case CollegeWriterResult.Submitted or CollegeWriterResult.HandedIn:
                ClosingOnSave = false;
                MarkSaved();
                Hide();

                break;
            case CollegeWriterResult.Saved when ClosingOnSave:
                ClosingOnSave = false;

                //drawing done while the save was on its way keeps the window open
                if (!IsDirty)
                    Hide();

                break;
            case CollegeWriterResult.Refused:
                ClosingOnSave = false;
                Editor.Drawing.MarkDirty();

                if (!Visible)
                {
                    Show();
                    ReportHandInWindow();

                    return true;
                }

                break;
        }

        return false;
    }

    public void SaveIfDirty()
    {
        if (Visible && Session is not null && ((Mode == ArtCanvasMode.Draft) || LibraryMode) && IsDirty)
            Send(CollegeActionType.SaveDraft);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        ClosePicker();
        base.Hide();

        if ((Mode == ArtCanvasMode.HandIn) && Session is not null)
            SendHandInWindow(false);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (CoverChoiceOpen)
                HideCoverChoice();
            else if (Picker.Visible)
                ClosePicker();
            else
                OnCloseClicked();

            e.Handled = true;

            return;
        }

        if (e.Accelerator && !TitleBox.IsFocused && !NoteBox.IsFocused && e.Keycode is Keycode.Z or Keycode.Y)
        {
            if (e.Keycode == Keycode.Z)
                Editor.Undo();
            else
                Editor.Redo();

            DisarmSubmit();
            RefreshControls();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        FinishCoverPicking();

        if (Visible)
        {
            var counter = $"{NoteBox.Text.Length}/{CollegeProtocol.MAX_ART_NOTE_CHARS}";

            if (NoteCounter.Text != counter)
                NoteCounter.Text = counter;

            if (LibraryMode)
            {
                var coverReady = CoverUpload.Length == 0;
                SaveButton.Enabled = coverReady;
                SubmitButton.Enabled = coverReady;
            }
        }

        base.Update(gameTime);
    }

    private void Begin()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        Editor.Drawing.MarkClean();
        CloseArmed = false;
        ClosingOnSave = false;
        ClosePicker();

        HideCoverChoice();
        var picture = Mode == ArtCanvasMode.Picture;
        var library = Mode == ArtCanvasMode.Library;
        TitleBox.Visible = !picture;
        NoteBox.Visible = !picture && !library;
        NoteCounter.Visible = !picture && !library;
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        SaveButton.Visible = (Mode == ArtCanvasMode.Draft) || library;
        LibraryOptions.Visible = library;
        SubmitButton.Visible = !picture;
        SubmitButton.X = Mode == ArtCanvasMode.HandIn ? LEFT : SUBMIT_X;
        InsertButton.Visible = picture;
        CancelButton.Visible = picture;
        Status.Text = string.Empty;
        DisarmSubmit();
        RefreshControls();
        Show();
    }

    private void RefreshControls()
    {
        foreach (var (tool, button) in ToolButtons)
            button.Selected = Editor.Tool == tool;

        SizeButton.Caption = $"Size {Editor.BrushSize}";
        MirrorButton.Selected = Editor.Mirror;
        GridButton.Selected = Canvas.ShowGrid;
        UndoButton.Enabled = Editor.Drawing.CanUndo;
        RedoButton.Enabled = Editor.Drawing.CanRedo;

        var colour = Editor.Drawing.Palette[Editor.Current];
        CurrentBox.BackgroundColor = colour;
        CurrentHex.Text = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
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
                DisarmSubmit();
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
        DisarmSubmit();
        RefreshControls();
    }

    private void ClosePicker()
    {
        EditingSwatch = -1;
        Picker.Visible = false;
        PickerCancel.Visible = false;
    }

    private void OnSaveClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if (((Mode != ArtCanvasMode.Draft) && !LibraryMode) || Session is null)
            return;

        if (LibraryMode && (CoverUpload.Length > 0))
        {
            Status.Text = UPLOADING_COVER;

            return;
        }

        Status.Text = SAVING;
        Send(CollegeActionType.SaveDraft);
    }

    private void OnSubmitClicked()
    {
        CloseArmed = false;

        if (Session is null)
            return;

        if (Mode == ArtCanvasMode.HandIn)
        {
            if (Editor.Drawing.DrawnPixels < CollegeProtocol.MIN_HAND_IN_DRAWN_PIXELS)
            {
                Status.Text = DRAW_MORE_HAND_IN;

                return;
            }

            Status.Text = "Handing in...";
            Send(CollegeActionType.HandIn);

            return;
        }

        if (LibraryMode && (CoverUpload.Length > 0))
        {
            DisarmSubmit();
            Status.Text = UPLOADING_COVER;

            return;
        }

        if (TitleBox.Text.Trim().Length == 0)
        {
            DisarmSubmit();
            Status.Text = "Give your drawing a title.";

            return;
        }

        if (Editor.Drawing.DrawnPixels < CollegeProtocol.MIN_ENTRY_DRAWN_PIXELS)
        {
            DisarmSubmit();
            Status.Text = DRAW_MORE;

            return;
        }

        if (!SubmitArmed)
        {
            SubmitArmed = true;

            if (LibraryMode)
            {
                SubmitButton.Caption = LIBRARY_CONFIRM_SUBMIT_CAPTION;
                Status.Text = "Send this book for review? Click Yes, send.";
            } else
            {
                SubmitButton.Caption = CONFIRM_SUBMIT_CAPTION;

                Status.Text = Session.FreeEntries > 0
                    ? "Use your free entry? Click Yes, submit."
                    : $"Enter it for {Session.EntryCost} {Marks(Session.EntryCost)}? Click Yes, submit.";
            }

            return;
        }

        DisarmSubmit();
        Status.Text = "Submitting...";
        Send(CollegeActionType.Submit);
    }

    private void OnInsertClicked()
    {
        if (Mode != ArtCanvasMode.Picture)
            return;

        PictureMade?.Invoke(DrawingPictures.ToPicture(Editor.Drawing), ReplacingHash);
        Editor.Drawing.MarkClean();
        Hide();
    }

    private void OnCloseClicked()
    {
        DisarmSubmit();

        //a second Close while the save is on its way hides anyway; a refusal still brings the piece back
        if (((Mode == ArtCanvasMode.Draft) || LibraryMode) && Session is not null && !ClosingOnSave && IsDirty)
        {
            if (LibraryMode && (CoverUpload.Length > 0))
            {
                Status.Text = UPLOADING_COVER;

                return;
            }

            ClosingOnSave = true;
            Status.Text = SAVING;
            Send(CollegeActionType.SaveDraft);

            return;
        }

        if ((Mode != ArtCanvasMode.Draft) && !LibraryMode && IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            Status.Text = Mode == ArtCanvasMode.HandIn ? "Close without handing in? Click Close again." : "Close without inserting? Click again.";

            return;
        }

        ClosingOnSave = false;
        CloseArmed = false;
        Hide();
    }

    private void Send(CollegeActionType type)
    {
        var library = LibraryMode;

        if (library)
        {
            SentSpine = LibraryOptions.Spine;
            SentCoverHash = CoverHash;
        }
        else
        {
            SentSpine = 0;
            SentCoverHash = string.Empty;
        }

        if (library)
        {
            type = type switch
            {
                CollegeActionType.SaveDraft => CollegeActionType.LibrarySaveDraft,
                CollegeActionType.Submit => CollegeActionType.LibrarySend,
                _ => type
            };

            if (CoverUpload.Length > 0)
            {
                Status.Text = UPLOADING_COVER;

                return;
            }
        }

        if (library)
            AwaitingLibraryReplies++;

        var blocks = new List<CollegeBlockInfo>
        {
            new()
            {
                Kind = CollegeBlockKind.Drawing,
                Palette = Editor.Drawing.PaletteBytes(),
                Pixels = Editor.Drawing.Pixels.ToArray()
            }
        };

        if (NoteBox.Text.Trim().Length > 0)
            blocks.Add(new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = NoteBox.Text });

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = CollegeSubjectCode.Art,
                Piece = new CollegePieceInfo
                {
                    Subject = CollegeSubjectCode.Art,
                    Title = TitleBox.Text.Trim(),
                    Blocks = blocks
                },
                Id = library ? LibraryBookId : 0,
                Shelf = library ? LibraryOptions.Shelf : default,
                Spine = library ? LibraryOptions.Spine : (byte)0,
                Hash = library ? CoverHash : string.Empty
            });

        //marked saved as it is sent, so drawing done while the reply is on its way still counts as a change
        MarkSaved();
    }

    private void OnCoverUploadClicked()
    {
        if (CoverPicking is not null || !LibraryMode)
            return;

        if (CoverTransfers is null)
        {
            Status.Text = "Cover upload isn't ready.";

            return;
        }

        DisarmSubmit();
        CloseArmed = false;
        HideCoverChoice();
        Status.Text = "Choose a cover...";

        var owner = Sdl.GetWin32Window(GameWindow);
        CoverPickingSession = Session;
        CoverPicking = Task.Run(() => PickAndPrepareCoverAsync(owner));
    }

    private void OnCoverClearClicked()
    {
        if (!LibraryMode)
            return;

        DisarmSubmit();
        CloseArmed = false;

        if (CoverUpload.Length > 0)
        {
            HideCoverChoice();
            Status.Text = UPLOADING_COVER;

            return;
        }

        CoverHash = string.Empty;
        LibraryOptions.CoverHash = string.Empty;
        HideCoverChoice();
    }

    private static async Task<(PreparedPicture? Picture, string Error)> PickAndPrepareCoverAsync(nint owner)
    {
        var path = await PictureFilePicker.PickAsync(owner);

        if (path is null)
            return (null, string.Empty);

        if (new FileInfo(path).Length > PicturePrep.MAX_FILE_BYTES)
            return (null, PicturePrep.FILE_TOO_BIG);

        var picture = PicturePrep.PrepareCover(await File.ReadAllBytesAsync(path), out var error);

        return (picture, error);
    }

    private void FinishCoverPicking()
    {
        if (CoverPicking is not { IsCompleted: true } done)
            return;

        CoverPicking = null;

        var failed = done.Exception is not null || !done.IsCompletedSuccessfully;

        if (!Visible || !LibraryMode || !ReferenceEquals(CoverPickingSession, Session))
            return;

        if (failed)
        {
            Status.Text = "That file couldn't be opened.";

            return;
        }

        var (picture, error) = done.Result;

        if (picture is null)
        {
            Status.Text = error;

            return;
        }

        CoverBeforeUpload = CoverHash;
        CoverUpload = picture.Hash;
        CoverHash = picture.Hash;
        LibraryOptions.CoverHash = picture.Hash;
        Status.Text = UPLOADING_COVER;
        CoverTransfers!.Upload(picture);
    }

    private void OnUploadFinished(string hash, bool ok, string message)
    {
        if (!LibraryMode || (CoverUpload.Length == 0) || (hash != CoverUpload))
            return;

        CoverUpload = string.Empty;

        if (ok)
        {
            CoverBeforeUpload = string.Empty;
            Status.Text = "Cover added.";

            return;
        }

        Status.Text = message.Length > 0 ? message : "That picture was refused.";

        if (CoverHash == hash)
        {
            CoverHash = CoverBeforeUpload;
            LibraryOptions.CoverHash = CoverBeforeUpload;
        }

        CoverBeforeUpload = string.Empty;
    }

    private void ShowCoverChoice()
    {
        if (!LibraryMode)
            return;

        DisarmSubmit();
        CloseArmed = false;
        CoverChoiceOpen = true;
        LibraryOptions.Visible = false;
        CoverUploadButton.Visible = true;
        CoverClearButton.Visible = CoverHash.Length > 0;
        Status.Text = "Upload a cover, or press Esc.";
    }

    private void HideCoverChoice()
    {
        CoverChoiceOpen = false;
        CoverUploadButton.Visible = false;
        CoverClearButton.Visible = false;
        LibraryOptions.Visible = LibraryMode;
    }

    private void SendHandInWindow(bool open)
        => ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.HandInWindow, Raised = open });

    private void MarkSaved()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        Editor.Drawing.MarkClean();
    }

    private void DisarmSubmit()
    {
        SubmitArmed = false;
        SubmitButton.Caption = SubmitCaption;
    }

    private static string OneLine(string text)
    {
        var lines = TextRenderer.WrapText(text, INNER_WIDTH - 8);

        return lines.Count <= 1 ? text : lines[0].TrimEnd() + "...";
    }

    private static string Marks(int count) => count == 1 ? "Mark" : "Marks";
}

#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The College writing window for drafts and class hand-ins. Every editing rule is in <see cref="WritingDocument" />;
///     this window shows the blocks, forwards keys and buttons, and runs picture uploads through
///     <see cref="CollegePictureTransfers" />. The server checks the piece again on Save, Submit and Hand in.
/// </summary>
/// <remarks>
///     Layout, top to bottom: the window caption, the class prompt (hand-in only), the title box, the page (a scrolling
///     stack of blocks), the status line and the button row with the counter. The blocks are rebuilt from the document
///     after every structural edit (heading, picture, join); typed text is copied into the document every frame.
/// </remarks>
public sealed class CollegeWriterControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 440;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int PROMPT_TOP = 26;
    private const int GAP = 4;
    private const int BLOCK_GAP = 4;
    private const int PAGE_INSET = 4;
    private const int BAR_GAP = 4;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - GAP;
    private const int BUTTONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int STATUS_TOP = BUTTONS_TOP - GAP - TextRenderer.CHAR_HEIGHT;
    private const int BODY_BOTTOM = STATUS_TOP - GAP;
    private const int PAGE_WIDTH = INNER_WIDTH;
    private const int VIEWER_WIDTH = PAGE_WIDTH - (2 * PAGE_INSET);
    private const int CONTENT_WIDTH = VIEWER_WIDTH - ScrollBarControl.DEFAULT_WIDTH - BAR_GAP;
    private const int SCROLL_STEP = 2 * TextRenderer.CHAR_HEIGHT;
    private const int HEADING_X = LEFT;
    private const int PICTURE_X = HEADING_X + 64 + 6;
    private const int DRAW_X = PICTURE_X + 60 + 6;
    private const int SAVE_X = DRAW_X + 46 + 6;
    private const int SUBMIT_X = SAVE_X + 50 + 6;
    private const int SUBMIT_WIDTH = 130;
    private const int COUNTER_X = SUBMIT_X + SUBMIT_WIDTH + 6;
    private const string UPLOADING = "Uploading picture...";
    private const string LOADING = "Loading picture...";
    private const string WAIT_FOR_UPLOADS = "Wait for the pictures to finish.";
    private const string SAVING = "Saving...";
    private const string CONFIRM_SUBMIT_CAPTION = "Yes, submit";

    private static readonly SKColor PageFill = new(10, 8, 5, 255);

    private readonly List<UIElement> BlockViews = [];
    private readonly UIPanel Content;
    private readonly UILabel Counter;
    private readonly UILabel PromptLabel;
    private readonly UIPanel Page;
    private readonly CustomButton SaveButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly CustomTextBox TitleBox;
    private readonly UILabel TitleCaption;
    private readonly nint GameWindow;
    private readonly CollegePictureTransfers Transfers;
    private readonly HashSet<string> Uploading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ReplacedBy = new(StringComparer.Ordinal);
    private readonly ScrollViewerControl Viewer;
    private readonly BlockViewport Viewport;

    private bool CloseArmed;

    //hashes known to be canvas drawings, and those checked and found not to be
    private readonly HashSet<string> DrawingHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> NotDrawings = new(StringComparer.Ordinal);

    //the session a Draw or Edit was started for; its picture goes nowhere else
    private CollegeDisplayArgs? DrawSession;

    //Close sent a save and waits for its Saved before hiding
    private bool ClosingOnSave;
    private WritingDocument Document = new();
    private int FocusCaret;
    private int FocusIndex;
    private (int Index, int Caret, int Length) LastCaret = (-1, -1, -1);
    private Task<(PreparedPicture? Picture, string Error)>? Picking;

    //the session the open file dialog was started for; its picture goes nowhere else
    private CollegeDisplayArgs? PickingSession;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;

    /// <param name="transfers">The picture uploads and downloads.</param>
    /// <param name="gameWindow">The game's SDL window, which owns the picture file dialog.</param>
    public CollegeWriterControl(CollegePictureTransfers transfers, nint gameWindow = 0)
        : base("_nsett", false)
    {
        GameWindow = gameWindow;
        Name = "CollegeWriter";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        Transfers = transfers;
        Transfers.UploadFinished += OnUploadFinished;

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        TitleCaption = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        PromptLabel = Caption(string.Empty, LEFT, PROMPT_TOP, INNER_WIDTH, color: LegendColors.Gray);
        PromptLabel.WordWrap = true;
        PromptLabel.VerticalAlignment = VerticalAlignment.Top;

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = PROMPT_TOP,
            Width = INNER_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Title"
        };

        AddChild(TitleBox);

        Content = new UIPanel
        {
            Width = CONTENT_WIDTH,
            IsPassThrough = true
        };

        Viewport = new BlockViewport(Content, SCROLL_STEP);
        Viewport.AddChild(Content);
        Viewport.PressedBelowBlocks += FocusEnd;

        Viewer = new ScrollViewerControl(Viewport)
        {
            X = PAGE_INSET,
            Y = PAGE_INSET,
            Width = VIEWER_WIDTH,
            ContentRightPadding = BAR_GAP
        };

        Page = new UIPanel
        {
            X = LEFT,
            Width = PAGE_WIDTH
        };

        Page.AddChild(Viewer);
        AddChild(Page);
        SetBodyTop(TitleBox.Y + TitleBox.Height + GAP);

        Status = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH);
        AddButton("Heading", 64, HEADING_X, BUTTONS_TOP, OnHeadingClicked);
        AddButton("Picture", 60, PICTURE_X, BUTTONS_TOP, OnPictureClicked);
        AddButton("Draw", 46, DRAW_X, BUTTONS_TOP, OnDrawClicked);
        SaveButton = AddButton("Save", 50, SAVE_X, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);

        Counter = Caption(
            string.Empty,
            COUNTER_X,
            BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            LEFT + INNER_WIDTH - COUNTER_X,
            HorizontalAlignment.Right,
            LegendColors.Gray);
    }

    /// <summary>Raised with SaveDraft, Submit or HandIn, each carrying the subject and the whole piece.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>Draw (null, null) or Edit (the drawing, its hash): open the picture canvas.</summary>
    public event Action<PixelDrawing?, string?>? DrawRequested;

    private bool HandInMode => Session?.Mode == CollegeWriterMode.HandIn;

    private string SubmitCaption
        => HandInMode ? "Hand in"
            : Session?.FreeEntries > 0 ? "Submit (free entry)"
            : $"Submit ({Session?.EntryCost} {Marks(Session?.EntryCost ?? 0)})";

    public override void Dispose()
    {
        Transfers.UploadFinished -= OnUploadFinished;
        base.Dispose();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;

        foreach (var box in BlockViews.OfType<BlockTextBox>())
            box.IsFocused = false;

        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            OnCloseClicked();
            e.Handled = true;

            return;
        }

        //Enter or Tab in the title goes on to the writing
        if (TitleBox.IsFocused && e.Keycode is Keycode.Enter or Keycode.Tab)
        {
            FocusBlock(0, 0);
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Opens the writer on a draft or a hand-in (a null piece is a blank one).</summary>
    public void Open(CollegeDisplayArgs args)
    {
        Session = args;
        Document = WritingDocument.From(args.Piece);
        Uploading.Clear();
        ReplacedBy.Clear();
        CloseArmed = false;
        ClosingOnSave = false;
        DisarmSubmit();

        TitleBox.IsFocused = false;
        TitleBox.Text = Document.Title;
        TitleCaption.Text = HandInMode ? $"{args.Subject}: class hand-in" : $"{args.Subject}: draft";
        PromptLabel.Text = HandInMode && (args.Prompt.Length > 0) ? $"Prompt: {args.Prompt}" : string.Empty;
        PromptLabel.Visible = PromptLabel.Text.Length > 0;

        var promptLines = PromptLabel.Visible
            ? TextRenderer.WrapText(PromptLabel.Text, PromptLabel.Width - PromptLabel.PaddingLeft - PromptLabel.PaddingRight)
                          .Count
            : 0;

        PromptLabel.Height = (promptLines * TextRenderer.CHAR_HEIGHT) + PromptLabel.PaddingTop + PromptLabel.PaddingBottom;
        TitleBox.Y = PROMPT_TOP + (PromptLabel.Visible ? PromptLabel.Height + GAP : 0);
        SetBodyTop(TitleBox.Y + TitleBox.Height + GAP);

        SaveButton.Visible = !HandInMode;
        SubmitButton.X = HandInMode ? SAVE_X : SUBMIT_X;
        SubmitButton.Caption = SubmitCaption;
        Status.Text = string.Empty;

        FocusIndex = Document.Blocks.Count - 1;
        FocusCaret = Document.Blocks[^1].Text.Length;
        LastCaret = (-1, -1, -1);
        Rebuild(null, false);
        ((IVerticalScrollable)Viewport).VerticalOffset = 0;
        Show();

        if (TitleBox.Text.Length == 0)
            TitleBox.IsFocused = true;
        else
            FocusBlock(0, 0);
    }

    /// <summary>
    ///     A WriterResult: the message goes on the status line; Submitted and HandedIn close the window, and so does the
    ///     Saved that answers Close. A result for another writer session (another subject, or a hand-in) is ignored. True
    ///     when a hidden window was shown again, for a refusal, so the caller can bring it to the front.
    /// </summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        if (Session is null || (args.Mode != Session.Mode) || (args.Subject != Session.Subject))
            return false;

        Status.Text = args.Message;

        switch (args.Result)
        {
            case CollegeWriterResult.Submitted or CollegeWriterResult.HandedIn:
                ClosingOnSave = false;
                Document.MarkClean();
                Hide();

                break;
            case CollegeWriterResult.Saved when ClosingOnSave:
                ClosingOnSave = false;

                //text typed while the save was on its way keeps the window open
                if (!Document.IsDirty)
                    Hide();

                break;
            case CollegeWriterResult.Refused:
                ClosingOnSave = false;
                Document.MarkDirty();

                //a save the server refused after the window was hidden: bring the piece back so the reason shows and
                //nothing is lost
                if (!Visible)
                {
                    Show();

                    return true;
                }

                break;
        }

        return false;
    }

    /// <summary>
    ///     Sends a draft with unsent changes, as Close does, but leaves the window as it is. For logging out, where the
    ///     window goes away without Close. A hand-in is only ever sent by Hand in, so it is left alone.
    /// </summary>
    public void SaveIfDirty()
    {
        if (!Visible || Session is null || HandInMode)
            return;

        PullText();

        if (Document.IsDirty)
            Send(CollegeActionType.SaveDraft);
    }

    public override void Update(GameTime gameTime)
    {
        //a dialog that finishes while the window is hidden is still collected, so its task never goes unobserved
        FinishPicking();

        if (Visible)
        {
            //typing after "Close without handing in?" takes the question back
            if (PullText())
                CloseArmed = false;

            TrackFocus();
            LayoutBlocks();
            FollowCaret();

            var counter = string.Create(
                CultureInfo.InvariantCulture,
                $"{Document.TextLength:N0} / {CollegeProtocol.MAX_TEXT_CHARS:N0}   {Document.PictureCount} / {CollegeProtocol.MAX_PICTURES}");

            if (Counter.Text != counter)
                Counter.Text = counter;
        }

        base.Update(gameTime);
    }

    // ---- blocks ----

    private void Rebuild((int Index, int Caret)? focusAt, bool alwaysFocus)
    {
        var hadFocus = BlockViews.OfType<BlockTextBox>()
                                 .Any(box => box.IsFocused);

        foreach (var view in BlockViews)
        {
            Content.Children.Remove(view);
            view.Dispose();
        }

        BlockViews.Clear();

        for (var i = 0; i < Document.Blocks.Count; i++)
        {
            var block = Document.Blocks[i];

            UIElement view = block.Kind == WritingBlockKind.Picture ? PictureView(block.Hash, i) : TextView(block, i);

            BlockViews.Add(view);
            Content.AddChild(view);
        }

        //a picture removed by an edit (Backspace, a join) can't still be waited for
        Uploading.RemoveWhere(hash => !Document.Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash)));

        LayoutBlocks();

        if (focusAt is { } at && (alwaysFocus || hadFocus))
            FocusBlock(at.Index, at.Caret);
        else if (hadFocus)
            FocusBlock(FocusIndex, FocusCaret);

        RetargetFocus();
    }

    /// <summary>Keeps the block the next Heading or picture applies to on a text block that still exists.</summary>
    private void RetargetFocus()
    {
        if (BlockViews.Count == 0)
        {
            FocusIndex = 0;
            FocusCaret = 0;

            return;
        }

        var index = Math.Clamp(FocusIndex, 0, BlockViews.Count - 1);

        if (BlockViews[index] is not BlockTextBox)
        {
            var box = BlockViews.Skip(index).OfType<BlockTextBox>().FirstOrDefault()
                      ?? BlockViews.Take(index).OfType<BlockTextBox>().LastOrDefault();

            if (box is not null)
                index = BlockViews.IndexOf(box);
        }

        FocusIndex = index;
        FocusCaret = BlockViews[index] is BlockTextBox target ? Math.Clamp(FocusCaret, 0, target.Text.Length) : 0;
    }

    private BlockTextBox TextView(WritingBlock block, int index)
    {
        var heading = block.Kind == WritingBlockKind.Heading;

        var box = new BlockTextBox
        {
            Width = Content.Width,
            Height = TextRenderer.CHAR_HEIGHT + 4,
            IsMultiLine = !heading,
            MaxLength = heading ? CollegeProtocol.MAX_HEADING_CHARS : CollegeProtocol.MAX_TEXT_CHARS,
            IsSelectable = true,
            ForegroundColor = heading ? LegendColors.Gold : LegendColors.White,
            Text = block.Text,
            HintText = heading ? "Heading" : Document.Blocks.Count == 1 ? "Write here..." : string.Empty
        };

        box.FitHeight();
        box.BackspaceAtStart += () => Edit(() => Document.BackspaceAtStart(index));
        box.TryLeaveUp = () => FocusNeighbour(index, -1);
        box.TryLeaveDown = () => FocusNeighbour(index, 1);

        if (heading)
            box.EnterPressed += () => Edit(() => Document.EnterAfterHeading(index));

        return box;
    }

    private PictureBlock PictureView(string hash, int index)
    {
        var view = new PictureBlock(hash, Content.Width);

        view.RemoveButton.Clicked += () =>
        {
            Uploading.Remove(hash);
            Edit(() => Document.RemovePicture(index));
        };

        view.EditButton.Clicked += () => OnEditClicked(hash);
        view.Loaded += () => view.EditButton.Visible = IsDrawing(hash);

        return view;
    }

    private void LayoutBlocks()
    {
        var y = 0;

        foreach (var view in BlockViews)
        {
            switch (view)
            {
                case BlockTextBox box:
                    box.FitHeight();

                    break;
                case PictureBlock picture:
                    picture.Refresh(Uploading.Contains(picture.Hash), Transfers);

                    break;
            }

            view.Y = y;
            y += view.Height + BLOCK_GAP;
        }

        Content.Height = Math.Max(0, y - BLOCK_GAP);
    }

    /// <summary>
    ///     Copies the typed text into the document, and keeps each box from going past the piece's character budget. True
    ///     when any text changed.
    /// </summary>
    private bool PullText()
    {
        var changed = false;

        //the Title setter marks the piece changed, so only set it when it did change
        if (TitleBox.Text != Document.Title)
        {
            Document.Title = TitleBox.Text;
            changed = true;
        }

        for (var i = 0; (i < BlockViews.Count) && (i < Document.Blocks.Count); i++)
            if (BlockViews[i] is BlockTextBox box)
            {
                changed |= box.Text != Document.Blocks[i].Text;
                Document.SetText(i, box.Text);

                //SetText may cut the text to the budget; show what was kept
                if (box.Text != Document.Blocks[i].Text)
                    box.Text = Document.Blocks[i].Text;
            }

        var room = CollegeProtocol.MAX_TEXT_CHARS - Document.TextLength;

        foreach (var box in BlockViews.OfType<BlockTextBox>())
            box.MaxLength = Math.Min(box.IsMultiLine ? CollegeProtocol.MAX_TEXT_CHARS : CollegeProtocol.MAX_HEADING_CHARS, box.Text.Length + room);

        return changed;
    }

    /// <summary>Remembers the block and caret the next Heading or picture applies to.</summary>
    private void TrackFocus()
    {
        for (var i = 0; i < BlockViews.Count; i++)
            if (BlockViews[i] is BlockTextBox { IsFocused: true } box)
            {
                FocusIndex = i;
                FocusCaret = box.CursorPosition;

                return;
            }
    }

    /// <summary>Scrolls the stack to the caret when it moves or the text changes, so typing never goes out of sight.</summary>
    private void FollowCaret()
    {
        if (BlockViews.ElementAtOrDefault(FocusIndex) is not BlockTextBox { IsFocused: true } box)
            return;

        var caret = (FocusIndex, box.CursorPosition, box.Text.Length);

        if (caret == LastCaret)
            return;

        LastCaret = caret;
        var top = box.Y + (box.CaretLine * TextRenderer.CHAR_HEIGHT);
        Viewport.ScrollToShow(top, top + TextRenderer.CHAR_HEIGHT + box.PaddingTop + box.PaddingBottom);
    }

    /// <summary>Applies a structural edit (it returns where the caret goes) and rebuilds the blocks around it.</summary>
    private void Edit(Func<(int Index, int Caret)> change)
    {
        PullText();
        TrackFocus();
        DisarmSubmit();
        CloseArmed = false;
        Rebuild(change(), true);
    }

    /// <summary>Focuses the text block at <paramref name="index" />, or the nearest one after it (else before it).</summary>
    private void FocusBlock(int index, int caret)
    {
        if (BlockViews.Count == 0)
            return;

        index = Math.Clamp(index, 0, BlockViews.Count - 1);

        var box = BlockViews.Skip(index).OfType<BlockTextBox>().FirstOrDefault()
                  ?? BlockViews.Take(index).OfType<BlockTextBox>().LastOrDefault();

        if (box is null)
            return;

        box.PlaceCaret(caret);
        FocusIndex = BlockViews.IndexOf(box);
        FocusCaret = box.CursorPosition;
    }

    /// <summary>
    ///     Up from a block's first line or Down from its last: the caret goes to the next text block that way (Up from the
    ///     first goes to the title). False when there is nowhere to go.
    /// </summary>
    private bool FocusNeighbour(int index, int direction)
    {
        for (var i = index + direction; (i >= 0) && (i < BlockViews.Count); i += direction)
            if (BlockViews[i] is BlockTextBox box)
            {
                box.PlaceCaret(direction < 0 ? box.Text.Length : 0);

                return true;
            }

        if (direction > 0)
            return false;

        TitleBox.IsFocused = true;

        return true;
    }

    /// <summary>A press on the page below the last block puts the caret at the end of the piece.</summary>
    private void FocusEnd(int y)
    {
        if ((BlockViews.Count == 0) || (y < (BlockViews[^1].Y + BlockViews[^1].Height)))
            return;

        if (BlockViews[^1] is BlockTextBox last)
            last.PlaceCaret(last.Text.Length);
    }

    private void SetBodyTop(int top)
    {
        var height = BODY_BOTTOM - top;

        Page.Y = top;

        if (Page.Height != height)
        {
            Page.Height = height;
            Page.Background?.Dispose();
            Page.Background = DialogFrame.BuildRecessedTexture(PageFill, PAGE_WIDTH, height);
        }

        Viewer.Height = height - (2 * PAGE_INSET);
        Viewport.Height = Viewer.Height;
    }

    // ---- pictures ----

    private void OnPictureClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if (Picking is not null)
            return;

        if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
        {
            Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

            return;
        }

        Status.Text = "Choose a picture...";

        //SDL is asked on the game thread; the dialog itself runs on its own
        var owner = Sdl.GetWin32Window(GameWindow);
        PickingSession = Session;
        Picking = Task.Run(() => PickAndPrepareAsync(owner));
    }

    //the file dialog, the file read and the resizing all run off the game thread
    private static async Task<(PreparedPicture? Picture, string Error)> PickAndPrepareAsync(nint owner)
    {
        var path = await PictureFilePicker.PickAsync(owner);

        if (path is null)
            return (null, string.Empty);

        if (new FileInfo(path).Length > PicturePrep.MAX_FILE_BYTES)
            return (null, PicturePrep.FILE_TOO_BIG);

        var picture = PicturePrep.Prepare(await File.ReadAllBytesAsync(path), out var error);

        return (picture, error);
    }

    private void FinishPicking()
    {
        if (Picking is not { IsCompleted: true } done)
            return;

        Picking = null;

        //reading Exception marks a fault observed
        var failed = done.Exception is not null || !done.IsCompletedSuccessfully;

        //a picture chosen for a window that has since closed, or opened on another piece, is dropped
        if (!Visible || !ReferenceEquals(PickingSession, Session))
            return;

        if (failed)
        {
            Status.Text = "That file couldn't be opened.";

            return;
        }

        var (picture, error) = done.Result;

        if (picture is null)
        {
            //an empty error is a cancelled dialog
            Status.Text = error;

            return;
        }

        if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
        {
            Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

            return;
        }

        PullText();
        var index = Math.Clamp(FocusIndex, 0, Document.Blocks.Count - 1);

        if (Document.InsertPicture(index, FocusCaret, picture.Hash) is not { } at)
        {
            Status.Text = "That picture is already in this piece.";

            return;
        }

        Uploading.Add(picture.Hash);
        Transfers.Upload(picture);
        Status.Text = UPLOADING;
        Rebuild(at, true);
    }

    /// <summary>The picture canvas's Insert: a new picture at the caret, or an edited one in place of <paramref name="replacing" />.</summary>
    public void InsertDrawnPicture(PreparedPicture picture, string? replacing)
    {
        if (!Visible || !ReferenceEquals(DrawSession, Session))
            return;

        PullText();
        (int Index, int Caret)? at = null;

        if (replacing is not null)
        {
            var stillThere = Document.Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == replacing));

            if (stillThere && (picture.Hash == replacing))
                return;

            if (!Document.ReplacePicture(replacing, picture.Hash))
            {
                Status.Text = stillThere ? "That picture is already in this piece." : "The picture you were editing is gone.";

                return;
            }

            Uploading.Remove(replacing);
            ReplacedBy[picture.Hash] = replacing;
        } else
        {
            if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
            {
                Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

                return;
            }

            var index = Math.Clamp(FocusIndex, 0, Document.Blocks.Count - 1);

            if (Document.InsertPicture(index, FocusCaret, picture.Hash) is not { } inserted)
            {
                Status.Text = "That picture is already in this piece.";

                return;
            }

            at = inserted;
        }

        DrawingHashes.Add(picture.Hash);
        Uploading.Add(picture.Hash);
        Transfers.Upload(picture);
        Status.Text = UPLOADING;
        Rebuild(at, at is not null);
    }

    private void OnDrawClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
        {
            Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

            return;
        }

        DrawSession = Session;
        DrawRequested?.Invoke(null, null);
    }

    private void OnEditClicked(string hash)
    {
        DisarmSubmit();
        CloseArmed = false;

        if (Transfers.BytesOf(hash) is not { } bytes || DrawingPictures.TryRead(bytes) is not { } drawing)
        {
            Status.Text = "That picture can't be edited.";

            return;
        }

        DrawSession = Session;
        DrawRequested?.Invoke(drawing, hash);
    }

    private bool IsDrawing(string hash)
    {
        if (DrawingHashes.Contains(hash))
            return true;

        if (NotDrawings.Contains(hash) || Transfers.BytesOf(hash) is not { } bytes)
            return false;

        if (DrawingPictures.TryRead(bytes) is null)
        {
            NotDrawings.Add(hash);

            return false;
        }

        DrawingHashes.Add(hash);

        return true;
    }

    private void OnUploadFinished(string hash, bool ok, string message)
    {
        if (!Uploading.Remove(hash))
            return;

        var hadOriginal = ReplacedBy.Remove(hash, out var original);

        if (ok)
        {
            Status.Text = "Picture added.";

            return;
        }

        Status.Text = message.Length > 0 ? message : "That picture was refused.";
        PullText();

        if (hadOriginal && Document.ReplacePicture(hash, original!))
        {
            Rebuild(null, false);

            return;
        }

        var index = Document.Blocks.FindIndex(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash));

        if (index >= 0)
            Rebuild(Document.RemovePicture(index), false);
    }

    // ---- buttons ----

    private void OnHeadingClicked() => Edit(() => Document.ToggleHeading(Math.Clamp(FocusIndex, 0, Document.Blocks.Count - 1), FocusCaret));

    private void OnSaveClicked()
    {
        PullText();
        DisarmSubmit();
        CloseArmed = false;

        if (HandInMode || Session is null)
            return;

        if (Uploading.Count > 0)
        {
            Status.Text = WAIT_FOR_UPLOADS;

            return;
        }

        Status.Text = SAVING;
        Send(CollegeActionType.SaveDraft);
    }

    private void OnSubmitClicked()
    {
        PullText();
        CloseArmed = false;

        if (Session is null)
            return;

        if (Uploading.Count > 0)
        {
            DisarmSubmit();
            Status.Text = WAIT_FOR_UPLOADS;

            return;
        }

        if (HandInMode)
        {
            Status.Text = "Handing in...";
            Send(CollegeActionType.HandIn);

            return;
        }

        if (!SubmitArmed)
        {
            SubmitArmed = true;
            SubmitButton.Caption = CONFIRM_SUBMIT_CAPTION;

            Status.Text = Session.FreeEntries > 0
                ? "Enter this piece for your free entry? Click Yes, submit."
                : $"Enter this piece for {Session.EntryCost} Educated {Marks(Session.EntryCost)}? Click Yes, submit.";

            return;
        }

        DisarmSubmit();
        Status.Text = "Submitting...";
        Send(CollegeActionType.Submit);
    }

    private void OnCloseClicked()
    {
        PullText();
        DisarmSubmit();

        //a second Close while the save is on its way hides anyway; a refusal still brings the piece back
        if ((Session is null) || ClosingOnSave)
        {
            ClosingOnSave = false;
            Hide();

            return;
        }

        if (HandInMode)
        {
            if (Document.IsDirty && !CloseArmed)
            {
                CloseArmed = true;
                Status.Text = "Close without handing in? Click Close again.";

                return;
            }
        } else if (Document.IsDirty)
        {
            if (Uploading.Count > 0)
            {
                Status.Text = WAIT_FOR_UPLOADS;

                return;
            }

            //the window stays up until the server says the draft is saved, so a refusal shows on this piece
            ClosingOnSave = true;
            Status.Text = SAVING;
            Send(CollegeActionType.SaveDraft);

            return;
        }

        CloseArmed = false;
        Hide();
    }

    private void Send(CollegeActionType type)
    {
        var piece = Document.ToInfo(Session!.Subject);
        piece.Blocks.RemoveAll(b => (b.Kind == CollegeBlockKind.Picture) && Uploading.Contains(b.Hash));

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = Session.Subject,
                Piece = piece
            });

        //marked clean as it is sent, so edits made while the reply is on its way still count as changes
        Document.MarkClean();
    }

    private void DisarmSubmit()
    {
        SubmitArmed = false;
        SubmitButton.Caption = SubmitCaption;
    }

    private static string Marks(int count) => count == 1 ? "Mark" : "Marks";

    /// <summary>A picture block: the picture centred (or "Uploading"/"Loading picture..."), with a remove button.</summary>
    private sealed class PictureBlock : UIPanel
    {
        private const int REMOVE_WIDTH = 22;
        private const int EDIT_WIDTH = 32;

        private readonly UIImage Image;
        private readonly UILabel Label;

        public string Hash { get; }
        public CustomButton EditButton { get; }
        public CustomButton RemoveButton { get; }

        public event Action? Loaded;

        public PictureBlock(string hash, int width)
        {
            Hash = hash;
            Width = width;
            Height = CustomButton.HEIGHT;

            Label = new UILabel
            {
                X = 2,
                Y = (CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = width - REMOVE_WIDTH - EDIT_WIDTH - 8,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.Gray,
                IsHitTestVisible = false
            };

            Image = new UIImage
            {
                Visible = false,
                IsHitTestVisible = false
            };

            RemoveButton = new CustomButton("x", REMOVE_WIDTH) { X = width - REMOVE_WIDTH };

            EditButton = new CustomButton("Edit", EDIT_WIDTH)
            {
                X = width - REMOVE_WIDTH - 2 - EDIT_WIDTH,
                Visible = false
            };

            AddChild(Label);
            AddChild(Image);
            AddChild(RemoveButton);
            AddChild(EditButton);
        }

        public override void Dispose()
        {
            //the texture belongs to CollegePictureTransfers, and UIImage.Dispose would dispose it
            Image.Texture = null;
            base.Dispose();
        }

        /// <summary>Shows the picture once it is uploaded (or downloaded); until then, a line saying which.</summary>
        public void Refresh(bool uploading, CollegePictureTransfers transfers)
        {
            if (Image.Texture is not null)
                return;

            if (uploading || !transfers.TryGetTexture(Hash, out var texture))
            {
                Label.Text = uploading ? UPLOADING : LOADING;

                return;
            }

            Image.Texture = texture;
            Image.Width = texture.Width;
            Image.Height = texture.Height;
            Image.X = Math.Max(0, (Width - texture.Width) / 2);
            Image.Visible = true;
            Label.Visible = false;
            Height = Math.Max(texture.Height, CustomButton.HEIGHT);
            Loaded?.Invoke();
        }
    }

    //the clip host the viewer scrolls: it moves the blocks up by whole steps
    private sealed class BlockViewport(UIPanel content, int step) : UIPanel, IVerticalScrollable
    {
        private int OffsetUnits;

        private int MaxScrollPx => Math.Max(0, content.Height - Height);

        int IVerticalScrollable.VerticalViewport => Math.Max(1, Height / step);

        int IVerticalScrollable.VerticalExtent => ((IVerticalScrollable)this).VerticalViewport + ((MaxScrollPx + step - 1) / step);

        int IVerticalScrollable.VerticalOffset
        {
            get => OffsetUnits;
            set
            {
                OffsetUnits = Math.Max(0, value);
                content.Y = -Math.Min(OffsetUnits * step, MaxScrollPx);
            }
        }

        /// <summary>A left press on the page that hit no block, with its y in block coordinates.</summary>
        public event Action<int>? PressedBelowBlocks;

        public override void OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left)
                PressedBelowBlocks?.Invoke(e.ScreenY - content.ScreenY);

            e.Handled = true;
        }

        /// <summary>Scrolls the least distance that shows the rows from <paramref name="top" /> to <paramref name="bottom" />.</summary>
        public void ScrollToShow(int top, int bottom)
        {
            var shown = Math.Min(OffsetUnits * step, MaxScrollPx);
            var scrollable = (IVerticalScrollable)this;

            if (top < shown)
                scrollable.VerticalOffset = top / step;
            else if (bottom > (shown + Height))
                scrollable.VerticalOffset = (bottom - Height + step - 1) / step;
        }
    }
}

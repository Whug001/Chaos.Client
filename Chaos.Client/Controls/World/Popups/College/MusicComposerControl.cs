#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

public enum MusicComposerMode
{
    Draft,
    HandIn
}

/// <summary>
///     The Music composer (spec: docs/superpowers/specs/2026-10-05-mileth-college-music-design.md): title, scale, speed
///     and melody instrument, the stacked Melody, Bass and Drums grids, the toolbar, the composer's note and the footer.
///     Draft and HandIn send the piece to the server; tunes play through the shared <see cref="TunePlayer" />.
/// </summary>
public sealed class MusicComposerControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TOP_ROW = 26;
    private const int GAP = 4;
    private const int SCALE_WIDTH = 114;
    private const int SPEED_WIDTH = 96;
    private const int INSTRUMENT_WIDTH = 90;
    private const int TITLE_WIDTH = INNER_WIDTH - SCALE_WIDTH - SPEED_WIDTH - INSTRUMENT_WIDTH - (3 * GAP);
    private const int GRID_X = LEFT - 2;
    private const int GRID_Y = 52;
    private const int TOOL_GAP = 2;
    private const int GROUP_GAP = 6;
    private const int PLAY_WIDTH = 54;
    private const int SMALL_TOOL_WIDTH = 40;
    private const int COPY_WIDTH = 130;
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
    private const string MORE_MELODY = "Entries need 16 melody notes on 3 rows.";
    private const string MORE_NOTES = "Hand-ins need at least 8 notes.";
    private const string SAVING = "Saving...";

    private static readonly TuneGridLayout GridLayout = TuneGridLayout.Composer;
    private static readonly int ToolsY = GRID_Y + GridLayout.Height + 3;
    private static readonly int NoteTop = ToolsY + CustomButton.HEIGHT + 3;

    private readonly UILabel CaptionLabel;
    private readonly CustomButton ClearButton;
    private readonly CustomButton CopyButton;
    private readonly TuneDocument Document = new();
    private readonly TuneGrid Grid;
    private readonly CustomButton InstrumentButton;
    private readonly Dictionary<TuneLayer, CustomButton> LayerButtons = new();
    private readonly CustomButton LoopButton;
    private readonly CustomTextBox NoteBox;
    private readonly UILabel NoteCounter;
    private readonly CustomButton PlayButton;
    private readonly TunePlayer Player;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly CustomButton ScaleButton;
    private readonly CustomButton SpeedButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly CustomTextBox TitleBox;
    private readonly CustomButton UndoButton;

    private string? CaptionTitle;
    private bool CloseArmed;

    //Close sent a save and waits for its Saved before hiding
    private bool ClosingOnSave;
    private bool Loop;
    private string SavedNote = string.Empty;
    private string SavedTitle = string.Empty;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;
    private bool TuneDirty;

    public MusicComposerControl(TunePlayer player)
        : base("_nsett", false)
    {
        Name = "MusicComposer";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        Player = player;

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TOP_ROW,
            Width = TITLE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Title"
        };

        AddChild(TitleBox);

        var x = LEFT + TITLE_WIDTH + GAP;
        ScaleButton = Tool(string.Empty, SCALE_WIDTH, x, TOP_ROW, () => Document.SetScale(TuneLabels.Next(Document.Scale)));
        x += SCALE_WIDTH + GAP;
        SpeedButton = Tool(string.Empty, SPEED_WIDTH, x, TOP_ROW, () => Document.SetSpeed(TuneLabels.Next(Document.Speed)));
        x += SPEED_WIDTH + GAP;

        InstrumentButton = Tool(
            string.Empty,
            INSTRUMENT_WIDTH,
            x,
            TOP_ROW,
            () => Document.SetInstrument(TuneLabels.Next(Document.Instrument)));

        Grid = new TuneGrid(GridLayout)
        {
            X = GRID_X,
            Y = GRID_Y,
            Document = Document,
            Editable = true
        };

        Grid.NotePlaced += (layer, row) => Player.Preview(Document.Scale, Document.Instrument, layer, row);

        Grid.Edited += () =>
        {
            DisarmSubmit();
            CloseArmed = false;
            RefreshControls();
        };

        Grid.BarClicked += bar => PlayFrom(bar * TuneGridLayout.COLUMNS_PER_BAR);
        AddChild(Grid);

        Document.Changed += () => TuneDirty = true;

        x = LEFT;

        foreach (var (layer, caption, width) in new[]
                 {
                     (TuneLayer.Melody, "Melody", 48),
                     (TuneLayer.Bass, "Bass", 36),
                     (TuneLayer.Drums, "Drums", 42)
                 })
        {
            LayerButtons[layer] = Tool(caption, width, x, ToolsY, () => Grid.SelectedLayer = layer);
            x += width + TOOL_GAP;
        }

        x += GROUP_GAP - TOOL_GAP;
        PlayButton = AddButton("Play", PLAY_WIDTH, x, ToolsY, OnPlayClicked);
        x += PLAY_WIDTH + TOOL_GAP;
        LoopButton = Tool("Loop", SMALL_TOOL_WIDTH, x, ToolsY, () => Loop = !Loop);
        x += SMALL_TOOL_WIDTH + TOOL_GAP;
        UndoButton = Tool("Undo", SMALL_TOOL_WIDTH, x, ToolsY, () => Document.Undo());
        x += SMALL_TOOL_WIDTH + TOOL_GAP;
        RedoButton = Tool("Redo", SMALL_TOOL_WIDTH, x, ToolsY, () => Document.Redo());
        x += SMALL_TOOL_WIDTH + GROUP_GAP;
        CopyButton = Tool("Copy bars 1-4 to 5-8", COPY_WIDTH, x, ToolsY, () => Document.CopyFirstHalf(Grid.SelectedLayer));
        x += COPY_WIDTH + TOOL_GAP;
        ClearButton = Tool("Clear layer", LEFT + INNER_WIDTH - x, x, ToolsY, () => Document.Clear(Grid.SelectedLayer));

        NoteBox = new CustomTextBox
        {
            X = LEFT,
            Y = NoteTop,
            Width = INNER_WIDTH,
            Height = NOTE_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_MUSIC_NOTE_CHARS,
            HintText = "Composer's note"
        };

        AddChild(NoteBox);

        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        SaveButton = AddButton("Save", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - COUNTER_WIDTH - 4 - STATUS_X);
        NoteCounter = Caption(string.Empty, LEFT + INNER_WIDTH - COUNTER_WIDTH, footerTextY, COUNTER_WIDTH, HorizontalAlignment.Right, LegendColors.Gray);
    }

    /// <summary>Raised with SaveDraft, Submit and HandIn.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    public MusicComposerMode Mode { get; private set; }

    private bool IsDirty => TuneDirty || (TitleBox.Text != SavedTitle) || (NoteBox.Text != SavedNote);

    private bool IsMine => ReferenceEquals(Player.Owner, this);

    private string SubmitCaption
        => Mode == MusicComposerMode.HandIn ? "Hand in"
            : Session?.FreeEntries > 0 ? "Submit (free entry)"
            : $"Submit ({Session?.EntryCost} {Marks(Session?.EntryCost ?? 0)})";

    /// <summary>Opens a Music draft or hand-in (a null piece is a blank tune).</summary>
    public void Open(CollegeDisplayArgs args)
    {
        Session = args;
        Mode = args.Mode == CollegeWriterMode.HandIn ? MusicComposerMode.HandIn : MusicComposerMode.Draft;

        Document.Load(
            args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Tune) is { } block
                ? TuneData.From(block)
                : TuneData.Empty);

        TitleBox.Text = args.Piece?.Title ?? string.Empty;
        NoteBox.Text = args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Text)?.Text ?? string.Empty;
        Begin();
    }

    /// <summary>Applies a WriterResult for this window's session. True when a hidden window was shown again (a refused save).</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        if (Session is null || (args.Mode != Session.Mode) || (args.Subject != Session.Subject))
            return false;

        Status.Text = args.Message;

        switch (args.Result)
        {
            case CollegeWriterResult.Submitted or CollegeWriterResult.HandedIn:
                ClosingOnSave = false;
                MarkSaved();
                Hide();

                break;
            case CollegeWriterResult.Saved when ClosingOnSave:
                ClosingOnSave = false;

                //composing done while the save was on its way keeps the window open
                if (!IsDirty)
                    Hide();

                break;
            case CollegeWriterResult.Refused:
                ClosingOnSave = false;
                TuneDirty = true;

                if (!Visible)
                {
                    Show();

                    return true;
                }

                break;
        }

        return false;
    }

    public void SaveIfDirty()
    {
        if (Visible && Session is not null && (Mode == MusicComposerMode.Draft) && IsDirty)
            Send(CollegeActionType.SaveDraft);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        Player.StopIfOwner(this);
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
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

        var typing = TitleBox.IsFocused || NoteBox.IsFocused;

        if (!typing && e.Accelerator && e.Keycode is Keycode.Z or Keycode.Y)
        {
            if (e.Keycode == Keycode.Z)
                Document.Undo();
            else
                Document.Redo();

            DisarmSubmit();
            RefreshControls();
            e.Handled = true;

            return;
        }

        if (!typing && !e.Accelerator && (e.Keycode == Keycode.Space))
        {
            OnPlayClicked();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            var counter = $"{NoteBox.Text.Length}/{CollegeProtocol.MAX_MUSIC_NOTE_CHARS}";

            if (NoteCounter.Text != counter)
                NoteCounter.Text = counter;

            if ((Mode == MusicComposerMode.Draft) && (TitleBox.Text != CaptionTitle))
            {
                CaptionTitle = TitleBox.Text;
                CaptionLabel.Text = DraftCaption(CaptionTitle);
            }

            var mine = IsMine;
            var play = !mine ? "Play" : Player.IsRendering ? "Wait..." : Player.IsPlaying ? "Stop" : "Play";

            if (PlayButton.Caption != play)
                PlayButton.Caption = play;

            Grid.Playhead = mine && Player.IsPlaying ? Player.Column : null;
        }

        base.Update(gameTime);
    }

    private void Begin()
    {
        Player.StopIfOwner(this);
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        TuneDirty = false;
        CloseArmed = false;
        ClosingOnSave = false;
        Loop = false;
        Grid.SelectedLayer = TuneLayer.Melody;
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        SaveButton.Visible = Mode == MusicComposerMode.Draft;
        SubmitButton.X = Mode == MusicComposerMode.HandIn ? LEFT : SUBMIT_X;
        CaptionTitle = TitleBox.Text;

        CaptionLabel.Text = Mode == MusicComposerMode.HandIn
            ? OneLine(Session is { Prompt.Length: > 0 } session ? $"Hand-in: {session.Prompt}" : "Music: class hand-in")
            : DraftCaption(TitleBox.Text);

        Status.Text = string.Empty;
        DisarmSubmit();
        RefreshControls();
        Show();
    }

    private void RefreshControls()
    {
        ScaleButton.Caption = $"Scale: {TuneLabels.Scale(Document.Scale)}";
        SpeedButton.Caption = $"Speed: {TuneLabels.Speed(Document.Speed)}";
        InstrumentButton.Caption = $"Melody: {TuneLabels.Instrument(Document.Instrument)}";

        foreach (var (layer, button) in LayerButtons)
            button.Selected = Grid.SelectedLayer == layer;

        LoopButton.Selected = Loop;
        UndoButton.Enabled = Document.CanUndo;
        RedoButton.Enabled = Document.CanRedo;
    }

    private CustomButton Tool(string caption, int width, int x, int y, Action onClick)
        => AddButton(
            caption,
            width,
            x,
            y,
            () =>
            {
                onClick();
                DisarmSubmit();
                CloseArmed = false;
                RefreshControls();
            });

    private void OnPlayClicked()
    {
        if (IsMine && (Player.IsPlaying || Player.IsRendering))
        {
            Player.Stop();

            return;
        }

        PlayFrom(0);
    }

    private void PlayFrom(int column) => Player.Play(Document.Snapshot(), this, column, LoopSource);

    //asked at the end of each pass: the tune as it is now while Loop is on, so edits are heard on the next pass
    private TuneData? LoopSource() => Loop && Visible ? Document.Snapshot() : null;

    private void OnSaveClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if ((Mode != MusicComposerMode.Draft) || Session is null)
            return;

        Status.Text = SAVING;
        Send(CollegeActionType.SaveDraft);
    }

    private void OnSubmitClicked()
    {
        CloseArmed = false;

        if (Session is null)
            return;

        if (Mode == MusicComposerMode.HandIn)
        {
            if (Document.NoteCount < CollegeProtocol.MIN_HAND_IN_NOTES)
            {
                Status.Text = MORE_NOTES;

                return;
            }

            Status.Text = "Handing in...";
            Send(CollegeActionType.HandIn);

            return;
        }

        if (TitleBox.Text.Trim().Length == 0)
        {
            DisarmSubmit();
            Status.Text = "Give your tune a title.";

            return;
        }

        if ((Document.MelodyNotes < CollegeProtocol.MIN_ENTRY_MELODY_NOTES)
            || (Document.MelodyRows < CollegeProtocol.MIN_ENTRY_MELODY_ROWS))
        {
            DisarmSubmit();
            Status.Text = MORE_MELODY;

            return;
        }

        if (!SubmitArmed)
        {
            SubmitArmed = true;
            SubmitButton.Caption = CONFIRM_SUBMIT_CAPTION;

            Status.Text = Session.FreeEntries > 0
                ? "Use your free entry? Click Yes, submit."
                : $"Enter it for {Session.EntryCost} {Marks(Session.EntryCost)}? Click Yes, submit.";

            return;
        }

        DisarmSubmit();
        Status.Text = "Submitting...";
        Send(CollegeActionType.Submit);
    }

    private void OnCloseClicked()
    {
        DisarmSubmit();

        //a second Close while the save is on its way hides anyway; a refusal still brings the piece back
        if ((Mode == MusicComposerMode.Draft) && Session is not null && !ClosingOnSave && IsDirty)
        {
            ClosingOnSave = true;
            Status.Text = SAVING;
            Send(CollegeActionType.SaveDraft);

            return;
        }

        if ((Mode == MusicComposerMode.HandIn) && IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            Status.Text = "Close without handing in? Click Close again.";

            return;
        }

        ClosingOnSave = false;
        CloseArmed = false;
        Hide();
    }

    private void Send(CollegeActionType type)
    {
        var blocks = new List<CollegeBlockInfo>
        {
            Document.Snapshot()
                    .ToBlock()
        };

        if (NoteBox.Text.Trim().Length > 0)
            blocks.Add(new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = NoteBox.Text });

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = CollegeSubjectCode.Music,
                Piece = new CollegePieceInfo
                {
                    Subject = CollegeSubjectCode.Music,
                    Title = TitleBox.Text.Trim(),
                    Blocks = blocks
                }
            });

        //marked saved as it is sent, so composing done while the reply is on its way still counts as a change
        MarkSaved();
    }

    private void MarkSaved()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        TuneDirty = false;
    }

    private void DisarmSubmit()
    {
        SubmitArmed = false;
        SubmitButton.Caption = SubmitCaption;
    }

    private static string DraftCaption(string title)
        => title.Trim().Length > 0 ? OneLine($"Music: \"{title.Trim()}\"") : "Music: draft";

    private static string OneLine(string text)
    {
        var lines = TextRenderer.WrapText(text, INNER_WIDTH - 8);

        return lines.Count <= 1 ? text : lines[0].TrimEnd() + "...";
    }

    private static string Marks(int count) => count == 1 ? "Mark" : "Marks";
}

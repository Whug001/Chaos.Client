#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Mileth College windows: writer, Art canvas, Music composer, reader, judging list, gallery, hand-in list, quiz editor and the class quiz and debate windows.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private CollegeWindows? College;

    private void WireCollege()
    {
        Game.Connection.OnCollegeDisplay += HandleCollegeDisplay;

        //logging out takes the writer away without its Close, which is what saves a draft. This runs after
        //WireOptionsDialog's RequestExit(), and that is still in time: RequestExit() only asks, and the client sends
        //the real logout (RequestExit(false)) when the server's ExitResponse comes back, so the server takes this
        //SaveDraft first, while the player is still in the world
        MainOptions.OnExit += SaveCollegeDraft;

        //closing the game window (X, Alt+F4) while in the world skips the logout, so the drafts are sent as it closes
        Game.Closing += SaveCollegeDraft;
    }

    private void UnwireCollege()
    {
        Game.Connection.OnCollegeDisplay -= HandleCollegeDisplay;
        MainOptions.OnExit -= SaveCollegeDraft;
        Game.Closing -= SaveCollegeDraft;
    }

    private void SaveCollegeDraft()
    {
        College?.Writer.SaveIfDirty();
        College?.Canvas.SaveIfDirty();
        College?.Composer.SaveIfDirty();
        College?.QuizEditor.SaveIfDirty();
    }

    private void SendCollegeAction(CollegeActionArgs args) => Game.Connection.SendCollegeAction(args);

    private CollegeWindows BuildCollege()
    {
        var transfers = new CollegePictureTransfers(SendCollegeAction, new CollegePictureCache());
        var drawings = new CollegeDrawings();
        var tunes = new CollegeTunes();

        //above the reader, which shares the screen's ZIndex 2 with the other popups
        var votesPopup = new TextPopupControl
        {
            ZIndex = 3
        };

        var windows = new CollegeWindows
        {
            Transfers = transfers,
            Judging = new CollegeJudgingControl
            {
                ZIndex = 2
            },
            Drawings = drawings,
            Tunes = tunes,
            Composer = new MusicComposerControl(Game.TunePlayer)
            {
                ZIndex = 2
            },
            Canvas = new ArtCanvasControl
            {
                ZIndex = 2
            },
            PictureCanvas = new ArtCanvasControl
            {
                ZIndex = 2
            },
            Gallery = new CollegeGalleryControl(drawings, tunes, Game.TunePlayer)
            {
                ZIndex = 2
            },
            HandIns = new CollegeHandInsControl
            {
                ZIndex = 2
            },
            Writer = new CollegeWriterControl(transfers, Game.Window.Handle)
            {
                ZIndex = 2
            },
            Reader = new CollegeReaderControl(transfers, votesPopup, Game.TunePlayer)
            {
                ZIndex = 2
            },
            QuizEditor = new QuizEditorControl
            {
                ZIndex = 2
            },
            QuizCard = new QuizCardControl
            {
                ZIndex = 2
            },
            QuizTeacher = new QuizTeacherPanel
            {
                ZIndex = 2
            },
            Debate = new DebatePanel
            {
                ZIndex = 2
            },
            DebateVote = new DebateVoteCard
            {
                ZIndex = 2
            },
            Marks = new DebateMarkTable(),
            Work = new WorkMarkTable()
        };

        windows.Judging.ActionRequested += SendCollegeAction;
        windows.Gallery.ActionRequested += SendCollegeAction;
        windows.HandIns.ActionRequested += SendCollegeAction;
        windows.Writer.ActionRequested += SendCollegeAction;
        windows.Reader.ActionRequested += SendCollegeAction;
        windows.Canvas.ActionRequested += SendCollegeAction;
        windows.Composer.ActionRequested += SendCollegeAction;
        windows.QuizEditor.ActionRequested += SendCollegeAction;
        windows.QuizCard.ActionRequested += SendCollegeAction;
        windows.QuizTeacher.ActionRequested += SendCollegeAction;
        windows.Debate.ActionRequested += SendCollegeAction;
        windows.DebateVote.ActionRequested += SendCollegeAction;
        windows.PictureCanvas.PictureMade += windows.Writer.InsertDrawnPicture;

        windows.Writer.DrawRequested += (drawing, replacing) =>
        {
            BringToFront(windows.PictureCanvas);
            windows.PictureCanvas.OpenPicture(drawing, replacing);
        };

        Root!.AddChild(windows.Judging);
        Root.AddChild(windows.Gallery);
        Root.AddChild(windows.HandIns);
        Root.AddChild(windows.Writer);
        Root.AddChild(windows.Reader);
        Root.AddChild(windows.Canvas);
        Root.AddChild(windows.PictureCanvas);
        Root.AddChild(windows.Composer);
        Root.AddChild(windows.QuizTeacher);
        Root.AddChild(windows.Debate);
        Root.AddChild(windows.QuizEditor);
        Root.AddChild(windows.QuizCard);
        Root.AddChild(windows.DebateVote);
        Root.AddChild(votesPopup);

        return windows;
    }

    private void HandleCollegeDisplay(CollegeDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        College ??= BuildCollege();

        switch (args.Type)
        {
            case CollegeDisplayType.OpenWriter when args.Subject == CollegeSubjectCode.Art:
                BringToFront(College.Canvas);
                College.Canvas.Open(args);

                break;
            case CollegeDisplayType.OpenWriter when args.Subject == CollegeSubjectCode.Music:
                BringToFront(College.Composer);
                College.Composer.Open(args);

                break;
            case CollegeDisplayType.OpenWriter:
                BringToFront(College.Writer);
                College.Writer.Open(args);

                break;
            case CollegeDisplayType.WriterResult when args.Subject == CollegeSubjectCode.Art:
                if (College.Canvas.ShowResult(args))
                    BringToFront(College.Canvas);

                break;
            case CollegeDisplayType.WriterResult when args.Subject == CollegeSubjectCode.Music:
                if (College.Composer.ShowResult(args))
                    BringToFront(College.Composer);

                break;
            case CollegeDisplayType.WriterResult:
                //a refused save shows a hidden writer again, over the other windows
                if (College.Writer.ShowResult(args))
                    BringToFront(College.Writer);

                break;
            case CollegeDisplayType.Drawing:
                College.Drawings.OnDrawing(args);

                break;
            case CollegeDisplayType.Tune:
                College.Tunes.OnTune(args);

                break;
            case CollegeDisplayType.PictureReply:
                College.Transfers.OnReply(args);

                break;
            case CollegeDisplayType.PicturePart:
                College.Transfers.OnPart(args);

                break;
            case CollegeDisplayType.JudgingList:
                ShowList(College.Judging, () => College.Judging.Open(args));

                break;
            case CollegeDisplayType.Piece:
                //Prev and Next need the list's order before the footer is built
                College.Reader.SetSiblings(
                    args.Context is CollegePieceContext.Judge or CollegePieceContext.Verdict ? College.Judging.VisibleOrder : []);

                BringToFront(College.Reader);
                College.Reader.Open(args);

                break;
            case CollegeDisplayType.GalleryList:
                ShowList(College.Gallery, () => College.Gallery.Open(args));

                break;
            case CollegeDisplayType.HandInList:
                ShowList(College.HandIns, () => College.HandIns.Open(args));

                break;
            case CollegeDisplayType.QuizList:
                ShowList(College.QuizEditor, () => College.QuizEditor.OpenList(args));

                break;
            case CollegeDisplayType.QuizEdit:
                BringToFront(College.QuizEditor);
                College.QuizEditor.OpenQuiz(args);

                break;
            case CollegeDisplayType.QuizResult:
                if (College.QuizEditor.ShowResult(args))
                    BringToFront(College.QuizEditor);

                break;
            case CollegeDisplayType.QuizCard:
                College.QuizCard.Apply(args.QuizView, WorldState.PlayerName, args.Reopen);

                break;
            case CollegeDisplayType.QuizTeacher:
                College.QuizTeacher.Apply(args.QuizView, args.Reopen);

                break;
            case CollegeDisplayType.DebateState:
                College.Debate.Apply(args.Debate, args.Reopen);
                College.DebateVote.Apply(args.Debate, args.Reopen);

                break;
            case CollegeDisplayType.DebateMarks:
                College.Marks.Set(args.DebateMarks);

                break;
            case CollegeDisplayType.ToolClosed:
                CloseClassTools();

                break;
            case CollegeDisplayType.WorkMarks:
                College.Work.Set(args.WorkMarks);

                break;
        }
    }

    /// <summary>
    ///     Moves a College window over the others. They share ZIndex 2, which draws (and takes clicks) in add order, so the
    ///     window last moved to the end is the one on top, matching the control stack's newest entry.
    /// </summary>
    private void BringToFront(UIElement window)
    {
        Root!.Children.Remove(window);
        Root.AddChild(window);
    }

    //a list that opens comes to the front; one already open just refreshes, so it stays under a piece being read
    private void ShowList(UIElement list, Action open)
    {
        if (!list.Visible)
            BringToFront(list);

        open();
    }

    /// <summary>Closes the class quiz and debate windows and clears the side markers: the tool ended, or the player left the room.</summary>
    private void CloseClassTools()
    {
        if (College is null)
            return;

        College.QuizCard.Close();
        College.QuizTeacher.Close();
        College.Debate.Close();
        College.DebateVote.Close();
        College.Marks.Clear();
    }

    /// <summary>A new map: its room sends its own hand-in marks, and an open hand-in window tells the new room about itself.</summary>
    private void ResetWorkMarks()
    {
        if (College is null)
            return;

        College.Work.Clear();
        College.Canvas.ReportHandInWindow();
    }

    /// <summary>Keeps the Teacher's quiz panel and the debate panel at the viewport's top right, under the poll box while it shows.</summary>
    private void PlaceClassToolPanels()
    {
        if (College is null)
            return;

        var viewport = WorldHud.ViewportBounds;
        var top = viewport.Top + 2 + (WorldState.Poll.ShouldShow ? VotePanel.Height + 2 : 0);

        //a tall panel under a tall poll box would run off the bottom; being on screen wins over clearing the poll box
        College.QuizTeacher.Place(viewport, Math.Max(viewport.Top + 2, Math.Min(top, viewport.Bottom - College.QuizTeacher.Height - 2)));
        College.Debate.Place(viewport, Math.Max(viewport.Top + 2, Math.Min(top, viewport.Bottom - College.Debate.Height - 2)));
    }

    private sealed class CollegeWindows
    {
        public required ArtCanvasControl Canvas { get; init; }
        public required MusicComposerControl Composer { get; init; }
        public required CollegeDrawings Drawings { get; init; }
        public required CollegeGalleryControl Gallery { get; init; }
        public required CollegeHandInsControl HandIns { get; init; }
        public required CollegeJudgingControl Judging { get; init; }
        public required DebatePanel Debate { get; init; }
        public required DebateVoteCard DebateVote { get; init; }
        public required DebateMarkTable Marks { get; init; }
        public required WorkMarkTable Work { get; init; }
        public required QuizCardControl QuizCard { get; init; }
        public required QuizEditorControl QuizEditor { get; init; }
        public required QuizTeacherPanel QuizTeacher { get; init; }
        public required ArtCanvasControl PictureCanvas { get; init; }
        public required CollegeReaderControl Reader { get; init; }
        public required CollegePictureTransfers Transfers { get; init; }
        public required CollegeTunes Tunes { get; init; }
        public required CollegeWriterControl Writer { get; init; }
    }
}

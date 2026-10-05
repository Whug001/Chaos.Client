#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Mileth College windows: writer, Art canvas, reader, judging list, gallery and hand-in list.</summary>
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
    }

    private void UnwireCollege()
    {
        Game.Connection.OnCollegeDisplay -= HandleCollegeDisplay;
        MainOptions.OnExit -= SaveCollegeDraft;
    }

    private void SaveCollegeDraft()
    {
        College?.Writer.SaveIfDirty();
        College?.Canvas.SaveIfDirty();
    }

    private void SendCollegeAction(CollegeActionArgs args) => Game.Connection.SendCollegeAction(args);

    private CollegeWindows BuildCollege()
    {
        var transfers = new CollegePictureTransfers(SendCollegeAction, new CollegePictureCache());
        var drawings = new CollegeDrawings();

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
            Canvas = new ArtCanvasControl
            {
                ZIndex = 2
            },
            PictureCanvas = new ArtCanvasControl
            {
                ZIndex = 2
            },
            Gallery = new CollegeGalleryControl(drawings)
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
            Reader = new CollegeReaderControl(transfers, votesPopup)
            {
                ZIndex = 2
            }
        };

        windows.Judging.ActionRequested += SendCollegeAction;
        windows.Gallery.ActionRequested += SendCollegeAction;
        windows.HandIns.ActionRequested += SendCollegeAction;
        windows.Writer.ActionRequested += SendCollegeAction;
        windows.Reader.ActionRequested += SendCollegeAction;
        windows.Canvas.ActionRequested += SendCollegeAction;
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
            case CollegeDisplayType.OpenWriter:
                BringToFront(College.Writer);
                College.Writer.Open(args);

                break;
            case CollegeDisplayType.WriterResult when args.Subject == CollegeSubjectCode.Art:
                if (College.Canvas.ShowResult(args))
                    BringToFront(College.Canvas);

                break;
            case CollegeDisplayType.WriterResult:
                //a refused save shows a hidden writer again, over the other windows
                if (College.Writer.ShowResult(args))
                    BringToFront(College.Writer);

                break;
            case CollegeDisplayType.Drawing:
                College.Drawings.OnDrawing(args);

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

    private sealed class CollegeWindows
    {
        public required ArtCanvasControl Canvas { get; init; }
        public required CollegeDrawings Drawings { get; init; }
        public required CollegeGalleryControl Gallery { get; init; }
        public required CollegeHandInsControl HandIns { get; init; }
        public required CollegeJudgingControl Judging { get; init; }
        public required ArtCanvasControl PictureCanvas { get; init; }
        public required CollegeReaderControl Reader { get; init; }
        public required CollegePictureTransfers Transfers { get; init; }
        public required CollegeWriterControl Writer { get; init; }
    }
}

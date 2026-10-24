#region
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Controls.World.Popups.College.Library;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Mileth College library windows: the shelf carousel, the submissions, review and College-piece lists, and the second drawing cache behind the carousel's covers.</summary>
public sealed partial class WorldScreen
{
    //a cover Draw opens the picture canvas; the next picture it produces goes back into the writer's cover,
    //while a normal document Draw keeps inserting into the document
    private bool CoverDrawPending;

    private void BuildLibrary(CollegeWindows windows)
    {
        windows.LibraryCarousel.ActionRequested += SendCollegeAction;
        windows.LibrarySubmissions.ActionRequested += SendCollegeAction;
        windows.LibraryReview.ActionRequested += SendCollegeAction;
        windows.LibraryCollegePieces.ActionRequested += SendCollegeAction;

        windows.Writer.DrawRequested += (drawing, replacing) =>
        {
            CoverDrawPending = false;
            BringToFront(windows.PictureCanvas);
            windows.PictureCanvas.OpenPicture(drawing, replacing);
        };

        windows.Writer.CoverDrawRequested += () =>
        {
            CoverDrawPending = true;
            BringToFront(windows.PictureCanvas);
            windows.PictureCanvas.OpenPicture(null, null);
        };

        windows.PictureCanvas.PictureMade += (picture, replacing) =>
        {
            if (CoverDrawPending)
            {
                CoverDrawPending = false;
                windows.Writer.InsertCoverPicture(picture);
            } else
                windows.Writer.InsertDrawnPicture(picture, replacing);
        };

        Root!.AddChild(windows.LibraryCarousel);
        Root.AddChild(windows.LibrarySubmissions);
        Root.AddChild(windows.LibraryReview);
        Root.AddChild(windows.LibraryCollegePieces);
    }

    private bool HandleLibraryDisplay(CollegeDisplayArgs args)
    {
        var college = College!;

        switch (args.Type)
        {
            case CollegeDisplayType.LibraryShelf:
                ShowList(college.LibraryCarousel, () => college.LibraryCarousel.Open(args));

                return true;
            case CollegeDisplayType.LibraryBook:
                BringToFront(college.Reader);
                college.Reader.SetSiblings([]);
                college.Reader.Open(args);

                return true;
            case CollegeDisplayType.LibrarySubmissions:
                //a Take down or a Dismiss answers with a list; the removed book must not stay open over it
                college.Reader.Hide();
                ShowList(college.LibrarySubmissions, () => college.LibrarySubmissions.Open(args));

                return true;
            case CollegeDisplayType.LibraryReview:
                college.Reader.Hide();
                ShowList(college.LibraryReview, () => college.LibraryReview.Open(args));

                return true;
            case CollegeDisplayType.LibraryCollegePieces:
                college.Reader.Hide();
                ShowList(college.LibraryCollegePieces, () => college.LibraryCollegePieces.Open(args));

                return true;
            case CollegeDisplayType.LibraryDrawing:
                college.LibraryDrawings.OnDrawing(args);

                return true;
            case CollegeDisplayType.OpenWriter when args is { Mode: CollegeWriterMode.Library, Subject: CollegeSubjectCode.Art }:
                BringToFront(college.Canvas);
                college.Canvas.Open(args);

                return true;
            case CollegeDisplayType.OpenWriter when args.Mode == CollegeWriterMode.Library:
                BringToFront(college.Writer);
                college.Writer.Open(args);

                return true;
            case CollegeDisplayType.WriterResult when args.Mode == CollegeWriterMode.Library:
                HandleLibraryWriterResult(args);

                return true;
            default:
                return false;
        }
    }

    private void HandleLibraryWriterResult(CollegeDisplayArgs args)
    {
        //whichever Library session window is open shows the result; a cover-draw picture canvas never takes one.
        //the server reports every LibrarySend on the orange bar itself, so there is no fallback here.
        if (College!.Writer.Visible && College.Writer.ShowResult(args))
            BringToFront(College.Writer);

        if (College.Canvas.Visible && (College.Canvas.Mode == ArtCanvasMode.Library) && College.Canvas.ShowResult(args))
            BringToFront(College.Canvas);
    }
}

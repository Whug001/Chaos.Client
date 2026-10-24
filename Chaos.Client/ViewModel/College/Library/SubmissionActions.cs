using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College.Library;

public readonly record struct SubmissionActions(bool Edit, bool Send, bool TakeBack, bool TakeDown)
{
    public static SubmissionActions For(LibraryStatusCode status)
        => status switch
        {
            LibraryStatusCode.Draft => new SubmissionActions(true, true, false, false),
            LibraryStatusCode.Waiting => new SubmissionActions(false, false, true, false),
            LibraryStatusCode.OnShelf => new SubmissionActions(true, false, false, true),
            LibraryStatusCode.EditWaiting => new SubmissionActions(false, false, true, true),
            LibraryStatusCode.Rejected => new SubmissionActions(true, true, false, false),
            LibraryStatusCode.TakenDown => new SubmissionActions(true, false, false, false),
            _ => new SubmissionActions(false, false, false, false)
        };
}

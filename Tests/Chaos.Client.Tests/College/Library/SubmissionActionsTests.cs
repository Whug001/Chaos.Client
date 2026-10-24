using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class SubmissionActionsTests
{
    [Test]
    [Arguments(LibraryStatusCode.Draft, true, true, false, false)]
    [Arguments(LibraryStatusCode.Waiting, false, false, true, false)]
    [Arguments(LibraryStatusCode.OnShelf, true, false, false, true)]
    [Arguments(LibraryStatusCode.EditWaiting, false, false, true, true)]
    [Arguments(LibraryStatusCode.Rejected, true, true, false, false)]
    [Arguments(LibraryStatusCode.TakenDown, true, false, false, false)]
    public void Each_status_offers_its_actions(LibraryStatusCode status, bool edit, bool send, bool takeBack, bool takeDown)
        => SubmissionActions.For(status).Should().Be(new SubmissionActions(edit, send, takeBack, takeDown));
}

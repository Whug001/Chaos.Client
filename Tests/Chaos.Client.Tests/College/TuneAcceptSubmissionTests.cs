using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

/// <summary>
///     The contest entry handshake: ContestEntered marks the tune saved only for the snapshot
///     that was submitted, so edits made after entering stay dirty.
/// </summary>
public class TuneAcceptSubmissionTests
{
    private static TuneDocument Document()
    {
        var document = new TuneDocument();
        document.Load(TuneData.Empty);

        return document;
    }

    private static void PlaceNote(TuneDocument document, int column)
    {
        document.BeginPlace(TuneLayer.Melody, 3, column).Should().BeTrue();
        document.EndPlace();
    }

    [Test]
    public void Accepting_an_unchanged_submission_marks_the_tune_saved()
    {
        var document = Document();
        PlaceNote(document, 5);
        document.IsDirty.Should().BeTrue();

        document.TryAcceptSubmission(document.Version).Should().BeTrue();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Edits_made_after_the_submit_stay_dirty()
    {
        var document = Document();
        PlaceNote(document, 5);

        var submittedVersion = document.Version;

        PlaceNote(document, 9);
        document.Version.Should().NotBe(submittedVersion);

        document.TryAcceptSubmission(submittedVersion).Should().BeFalse();
        document.IsDirty.Should().BeTrue();
    }

    [Test]
    public void A_later_submit_supersedes_the_earlier_one()
    {
        var document = Document();
        PlaceNote(document, 5);

        var firstSubmit = document.Version;
        PlaceNote(document, 9);

        var secondSubmit = document.Version;

        document.TryAcceptSubmission(firstSubmit).Should().BeFalse();
        document.IsDirty.Should().BeTrue();

        document.TryAcceptSubmission(secondSubmit).Should().BeTrue();
        document.IsDirty.Should().BeFalse();
    }
}

using Chaos.Client.ViewModel.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

/// <summary>
///     The quiz and word-list save queue: a logout or exit save landing on top of an earlier
///     pending save must still send the newest edits instead of returning early.
/// </summary>
public class PendingSaveTests
{
    [Test]
    public void The_first_save_goes_out_at_once()
    {
        var saver = new PendingSave();

        saver.TryBegin(3).Should().BeTrue();
        saver.AwaitingSave.Should().BeTrue();
        saver.SaveQueued.Should().BeFalse();
        saver.SentVersion.Should().Be(3);
    }

    [Test]
    public void A_second_save_while_one_is_pending_is_queued_instead_of_sent()
    {
        var saver = new PendingSave();
        saver.TryBegin(3).Should().BeTrue();

        saver.TryBegin(4).Should().BeFalse();
        saver.AwaitingSave.Should().BeTrue();
        saver.SaveQueued.Should().BeTrue();
        saver.SentVersion.Should().Be(3);
    }

    [Test]
    public void The_queued_save_goes_out_after_the_pending_one_answers()
    {
        var saver = new PendingSave();
        saver.TryBegin(3).Should().BeTrue();
        saver.TryBegin(4).Should().BeFalse();

        saver.TakeQueued(isDirty: true, currentVersion: 4, refusedVersion: -1).Should().BeTrue();
        saver.AwaitingSave.Should().BeFalse();
        saver.SaveQueued.Should().BeFalse();
    }

    [Test]
    public void No_follow_up_goes_out_when_nothing_changed_since_the_pending_save()
    {
        var saver = new PendingSave();
        saver.TryBegin(3).Should().BeTrue();
        saver.TryBegin(3).Should().BeFalse();

        saver.TakeQueued(isDirty: false, currentVersion: 3, refusedVersion: -1).Should().BeFalse();
        saver.AwaitingSave.Should().BeFalse();
        saver.SaveQueued.Should().BeFalse();
    }

    [Test]
    public void No_follow_up_goes_out_for_a_refused_version()
    {
        var saver = new PendingSave();
        saver.TryBegin(3).Should().BeTrue();
        saver.TryBegin(3).Should().BeFalse();

        saver.TakeQueued(isDirty: true, currentVersion: 3, refusedVersion: 3).Should().BeFalse();
        saver.SaveQueued.Should().BeFalse();
    }

    [Test]
    public void Reset_clears_a_pending_save_and_its_queue()
    {
        var saver = new PendingSave();
        saver.TryBegin(3).Should().BeTrue();
        saver.TryBegin(4).Should().BeFalse();

        saver.Reset();

        saver.AwaitingSave.Should().BeFalse();
        saver.SaveQueued.Should().BeFalse();
        saver.TryBegin(5).Should().BeTrue();
        saver.SentVersion.Should().Be(5);
    }
}

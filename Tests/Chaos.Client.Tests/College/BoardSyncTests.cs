using Chaos.Client.ViewModel.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class BoardSyncTests
{
    [Test]
    public void A_change_is_due_once_600_ms_after_the_last_send()
    {
        var sync = new BoardSync();

        sync.Due(1, 0).Should().BeTrue();
        sync.MarkSent(1, 0);
        sync.Due(1, 5_000).Should().BeFalse();
        sync.Due(2, 599).Should().BeFalse();
        sync.Unsent(2).Should().BeTrue();
        sync.Due(2, 600).Should().BeTrue();
    }

    [Test]
    public void Reset_counts_a_version_as_sent()
    {
        var sync = new BoardSync();

        sync.Reset(4);

        sync.Unsent(4).Should().BeFalse();
        sync.Due(4, 10_000).Should().BeFalse();
    }
}

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

    [Test]
    public void It_knows_the_last_eight_boards_it_sent_or_loaded()
    {
        var sync = new BoardSync();

        for (byte i = 0; i < 9; i++)
            sync.Remember([i, 1]);

        sync.IsKnown([0, 1]).Should().BeFalse();
        sync.IsKnown([1, 1]).Should().BeTrue();
        sync.IsKnown([8, 1]).Should().BeTrue();
        sync.IsKnown([9, 1]).Should().BeFalse();
    }
}

#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchArtTests
{
    [Test]
    public void Playmat_file_name_is_three_digits()
        => TcgMatchArt.PlaymatFile(4).Should().Be("tcgmat004.spf");

    [Test]
    public void Missing_playmat_draws_the_table_only()
        => TcgMatchArt.PlaymatPresent(4).Should().BeFalse();
}

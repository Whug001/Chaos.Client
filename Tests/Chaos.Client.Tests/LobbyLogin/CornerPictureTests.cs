using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CornerPictureTests
{
    [Test]
    public void Picks_stay_within_the_nine_pictures_and_reach_all_of_them()
    {
        var random = new Random(1234);

        var picks = Enumerable.Range(0, 1000)
                              .Select(_ => CornerPicture.Pick(random))
                              .ToHashSet();

        picks.Should()
             .BeEquivalentTo(Enumerable.Range(0, 9).Select(i => $"lodbk0{i}.epf"));
    }
}

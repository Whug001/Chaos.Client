using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerBoardSkinsTests
{
    [Test]
    public void The_default_skin_is_the_proving_ground()
        => TamerBoardSkins.FileFor(TamerBoardSkins.DEFAULT_KEY).Should().Be("tmrbd000.spf");

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("no-such-skin")]
    public void An_unknown_or_empty_key_draws_the_default_skin(string? key)
    {
        TamerBoardSkins.Resolve(key).Should().Be(TamerBoardSkins.DEFAULT_KEY);
        TamerBoardSkins.FileFor(key).Should().Be(TamerBoardSkins.FileFor(TamerBoardSkins.DEFAULT_KEY));
    }

    [Test]
    public void Keys_are_matched_without_regard_to_case_or_spaces()
        => TamerBoardSkins.Resolve(" Proving ").Should().Be(TamerBoardSkins.DEFAULT_KEY);

    [Test]
    public void Every_skin_file_fits_a_setoa_entry_name()
    {
        TamerBoardSkins.Keys.Should().Contain(TamerBoardSkins.DEFAULT_KEY);

        foreach (var key in TamerBoardSkins.Keys)
        {
            var file = TamerBoardSkins.FileFor(key);

            file.Length.Should().BeLessThanOrEqualTo(13);
            file.Should().EndWith(".spf");
        }
    }

    [Test]
    [Arguments(280, 240, true)]
    [Arguments(281, 240, false)]
    [Arguments(280, 239, false)]
    [Arguments(1, 1, false)]
    [Arguments(0, 0, false)]
    public void Only_a_picture_of_the_board_size_is_drawn(int width, int height, bool fits)
        => TamerBoardSkins.Fits(width, height, 280, 240).Should().Be(fits);
}

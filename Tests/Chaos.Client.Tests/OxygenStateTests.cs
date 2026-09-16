using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class OxygenStateTests
{
    [Test]
    public void StartsWithNoMeter()
    {
        var oxygen = new OxygenState();

        oxygen.HasValue.Should().BeFalse();
        oxygen.Amount.Should().Be(0);
    }

    [Test]
    public void AReadingShowsTheBar()
    {
        var oxygen = new OxygenState();

        oxygen.Set(64);

        oxygen.HasValue.Should().BeTrue();
        oxygen.Amount.Should().Be(64);
    }

    /// <summary>
    ///     Zero is the drowning warning, not an ending -- it has to leave the bar up and empty.
    /// </summary>
    [Test]
    public void ZeroShowsAnEmptyBarRatherThanHiding()
    {
        var oxygen = new OxygenState();
        oxygen.Set(40);

        oxygen.Set(0);

        oxygen.HasValue.Should().BeTrue();
        oxygen.Amount.Should().Be(0);
    }

    /// <summary>
    ///     Surfacing is reported above the range a meter can hold, which is the only thing that takes the bar down.
    /// </summary>
    [Test]
    public void AReadingAboveTheRangeTakesTheBarDown()
    {
        var oxygen = new OxygenState();
        oxygen.Set(40);

        oxygen.Set(byte.MaxValue);

        oxygen.HasValue.Should().BeFalse();
        oxygen.Amount.Should().Be(0);
    }

    [Test]
    public void ResetTakesTheBarDown()
    {
        var oxygen = new OxygenState();
        oxygen.Set(100);

        oxygen.Reset();

        oxygen.HasValue.Should().BeFalse();
        oxygen.Amount.Should().Be(0);
    }

    /// <summary>
    ///     The meter travels with the player, so a reading is only ever replaced by another reading -- nothing about
    ///     changing maps goes through here.
    /// </summary>
    [Test]
    public void AReadingIsHeldUntilTheNextOne()
    {
        var oxygen = new OxygenState();

        oxygen.Set(80);
        oxygen.Set(79);

        oxygen.Amount.Should().Be(79);
        oxygen.HasValue.Should().BeTrue();
    }
}

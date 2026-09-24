using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class BugReportUploadTests
{
    [Test]
    public void AnExactMultipleSplitsIntoFullParts()
    {
        var picture = Enumerable.Range(0, 65_536).Select(i => (byte)i).ToArray();

        var parts = BugReportUpload.SplitPicture(picture);

        parts.Should().HaveCount(2);
        parts.Should().OnlyContain(part => part.Length == BugReportProtocol.PART_SIZE);
        parts.SelectMany(part => part).Should().Equal(picture);
    }

    [Test]
    public void ARemainderGoesInTheLastPart()
    {
        var picture = Enumerable.Range(0, 70_000).Select(i => (byte)(i * 7)).ToArray();

        var parts = BugReportUpload.SplitPicture(picture);

        parts.Select(part => part.Length).Should().Equal(32_768, 32_768, 4_464);
        parts.SelectMany(part => part).Should().Equal(picture);
    }

    [Test]
    public void AnEmptyPictureHasNoParts() => BugReportUpload.SplitPicture([]).Should().BeEmpty();

    [Test]
    public void SendNeedsACategory() => BugReportUpload.CanSend(null, "0123456789").Should().BeFalse();

    [Test]
    public void SendNeedsTenTrimmedCharacters()
    {
        BugReportUpload.CanSend(BugReportCategory.Item, "0123456789").Should().BeTrue();
        BugReportUpload.CanSend(BugReportCategory.Item, "   012345678   ").Should().BeFalse();
    }

    [Test]
    public void SendAllowsAtMostAThousandCharacters()
    {
        BugReportUpload.CanSend(BugReportCategory.Item, new string('a', 1000)).Should().BeTrue();
        BugReportUpload.CanSend(BugReportCategory.Item, new string('a', 1001)).Should().BeFalse();
    }

    [Test]
    public void ClipLimitsDetailsToSixtyFourCharacters()
    {
        BugReportUpload.Clip(new string('x', 100)).Should().HaveLength(64);
        BugReportUpload.Clip("Windows 11").Should().Be("Windows 11");
    }
}

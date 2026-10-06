using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PictureFetchRetryTests
{
    private static readonly DateTime Sent = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_fetch_is_resent_every_thirty_seconds()
    {
        FetchRetry.ShouldResend(Sent, 1, Sent.AddSeconds(29)).Should().BeFalse();
        FetchRetry.ShouldResend(Sent, 1, Sent.AddSeconds(30)).Should().BeTrue();
        FetchRetry.ShouldResend(Sent, 5, Sent.AddSeconds(30)).Should().BeTrue();
    }

    [Test]
    public void A_fetch_stops_after_its_last_attempt()
    {
        FetchRetry.ShouldResend(Sent, FetchRetry.MAX_ATTEMPTS - 1, Sent.AddMinutes(5)).Should().BeTrue();
        FetchRetry.ShouldResend(Sent, FetchRetry.MAX_ATTEMPTS, Sent.AddMinutes(5)).Should().BeFalse();
        FetchRetry.HasGivenUp(Sent, FetchRetry.MAX_ATTEMPTS, Sent.AddSeconds(29)).Should().BeFalse();
        FetchRetry.HasGivenUp(Sent, FetchRetry.MAX_ATTEMPTS, Sent.AddSeconds(30)).Should().BeTrue();
    }
}

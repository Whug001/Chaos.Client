using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PictureFetchRetryTests
{
    private static readonly DateTime Sent = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_fetch_is_resent_once_after_thirty_seconds()
    {
        FetchRetry.ShouldResend(Sent, false, Sent.AddSeconds(29)).Should().BeFalse();
        FetchRetry.ShouldResend(Sent, false, Sent.AddSeconds(30)).Should().BeTrue();
        FetchRetry.ShouldResend(Sent, true, Sent.AddMinutes(5)).Should().BeFalse();
    }
}

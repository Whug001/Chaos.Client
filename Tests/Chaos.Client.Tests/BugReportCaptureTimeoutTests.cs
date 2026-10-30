using FluentAssertions;

namespace Chaos.Client.Tests;

public class BugReportCaptureTimeoutTests
{
    [Test]
    public void CaptureTimesOutAfterOneSecond()
    {
        ChaosGame.CaptureTimedOut(0, ChaosGame.CAPTURE_TIMEOUT_MS - 1).Should().BeFalse();
        ChaosGame.CaptureTimedOut(0, ChaosGame.CAPTURE_TIMEOUT_MS).Should().BeTrue();
        ChaosGame.CaptureTimedOut(0, ChaosGame.CAPTURE_TIMEOUT_MS + 5000).Should().BeTrue();
    }

    [Test]
    public void FreshCaptureDoesNotTimeOut()
    {
        ChaosGame.CaptureTimedOut(1000, 1000).Should().BeFalse();
        ChaosGame.CaptureTimedOut(1000, 1500).Should().BeFalse();
    }
}

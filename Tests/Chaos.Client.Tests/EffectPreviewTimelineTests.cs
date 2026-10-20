using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class EffectPreviewTimelineTests
{
    private static readonly EffectClip Login = new(390, 10, 100);
    private static readonly EffectClip Logout = new(391, 8, 100);
    private static readonly EffectClip Death = new(409, 12, 80);
    private static readonly EffectClip Missing = new(999, 0, 0);

    [Test]
    public async Task Login_logout_loop()
    {
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 0).Should().Be(new PreviewFrame(false, 390, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 479).Should().Be(new PreviewFrame(false, 390, 4));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 480).Should().Be(new PreviewFrame(true, 390, 4));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 1000).Should().Be(new PreviewFrame(true, 0, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 2200).Should().Be(new PreviewFrame(true, 391, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 2680).Should().Be(new PreviewFrame(false, 391, 4));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 3000).Should().Be(new PreviewFrame(false, 0, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, 3600).Should().Be(new PreviewFrame(false, 390, 0));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Death_loop()
    {
        //hold 1200, effect 960, gap 600: loop 2760
        EffectPreviewTimeline.Death(Death, 595, 0).Should().Be(new PreviewFrame(true, 0, 0));
        EffectPreviewTimeline.Death(Death, 595, 1200).Should().Be(new PreviewFrame(true, 409, 0));
        EffectPreviewTimeline.Death(Death, 595, 1794).Should().Be(new PreviewFrame(true, 409, 7));
        EffectPreviewTimeline.Death(Death, 595, 1795).Should().Be(new PreviewFrame(false, 409, 7));
        EffectPreviewTimeline.Death(Death, 595, 2160).Should().Be(new PreviewFrame(false, 0, 0));
        EffectPreviewTimeline.Death(Death, 595, 2760).Should().Be(new PreviewFrame(true, 0, 0));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Missing_effect_shows_no_frame()
    {
        for (var t = 0; t < 5000; t += 50)
        {
            //both missing: hold 1200, gap 600: loop 1800
            var loginLogout = EffectPreviewTimeline.LoginLogout(Missing, Missing, 480, t);
            loginLogout.EffectId.Should().Be(0);
            loginLogout.EffectFrame.Should().Be(0);
            loginLogout.ShowFigure.Should().Be((t % 1800) < 1200);

            var death = EffectPreviewTimeline.Death(Missing, 595, t);
            death.EffectId.Should().Be(0);
            death.EffectFrame.Should().Be(0);
            death.ShowFigure.Should().Be((t % 1800) < 1200);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Still_frame_is_the_frame_at_the_key_time()
    {
        EffectPreviewTimeline.StillFrame(Login, 480).Should().Be(4);
        EffectPreviewTimeline.StillFrame(Login, 5000).Should().Be(9);
        EffectPreviewTimeline.StillFrame(Missing, 480).Should().Be(-1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Zero_reveal_shows_and_hides_the_figure_at_once()
    {
        EffectPreviewTimeline.LoginLogout(Login, Logout, 0, 0).Should().Be(new PreviewFrame(true, 390, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 0, 999).Should().Be(new PreviewFrame(true, 390, 9));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 0, 2200).Should().Be(new PreviewFrame(false, 391, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 0, 2680).Should().Be(new PreviewFrame(false, 391, 4));
        EffectPreviewTimeline.Death(Death, 0, 0).Should().Be(new PreviewFrame(true, 0, 0));
        EffectPreviewTimeline.Death(Death, 0, 1200).Should().Be(new PreviewFrame(false, 409, 0));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reveal_past_the_clip_never_shows_or_never_hides()
    {
        EffectPreviewTimeline.LoginLogout(Login, Logout, 5000, 0).Should().Be(new PreviewFrame(false, 390, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 5000, 999).Should().Be(new PreviewFrame(false, 390, 9));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 5000, 2200).Should().Be(new PreviewFrame(true, 391, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 5000, 2999).Should().Be(new PreviewFrame(true, 391, 7));
        EffectPreviewTimeline.Death(Death, 5000, 1200).Should().Be(new PreviewFrame(true, 409, 0));
        EffectPreviewTimeline.Death(Death, 5000, 2159).Should().Be(new PreviewFrame(true, 409, 11));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Missing_effect_on_one_side_only()
    {
        //login missing: hold 1200, logout 800, gap 600: loop 2600
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 0).Should().Be(new PreviewFrame(true, 0, 0));
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 1200).Should().Be(new PreviewFrame(true, 391, 0));
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 1679).Should().Be(new PreviewFrame(true, 391, 4));
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 1680).Should().Be(new PreviewFrame(false, 391, 4));
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 2000).Should().Be(new PreviewFrame(false, 0, 0));
        EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, 2600).Should().Be(new PreviewFrame(true, 0, 0));

        //logout missing: login 1000, hold 1200, gap 600: loop 2800
        EffectPreviewTimeline.LoginLogout(Login, Missing, 480, 0).Should().Be(new PreviewFrame(false, 390, 0));
        EffectPreviewTimeline.LoginLogout(Login, Missing, 480, 480).Should().Be(new PreviewFrame(true, 390, 4));
        EffectPreviewTimeline.LoginLogout(Login, Missing, 480, 1000).Should().Be(new PreviewFrame(true, 0, 0));
        EffectPreviewTimeline.LoginLogout(Login, Missing, 480, 2200).Should().Be(new PreviewFrame(false, 0, 0));
        EffectPreviewTimeline.LoginLogout(Login, Missing, 480, 2800).Should().Be(new PreviewFrame(false, 390, 0));

        for (var t = 0; t < 5600; t += 50)
        {
            EffectPreviewTimeline.LoginLogout(Missing, Logout, 480, t).EffectId.Should().NotBe(999);
            EffectPreviewTimeline.LoginLogout(Login, Missing, 480, t).EffectId.Should().NotBe(999);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Negative_time_stays_in_the_opening_phase()
    {
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, -1).Should().Be(new PreviewFrame(false, 390, 0));
        EffectPreviewTimeline.LoginLogout(Login, Logout, 480, -3600).Should().Be(new PreviewFrame(false, 390, 0));
        EffectPreviewTimeline.Death(Death, 595, -1).Should().Be(new PreviewFrame(true, 0, 0));

        await Task.CompletedTask;
    }
}

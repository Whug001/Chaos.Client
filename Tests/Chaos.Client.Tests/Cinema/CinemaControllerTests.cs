using Chaos.Client.Systems.Cinema;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Tests.Cinema;

public class CinemaControllerTests
{
    private sealed class FakeVideo : ICinemaVideo
    {
        public bool ThrowOnOpen { get; init; }
        public bool Failed { get; set; }
        public bool Playing { get; set; }
        public bool Ended { get; set; }
        public long TimeMs { get; set; }
        public Texture2D? Frame => null;
        public string? OpenedUrl { get; private set; }
        public long OpenedAtMs { get; private set; }
        public List<long> Seeks { get; } = [];
        public bool? LastPaused { get; private set; }
        public (int Volume, bool Mute)? LastVolume { get; private set; }
        public int Uploads { get; private set; }
        public bool Disposed { get; private set; }
        public bool VolumeSetBeforeOpen { get; private set; }

        public void Open(string url, long startMs)
        {
            if (ThrowOnOpen)
                throw new InvalidOperationException("no vlc");

            VolumeSetBeforeOpen = LastVolume is not null;
            OpenedUrl = url;
            OpenedAtMs = startMs;
        }

        public void Seek(long positionMs) => Seeks.Add(positionMs);
        public void SetPaused(bool paused) => LastPaused = paused;
        public void SetVolume(int volume, bool mute) => LastVolume = (volume, mute);
        public void UploadFrame() => Uploads++;
        public void Dispose() => Disposed = true;
    }

    private readonly List<FakeVideo> Videos = [];
    private bool NextThrows;

    private CinemaController MakeWithReady(Func<bool> ready)
        => new(() =>
        {
            var video = new FakeVideo { ThrowOnOpen = NextThrows };
            Videos.Add(video);

            return video;
        }, ready);

    private CinemaController Make()
        => new(() =>
        {
            var video = new FakeVideo { ThrowOnOpen = NextThrows };
            Videos.Add(video);

            return video;
        });

    private static CinemaStateArgs Film(uint positionMs = 60_000, bool paused = false, string id = "nosferatu")
        => new()
        {
            FilmId = id,
            Title = "Nosferatu",
            Url = $"https://x/films/{id}/index.m3u8",
            PositionMs = positionMs,
            Paused = paused
        };

    [Test]
    public void New_film_opens_at_server_position_plus_half_ping()
    {
        var c = Make();
        c.SetVolume(60, false);

        c.Apply(Film(), 1000, 200);

        Videos.Should().ContainSingle();
        Videos[0].OpenedUrl.Should().Be("https://x/films/nosferatu/index.m3u8");
        Videos[0].OpenedAtMs.Should().Be(60_100);
        Videos[0].VolumeSetBeforeOpen.Should().BeTrue();
        Videos[0].LastVolume.Should().Be((60, false));
        c.Status.Should().Be(CinemaStatus.Playing);
        c.IsShowing.Should().BeTrue();
    }

    [Test]
    public void Paused_film_opens_paused_without_ping()
    {
        var c = Make();

        c.Apply(Film(paused: true), 1000, 200);

        Videos[0].OpenedAtMs.Should().Be(60_000);
        Videos[0].LastPaused.Should().BeTrue();
    }

    [Test]
    public void Empty_url_stops()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.Apply(new CinemaStateArgs(), 10, null);

        Videos[0].Disposed.Should().BeTrue();
        c.Status.Should().Be(CinemaStatus.Idle);
        c.IsShowing.Should().BeFalse();
        c.Video.Should().BeNull();
    }

    [Test]
    public void Same_film_seeks_instead_of_reopening()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.Apply(Film(positionMs: 70_000, paused: true), 10_000, null);

        Videos.Should().ContainSingle();
        Videos[0].Seeks.Should().Equal(70_000);
        Videos[0].LastPaused.Should().BeTrue();
    }

    [Test]
    public void Pause_then_seek_stays_paused_at_new_spot()
    {
        var c = Make();
        c.Apply(Film(), 0, null);
        c.Apply(Film(positionMs: 10_000, paused: true), 5_000, null);

        c.Apply(Film(positionMs: 1_800_000, paused: true), 6_000, null);

        Videos[0].Seeks.Last().Should().Be(1_800_000);
        Videos[0].LastPaused.Should().BeTrue();
        c.ExpectedPositionMs(60_000).Should().Be(1_800_000);
    }

    [Test]
    public void Drift_past_threshold_seeks_at_the_check()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 1_000;

        c.Update(4_999);
        video.Seeks.Should().BeEmpty();

        c.Update(5_000);
        video.Seeks.Should().Equal(5_000);
    }

    [Test]
    public void Small_drift_does_not_seek()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 4_000;

        c.Update(5_000);

        video.Seeks.Should().BeEmpty();
    }

    [Test]
    public void Buffering_never_seeks()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = false;
        video.TimeMs = 0;

        for (long now = 0; now <= 60_000; now += 100)
            c.Update(now);

        video.Seeks.Should().BeEmpty();
        video.Uploads.Should().BeGreaterThan(0);
    }

    [Test]
    public void Paused_never_drift_seeks()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0, paused: true), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 90_000;

        c.Update(20_000);

        video.Seeks.Should().BeEmpty();
    }

    [Test]
    public void Failed_video_retries_after_fifteen_seconds()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        Videos[0].Failed = true;

        c.Update(1_000);

        Videos[0].Disposed.Should().BeTrue();
        c.Status.Should().Be(CinemaStatus.Unavailable);
        c.Video.Should().BeNull();

        c.Update(15_999);
        Videos.Should().HaveCount(1);

        c.Update(16_000);
        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(16_000);
        c.Status.Should().Be(CinemaStatus.Playing);
    }

    [Test]
    public void Open_exception_marks_unavailable()
    {
        var c = Make();
        NextThrows = true;

        c.Apply(Film(), 0, null);

        c.Status.Should().Be(CinemaStatus.Unavailable);
        c.Video.Should().BeNull();
        Videos[0].Disposed.Should().BeTrue();
    }

    [Test]
    public void Same_film_while_unavailable_waits_for_retry()
    {
        var c = Make();
        NextThrows = true;
        c.Apply(Film(), 0, null);
        NextThrows = false;

        c.Apply(Film(positionMs: 70_000), 1_000, null);

        Videos.Should().HaveCount(1);
        c.Status.Should().Be(CinemaStatus.Unavailable);

        c.Update(15_000);

        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(84_000);
    }

    [Test]
    public void Different_film_while_unavailable_opens_at_once()
    {
        var c = Make();
        NextThrows = true;
        c.Apply(Film(), 0, null);
        NextThrows = false;

        c.Apply(Film(id: "metropolis", positionMs: 0), 1_000, null);

        Videos.Should().HaveCount(2);
        c.Status.Should().Be(CinemaStatus.Playing);
    }

    [Test]
    public void Hiding_disposes_and_showing_reopens_at_expected_position()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);

        c.SetShowVideo(false, 1_000);

        Videos[0].Disposed.Should().BeTrue();
        c.Video.Should().BeNull();
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.SetShowVideo(true, 10_000);

        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(10_000);
    }

    [Test]
    public void Hidden_video_does_not_open_new_films()
    {
        var c = Make();
        c.SetShowVideo(false, 0);

        c.Apply(Film(), 0, null);

        Videos.Should().BeEmpty();
        c.IsShowing.Should().BeTrue();
    }

    [Test]
    public void Duck_music_only_while_a_shown_film_plays()
    {
        var c = Make();
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.Apply(Film(), 0, null);
        c.ShouldDuckMusic(true).Should().BeTrue();
        c.ShouldDuckMusic(false).Should().BeFalse();

        c.Apply(Film(paused: true), 1_000, null);
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.Apply(Film(), 2_000, null);
        Videos[0].Failed = true;
        c.Update(3_000);
        c.ShouldDuckMusic(true).Should().BeFalse();
    }

    [Test]
    public void Volume_reaches_the_live_video()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.SetVolume(30, true);

        Videos[0].LastVolume.Should().Be((30, true));
    }

    [Test]
    public void Ended_past_the_grace_time_marks_unavailable_then_retries()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Ended = true;

        c.Update(1_000);
        c.Status.Should().Be(CinemaStatus.Playing);
        Videos.Should().ContainSingle();

        c.Update(3_000);
        c.Status.Should().Be(CinemaStatus.Unavailable);
        video.Disposed.Should().BeTrue();
        c.Video.Should().BeNull();

        c.Update(17_999);
        Videos.Should().ContainSingle();

        c.Update(18_000);
        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(18_000);
        c.Status.Should().Be(CinemaStatus.Playing);
    }

    [Test]
    public void Ended_inside_the_grace_time_keeps_playing()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        Videos[0].Ended = true;

        c.Update(1_000);
        c.Update(2_999);

        c.Status.Should().Be(CinemaStatus.Playing);
        Videos.Should().ContainSingle();
        Videos[0].Uploads.Should().BeGreaterThan(0);
    }

    [Test]
    public void Ended_clearing_again_resets_the_grace_timer()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];

        video.Ended = true;
        c.Update(10_000);

        video.Ended = false;
        c.Update(11_000);

        video.Ended = true;
        c.Update(11_500);

        c.Update(13_000);

        c.Status.Should().Be(CinemaStatus.Playing);
        Videos.Should().ContainSingle();
    }

    [Test]
    public void Same_film_apply_while_ended_reopens_instead_of_seeking()
    {
        var c = Make();
        c.Apply(Film(), 0, null);
        var old = Videos[0];
        old.Ended = true;

        c.Apply(Film(positionMs: 70_000), 10_000, null);

        Videos.Should().HaveCount(2);
        old.Disposed.Should().BeTrue();
        old.Seeks.Should().BeEmpty();
        Videos[1].OpenedAtMs.Should().Be(70_000);
        c.Video.Should().BeSameAs(Videos[1]);
    }

    [Test]
    public void Drift_seek_skips_one_check_then_seeks_again()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 1_000;

        c.Update(5_000);
        video.Seeks.Should().Equal(5_000);

        c.Update(10_000);
        video.Seeks.Should().HaveCount(1);

        c.Update(15_000);
        video.Seeks.Should().HaveCount(2);
        video.Seeks.Last().Should().Be(15_000);
    }

    [Test]
    public void Apply_seek_skips_the_next_drift_check()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 1_000;

        c.Apply(Film(positionMs: 70_000), 10_000, null);
        video.Seeks.Should().Equal(70_000);

        c.Update(15_000);
        video.Seeks.Should().HaveCount(1);

        c.Update(20_000);
        video.Seeks.Should().HaveCount(2);
        video.Seeks.Last().Should().Be(80_000);
    }

    [Test]
    public void Apply_while_video_not_ready_waits_without_creating_video()
    {
        var ready = false;
        var c = MakeWithReady(() => ready);

        c.Apply(Film(), 1000, null);

        Videos.Should().BeEmpty();
        c.Status.Should().Be(CinemaStatus.Playing);
        c.IsShowing.Should().BeTrue();
    }

    [Test]
    public void Becoming_ready_opens_at_the_expected_position()
    {
        var ready = false;
        var c = MakeWithReady(() => ready);

        c.Apply(Film(positionMs: 10_000), 1000, null);
        Videos.Should().BeEmpty();

        ready = true;
        c.Update(4000);

        Videos.Should().ContainSingle();
        Videos[0].OpenedAtMs.Should().Be(13_000);
    }

    [Test]
    public void Paused_film_waits_then_opens_paused_at_its_position()
    {
        var ready = false;
        var c = MakeWithReady(() => ready);

        c.Apply(Film(positionMs: 10_000, paused: true), 1000, null);

        ready = true;
        c.Update(4000);

        Videos.Should().ContainSingle();
        Videos[0].OpenedAtMs.Should().Be(10_000);
        Videos[0].LastPaused.Should().BeTrue();
    }

    [Test]
    public void Stop_while_waiting_cancels_the_pending_open()
    {
        var ready = false;
        var c = MakeWithReady(() => ready);

        c.Apply(Film(), 1000, null);
        c.Stop();

        ready = true;
        c.Update(4000);

        Videos.Should().BeEmpty();
    }

    [Test]
    public void Hide_video_while_waiting_cancels_the_pending_open()
    {
        var ready = false;
        var c = MakeWithReady(() => ready);

        c.Apply(Film(), 1000, null);
        c.SetShowVideo(false, 2000);

        ready = true;
        c.Update(4000);

        Videos.Should().BeEmpty();
    }

    [Test]
    public void Ready_from_the_start_opens_at_apply()
    {
        var c = MakeWithReady(() => true);

        c.Apply(Film(), 1000, 200);

        Videos.Should().ContainSingle();
        Videos[0].OpenedAtMs.Should().Be(60_100);
        c.Status.Should().Be(CinemaStatus.Playing);
    }
}

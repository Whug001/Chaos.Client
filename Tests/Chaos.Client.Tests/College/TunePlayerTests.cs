using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TunePlayerTests
{
    private readonly List<string> Notices = [];
    private readonly FakeOutput Output = new();
    private readonly List<object> Stopped = [];
    private double Now;
    private int NoteRenders;
    private int TuneRenders;

    private static readonly TuneData Steady = TuneData.Empty with { Speed = TuneSpeed.Steady };

    private TunePlayer Player(Func<TuneData, short[]>? render = null)
    {
        var player = new TunePlayer(
            Output,
            () => Now,
            (work, _) => Task.FromResult(work()),
            render is null
                ? (_, _) =>
                {
                    TuneRenders++;

                    return new short[4];
                }
                : (tune, _) => render(tune),
            (_, _, _, _) =>
            {
                NoteRenders++;

                return new short[4];
            },
            Notices.Add);

        player.Stopped += Stopped.Add;

        return player;
    }

    [Test]
    public void A_tune_renders_then_plays()
    {
        var player = Player();
        var owner = new object();

        player.Play(Steady, owner);

        player.IsRendering.Should().BeTrue();
        player.IsPlaying.Should().BeFalse();
        player.Owner.Should().BeSameAs(owner);

        player.Update();

        player.IsRendering.Should().BeFalse();
        player.IsPlaying.Should().BeTrue();
        Output.Calls.Should().Equal("tune");
    }

    [Test]
    public void The_playhead_follows_the_clock_from_the_start_column()
    {
        var player = Player();
        player.Play(Steady, new object(), 8);
        player.Update();

        Now = 1;

        player.Column.Should().BeApproximately(12, 1e-9);

        Now = 100;

        player.Column.Should().Be(CollegeProtocol.TUNE_STEPS);
    }

    [Test]
    public void Without_a_loop_the_tune_stops_when_the_sound_ends()
    {
        var player = Player();
        var owner = new object();
        player.Play(Steady, owner);
        player.Update();

        Output.IsTunePlaying = false;
        player.Update();

        player.Owner.Should().BeNull();
        player.Column.Should().BeNull();
        Stopped.Should().Equal(owner);
    }

    [Test]
    public void Playing_another_tune_stops_the_first()
    {
        var player = Player();
        var first = new object();
        var second = new object();
        player.Play(Steady, first);
        player.Update();

        player.Play(Steady, second);

        Stopped.Should().Equal(first);
        Output.Calls.Should().Equal("tune", "hold");
        player.Owner.Should().BeSameAs(second);
    }

    [Test]
    public void Stopping_lets_the_music_back()
    {
        var player = Player();
        player.Play(Steady, new object());
        player.Update();

        player.Stop();

        Output.Calls.Should().Equal("tune", "stop");
    }

    [Test]
    public void With_sound_effects_muted_a_tune_does_not_start_and_the_player_is_told_once()
    {
        var player = Player();
        Output.IsMuted = true;

        player.Play(Steady, new object());
        player.Update();
        player.Play(Steady, new object());

        player.Owner.Should().BeNull();
        player.IsRendering.Should().BeFalse();
        TuneRenders.Should().Be(0);
        Output.Calls.Should().BeEmpty();
        Notices.Should().Equal(TunePlayer.MUTED_NOTICE);
    }

    [Test]
    public void The_muted_notice_comes_again_after_sound_was_turned_back_up()
    {
        var player = Player();
        Output.IsMuted = true;
        player.Play(Steady, new object());

        Output.IsMuted = false;
        player.Play(Steady, new object());
        player.Update();
        player.Stop();

        Output.IsMuted = true;
        player.Play(Steady, new object());

        Notices.Should().Equal(TunePlayer.MUTED_NOTICE, TunePlayer.MUTED_NOTICE);
    }

    [Test]
    public void Muting_sound_effects_while_a_tune_plays_stops_it()
    {
        var player = Player();
        var owner = new object();
        player.Play(Steady, owner);
        player.Update();

        Output.IsMuted = true;
        player.Update();

        player.Owner.Should().BeNull();
        Stopped.Should().Equal(owner);
        Output.Calls.Should().Equal("tune", "stop");
        Notices.Should().Equal(TunePlayer.MUTED_NOTICE);
    }

    [Test]
    public void Stop_if_owner_ignores_other_owners()
    {
        var player = Player();
        var owner = new object();
        player.Play(Steady, owner);
        player.Update();

        player.StopIfOwner(new object());
        player.IsPlaying.Should().BeTrue();

        player.StopIfOwner(owner);
        player.IsPlaying.Should().BeFalse();
        Stopped.Should().Equal(owner);
    }

    [Test]
    public void A_loop_renders_the_next_pass_and_starts_it_at_column_64()
    {
        var player = Player();
        var passes = 0;
        var end = CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(TuneSpeed.Steady);

        player.Play(
            Steady,
            new object(),
            loop: () =>
            {
                passes++;

                return Steady;
            });

        player.Update();

        passes.Should().Be(0);
        TuneRenders.Should().Be(1);

        Now = end - 0.5;
        player.Update();
        player.Update();

        passes.Should().Be(1);
        TuneRenders.Should().Be(2);
        Output.Calls.Should().Equal("tune");

        Now = end;
        player.Update();

        Output.Calls.Should().Equal("tune", "tune");
        player.Column.Should().BeApproximately(0, 1e-9);
        passes.Should().Be(1);
    }

    [Test]
    public void An_edit_during_a_pass_is_heard_on_the_next_pass()
    {
        var edited = Steady with { Speed = TuneSpeed.Quick };
        var source = Steady;
        var rendered = new List<TuneData>();
        var player = Player(
            tune =>
            {
                rendered.Add(tune);

                return new short[4];
            });

        player.Play(Steady, new object(), loop: () => source);
        player.Update();

        source = edited;
        Now = CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(TuneSpeed.Steady);
        player.Update();

        rendered.Should().Equal(Steady, edited);
        Output.Calls.Should().Equal("tune", "tune");
    }

    [Test]
    public void A_loop_that_returns_null_ends_after_the_current_pass()
    {
        var player = Player();
        var looping = true;

        player.Play(Steady, new object(), loop: () => looping ? Steady : null);
        player.Update();

        looping = false;
        Now = CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(TuneSpeed.Steady);
        player.Update();

        Output.Calls.Should().Equal("tune");
        player.IsPlaying.Should().BeTrue();

        Output.IsTunePlaying = false;
        player.Update();

        player.IsPlaying.Should().BeFalse();
    }

    [Test]
    public void A_failed_render_stops_the_player()
    {
        var owner = new object();
        var player = new TunePlayer(
            Output,
            () => Now,
            (_, _) => Task.FromException<byte[]>(new InvalidOperationException()),
            (_, _) => [],
            (_, _, _, _) => []);
        player.Stopped += Stopped.Add;

        player.Play(Steady, owner);
        player.Update();

        player.Owner.Should().BeNull();
        Stopped.Should().Equal(owner);
        Output.Calls.Should().Equal("stop");
    }

    [Test]
    public void Stopping_cancels_the_render_and_it_never_plays()
    {
        var pending = new List<(TaskCompletionSource<byte[]> Source, CancellationToken Token, Func<byte[]> Work)>();
        var player = new TunePlayer(
            Output,
            () => Now,
            (work, token) =>
            {
                var source = new TaskCompletionSource<byte[]>();
                pending.Add((source, token, work));

                return source.Task;
            },
            (_, _) => new short[4],
            (_, _, _, _) => []);

        player.Play(Steady, new object());
        player.Stop();

        pending[0].Token.IsCancellationRequested.Should().BeTrue();

        pending[0].Source.SetResult(pending[0].Work());
        player.Update();

        Output.Calls.Should().NotContain("tune");

        player.Play(Steady, new object());
        player.Play(Steady, new object());

        pending[1].Token.IsCancellationRequested.Should().BeTrue();
        pending[2].Token.IsCancellationRequested.Should().BeFalse();

        pending[1].Source.SetResult(pending[1].Work());
        player.Update();

        Output.Calls.Should().NotContain("tune");
    }

    [Test]
    public void Previews_are_rendered_once_per_note()
    {
        var player = Player();

        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Bass, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Bells, TuneLayer.Bass, 3);

        NoteRenders.Should().Be(2);
        Output.Calls.Should().Equal("preview", "preview", "preview", "preview");
    }

    [Test]
    public void Previews_wait_while_a_tune_plays()
    {
        var player = Player();
        player.Play(Steady, new object());
        player.Update();

        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);

        Output.Calls.Should().Equal("tune");
    }

    private sealed class FakeOutput : ITuneOutput
    {
        public List<string> Calls { get; } = [];
        public bool IsTunePlaying { get; set; }
        public bool IsMuted { get; set; }

        public bool PlayTune(byte[] wav)
        {
            Calls.Add("tune");
            IsTunePlaying = true;

            return true;
        }

        public bool PlayPreview(byte[] wav)
        {
            Calls.Add("preview");

            return true;
        }

        public void StopTune(bool holdMusicDown)
        {
            Calls.Add(holdMusicDown ? "hold" : "stop");
            IsTunePlaying = false;
        }
    }
}

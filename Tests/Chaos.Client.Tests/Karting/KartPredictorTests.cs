#region
using Chaos.Client.Systems.Karting;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartPredictorTests
{
    private static readonly KartStats Stats = new KartStats().Rounded();
    private static readonly TrackSurface Road = TrackSurface.FromRows(Enumerable.Repeat(new string('R', 200), 60).ToList());

    private static KartInput In(ushort sequence, KartInputFlags flags = KartInputFlags.Accelerate | KartInputFlags.Right) => new(sequence, 0, flags);

    private static (KartBody Body, KartModifiers Mods) Server(KartBody start, int count, ushort first = 0)
    {
        var body = start;
        var mods = new KartModifiers();

        for (var i = 0; i < count; i++)
            KartPhysics.Step(ref body, ref mods, In((ushort)(first + i)), Stats, Road);

        return (body, mods);
    }

    [Test]
    public void Agreeing_server_state_changes_nothing()
    {
        var start = new KartBody { X = 20.5, Y = 30.5 };
        var predictor = new KartPredictor(Stats, Road);
        predictor.Reset(start, new KartModifiers());

        for (ushort s = 0; s < 20; s++)
            predictor.Step(In(s));

        var before = predictor.DrawPosition;
        var (body, mods) = Server(start, 12);

        predictor.Correct(11, body, mods);

        predictor.DrawPosition.X.Should().BeApproximately(before.X, 1e-9);
        predictor.DrawPosition.Y.Should().BeApproximately(before.Y, 1e-9);
        predictor.Pending.Should().Be(8);
    }

    [Test]
    public void A_small_error_is_blended_out()
    {
        var start = new KartBody { X = 20.5, Y = 30.5 };
        var predictor = new KartPredictor(Stats, Road);
        predictor.Reset(start, new KartModifiers());

        for (ushort s = 0; s < 10; s++)
            predictor.Step(In(s));

        var before = predictor.DrawPosition;
        var (body, mods) = Server(start, 10);
        body.X += 0.5;

        predictor.Correct(9, body, mods);

        predictor.DrawPosition.X.Should().BeApproximately(before.X, 1e-9);
        predictor.Body.X.Should().BeApproximately(before.X + 0.5, 1e-9);

        predictor.Decay(0.1);
        predictor.DrawPosition.X.Should().BeApproximately(predictor.Body.X, 1e-9);
    }

    [Test]
    public void A_large_error_snaps()
    {
        var start = new KartBody { X = 20.5, Y = 30.5 };
        var predictor = new KartPredictor(Stats, Road);
        predictor.Reset(start, new KartModifiers());
        predictor.Step(In(0));
        var (body, mods) = Server(start, 1);
        body.X += 4;

        predictor.Correct(0, body, mods);

        predictor.DrawPosition.X.Should().BeApproximately(predictor.Body.X, 1e-9);
    }

    [Test]
    public void At_most_64_inputs_wait_for_an_ack()
    {
        var predictor = new KartPredictor(Stats, Road);
        predictor.Reset(new KartBody { X = 20.5, Y = 30.5 }, new KartModifiers());

        for (ushort s = 0; s < 100; s++)
            predictor.Step(In(s));

        predictor.Pending.Should().Be(64);
    }

    [Test]
    public void Acks_across_the_wrap_remove_the_right_inputs()
    {
        var predictor = new KartPredictor(Stats, Road);
        var start = new KartBody { X = 20.5, Y = 30.5 };
        predictor.Reset(start, new KartModifiers());

        foreach (ushort s in new ushort[] { 65533, 65534, 65535, 0, 1 })
            predictor.Step(In(s));

        var (body, mods) = Server(start, 3, 65533);
        predictor.Correct(65535, body, mods);

        predictor.Pending.Should().Be(2);
    }
}

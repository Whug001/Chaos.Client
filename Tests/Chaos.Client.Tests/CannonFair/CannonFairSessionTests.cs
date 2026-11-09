#region
using Chaos.CannonFair;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.CannonFair;

public sealed class CannonFairSessionTests
{
    private static VolleyResult Volley()
        => new(
            [new RecordedObject(0, ObjectKind.Block, -1, 1f, 1f, BlockMaterial.Wood)],
            [new RecordingFrame(0, [new ObjectPose(0, 1f, 1f, 0f, false)]), new RecordingFrame(1000, [new ObjectPose(0, 2f, 1f, 0f, false)])],
            [],
            [],
            false);

    private static CannonFairDisplayArgs Piece(int volley, byte index, byte count, byte[] data)
        => new() { Kind = CannonFairMessageKind.RecordingPiece, VolleyId = (ushort)volley, PieceIndex = index, PieceCount = count, Data = data };

    private static CannonFairDisplayArgs Start(int volley) => new() { Kind = CannonFairMessageKind.ReplayStart, VolleyId = (ushort)volley };

    [Test]
    public void A_finished_recording_is_confirmed_once()
    {
        var confirmed = new List<int>();
        var session = new CannonFairSession { Confirm = confirmed.Add };
        var bytes = RecordingCodec.Encode(Volley());

        session.Apply(Piece(3, 0, 1, bytes), 0);
        session.Apply(Piece(3, 0, 1, bytes), 0);

        confirmed.Should().Equal(3);
    }

    [Test]
    public void A_replay_starts_at_zero_when_the_recording_is_ready()
    {
        var session = new CannonFairSession();
        session.Apply(Piece(1, 0, 1, RecordingCodec.Encode(Volley())), 0);

        session.Apply(Start(1), 500);

        session.IsReplaying.Should().BeTrue();
        session.ReplayMs(800).Should().Be(300);
    }

    [Test]
    public void A_late_recording_joins_the_replay_in_progress()
    {
        var session = new CannonFairSession();
        session.Apply(Start(1), 1000);
        session.IsReplaying.Should().BeFalse();

        session.Apply(Piece(1, 0, 1, RecordingCodec.Encode(Volley())), 1400);

        session.IsReplaying.Should().BeTrue();
        session.ReplayMs(1400).Should().Be(400);
    }

    [Test]
    public void A_start_for_another_volley_does_not_play_the_wrong_recording()
    {
        var session = new CannonFairSession();
        session.Apply(Piece(1, 0, 1, RecordingCodec.Encode(Volley())), 0);

        session.Apply(Start(2), 100);

        session.IsReplaying.Should().BeFalse();
    }

    [Test]
    public void Bad_recording_bytes_are_ignored()
    {
        var confirmed = new List<int>();
        var session = new CannonFairSession { Confirm = confirmed.Add };

        var act = () => session.Apply(Piece(1, 0, 1, [1, 2, 3]), 0);

        act.Should().NotThrow();
        confirmed.Should().BeEmpty();
    }

    [Test]
    public void An_unknown_shot_value_falls_back_to_plain()
    {
        var session = new CannonFairSession();

        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Aiming, Shot = 77, Cannon = 1, AimMs = 15000 }, 0);

        session.Shot.Should().Be(ShotKind.Plain);
    }

    [Test]
    public void An_onlooker_cannot_aim()
    {
        var session = new CannonFairSession();
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = -1 }, 0);

        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Aiming, Shot = 255, Cannon = 0, AimMs = 15000 }, 0);

        session.CanAim.Should().BeFalse();
    }

    [Test]
    public void Locking_in_is_only_sent_once_per_volley()
    {
        var sent = 0;
        var session = new CannonFairSession { LockIn = (_, _) => sent++ };
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 0 }, 0);
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Aiming, Shot = 0, Cannon = 1, AimMs = 15000 }, 0);

        session.TryLockIn(45f, 0.5f);
        session.TryLockIn(45f, 0.5f);

        sent.Should().Be(1);
        session.Locked.Should().BeTrue();
    }

    [Test]
    public void The_own_place_is_known_outside_the_top_three()
    {
        var session = new CannonFairSession();
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 4 }, 0);

        session.Apply(new CannonFairDisplayArgs
        {
            Kind = CannonFairMessageKind.Standings,
            Standings = Enumerable.Range(0, 5).Select(i => new CannonFairStandingInfo((byte)i, $"P{i}", 100 - i, (byte)(i + 1), true)).ToList()
        }, 0);

        session.TopThree.Should().HaveCount(3);
        session.Own!.Place.Should().Be(5);
    }

    [Test]
    public void Clear_forgets_the_match()
    {
        var session = new CannonFairSession();
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 0 }, 0);

        session.Clear();

        session.IsOpen.Should().BeFalse();
    }

    [Test]
    public void A_close_message_forgets_the_match()
    {
        var session = new CannonFairSession();
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 0 }, 0);

        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Close }, 0);

        session.IsOpen.Should().BeFalse();
    }

    [Test]
    public void Aiming_replaces_the_pieces_from_open()
    {
        var session = new CannonFairSession();
        var openPiece = new CannonFairPieceInfo(1, 0, 0, 0, 0, 10, 10);
        var aimingPiece = new CannonFairPieceInfo(1, 0, 5, 5, 0, 10, 10);
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 0, Pieces = [openPiece] }, 0);

        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Aiming, Shot = 0, Cannon = 1, AimMs = 15000, Pieces = [aimingPiece] }, 0);

        session.Pieces.Should().Equal(aimingPiece);
    }
}

using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MirrorStateTests
{
    private static MirrorLayoutArgs Layout()
        => new()
        {
            Segments =
            [
                new MirrorSegmentInfo { Id = "n", X = 2, Y = 5, Side = MirrorSide.North, Length = 3, Style = MirrorStyle.Glass },
                new MirrorSegmentInfo { Id = "w", X = 4, Y = 5, Side = MirrorSide.West, Length = 2, Style = MirrorStyle.Haunted }
            ],
            DarkStretches = [new MirrorStretchInfo { X = 10, Y = 20, Width = 4, Height = 2 }]
        };

    [Test]
    public async Task Apply_indexes_faces_by_wall_tile()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.FacesAt(2, 5)
             .Should()
             .Equal(new MirrorFace(0, 0, MirrorSide.North));

        state.FacesAt(3, 5)
             .Should()
             .Equal(new MirrorFace(0, 1, MirrorSide.North));

        //(4, 5) is the last north face and the first west face
        state.FacesAt(4, 5)
             .Should()
             .BeEquivalentTo(new[] { new MirrorFace(0, 2, MirrorSide.North), new MirrorFace(1, 0, MirrorSide.West) });

        state.FacesAt(4, 6)
             .Should()
             .Equal(new MirrorFace(1, 1, MirrorSide.West));

        state.FacesAt(5, 5)
             .Should()
             .BeEmpty();

        state.HasMirrors
             .Should()
             .BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_empties_everything()
    {
        var state = new MirrorState();
        state.Apply(Layout());
        state.AddDouble(7, 1, 1000);

        state.Clear();

        state.HasMirrors
             .Should()
             .BeFalse();

        state.FacesAt(2, 5)
             .Should()
             .BeEmpty();

        state.Doubles
             .Should()
             .BeEmpty();

        state.DarkStretches
             .Should()
             .BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Doubles_are_checked_replaced_and_pruned()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.AddDouble(7, 5, 1000);

        state.Doubles
             .Should()
             .BeEmpty();

        state.AddDouble(7, 1, 1000);
        state.AddDouble(7, 1, 2000);
        state.AddDouble(8, 1, 1500);

        state.Doubles
             .Should()
             .HaveCount(2)
             .And
             .Contain(new MirrorDouble(7, 1, 2000));

        state.PruneDoubles(1500 + MirrorState.DOUBLE_LIFETIME_MS);

        state.Doubles
             .Should()
             .Equal(new MirrorDouble(7, 1, 2000));

        await Task.CompletedTask;
    }

    [Test]
    public async Task IsInDarkStretch_matches_the_rectangle()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.IsInDarkStretch(10, 20)
             .Should()
             .BeTrue();

        state.IsInDarkStretch(13, 21)
             .Should()
             .BeTrue();

        state.IsInDarkStretch(14, 21)
             .Should()
             .BeFalse();

        state.IsInDarkStretch(10, 22)
             .Should()
             .BeFalse();

        state.IsInDarkStretch(9, 20)
             .Should()
             .BeFalse();

        await Task.CompletedTask;
    }
}

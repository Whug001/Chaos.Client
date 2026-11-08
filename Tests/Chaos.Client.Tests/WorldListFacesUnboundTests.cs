using Chaos.Client.Controls.World.Popups.WorldList;
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class WorldListFacesUnboundTests
{
    private static WorldListFace PeasantFace => WorldListFaces.Buttons.SelectMany(row => row).Single(face => face.Label == "Peasant");

    //formatter:off
    [Test]
    [Arguments(BaseClass.Peasant, true)]
    [Arguments(BaseClass.Unbound, true)]
    [Arguments(BaseClass.Warrior, false)]
    //formatter:on
    public void Peasant_face_counts_unbound(BaseClass baseClass, bool matches)
        => PeasantFace.Matches(new WorldListEntry("n", null, baseClass, false, false, default, default)).Should().Be(matches);
}

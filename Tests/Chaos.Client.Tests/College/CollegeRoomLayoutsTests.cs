using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeRoomLayoutsTests
{
    [Test]
    [Arguments((short)775, (short)779, true)]
    [Arguments((short)779, (short)775, true)]
    [Arguments((short)763, (short)779, true)]
    [Arguments((short)775, (short)774, false)]
    [Arguments((short)774, (short)775, false)]
    [Arguments((short)775, (short)776, true)]
    [Arguments((short)775, (short)775, false)]
    public void Layout_swap_needs_both_ends_in_a_college_room(short oldId, short newId, bool expected)
        => CollegeRoomLayouts.IsLayoutSwap(oldId, newId).Should().Be(expected);
}

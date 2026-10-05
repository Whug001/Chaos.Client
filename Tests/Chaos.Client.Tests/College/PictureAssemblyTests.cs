using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PictureAssemblyTests
{
    [Test]
    public void Bytes_come_back_only_when_every_part_is_in()
    {
        var assembly = new PictureAssembly(3);

        assembly.Add(2, 3, [5]).Should().BeNull();
        assembly.Add(0, 3, [1, 2]).Should().BeNull();
        assembly.Add(1, 3, [3, 4])!.Should().Equal(1, 2, 3, 4, 5);
    }

    [Test]
    public void A_mismatched_part_is_refused()
    {
        var assembly = new PictureAssembly(2);

        assembly.Add(0, 3, [1]).Should().BeNull();
        assembly.Failed.Should().BeTrue();
        new PictureAssembly(2).Add(5, 2, [1]).Should().BeNull();
    }
}

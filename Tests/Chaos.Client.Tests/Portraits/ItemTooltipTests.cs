using System.Reflection;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups;
using FluentAssertions;

namespace Chaos.Client.Tests.Portraits;

public sealed class ItemTooltipTests
{
    [Test]
    public void Painter_label_aligns_with_the_heading()
    {
        var tip = new ItemTooltipControl();
        var painter = (UILabel)typeof(ItemTooltipControl).GetField("PainterLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tip)!;
        var heading = (UILabel)typeof(TooltipPanelBase).GetProperty("HeadingLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tip)!;

        painter.X.Should().Be(heading.X);
    }
}

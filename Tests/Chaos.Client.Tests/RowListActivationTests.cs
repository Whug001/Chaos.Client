using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class RowListActivationTests
{
    private const int ROW_HEIGHT = 20;

    private static (VirtualizedRowList<string> List, List<string> Activated) Build()
    {
        var list = new VirtualizedRowList<string>(100, ROW_HEIGHT * 3, ROW_HEIGHT, () => new UIPanel(), (_, _, _) => { }, selectable: true);
        list.SetItems(["a", "b", "c"]);
        var activated = new List<string>();
        list.RowActivated += activated.Add;

        return (list, activated);
    }

    private static void Release(VirtualizedRowList<string> list, int row, bool pairedWithLast)
    {
        var y = (row * ROW_HEIGHT) + 5;
        list.OnClick(new ClickEvent { ScreenY = y, Button = MouseButton.Left });

        if (pairedWithLast)
            list.OnDoubleClick(new DoubleClickEvent { ScreenY = y, Button = MouseButton.Left });
    }

    [Test]
    public void Two_clicks_on_one_row_activate_it()
    {
        var (list, activated) = Build();

        Release(list, 1, false);
        Release(list, 1, true);

        activated.Should().Equal("b");
    }

    [Test]
    public void Quick_clicks_on_two_different_rows_do_not_activate()
    {
        var (list, activated) = Build();

        Release(list, 0, false);
        Release(list, 1, true);

        activated.Should().BeEmpty();
        list.SelectedItem.Should().Be("b");
    }
}

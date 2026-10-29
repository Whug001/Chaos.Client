#if DEBUG
using Chaos.Client.Systems.TestHook;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.TestHook;

public class PanelInspectorTests
{
    private static int Measure(string s) => s.Length * 6;

    private static UiNode Node(string name, int x, int y, int w, int h, string? text = null,
        UiNodeKind kind = UiNodeKind.Other, bool visible = true, params UiNode[] children)
        => new(name, name, new Rectangle(x, y, w, h), visible, kind, text, false, children);

    private static UiNode Root(params UiNode[] children) => Node("WorldRoot", 0, 0, 640, 480, children: children);

    [Test]
    public void CleanPanel_HasNoProblems()
    {
        var panel = Node("Shop", 100, 100, 200, 100, children: [Node("Title", 110, 110, 100, 12, "Shop", UiNodeKind.Label)]);
        var reports = PanelInspector.Inspect(Root(panel), Measure);

        reports.Should().ContainSingle().Which.Problems.Should().BeEmpty();
        reports[0].Elements.Should().ContainSingle(e => e.Path == "Shop/Title");
    }

    [Test]
    public void LongLabel_IsTextOverflow()
    {
        var panel = Node("Shop", 100, 100, 200, 100, children: [Node("Title", 110, 110, 30, 12, "Very long title", UiNodeKind.Label)]);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems
            .Should().ContainSingle(p => p.Kind == "text-overflow" && p.Path == "Shop/Title");
    }

    [Test]
    public void MultiLineTextBox_IsNotCheckedForOverflow()
    {
        var box = new UiNode("Body", "Body", new Rectangle(110, 110, 30, 40), true, UiNodeKind.TextBox, "Very long text here", true, []);
        var panel = Node("Mail", 100, 100, 200, 100, children: [box]);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems.Should().BeEmpty();
    }

    [Test]
    public void OverlappingButtons_AreReportedOnce()
    {
        var panel = Node("Shop", 100, 100, 200, 100, children:
        [
            Node("Buy", 110, 150, 50, 20, "Buy", UiNodeKind.Button),
            Node("Sell", 150, 150, 50, 20, "Sell", UiNodeKind.Button),
        ]);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems
            .Should().ContainSingle(p => p.Kind == "overlap");
    }

    [Test]
    public void ImageUnderLabel_IsNotAnOverlap()
    {
        var panel = Node("Shop", 100, 100, 200, 100, children:
        [
            Node("Backdrop", 100, 100, 200, 100, kind: UiNodeKind.Image),
            Node("Title", 110, 110, 100, 12, "Shop", UiNodeKind.Label),
        ]);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems.Should().BeEmpty();
    }

    [Test]
    public void ChildPastPanelEdge_IsOutsideParent()
    {
        var panel = Node("Shop", 100, 100, 200, 100, children: [Node("Close", 290, 100, 20, 20, kind: UiNodeKind.Button)]);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems
            .Should().ContainSingle(p => p.Kind == "outside-parent" && p.Path == "Shop/Close");
    }

    [Test]
    public void PanelPastScreenEdge_IsOffScreen()
    {
        var panel = Node("Shop", 600, 100, 100, 100);
        PanelInspector.Inspect(Root(panel), Measure)[0].Problems
            .Should().ContainSingle(p => p.Kind == "off-screen" && p.Path == "Shop");
    }

    [Test]
    public void HiddenPanel_IsSkipped()
    {
        var panel = Node("Shop", 100, 100, 200, 100, visible: false);
        PanelInspector.Inspect(Root(panel), Measure).Should().BeEmpty();
    }

    [Test]
    public void EmptyRoot_ReturnsEmptyList()
        => PanelInspector.Inspect(Root(), Measure).Should().BeEmpty();
}
#endif

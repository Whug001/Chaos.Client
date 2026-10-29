#if DEBUG
using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.TestHook;

public sealed record PanelProblem(string Kind, string Path, string Detail);
public sealed record ElementInfo(string Path, string Type, int X, int Y, int Width, int Height, string? Text);
public sealed record PanelReport(string Path, string Type, int X, int Y, int Width, int Height,
    IReadOnlyList<ElementInfo> Elements, IReadOnlyList<PanelProblem> Problems);

public static class PanelInspector
{
    private static readonly Rectangle Screen = new(0, 0, 640, 480);

    public static IReadOnlyList<PanelReport> Inspect(UiNode root, Func<string, int> measure)
    {
        var reports = new List<PanelReport>();

        foreach (var panel in root.Children.Where(c => c.Visible))
        {
            var elements = new List<ElementInfo>();
            var problems = new List<PanelProblem>();

            if (!Screen.Contains(panel.ScreenBounds))
                problems.Add(new PanelProblem("off-screen", panel.Name, Describe(panel.ScreenBounds)));

            Walk(panel, panel.Name, measure, elements, problems);

            var b = panel.ScreenBounds;
            reports.Add(new PanelReport(panel.Name, panel.Type, b.X, b.Y, b.Width, b.Height, elements, problems));
        }

        return reports;
    }

    private static void Walk(UiNode parent, string parentPath, Func<string, int> measure,
        List<ElementInfo> elements, List<PanelProblem> problems)
    {
        var visible = parent.Children.Where(c => c.Visible).ToList();

        foreach (var child in visible)
        {
            var path = $"{parentPath}/{child.Name}";
            var b = child.ScreenBounds;
            elements.Add(new ElementInfo(path, child.Type, b.X, b.Y, b.Width, b.Height, child.Text));

            if (b.Width > 0 && b.Height > 0 && !parent.ScreenBounds.Contains(b))
                problems.Add(new PanelProblem("outside-parent", path, $"{Describe(b)} not inside {Describe(parent.ScreenBounds)}"));

            if (!child.MultiLine && !string.IsNullOrEmpty(child.Text))
            {
                var needed = measure(child.Text);
                if (needed > b.Width)
                    problems.Add(new PanelProblem("text-overflow", path, $"text needs {needed}px, control is {b.Width}px"));
            }

            Walk(child, path, measure, elements, problems);
        }

        var solid = visible.Where(IsSolid).ToList();
        for (var i = 0; i < solid.Count; i++)
            for (var j = i + 1; j < solid.Count; j++)
                if (solid[i].ScreenBounds.Intersects(solid[j].ScreenBounds))
                    problems.Add(new PanelProblem("overlap", $"{parentPath}/{solid[i].Name}",
                        $"overlaps {parentPath}/{solid[j].Name}"));
    }

    private static bool IsSolid(UiNode node)
        => node.Kind is UiNodeKind.Label or UiNodeKind.TextBox or UiNodeKind.Button
           && node.ScreenBounds.Width > 0 && node.ScreenBounds.Height > 0;

    private static string Describe(Rectangle r) => $"{r.X},{r.Y} {r.Width}x{r.Height}";
}
#endif

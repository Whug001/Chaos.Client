#if DEBUG
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.TestHook;

public enum UiNodeKind { Other, Panel, Label, TextBox, Button, Image }

public sealed record UiNode(
    string Name,
    string Type,
    Rectangle ScreenBounds,
    bool Visible,
    UiNodeKind Kind,
    string? Text,
    bool MultiLine,
    IReadOnlyList<UiNode> Children)
{
    public static UiNode From(UIElement element)
    {
        var type = element.GetType().Name;
        var name = string.IsNullOrEmpty(element.Name) ? type : element.Name;

        var (kind, text, multiLine) = element switch
        {
            UITextBox box => (UiNodeKind.TextBox, box.Text, box.IsMultiLine),
            UILabel label => (UiNodeKind.Label, label.Text, false),
            UIButton => (UiNodeKind.Button, (string?)null, false),
            UIImage => (UiNodeKind.Image, (string?)null, false),
            UIPanel => (UiNodeKind.Panel, (string?)null, false),
            _ => (UiNodeKind.Other, (string?)null, false)
        };

        var children = element is UIPanel panel
            ? panel.Children.Select(From).ToList()
            : [];

        return new UiNode(name, type, element.ScreenBounds, element.Visible, kind, text, multiLine, children);
    }
}
#endif

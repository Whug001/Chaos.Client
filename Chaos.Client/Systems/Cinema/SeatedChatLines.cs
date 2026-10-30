using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.Cinema;

/// <summary>The few recent chat lines shown above the seated view's chat line, each fading out after a while.</summary>
public sealed class SeatedChatLines
{
    public const int MAX_LINES = 4;
    public const long SHOW_MS = 6000;
    public const long FADE_MS = 1000;

    private readonly List<(string Text, Color Color, long At)> Lines = [];

    public void Add(string text, Color color, long now)
    {
        Lines.Add((text, color, now));

        if (Lines.Count > MAX_LINES)
            Lines.RemoveAt(0);
    }

    public IReadOnlyList<(string Text, Color Color, float Alpha)> Visible(long now)
    {
        if (Lines.Count == 0)
            return [];

        var visible = new List<(string, Color, float)>(Lines.Count);

        foreach (var (text, color, at) in Lines)
        {
            var age = now - at;

            if (age >= SHOW_MS + FADE_MS)
                continue;

            visible.Add((text, color, age <= SHOW_MS ? 1f : 1f - (age - SHOW_MS) / (float)FADE_MS));
        }

        return visible;
    }

    public void Clear() => Lines.Clear();
}

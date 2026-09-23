# Custom Emotes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move every client-side emote onto one shared system, then add five new pixel-art emotes: Heart Eyes, Halo, Lightbulb, Anger Steam and Party Hat.

**Architecture:** Each custom emote is an `ICustomEmote`. The hand-drawn ones derive from `PixelArtEmote`, which describes the emote at time T as a list of `PixelLayer`s (art + composite position + opacity). The same list drives the in-world draw (MonoGame), the wheel icon (SkiaSharp) and the throwaway preview sheet. One registry (`CustomEmoteRegistry`), one clock on `WorldEntity`, and one `CustomEmoteRenderer` replace the per-emote wiring.

**Tech Stack:** C# 14 / .NET 10, MonoGame 3.8, SkiaSharp 3.116, DALib 0.7, TUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-23-custom-emotes-design.md`

## Global Constraints

- Composite space is the 111×85 aisling canvas. The head spans x50–60 and rows 24–38 (unflipped). The eyes sit on rows 32–33. `AislingRenderer.MirrorX(x) = 109 - x`.
- Every art layer is placed unflipped. Flipping always maps a layer's left edge to `AislingRenderer.MirrorX(x + width - 1)`, drawn with `SpriteEffects.FlipHorizontally`.
- Emote bytes: Sunglasses 18, Middle Finger 19, Heart Eyes 20, Halo 2, Lightbulb 3, Anger Steam 4, Party Hat 5. All must be in 1–44 and must not be defined `BodyAnimation` members.
- Icon codes (`PreviewFrame`): Sunglasses 1000, Middle Finger 1001, Heart Eyes 1002, Halo 1003, Lightbulb 1004, Anger Steam 1005, Party Hat 1006.
- Lengths: Heart Eyes 1500 ms, Halo 2000 ms, Lightbulb 1600 ms, Anger Steam 1600 ms, Party Hat 1800 ms. Sunglasses (1400) and Middle Finger (1500) are unchanged.
- No colour in any new emote's art may have R, G and B all ≥ 220. `ImageUtil.FindHeadBounds` drops such pixels from the icon crop.
- Every `PixelArt.Key` is unique across all emotes (it is the texture cache key).
- Sunglasses and Middle Finger must look and time exactly as before.
- The registry class is `CustomEmoteRegistry`, not `CustomEmotes` as the spec says. A class may not share its namespace's name (`Chaos.Client.Rendering.CustomEmotes`).
- Commit strategy is at-end: implementers do NOT commit. Task 7 makes the single commit on a new branch. Do not stage `Chaos.Client/Properties/launchSettings.json` or the `Chaos-Server` submodule pointer. Both carry unrelated local changes.
- Code tools: this machine uses Serena for code reads and edits (see `~/.claude/CLAUDE.md`). Use `git mv` / `git rm` for file moves.
- Test command: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` (`dotnet test` does not work with this SDK). Run it from the `Chaos.Client` repo root.

**User decisions (already made):**
- "1": one shared system for all custom emotes, with Sunglasses and Middle Finger moved onto it.
- "2, 5, 6, 7 and 8": the new emotes are Heart Eyes, Halo, Lightbulb, Anger Steam and Party Hat.
- Part 2 approved: bytes 20/2/3/4/5, front-facing only, drawn over headgear, art as code pixel maps, no hotkeys, walking blocked while playing, the timings above.
- Part 3 approved: a throwaway preview sheet for art sign-off before wiring, TUnit tests, and an in-game check by the user, then one commit.
- Spec approved ("Looks good").

---

### Task 1: Shared building blocks

**Goal:** Add the types every custom emote builds on: head geometry, pixel art, pixel-to-texture/bitmap conversion, the texture cache, the draw context, the `ICustomEmote` contract and the `PixelArtEmote` base class.

**Files:**
- Create: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteGeometry.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/PixelArt.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/PixelSprite.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteTextureCache.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteDrawContext.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/ICustomEmote.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/PixelArtEmote.cs`
- Test: `Tests/Chaos.Client.Tests/CustomEmoteCoreTests.cs`

**Acceptance Criteria:**
- [ ] `PixelArt` rejects empty or ragged rows with `ArgumentException`.
- [ ] `PixelArt.Colors()` yields each distinct non-transparent colour once, and throws `InvalidOperationException` on a character with no colour.
- [ ] `PixelSprite.Stamp` grows the canvas in all four directions and places the source and the layers at their composite positions.
- [ ] `CustomEmoteDrawContext.ResolveLeftX(true, x, w) == AislingRenderer.MirrorX(x + w - 1)`.
- [ ] `CustomEmoteGeometry.HeadBobOffset` returns 1 only for walk (`"01"`) frames 7 and 9.
- [ ] `dotnet build Chaos.Client.slnx` succeeds, and `CustomEmoteCoreTests` pass.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CustomEmoteCoreTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** in `Tests/Chaos.Client.Tests/CustomEmoteCoreTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class CustomEmoteCoreTests
{
    private static readonly Dictionary<char, Color> RedBlue = new()
    {
        ['r'] = new Color(200, 0, 0),
        ['b'] = new Color(0, 0, 200)
    };

    private static SKImage SolidImage(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);

        return SKImage.FromBitmap(bitmap);
    }

    [Test]
    public async Task Pixel_art_rejects_ragged_rows()
    {
        var act = () => PixelArt.FromPalette("test:ragged", ["rr", "r"], RedBlue);

        act.Should().Throw<ArgumentException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pixel_art_rejects_an_empty_map()
    {
        var act = () => PixelArt.FromPalette("test:empty", [], RedBlue);

        act.Should().Throw<ArgumentException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Colors_lists_each_distinct_colour_once_and_skips_transparent()
    {
        var art = PixelArt.FromPalette("test:colors", ["r.b", "rr."], RedBlue);

        art.Colors().Should().BeEquivalentTo(new[] { new Color(200, 0, 0), new Color(0, 0, 200) });
        await Task.CompletedTask;
    }

    [Test]
    public async Task Colors_throws_on_a_character_the_palette_does_not_define()
    {
        var art = PixelArt.FromPalette("test:unknown", ["rx"], RedBlue);
        var act = () => art.Colors().ToList();

        act.Should().Throw<InvalidOperationException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task To_bitmap_paints_mapped_pixels_and_leaves_dots_clear()
    {
        using var bitmap = PixelSprite.ToBitmap(PixelArt.FromPalette("test:bitmap", ["r.", "b."], RedBlue));

        bitmap.GetPixel(0, 0).Should().Be(new SKColor(200, 0, 0));
        bitmap.GetPixel(1, 0).Alpha.Should().Be(0);
        bitmap.GetPixel(0, 1).Should().Be(new SKColor(0, 0, 200));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Stamp_grows_up_and_left_for_art_above_and_beside_the_source()
    {
        using var source = SolidImage(2, 2, new SKColor(200, 0, 0));
        var dot = PixelArt.Dot("test:dot-up-left", new Color(0, 0, 200));

        //source's top-left sits at composite (27, 0); the dot sits at (25, -2)
        using var stamped = PixelSprite.Stamp(source, 27, 0, [new PixelLayer(dot, 25, -2)]);
        using var result = SKBitmap.FromImage(stamped);

        result.Width.Should().Be(4);
        result.Height.Should().Be(4);
        result.GetPixel(0, 0).Should().Be(new SKColor(0, 0, 200));
        result.GetPixel(2, 2).Should().Be(new SKColor(200, 0, 0));
        result.GetPixel(3, 3).Should().Be(new SKColor(200, 0, 0));
        result.GetPixel(1, 1).Alpha.Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Stamp_grows_right_and_down_too()
    {
        using var source = SolidImage(2, 2, new SKColor(200, 0, 0));
        var dot = PixelArt.Dot("test:dot-down-right", new Color(0, 0, 200));

        using var stamped = PixelSprite.Stamp(source, 27, 0, [new PixelLayer(dot, 30, 3)]);
        using var result = SKBitmap.FromImage(stamped);

        result.Width.Should().Be(4);
        result.Height.Should().Be(4);
        result.GetPixel(3, 3).Should().Be(new SKColor(0, 0, 200));
        result.GetPixel(0, 0).Should().Be(new SKColor(200, 0, 0));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Resolve_left_x_mirrors_the_right_edge_about_the_sprite_pivot()
    {
        CustomEmoteDrawContext.ResolveLeftX(false, 52, 10).Should().Be(52);
        CustomEmoteDrawContext.ResolveLeftX(true, 52, 10).Should().Be(AislingRenderer.MirrorX(61));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Head_bob_only_applies_to_the_walk_frames_that_drop_the_head()
    {
        CustomEmoteGeometry.HeadBobOffset(7, "01").Should().Be(1);
        CustomEmoteGeometry.HeadBobOffset(9, "01").Should().Be(1);
        CustomEmoteGeometry.HeadBobOffset(8, "01").Should().Be(0);
        CustomEmoteGeometry.HeadBobOffset(7, "02").Should().Be(0);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CustomEmoteCoreTests/*"`
Expected: a build error. `Chaos.Client.Rendering.CustomEmotes` does not exist.

- [ ] **Step 3: Create `CustomEmoteGeometry.cs`**

```csharp
namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Where the aisling head sits in aisling-composite space — the 111x85 canvas <c>AislingRenderer.Composite</c>
///     builds — on the front-facing poses custom emotes are drawn on. Every position is unflipped.
/// </summary>
public static class CustomEmoteGeometry
{
    /// <summary>Top row of the head in every front-facing frame.</summary>
    public const int HEAD_TOP_Y = 24;

    /// <summary>Bottom row of the head in every front-facing frame.</summary>
    public const int HEAD_BOTTOM_Y = 38;

    /// <summary>Leftmost column of the head, measured off the rendered face EPF.</summary>
    public const int HEAD_LEFT_X = 50;

    /// <summary>Rightmost column of the head, measured off the rendered face EPF.</summary>
    public const int HEAD_RIGHT_X = 60;

    /// <summary>Top of the two eye rows.</summary>
    public const int EYE_TOP_Y = 32;

    /// <summary>
    ///     emot01 frame 0 (Smile): a plain head with no speech-bubble backdrop. Most custom emote icons are painted
    ///     onto it.
    /// </summary>
    public const int PLAIN_FACE_FRAME = 0;

    /// <summary>
    ///     Rows the head sits lower than its resting position on this animation frame. The walk EPF drops the head one row
    ///     on its second and fourth strides; every other front-facing frame keeps it at rows 24-38. Without this, art
    ///     drawn on the head floats a pixel above it for half of a walk cycle.
    /// </summary>
    public static int HeadBobOffset(int frameIndex, string animSuffix) => animSuffix == "01" && frameIndex is 7 or 9 ? 1 : 0;
}
```

- [ ] **Step 4: Create `PixelArt.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Maps a pixel-map character to the colour it paints. Returns false for a character the palette does not define.
/// </summary>
public delegate bool PixelColorLookup(char c, out Color color);

/// <summary>
///     A small piece of hand-drawn pixel art: rows of characters plus a lookup from character to colour. '.' is
///     transparent. <see cref="Key" /> names the art in texture caches, so it must be unique across every custom emote.
/// </summary>
public sealed class PixelArt
{
    public const char TRANSPARENT = '.';

    public PixelArt(string key, IReadOnlyList<string> rows, PixelColorLookup lookup)
    {
        if ((rows.Count == 0) || (rows[0].Length == 0) || rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException($"Pixel art '{key}' must be a non-empty rectangle.", nameof(rows));

        Key = key;
        Rows = rows;
        Lookup = lookup;
    }

    public string Key { get; }
    public IReadOnlyList<string> Rows { get; }
    public PixelColorLookup Lookup { get; }
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;

    /// <summary>Builds art from a plain character-to-colour table. '.' is transparent and needs no entry.</summary>
    public static PixelArt FromPalette(string key, IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette)
        => new(
            key,
            rows,
            (char c, out Color color) =>
            {
                if (c == TRANSPARENT)
                {
                    color = Color.Transparent;

                    return true;
                }

                return palette.TryGetValue(c, out color);
            });

    /// <summary>A single opaque pixel, for confetti and glints.</summary>
    public static PixelArt Dot(string key, Color color) => FromPalette(key, ["x"], new Dictionary<char, Color> { ['x'] = color });

    /// <summary>
    ///     Every distinct colour this art paints, transparent pixels excluded. Throws when a character has no colour, so a
    ///     typo in a pixel map fails a test instead of silently drawing nothing.
    /// </summary>
    public IEnumerable<Color> Colors()
    {
        var seen = new HashSet<Color>();

        foreach (var row in Rows)
        foreach (var c in row)
        {
            if (c == TRANSPARENT)
                continue;

            if (!Lookup(c, out var color))
                throw new InvalidOperationException($"Pixel art '{Key}' uses '{c}', which its palette does not define.");

            if ((color.A != 0) && seen.Add(color))
                yield return color;
        }
    }
}

/// <summary>
///     One piece of art placed at an unflipped composite position. <see cref="Opacity" /> multiplies the aisling's own
///     alpha.
/// </summary>
public readonly record struct PixelLayer(PixelArt Art, int X, int Y, float Opacity = 1f);
```

- [ ] **Step 5: Create `PixelSprite.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Turns <see cref="PixelArt" /> into pixels: a MonoGame texture for the world, and a SkiaSharp bitmap for icons and
///     previews.
/// </summary>
public static class PixelSprite
{
    /// <summary>Renders the art at 1:1 into a new bitmap. The caller owns the bitmap.</summary>
    public static SKBitmap ToBitmap(PixelArt art)
    {
        var bitmap = new SKBitmap(art.Width, art.Height);
        bitmap.Erase(SKColors.Transparent);

        for (var y = 0; y < art.Height; y++)
        for (var x = 0; x < art.Width; x++)
        {
            if (!art.Lookup(art.Rows[y][x], out var color) || (color.A == 0))
                continue;

            bitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B, color.A));
        }

        return bitmap;
    }

    /// <summary>Renders the art at 1:1 into a new texture. Returns null before the graphics device exists.</summary>
    public static Texture2D? ToTexture(PixelArt art)
    {
        var device = TextureConverter.Device;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (device is null)
            return null;

        var pixels = new Color[art.Width * art.Height];

        for (var y = 0; y < art.Height; y++)
        for (var x = 0; x < art.Width; x++)
        {
            if (!art.Lookup(art.Rows[y][x], out var color) || (color.A == 0))
                continue;

            pixels[y * art.Width + x] = Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
        }

        var texture = new Texture2D(device, art.Width, art.Height);
        texture.SetData(pixels);

        return texture;
    }

    /// <summary>
    ///     Returns a copy of <paramref name="source" /> with the layers painted over it. The source's top-left corner sits
    ///     at composite (<paramref name="sourceX" />, <paramref name="sourceY" />), and the layers are in composite
    ///     coordinates. The canvas grows in any direction to fit everything, so art above or beside the head is kept.
    /// </summary>
    public static SKImage Stamp(SKImage source, int sourceX, int sourceY, IReadOnlyList<PixelLayer> layers)
    {
        var minX = sourceX;
        var minY = sourceY;
        var maxX = sourceX + source.Width;
        var maxY = sourceY + source.Height;

        foreach (var layer in layers)
        {
            minX = Math.Min(minX, layer.X);
            minY = Math.Min(minY, layer.Y);
            maxX = Math.Max(maxX, layer.X + layer.Art.Width);
            maxY = Math.Max(maxY, layer.Y + layer.Art.Height);
        }

        using var bitmap = new SKBitmap(maxX - minX, maxY - minY);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(source, sourceX - minX, sourceY - minY);

            foreach (var layer in layers)
            {
                using var art = ToBitmap(layer.Art);

                using var paint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity * 255f, 0f, 255f))
                };

                canvas.DrawBitmap(art, layer.X - minX, layer.Y - minY, paint);
            }
        }

        return SKImage.FromBitmap(bitmap);
    }
}
```

- [ ] **Step 6: Create `CustomEmoteTextureCache.cs`**

```csharp
#region
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Textures built for custom emotes, keyed by name. Pixel-art textures are small and shared by every aisling
///     playing the emote.
/// </summary>
public sealed class CustomEmoteTextureCache : IDisposable
{
    private readonly Dictionary<string, Texture2D?> Cache = [];

    public void Dispose() => Clear();

    /// <summary>
    ///     Returns the texture for this art, building it on first use. A null result (no graphics device yet) is not
    ///     cached, so the next draw tries again.
    /// </summary>
    public Texture2D? GetOrBuild(PixelArt art)
    {
        if (Cache.TryGetValue(art.Key, out var cached))
            return cached;

        var texture = PixelSprite.ToTexture(art);

        if (texture is not null)
            Cache[art.Key] = texture;

        return texture;
    }

    /// <summary>
    ///     Returns the texture for this key, building it on first use. A null result is cached, so a failed build (missing
    ///     or wrong-shaped source art) is not retried every frame.
    /// </summary>
    public Texture2D? GetOrBuild(string key, Func<Texture2D?> build)
    {
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var texture = build();
        Cache[key] = texture;

        return texture;
    }

    /// <summary>Disposes every cached texture. They rebuild on the next draw.</summary>
    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture?.Dispose();

        Cache.Clear();
    }
}
```

- [ ] **Step 7: Create `CustomEmoteDrawContext.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     What a custom emote needs to draw itself on top of one aisling's finished composite.
/// </summary>
/// <param name="Batch">The sprite batch the aisling was just drawn into.</param>
/// <param name="Origin">
///     Screen position of the composite's top-left corner. Composite pixel coordinates map one-to-one onto screen pixels
///     from here.
/// </param>
/// <param name="Flip">True when the aisling composite is drawn mirrored.</param>
/// <param name="HeadBobRows">Rows the head sits lower on the current walk frame. See <see cref="CustomEmoteGeometry.HeadBobOffset" />.</param>
/// <param name="Alpha">Alpha the aisling itself was drawn with, so ghosts and faded players match.</param>
/// <param name="BodyColor">The aisling's body colour, for art built from the body palette.</param>
/// <param name="Textures">The shared custom-emote texture cache.</param>
public readonly record struct CustomEmoteDrawContext(
    SpriteBatch Batch,
    Vector2 Origin,
    bool Flip,
    int HeadBobRows,
    float Alpha,
    int BodyColor,
    CustomEmoteTextureCache Textures)
{
    /// <summary>
    ///     Composite X of a texture's left edge once flipping is applied. Mirroring maps the texture's right edge onto its
    ///     new left edge, about the same pivot the aisling sprite uses.
    /// </summary>
    public static int ResolveLeftX(bool flip, int compositeX, int width)
        => flip ? AislingRenderer.MirrorX(compositeX + width - 1) : compositeX;

    /// <summary>
    ///     Draws a texture whose unflipped top-left corner is at composite (<paramref name="compositeX" />,
    ///     <paramref name="compositeY" />). When the aisling is flipped, the texture is mirrored and moved to match.
    /// </summary>
    public void DrawTexture(Texture2D texture, int compositeX, int compositeY, float opacity = 1f)
    {
        var x = ResolveLeftX(Flip, compositeX, texture.Width);

        Batch.Draw(
            texture,
            new Vector2(Origin.X + x, Origin.Y + compositeY),
            null,
            Color.White * (Alpha * opacity),
            0f,
            Vector2.Zero,
            1f,
            Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }
}
```

- [ ] **Step 8: Create `ICustomEmote.cs`**

```csharp
#region
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A client-side emote: one with no emot01 frame of its own. It rides on a body-animation byte the enum leaves
///     unused, and is drawn on top of the finished aisling composite rather than composited into it.
/// </summary>
public interface ICustomEmote
{
    /// <summary>
    ///     The body-animation byte the client sends and listens for. It must be in the 1-44 range the server relays
    ///     untouched, and must not be a defined <c>BodyAnimation</c> member.
    /// </summary>
    int BodyAnimation { get; }

    /// <summary>The name shown in the emote catalog.</summary>
    string Name { get; }

    /// <summary>
    ///     The icon code the wheel and catalog pass to <c>UiRenderer.GetEmoteFaceTexture</c>. It sits at 1000 or above,
    ///     past emot01's 50 frames, so it can never collide with a real frame.
    /// </summary>
    int PreviewFrame { get; }

    /// <summary>The emot01 frame the icon is painted onto.</summary>
    int IconSourceFrame { get; }

    /// <summary>Total length. Nothing is drawn from this point on.</summary>
    float DurationMs { get; }

    /// <summary>Draws the emote as it looks <paramref name="elapsedMs" /> after it started.</summary>
    void Draw(in CustomEmoteDrawContext context, float elapsedMs);

    /// <summary>
    ///     Paints the emote onto <paramref name="sourceFrame" /> — emot01 <see cref="IconSourceFrame" />, already
    ///     palettized with the viewer's body colour — for the wheel icon. Returns null when it can't, which leaves the
    ///     plain source frame as the icon.
    /// </summary>
    SKImage? BuildIcon(SKImage sourceFrame);
}
```

- [ ] **Step 9: Create `PixelArtEmote.cs`**

```csharp
#region
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Base for emotes made of hand-drawn <see cref="PixelArt" />. An emote only describes what is on screen at a
///     moment, as a list of layers; drawing, flipping, icons and previews all come from that one description.
/// </summary>
public abstract class PixelArtEmote : ICustomEmote
{
    public abstract int BodyAnimation { get; }
    public abstract string Name { get; }
    public abstract int PreviewFrame { get; }
    public virtual int IconSourceFrame => CustomEmoteGeometry.PLAIN_FACE_FRAME;
    public abstract float DurationMs { get; }

    /// <summary>The moment whose layers become the wheel icon. Pick one where every layer is fully opaque.</summary>
    public abstract float IconTimeMs { get; }

    /// <summary>
    ///     What is on screen <paramref name="elapsedMs" /> after the emote started, in unflipped composite coordinates.
    ///     Returns an empty list outside 0 to <see cref="DurationMs" />.
    /// </summary>
    /// <param name="elapsedMs">Milliseconds since the emote started.</param>
    /// <param name="headBobRows">Rows the head sits lower on the current walk frame.</param>
    public abstract IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows);

    public void Draw(in CustomEmoteDrawContext context, float elapsedMs)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= DurationMs))
            return;

        foreach (var layer in Compose(elapsedMs, context.HeadBobRows))
        {
            var texture = context.Textures.GetOrBuild(layer.Art);

            if (texture is not null)
                context.DrawTexture(texture, layer.X, layer.Y, layer.Opacity);
        }
    }

    /// <summary>
    ///     An emot01 frame is composited into the aisling at an X offset of
    ///     <see cref="AislingRenderer.LAYER_OFFSET_PADDING" /> and a Y offset of zero, so the source frame is placed there
    ///     and the icon layers land on the same pixels they cover in the world.
    /// </summary>
    public SKImage? BuildIcon(SKImage sourceFrame)
        => PixelSprite.Stamp(sourceFrame, AislingRenderer.LAYER_OFFSET_PADDING, 0, Compose(IconTimeMs, 0));
}
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet build Chaos.Client.slnx` → build succeeded, 0 errors.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CustomEmoteCoreTests/*"` → 9 passed, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/CustomEmotes/CustomEmoteGeometry.cs", "Chaos.Client.Rendering/CustomEmotes/PixelArt.cs", "Chaos.Client.Rendering/CustomEmotes/PixelSprite.cs", "Chaos.Client.Rendering/CustomEmotes/CustomEmoteTextureCache.cs", "Chaos.Client.Rendering/CustomEmotes/CustomEmoteDrawContext.cs", "Chaos.Client.Rendering/CustomEmotes/ICustomEmote.cs", "Chaos.Client.Rendering/CustomEmotes/PixelArtEmote.cs", "Tests/Chaos.Client.Tests/CustomEmoteCoreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/CustomEmoteCoreTests/*\"", "acceptanceCriteria": ["PixelArt rejects empty/ragged rows with ArgumentException", "Colors() yields distinct colours and throws InvalidOperationException on unknown chars", "Stamp grows the canvas in all directions with correct placement", "ResolveLeftX(true,x,w) == MirrorX(x+w-1)", "HeadBobOffset is 1 only for walk frames 7 and 9", "Solution builds and CustomEmoteCoreTests pass"], "modelTier": "standard"}
```

---

### Task 2: Migrate the two emotes and rewire the client

**Goal:** Sunglasses and Middle Finger become `ICustomEmote`s in a registry. Every per-emote field, switch and renderer in the client becomes one generic path. Nothing visible changes.

**Files:**
- Move: `Chaos.Client.Rendering/SunglassesEmote.cs` → `Chaos.Client.Rendering/CustomEmotes/SunglassesEmote.cs`
- Move: `Chaos.Client.Rendering/MiddleFingerEmote.cs` → `Chaos.Client.Rendering/CustomEmotes/MiddleFingerEmote.cs`
- Delete: `Chaos.Client.Rendering/SunglassesRenderer.cs`, `Chaos.Client.Rendering/MiddleFingerRenderer.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteRegistry.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteRenderer.cs`
- Modify: `Chaos.Client.Rendering/UiRenderer.cs` (`GetEmoteFaceTexture`; delete `StampSunglasses`, `FoldMiddleFinger`)
- Modify: `Chaos.Client/GlobalUsings.cs`
- Modify: `Chaos.Client/Models/WorldEntity.cs` (`IsAtRest`, custom emote state at lines 89–106)
- Modify: `Chaos.Client/Screens/WorldScreen.Update.cs` (tick at lines 104–116, `TryPlayLocalEmote`)
- Modify: `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (`HandleBodyAnimation`)
- Modify: `Chaos.Client/Screens/WorldScreen.Draw.cs` (lines 869–903)
- Modify: `Chaos.Client/ChaosGame.cs` (renderer properties at lines ~100–108, `Dispose` at ~806–807)
- Modify: `Chaos.Client/Definitions/EmoteCatalog.cs`
- Test: `Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs` (new)
- Test: `Tests/Chaos.Client.Tests/EmoteCatalogTests.cs`, `SunglassesEmoteTests.cs`, `MiddleFingerEmoteTests.cs` (usings + moved constants)

**Acceptance Criteria:**
- [ ] `grep -rn "SunglassesRenderer\|MiddleFingerRenderer\|IsWearingSunglasses\|IsFlippingOff\|SunglassesElapsedMs\|MiddleFingerRemainingMs\|SUNGLASSES_PREVIEW_FRAME\|MIDDLE_FINGER_PREVIEW_FRAME\|StampSunglasses\|FoldMiddleFinger" --include=*.cs .` prints nothing.
- [ ] `CustomEmoteRegistry.All` is `[SunglassesEmote.Instance, MiddleFingerEmote.Instance]`.
- [ ] Sunglasses layers sit exactly where the old renderer drew them, flipped and unflipped, sparkle included (tests below).
- [ ] The Middle Finger bubble is drawn at composite (`LAYER_OFFSET_PADDING`, 0), mirrored the same way as before, with no head bob.
- [ ] The existing `SunglassesEmoteTests`, `MiddleFingerEmoteTests` and `EmoteCatalogTests` pass. Only usings and the two moved constants change.
- [ ] The full test suite passes, and `dotnet build Chaos.Client.slnx` has 0 errors.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing registry tests** in `Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CustomEmoteRegistryTests
{
    [Test]
    public async Task Every_custom_emote_byte_is_relayed_by_the_server_and_collides_with_nothing()
    {
        foreach (var emote in CustomEmoteRegistry.All)
        {
            //Chaos-Server WorldServer.OnEmote relays 1..44 and drops everything else
            emote.BodyAnimation.Should().BeInRange(1, 44, emote.Name);

            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation)
                .Should()
                .BeFalse($"{emote.Name} must ride on an unused byte");
        }

        CustomEmoteRegistry.All.Select(e => e.BodyAnimation).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_icon_code_sits_past_emot01_and_is_unique()
    {
        CustomEmoteRegistry.All.Should().OnlyContain(e => e.PreviewFrame >= 1000);
        CustomEmoteRegistry.All.Select(e => e.PreviewFrame).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Lookups_find_every_registered_emote()
    {
        foreach (var emote in CustomEmoteRegistry.All)
        {
            CustomEmoteRegistry.TryGet(emote.BodyAnimation, out var byByte).Should().BeTrue();
            byByte.Should().BeSameAs(emote);

            CustomEmoteRegistry.TryGetByPreviewFrame(emote.PreviewFrame, out var byIcon).Should().BeTrue();
            byIcon.Should().BeSameAs(emote);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Lookups_miss_on_ordinary_emotes()
    {
        CustomEmoteRegistry.TryGet((int)BodyAnimation.Smile, out _).Should().BeFalse();
        CustomEmoteRegistry.TryGetByPreviewFrame(0, out _).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_registry_starts_with_the_two_original_emotes()
    {
        CustomEmoteRegistry.All[0].Should().BeSameAs(SunglassesEmote.Instance);
        CustomEmoteRegistry.All[1].Should().BeSameAs(MiddleFingerEmote.Instance);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Settled_sunglasses_sit_where_the_old_renderer_put_them()
    {
        var layers = SunglassesEmote.Instance.Compose(SunglassesEmote.Instance.IconTimeMs, 0);

        layers.Should().ContainSingle();
        layers[0].X.Should().Be(SunglassesEmote.SETTLED_LEFT_X);
        layers[0].Y.Should().Be(SunglassesEmote.SETTLED_TOP_Y);
        layers[0].Art.Width.Should().Be(SunglassesEmote.GLASSES_WIDTH);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_sparkle_hangs_off_the_glasses_corner()
    {
        var layers = SunglassesEmote.Instance.Compose(SunglassesEmote.DROP_MS + 1f, 0);

        layers.Should().HaveCount(2);
        layers[1].X.Should().Be(SunglassesEmote.SETTLED_LEFT_X + SunglassesEmote.SPARKLE_OFFSET_X);
        layers[1].Y.Should().Be(SunglassesEmote.SETTLED_TOP_Y + SunglassesEmote.SPARKLE_OFFSET_Y);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Sunglasses_follow_the_head_bob()
    {
        SunglassesEmote.Instance.Compose(SunglassesEmote.Instance.IconTimeMs, 1)[0]
                       .Y
                       .Should()
                       .Be(SunglassesEmote.SETTLED_TOP_Y + 1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipped_sunglasses_and_sparkle_land_where_the_old_renderer_put_them()
    {
        const int SPARKLE_WIDTH = 5;
        var flippedGlasses = SunglassesEmote.ResolveLeftX(true);

        CustomEmoteDrawContext.ResolveLeftX(true, SunglassesEmote.SETTLED_LEFT_X, SunglassesEmote.GLASSES_WIDTH)
                              .Should()
                              .Be(flippedGlasses);

        //the old renderer put the flipped sparkle at leftX + GLASSES_WIDTH - SPARKLE_OFFSET_X - sparkle.Width
        CustomEmoteDrawContext.ResolveLeftX(
                                  true,
                                  SunglassesEmote.SETTLED_LEFT_X + SunglassesEmote.SPARKLE_OFFSET_X,
                                  SPARKLE_WIDTH)
                              .Should()
                              .Be(flippedGlasses + SunglassesEmote.GLASSES_WIDTH - SunglassesEmote.SPARKLE_OFFSET_X - SPARKLE_WIDTH);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sunglasses_are_gone_at_the_end()
    {
        SunglassesEmote.Instance.Compose(SunglassesEmote.TOTAL_MS, 0).Should().BeEmpty();
        SunglassesEmote.Instance.DurationMs.Should().Be(SunglassesEmote.TOTAL_MS);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_middle_finger_icon_starts_from_the_stop_hand()
    {
        MiddleFingerEmote.Instance.IconSourceFrame.Should().Be(MiddleFingerEmote.SOURCE_FRAME);
        MiddleFingerEmote.Instance.DurationMs.Should().Be(MiddleFingerEmote.DURATION_MS);

        //the old renderer drew the bubble at ResolveLeftX; the shared draw must match for any width
        CustomEmoteDrawContext.ResolveLeftX(true, AislingRenderer.LAYER_OFFSET_PADDING, 34)
                              .Should()
                              .Be(MiddleFingerEmote.ResolveLeftX(true, 34));

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CustomEmoteRegistryTests/*"`
Expected: a build error. `CustomEmoteRegistry`, `SunglassesEmote.Instance` and `MiddleFingerEmote.Instance` do not exist.

- [ ] **Step 3: Move the two emote files**

```bash
git mv Chaos.Client.Rendering/SunglassesEmote.cs Chaos.Client.Rendering/CustomEmotes/SunglassesEmote.cs
git mv Chaos.Client.Rendering/MiddleFingerEmote.cs Chaos.Client.Rendering/CustomEmotes/MiddleFingerEmote.cs
```

In both files change `namespace Chaos.Client.Rendering;` to `namespace Chaos.Client.Rendering.CustomEmotes;`. The `cref`s to `AislingRenderer` and `UiRenderer` still resolve, because the parent namespace is searched.

- [ ] **Step 4: Turn `SunglassesEmote` into a `PixelArtEmote`**

1. Change `public static class SunglassesEmote` to `public sealed class SunglassesEmote : PixelArtEmote`.
2. In the class `<summary>`, replace `<see cref="SunglassesRenderer" /> turns it into textures and draws it.` with `<see cref="PixelArtEmote" /> turns it into textures and draws it.`
3. Replace the body of `HeadBobOffset` so there is one copy of the rule. Keep its doc comment:

```csharp
    public static int HeadBobOffset(int frameIndex, string animSuffix) => CustomEmoteGeometry.HeadBobOffset(frameIndex, animSuffix);
```

4. Insert after `HeadBobOffset`, at the end of the class. These statics must come after `GlassesPixels`, `SparklePixels` and the palette fields, because static initializers run in textual order:

```csharp
    #region ICustomEmote
    private static readonly PixelArt GlassesArt = new("sunglasses:glasses", GlassesPixels, TryGetColor);

    private static readonly PixelArt[] SparkleArt = SparklePixels
                                                    .Select((rows, i) => new PixelArt($"sunglasses:sparkle{i}", rows, TryGetColor))
                                                    .ToArray();

    public static SunglassesEmote Instance { get; } = new();

    private SunglassesEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Sunglasses";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override int IconSourceFrame => PREVIEW_FACE_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The glasses settled on the eyes, the sparkle over.</summary>
    public override float IconTimeMs => DROP_MS + SPARKLE_MS;

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        var frame = Resolve(elapsedMs);

        if (frame.IsFinished)
            return [];

        var top = SETTLED_TOP_Y + frame.RowOffset + headBobRows;
        var glasses = new PixelLayer(GlassesArt, SETTLED_LEFT_X, top);

        if (frame.SparkleStage < 0)
            return [glasses];

        //the sparkle hangs off the glasses' top-left corner; flipping mirrors it to the top-right like everything else
        return [glasses, new PixelLayer(SparkleArt[frame.SparkleStage], SETTLED_LEFT_X + SPARKLE_OFFSET_X, top + SPARKLE_OFFSET_Y)];
    }
    #endregion
```

- [ ] **Step 5: Turn `MiddleFingerEmote` into an `ICustomEmote`**

1. Replace the `#region using` block with:

```csharp
#region
using Chaos.Client.Data;
using DALib.Drawing;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion
```

2. Change `public static class MiddleFingerEmote` to `public sealed class MiddleFingerEmote : ICustomEmote`.
3. Insert at the end of the class, after `ResolveLeftX`. The bodies of `BuildIcon` and `BuildBubble` are `UiRenderer.FoldMiddleFinger` and `MiddleFingerRenderer.Build`, moved here:

```csharp
    #region ICustomEmote
    public static MiddleFingerEmote Instance { get; } = new();

    private MiddleFingerEmote() { }

    public int BodyAnimation => BODY_ANIMATION;
    public string Name => "Middle Finger";
    public int PreviewFrame => PREVIEW_FRAME;
    public int IconSourceFrame => SOURCE_FRAME;
    public float DurationMs => DURATION_MS;

    /// <summary>
    ///     Draws the bubble above the head. Draws nothing when the source art is missing or the wrong shape, so a
    ///     repacked emot01 makes the emote silent rather than garbled.
    /// </summary>
    public void Draw(in CustomEmoteDrawContext context, float elapsedMs)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= DURATION_MS))
            return;

        //one texture per body colour: the source frame is palettized through the body palette, and a green aisling
        //needs a green hand
        var bodyColor = context.BodyColor;
        var texture = context.Textures.GetOrBuild($"middle-finger:{bodyColor}", () => BuildBubble(bodyColor));

        if (texture is null)
            return;

        //the bubble sits above the head rather than on the face, so unlike the glasses it ignores the walk bob —
        //the real gesture bubbles do not bob with the head either
        context.DrawTexture(texture, AislingRenderer.LAYER_OFFSET_PADDING, 0);
    }

    /// <summary>
    ///     Folds the Stop hand for the wheel and catalog icon — the same fold the world bubble gets, so the two cannot
    ///     drift apart. Returns null when the frame is the wrong shape, leaving the plain hand as the icon.
    /// </summary>
    public SKImage? BuildIcon(SKImage sourceFrame)
    {
        using var bitmap = new SKBitmap(sourceFrame.Width, sourceFrame.Height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(sourceFrame, 0, 0);
        }

        return TryFold(bitmap, AislingRenderer.LAYER_OFFSET_PADDING) ? SKImage.FromBitmap(bitmap) : null;
    }

    private static Texture2D? BuildBubble(int bodyColor)
    {
        var drawData = DataContext.AislingDrawData;
        var epf = drawData.EmotionsEpf;

        if (epf is null || (SOURCE_FRAME >= epf.Count))
            return null;

        if (!drawData.BodyPalettes.TryGetValue(bodyColor, out var palette) && !drawData.BodyPalettes.TryGetValue(0, out palette))
            return null;

        using var image = Graphics.RenderImage(epf[SOURCE_FRAME], palette);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (image is null)
            return null;

        var height = BUBBLE_BOTTOM_Y + 1;

        if (image.Height < height)
            return null;

        //crop to the bubble and its tail; rows 24 and below are the frame's own face, which would paint over
        //the character's real one
        using var bitmap = new SKBitmap(image.Width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(image, 0, 0);
        }

        //a repacked or resized frame would put the fold over the wrong pixels — draw nothing rather than garbage
        if (!TryFold(bitmap, AislingRenderer.LAYER_OFFSET_PADDING))
            return null;

        return TextureConverter.ToTexture2D(SKImage.FromBitmap(bitmap));
    }
    #endregion
```

- [ ] **Step 6: Delete the two old renderers**

```bash
git rm Chaos.Client.Rendering/SunglassesRenderer.cs Chaos.Client.Rendering/MiddleFingerRenderer.cs
```

- [ ] **Step 7: Create `CustomEmoteRegistry.cs`**

```csharp
#region
using System.Diagnostics.CodeAnalysis;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Every client-side emote, in the order the emote catalog lists them. Adding an emote means adding it here and
///     nowhere else.
/// </summary>
public static class CustomEmoteRegistry
{
    public static IReadOnlyList<ICustomEmote> All { get; } =
    [
        SunglassesEmote.Instance,
        MiddleFingerEmote.Instance
    ];

    /// <summary>Finds the custom emote that rides on this body-animation byte.</summary>
    public static bool TryGet(int bodyAnimation, [NotNullWhen(true)] out ICustomEmote? emote)
    {
        foreach (var candidate in All)
            if (candidate.BodyAnimation == bodyAnimation)
            {
                emote = candidate;

                return true;
            }

        emote = null;

        return false;
    }

    /// <summary>Finds the custom emote whose wheel icon uses this icon code.</summary>
    public static bool TryGetByPreviewFrame(int previewFrame, [NotNullWhen(true)] out ICustomEmote? emote)
    {
        foreach (var candidate in All)
            if (candidate.PreviewFrame == previewFrame)
            {
                emote = candidate;

                return true;
            }

        emote = null;

        return false;
    }
}
```

- [ ] **Step 8: Create `CustomEmoteRenderer.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Draws the custom emote an aisling is playing on top of its already-composited sprite.
/// </summary>
/// <remarks>
///     Custom emotes deliberately do not go through <see cref="AislingRenderer" />'s layer stack. That stack caches one
///     composited texture per entity and rebuilds it whenever any input changes, so art that moves every frame would
///     force a full recomposite every frame. Drawing on top costs a few quads instead.
/// </remarks>
public sealed class CustomEmoteRenderer : IDisposable
{
    private readonly CustomEmoteTextureCache Textures = new();

    public void Dispose() => Textures.Dispose();

    /// <summary>Disposes the cached textures. They rebuild on the next draw.</summary>
    public void Clear() => Textures.Clear();

    /// <param name="batch">The sprite batch the aisling was just drawn into.</param>
    /// <param name="camera">Camera used to place the aisling composite.</param>
    /// <param name="emote">The emote to draw.</param>
    /// <param name="elapsedMs">Milliseconds since the emote started.</param>
    /// <param name="flip">True when the aisling composite is drawn mirrored.</param>
    /// <param name="frameIndex">Current animation frame, used to follow the head as it bobs during a walk.</param>
    /// <param name="animSuffix">Current animation suffix, used with <paramref name="frameIndex" />.</param>
    /// <param name="bodyColor">The aisling's body colour.</param>
    /// <param name="tileCenterX">World X of the entity's tile centre.</param>
    /// <param name="tileCenterY">World Y of the entity's tile centre.</param>
    /// <param name="visualOffset">The entity's sub-tile walk offset.</param>
    /// <param name="topPadding">
    ///     The cached composite's top padding — how far <see cref="AislingRenderer" /> shifts that texture up for an
    ///     oversized sprite. The emote shifts with it or it detaches from the head.
    /// </param>
    /// <param name="alpha">Alpha the aisling itself was drawn with.</param>
    public void Draw(
        SpriteBatch batch,
        Camera camera,
        ICustomEmote emote,
        float elapsedMs,
        bool flip,
        int frameIndex,
        string animSuffix,
        int bodyColor,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset,
        int topPadding,
        float alpha)
    {
        //mirrors AislingRenderer.Draw: the composite's top-left corner in screen space
        var baseX = tileCenterX + visualOffset.X - AislingRenderer.CANVAS_CENTER_X;
        var baseY = tileCenterY + visualOffset.Y - AislingRenderer.CANVAS_CENTER_Y - topPadding;

        var context = new CustomEmoteDrawContext(
            batch,
            camera.WorldToScreen(new Vector2(baseX, baseY)),
            flip,
            CustomEmoteGeometry.HeadBobOffset(frameIndex, animSuffix),
            alpha,
            bodyColor,
            Textures);

        emote.Draw(in context, elapsedMs);
    }
}
```

- [ ] **Step 9: Rewire `UiRenderer`**

1. Add `using Chaos.Client.Rendering.CustomEmotes;` to the file's usings.
2. Replace `GetEmoteFaceTexture` with:

```csharp
    public Texture2D GetEmoteFaceTexture(int frameIndex, int bodyColor = (int)BodyColor.White)
    {
        var key = $"emote-face:{frameIndex}:{bodyColor}";

        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var epf = DataContext.AislingDrawData.EmotionsEpf;

        //the client-side emotes have no emot01 frame of their own: each icon starts from a real frame and the
        //emote paints itself onto it — see ICustomEmote.BuildIcon
        var customEmote = CustomEmoteRegistry.TryGetByPreviewFrame(frameIndex, out var found) ? found : null;
        var sourceFrame = customEmote?.IconSourceFrame ?? frameIndex;

        if (epf is null || sourceFrame < 0 || sourceFrame >= epf.Count)
            return MissingTexture;

        if (!DataContext.AislingDrawData.BodyPalettes.TryGetValue(bodyColor, out var palette)
            && !DataContext.AislingDrawData.BodyPalettes.TryGetValue((int)BodyColor.White, out palette))
            return MissingTexture;

        using var image = Graphics.RenderImage(epf[sourceFrame], palette);

        if (image is null)
            return MissingTexture;

        using var reworked = customEmote?.BuildIcon(image);

        var texture = Convert(reworked ?? image);

        Cache[key] = texture;
        EmoteContentBounds[key] = ImageUtil.FindOpaqueBounds(texture);

        return texture;
    }
```

3. Delete `FoldMiddleFinger` and `StampSunglasses`. Also delete the stray doc comment block that sits just above `FoldMiddleFinger` and begins `Returns a copy of an emot01 face image with the settled sunglasses painted on`. Try `safe_delete_symbol` first, and fall back to a regex `replace_content` if it refuses.

- [ ] **Step 10: Add the global using** to `Chaos.Client/GlobalUsings.cs`, after `global using Chaos.Client.Rendering;`:

```csharp
global using Chaos.Client.Rendering.CustomEmotes;
```

- [ ] **Step 11: Rewire `WorldEntity`**

1. Change `IsAtRest` (line 75) to:

```csharp
    public bool IsAtRest => (AnimState == EntityAnimState.Idle) && (ActiveEmoteFrame < 0) && !IsPlayingCustomEmote;
```

2. Replace the `SunglassesElapsedMs`, `IsWearingSunglasses`, `MiddleFingerRemainingMs` and `IsFlippingOff` members and their doc comments (lines 89–106) with:

```csharp
    /// <summary>
    ///     The client-side emote playing on this entity, or null. Unlike the emot01 overlay above, custom emotes are drawn
    ///     on top of the finished aisling composite rather than composited into it, so they carry their own clock instead
    ///     of a frame index.
    /// </summary>
    public ICustomEmote? ActiveCustomEmote { get; private set; }

    /// <summary>Milliseconds since <see cref="ActiveCustomEmote" /> started.</summary>
    public float CustomEmoteElapsedMs { get; set; }

    /// <summary>True while a client-side emote is playing.</summary>
    public bool IsPlayingCustomEmote => ActiveCustomEmote is not null;

    /// <summary>Starts a client-side emote from its first frame.</summary>
    public void StartCustomEmote(ICustomEmote emote)
    {
        ActiveCustomEmote = emote;
        CustomEmoteElapsedMs = 0f;
    }

    /// <summary>Ends the client-side emote, if one is playing.</summary>
    public void StopCustomEmote()
    {
        ActiveCustomEmote = null;
        CustomEmoteElapsedMs = 0f;
    }
```

- [ ] **Step 12: Rewire `WorldScreen.Update.cs`**

1. Replace the two tick blocks (lines 104–116, from `//tick the sunglasses emote` through the `MiddleFingerRemainingMs` countdown) with:

```csharp
            //tick the client-side emote and retire it once it has run its course
            if (entity.ActiveCustomEmote is { } customEmote)
            {
                entity.CustomEmoteElapsedMs += elapsedMs;

                if (entity.CustomEmoteElapsedMs >= customEmote.DurationMs)
                    entity.StopCustomEmote();
            }
```

2. In `TryPlayLocalEmote`, replace the busy check and the `switch` (lines 417–435) with:

```csharp
        if ((entity.AnimState == EntityAnimState.BodyAnim)
            || (entity.ActiveEmoteFrame >= 0)
            || entity.IsPlayingCustomEmote)
            return;

        //the client-side emotes have no emot01 frame of their own, so they never reach the overlay path below
        if (CustomEmoteRegistry.TryGet((int)anim, out var customEmote))
        {
            entity.StartCustomEmote(customEmote);

            return;
        }
```

- [ ] **Step 13: Rewire `HandleBodyAnimation`** in `WorldScreen.ServerHandlers.cs`. Replace its busy check and custom-emote `switch` (from `//emotes are body animations — ignore if…` through the closing brace of the `switch`) with:

```csharp
        //emotes are body animations — ignore if any body anim or emote overlay is already playing
        if ((entity.AnimState == EntityAnimState.BodyAnim)
            || (entity.ActiveEmoteFrame >= 0)
            || entity.IsPlayingCustomEmote)
            return;

        //the client-side emotes ride on body-animation bytes the enum leaves unused, so they never reach
        //ResolveBodyAnimParams or the emot01 overlay below — they are drawn on top of the composite instead.
        //Creature sprites have no aisling head to hang them off, so they skip them.
        if (!entity.IsRenderedAsCreatureSprite && CustomEmoteRegistry.TryGet((int)args.BodyAnimation, out var customEmote))
        {
            entity.StartCustomEmote(customEmote);

            return;
        }
```

Check the code right after the old `switch` first. It must match the old flow: creature sprites fall through to the creature path below.

- [ ] **Step 14: Rewire `WorldScreen.Draw.cs`**. Replace lines 869–903 (from `//the sunglasses ride on top of the finished composite` through the closing brace of that `if`) with:

```csharp
        //custom emotes ride on top of the finished composite rather than inside it — see CustomEmoteRenderer.
        //Only the front-facing poses show a face to hang them off; the swimming, resting and creature-form
        //paths have already returned above.
        if (isFrontFacing && (textureBottomY != 0) && entity.ActiveCustomEmote is { } customEmote)
        {
            Game.AislingRenderer.TryGetCompositeTopPadding(entity.Id, out var topPadding);

            Game.CustomEmoteRenderer.Draw(
                spriteBatch,
                Camera,
                customEmote,
                entity.CustomEmoteElapsedMs,
                flip,
                frameIndex,
                animSuffix,
                appearance.BodyColor,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset,
                topPadding,
                alpha);
        }
```

- [ ] **Step 15: Rewire `ChaosGame.cs`**

1. Replace the `SunglassesRenderer` and `MiddleFingerRenderer` properties and their `///` doc comments (around lines 100–108) with:

```csharp
    /// <summary>
    ///     Draws client-side emotes (sunglasses, middle finger, …) over an already-composited aisling. Holds only their
    ///     own small textures.
    /// </summary>
    public CustomEmoteRenderer CustomEmoteRenderer { get; } = new();
```

2. In the dispose method, replace `SunglassesRenderer.Dispose();` and `MiddleFingerRenderer.Dispose();` with `CustomEmoteRenderer.Dispose();`.

- [ ] **Step 16: Rewire `EmoteCatalog.cs`**

1. Delete the `SUNGLASSES_PREVIEW_FRAME` and `MIDDLE_FINGER_PREVIEW_FRAME` constants and their doc comments.
2. In `BuildAll`, change `new List<EmoteCatalogEntry>(35)` to `new List<EmoteCatalogEntry>(33 + CustomEmoteRegistry.All.Count)`. Then replace the two hand-added custom entries and their comment with:

```csharp
        //client-side emotes with no emot01 frame of their own — see ICustomEmote for why they ride on unused bytes
        foreach (var emote in CustomEmoteRegistry.All)
            list.Add(new EmoteCatalogEntry((BodyAnimation)emote.BodyAnimation, emote.Name, emote.PreviewFrame));
```

- [ ] **Step 17: Update the existing tests**

1. Add `using Chaos.Client.Rendering.CustomEmotes;` to `SunglassesEmoteTests.cs`, `MiddleFingerEmoteTests.cs` and `EmoteCatalogTests.cs`.
2. In `EmoteCatalogTests.cs`, replace `EmoteCatalog.MIDDLE_FINGER_PREVIEW_FRAME` with `MiddleFingerEmote.PREVIEW_FRAME`, and `EmoteCatalog.SUNGLASSES_PREVIEW_FRAME` with `SunglassesEmote.PREVIEW_FRAME`.
3. In `EmoteCatalogTests.cs`, replace the first two tests. That way they keep holding as emotes are added:

```csharp
    [Test]
    public async Task All_contains_the_33_keyboard_emotes_plus_the_client_side_ones()
    {
        EmoteCatalog.All.Should().HaveCount(33 + CustomEmoteRegistry.All.Count);
        EmoteCatalog.All.Select(e => e.Animation).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_client_side_emotes_come_last_in_registry_order()
    {
        var tail = EmoteCatalog.All.TakeLast(CustomEmoteRegistry.All.Count).ToList();

        tail.Select(e => (int)e.Animation).Should().Equal(CustomEmoteRegistry.All.Select(e => e.BodyAnimation));
        tail.Select(e => e.Name).Should().Equal(CustomEmoteRegistry.All.Select(e => e.Name));
        tail.Select(e => e.PreviewFrame).Should().Equal(CustomEmoteRegistry.All.Select(e => e.PreviewFrame));

        EmoteCatalog.TryGet((BodyAnimation)SunglassesEmote.BODY_ANIMATION, out var sunglasses).Should().BeTrue();
        sunglasses.Name.Should().Be("Sunglasses");

        EmoteCatalog.TryGet((BodyAnimation)MiddleFingerEmote.BODY_ANIMATION, out var middleFinger).Should().BeTrue();
        middleFinger.Name.Should().Be("Middle Finger");

        await Task.CompletedTask;
    }
```

- [ ] **Step 18: Build, check for leftovers, run everything**

Run: `dotnet build Chaos.Client.slnx` → 0 errors.
Run: `grep -rn "SunglassesRenderer\|MiddleFingerRenderer\|IsWearingSunglasses\|IsFlippingOff\|SunglassesElapsedMs\|MiddleFingerRemainingMs\|SUNGLASSES_PREVIEW_FRAME\|MIDDLE_FINGER_PREVIEW_FRAME\|StampSunglasses\|FoldMiddleFinger" --include=*.cs .` → no output. Doc comments count too, so fix any that still name the old types.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/CustomEmotes/SunglassesEmote.cs", "Chaos.Client.Rendering/CustomEmotes/MiddleFingerEmote.cs", "Chaos.Client.Rendering/CustomEmotes/CustomEmoteRegistry.cs", "Chaos.Client.Rendering/CustomEmotes/CustomEmoteRenderer.cs", "Chaos.Client.Rendering/UiRenderer.cs", "Chaos.Client/GlobalUsings.cs", "Chaos.Client/Models/WorldEntity.cs", "Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Chaos.Client/ChaosGame.cs", "Chaos.Client/Definitions/EmoteCatalog.cs", "Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs", "Tests/Chaos.Client.Tests/EmoteCatalogTests.cs", "Tests/Chaos.Client.Tests/SunglassesEmoteTests.cs", "Tests/Chaos.Client.Tests/MiddleFingerEmoteTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["grep for old per-emote names prints nothing", "Registry is [Sunglasses, MiddleFinger]", "Sunglasses layers match old renderer placement flipped and unflipped", "Middle finger bubble drawn at (LAYER_OFFSET_PADDING,0) without head bob", "Existing emote tests pass with only using/constant changes", "Full suite passes and solution builds"], "modelTier": "standard"}
```

---

### Task 3: Draw the five new emotes

**Goal:** Add `HeartEyesEmote`, `HaloEmote`, `LightbulbEmote`, `AngerSteamEmote` and `PartyHatEmote` as `PixelArtEmote`s with their timing logic and tests. They are not registered yet.

**Files:**
- Create: `Chaos.Client.Rendering/CustomEmotes/HeartEyesEmote.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/HaloEmote.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/LightbulbEmote.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/AngerSteamEmote.cs`
- Create: `Chaos.Client.Rendering/CustomEmotes/PartyHatEmote.cs`
- Test: `Tests/Chaos.Client.Tests/NewCustomEmoteTests.cs`

**Acceptance Criteria:**
- [ ] Each emote's `Compose` returns an empty list at `DurationMs` and a non-empty list at `IconTimeMs`. Every icon layer has opacity 1.
- [ ] Every colour in every art piece the five emotes use has at least one channel below 220.
- [ ] Art keys are unique: one key never maps to two different `PixelArt` instances.
- [ ] `Compose(t, 1)` puts every layer one row lower than `Compose(t, 0)`.
- [ ] Heart Eyes alternates big/small every 125 ms: 4 big→small switches in the first 1000 ms.
- [ ] Halo fades in from 0 to 1 over 200 ms, bobs between 0 and −1 rows, and its glint visits columns 2–8 in order between 400 and 1000 ms.
- [ ] Lightbulb rises 3 rows to 0 over 150 ms, turns on exactly twice before 620 ms, stays lit from 620 ms, and shows rays only from 620 ms.
- [ ] Anger Steam gives 2 puffs at 0 ms and 4 at 500 ms and none at 1500 ms. Left puffs drift left, right puffs drift right, and puffs only grow and fade with age.
- [ ] Party Hat falls from −10 rows to 0 by 240 ms. It shows 8 confetti pieces from landing, and none from 640 ms.
- [ ] `NewCustomEmoteTests` pass.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/NewCustomEmoteTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** in `Tests/Chaos.Client.Tests/NewCustomEmoteTests.cs`:

```csharp
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class NewCustomEmoteTests
{
    private static readonly PixelArtEmote[] NewEmotes =
    [
        HeartEyesEmote.Instance,
        HaloEmote.Instance,
        LightbulbEmote.Instance,
        AngerSteamEmote.Instance,
        PartyHatEmote.Instance
    ];

    /// <summary>Every layer an emote shows over its whole run, sampled every 5ms.</summary>
    private static IEnumerable<PixelLayer> AllLayers(PixelArtEmote emote)
    {
        for (var t = 0f; t < emote.DurationMs; t += 5f)
            foreach (var layer in emote.Compose(t, 0))
                yield return layer;
    }

    #region shared
    [Test]
    public async Task Bytes_and_icon_codes_are_the_agreed_ones()
    {
        NewEmotes.Select(e => e.BodyAnimation).Should().Equal(20, 2, 3, 4, 5);
        NewEmotes.Select(e => e.PreviewFrame).Should().Equal(1002, 1003, 1004, 1005, 1006);
        NewEmotes.Select(e => e.DurationMs).Should().Equal(1500f, 2000f, 1600f, 1600f, 1800f);

        foreach (var emote in NewEmotes)
            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation).Should().BeFalse(emote.Name);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_emote_is_gone_at_its_end_and_opaque_on_its_icon()
    {
        foreach (var emote in NewEmotes)
        {
            emote.Compose(emote.DurationMs, 0).Should().BeEmpty(emote.Name);

            var icon = emote.Compose(emote.IconTimeMs, 0);
            icon.Should().NotBeEmpty(emote.Name);
            icon.Should().OnlyContain(l => l.Opacity == 1f, emote.Name);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_colour_is_near_white_so_the_icon_crop_keeps_it()
    {
        //ImageUtil.FindHeadBounds skips pixels whose R, G and B are all >= 220 (speech-bubble white)
        foreach (var emote in NewEmotes)
        foreach (var art in AllLayers(emote).Select(l => l.Art).Distinct())
        foreach (var color in art.Colors())
            (color.R >= 220 && color.G >= 220 && color.B >= 220)
                .Should()
                .BeFalse($"{art.Key} paints {color}, which the icon crop would drop");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Art_keys_are_unique_across_emotes()
    {
        var byKey = new Dictionary<string, PixelArt>();

        foreach (var art in NewEmotes.SelectMany(AllLayers).Select(l => l.Art).Distinct())
        {
            if (byKey.TryGetValue(art.Key, out var existing))
                existing.Should().BeSameAs(art, $"key '{art.Key}' names two different pieces of art");

            byKey[art.Key] = art;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_layer_follows_the_head_bob()
    {
        foreach (var emote in NewEmotes)
            for (var t = 0f; t < emote.DurationMs; t += 50f)
            {
                var still = emote.Compose(t, 0);
                var bobbed = emote.Compose(t, 1);

                bobbed.Select(l => l.Y).Should().Equal(still.Select(l => l.Y + 1), $"{emote.Name} at {t}ms");
                bobbed.Select(l => l.X).Should().Equal(still.Select(l => l.X), $"{emote.Name} at {t}ms");
            }

        await Task.CompletedTask;
    }
    #endregion

    #region heart eyes
    [Test]
    public async Task Hearts_pulse_about_four_times_a_second()
    {
        HeartEyesEmote.IsBig(0f).Should().BeTrue();
        HeartEyesEmote.IsBig(HeartEyesEmote.PULSE_MS).Should().BeFalse();
        HeartEyesEmote.IsBig(HeartEyesEmote.PULSE_MS * 2).Should().BeTrue();

        var shrinks = 0;

        for (var t = 1f; t < 1000f; t += 1f)
            if (HeartEyesEmote.IsBig(t - 1f) && !HeartEyesEmote.IsBig(t))
                shrinks++;

        shrinks.Should().Be(4);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Big_and_small_hearts_share_a_centre()
    {
        var big = HeartEyesEmote.Instance.Compose(0f, 0).Single();
        var small = HeartEyesEmote.Instance.Compose(HeartEyesEmote.PULSE_MS, 0).Single();

        (big.X + big.Art.Width).Should().Be(small.X + small.Art.Width);
        big.Art.Width.Should().Be(small.Art.Width);
        big.Y.Should().BeLessThan(small.Y);
        await Task.CompletedTask;
    }
    #endregion

    #region halo
    [Test]
    public async Task The_halo_fades_in_then_holds()
    {
        HaloEmote.Opacity(0f).Should().Be(0f);
        HaloEmote.Opacity(HaloEmote.FADE_IN_MS / 2f).Should().BeApproximately(0.5f, 0.001f);
        HaloEmote.Opacity(HaloEmote.FADE_IN_MS).Should().Be(1f);
        HaloEmote.Opacity(HaloEmote.TOTAL_MS - 1f).Should().Be(1f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_halo_bobs_one_row()
    {
        var rows = new HashSet<int>();

        for (var t = 0f; t < HaloEmote.TOTAL_MS; t += 10f)
            rows.Add(HaloEmote.BobRows(t));

        rows.Should().BeEquivalentTo(new[] { 0, -1 });
        HaloEmote.BobRows(0f).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_glint_slides_across_once()
    {
        HaloEmote.GlintColumn(HaloEmote.GLINT_START_MS - 1f).Should().Be(-1);
        HaloEmote.GlintColumn(HaloEmote.GLINT_END_MS).Should().Be(-1);

        var visited = new List<int>();

        for (var t = HaloEmote.GLINT_START_MS; t < HaloEmote.GLINT_END_MS; t += 1f)
        {
            var column = HaloEmote.GlintColumn(t);

            if (visited.Count == 0 || visited[^1] != column)
                visited.Add(column);
        }

        visited.Should().Equal(2, 3, 4, 5, 6, 7, 8);
        await Task.CompletedTask;
    }
    #endregion

    #region lightbulb
    [Test]
    public async Task The_bulb_rises_into_place()
    {
        LightbulbEmote.RiseOffset(0f).Should().Be(LightbulbEmote.RISE_ROWS);
        LightbulbEmote.RiseOffset(LightbulbEmote.RISE_MS).Should().Be(0);

        var previous = int.MaxValue;

        for (var t = 0f; t <= LightbulbEmote.RISE_MS; t += 5f)
        {
            var offset = LightbulbEmote.RiseOffset(t);
            offset.Should().BeLessThanOrEqualTo(previous);
            previous = offset;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_bulb_flickers_on_twice_then_stays_lit()
    {
        var turnOns = 0;

        for (var t = 1f; t < LightbulbEmote.STEADY_MS; t += 1f)
            if (!LightbulbEmote.IsLit(t - 1f) && LightbulbEmote.IsLit(t))
                turnOns++;

        turnOns.Should().Be(2);

        for (var t = LightbulbEmote.STEADY_MS; t < LightbulbEmote.TOTAL_MS; t += 10f)
            LightbulbEmote.IsLit(t).Should().BeTrue();

        LightbulbEmote.IsLit(LightbulbEmote.RISE_MS).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rays_only_show_once_the_bulb_is_steady()
    {
        LightbulbEmote.Instance.Compose(LightbulbEmote.STEADY_MS - 1f, 0).Should().ContainSingle();
        LightbulbEmote.Instance.Compose(LightbulbEmote.STEADY_MS, 0).Should().HaveCount(2);
        await Task.CompletedTask;
    }
    #endregion

    #region anger steam
    [Test]
    public async Task Steam_puffs_come_in_pairs_and_are_gone_by_the_end()
    {
        AngerSteamEmote.Puffs(0f).Should().HaveCount(2);
        AngerSteamEmote.Puffs(500f).Should().HaveCount(4);
        AngerSteamEmote.Puffs(1500f).Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_side_drifts_outward_and_up()
    {
        for (var t = 0f; t < AngerSteamEmote.TOTAL_MS; t += 10f)
            foreach (var puff in AngerSteamEmote.Puffs(t))
            {
                if (puff.IsLeft)
                    puff.CenterX.Should().BeLessThanOrEqualTo(AngerSteamEmote.LEFT_ORIGIN_X);
                else
                    puff.CenterX.Should().BeGreaterThanOrEqualTo(AngerSteamEmote.RIGHT_ORIGIN_X);

                puff.CenterY.Should().BeLessThanOrEqualTo(AngerSteamEmote.ORIGIN_Y);
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_puff_only_grows_and_fades_as_it_ages()
    {
        var previousStage = -1;
        var previousOpacity = float.MaxValue;

        //follow the first left-hand puff through its life
        for (var t = 0f; t < AngerSteamEmote.PUFF_LIFE_MS; t += 5f)
        {
            var puff = AngerSteamEmote.Puffs(t).First(p => p.IsLeft);

            puff.Stage.Should().BeGreaterThanOrEqualTo(previousStage);
            puff.Opacity.Should().BeLessThanOrEqualTo(previousOpacity);
            puff.Opacity.Should().BeInRange(0f, 1f);

            previousStage = puff.Stage;
            previousOpacity = puff.Opacity;
        }

        previousStage.Should().Be(2);
        await Task.CompletedTask;
    }
    #endregion

    #region party hat
    [Test]
    public async Task The_hat_falls_onto_the_head()
    {
        PartyHatEmote.RowOffset(0f).Should().Be(PartyHatEmote.START_ROW_OFFSET);
        PartyHatEmote.RowOffset(PartyHatEmote.DROP_MS).Should().Be(0);

        var previous = int.MinValue;

        for (var t = 0f; t <= PartyHatEmote.DROP_MS; t += 5f)
        {
            var offset = PartyHatEmote.RowOffset(t);
            offset.Should().BeGreaterThanOrEqualTo(previous);
            previous = offset;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confetti_bursts_on_landing_and_is_gone_after()
    {
        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS - 1f, 0).Should().ContainSingle();
        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS, 0).Should().HaveCount(1 + 8);

        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + PartyHatEmote.CONFETTI_MS, 0)
                     .Should()
                     .ContainSingle();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confetti_goes_up_before_it_comes_down()
    {
        var landing = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS, 0).Skip(1).ToList();
        var early = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + 60f, 0).Skip(1).ToList();
        var late = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + PartyHatEmote.CONFETTI_MS - 1f, 0).Skip(1).ToList();

        early.Select(l => l.Y).Average().Should().BeLessThan(landing.Select(l => l.Y).Average());
        late.Select(l => l.Y).Average().Should().BeGreaterThan(early.Select(l => l.Y).Average());
        await Task.CompletedTask;
    }
    #endregion
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/NewCustomEmoteTests/*"`
Expected: a build error. The five emote types do not exist.

- [ ] **Step 3: Create `HeartEyesEmote.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Heart eyes: two red hearts cover the eyes and pulse between a big and a small size.
/// </summary>
public sealed class HeartEyesEmote : PixelArtEmote
{
    /// <summary>Byte 20 is the last gap between Mouth (17) and BlowKiss (21); 18 and 19 are the sunglasses and middle finger.</summary>
    public const int BODY_ANIMATION = 20;

    public const int PREVIEW_FRAME = 1002;
    public const float TOTAL_MS = 1500f;

    /// <summary>How long each heart size is held. Big, small, big… — four full pulses a second.</summary>
    public const float PULSE_MS = 125f;

    /// <summary>
    ///     Composite X of both maps' left column. The hearts centre on x54 and x60 and leave x57 clear between them, so
    ///     they read as two hearts rather than one blob.
    /// </summary>
    public const int LEFT_X = 52;

    /// <summary>The big hearts span rows 31-34, covering the eyes (32-33) and the brow above them.</summary>
    public const int BIG_TOP_Y = 31;

    /// <summary>The small hearts span rows 32-34, centred on the eyes.</summary>
    public const int SMALL_TOP_Y = 32;

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['r'] = new Color(214, 36, 64),
        ['h'] = new Color(255, 122, 150)
    };

    public static IReadOnlyList<string> BigPixels { get; } =
    [
        "rr.rr.rr.rr",
        "rhrrr.rhrrr",
        ".rrr...rrr.",
        "..r.....r.."
    ];

    public static IReadOnlyList<string> SmallPixels { get; } =
    [
        ".r.r...r.r.",
        ".hrr...hrr.",
        "..r.....r.."
    ];

    private static readonly PixelArt BigArt = PixelArt.FromPalette("heart-eyes:big", BigPixels, Palette);
    private static readonly PixelArt SmallArt = PixelArt.FromPalette("heart-eyes:small", SmallPixels, Palette);

    public static HeartEyesEmote Instance { get; } = new();

    private HeartEyesEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Heart Eyes";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The first beat: big hearts.</summary>
    public override float IconTimeMs => 0f;

    /// <summary>True while the big hearts are showing. The emote opens on a big beat.</summary>
    public static bool IsBig(float elapsedMs) => (int)(Math.Max(0f, elapsedMs) / PULSE_MS) % 2 == 0;

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        if (IsBig(elapsedMs))
            return [new PixelLayer(BigArt, LEFT_X, BIG_TOP_Y + headBobRows)];

        return [new PixelLayer(SmallArt, LEFT_X, SMALL_TOP_Y + headBobRows)];
    }
}
```

- [ ] **Step 4: Create `HaloEmote.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A halo: a gold ring fades in just above the head, bobs a row, and a glint slides across its front once.
/// </summary>
public sealed class HaloEmote : PixelArtEmote
{
    /// <summary>Byte 2 is unused by <c>BodyAnimation</c> (Assail is 1, HandsUp is 6) and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 2;

    public const int PREVIEW_FRAME = 1003;
    public const float TOTAL_MS = 2000f;
    public const float FADE_IN_MS = 200f;

    /// <summary>How long each half of the bob lasts, after the fade-in.</summary>
    public const float BOB_HALF_MS = 300f;

    public const float GLINT_START_MS = 400f;
    public const float GLINT_END_MS = 1000f;

    /// <summary>The ring is 11 wide, x50-60 — the head's own width.</summary>
    public const int LEFT_X = CustomEmoteGeometry.HEAD_LEFT_X;

    /// <summary>The ring spans rows 19-21, leaving rows 22-23 clear above the head's top row (24).</summary>
    public const int TOP_Y = 19;

    /// <summary>First and last ring columns the glint crosses, along the front (bottom) row.</summary>
    private const int GLINT_FIRST_COLUMN = 2;

    private const int GLINT_LAST_COLUMN = 8;

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['G'] = new Color(255, 212, 72),
        ['g'] = new Color(206, 150, 36)
    };

    public static IReadOnlyList<string> RingPixels { get; } =
    [
        "..GGGGGGG..",
        "GG.......GG",
        "..ggggggg.."
    ];

    private static readonly PixelArt RingArt = PixelArt.FromPalette("halo:ring", RingPixels, Palette);
    private static readonly PixelArt GlintArt = PixelArt.Dot("halo:glint", new Color(255, 246, 196));

    public static HaloEmote Instance { get; } = new();

    private HaloEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Halo";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Fully faded in, resting, glint gone.</summary>
    public override float IconTimeMs => GLINT_END_MS;

    /// <summary>The ring's opacity: 0 at the start, 1 once the fade-in is done.</summary>
    public static float Opacity(float elapsedMs) => Math.Clamp(elapsedMs / FADE_IN_MS, 0f, 1f);

    /// <summary>Rows the ring is lifted: 0 during the fade-in, then alternating 0 and -1.</summary>
    public static int BobRows(float elapsedMs)
    {
        if (elapsedMs < FADE_IN_MS)
            return 0;

        return (int)((elapsedMs - FADE_IN_MS) / BOB_HALF_MS) % 2 == 0 ? 0 : -1;
    }

    /// <summary>Ring column the glint is on, or -1 when it isn't showing.</summary>
    public static int GlintColumn(float elapsedMs)
    {
        if ((elapsedMs < GLINT_START_MS) || (elapsedMs >= GLINT_END_MS))
            return -1;

        const int STEPS = GLINT_LAST_COLUMN - GLINT_FIRST_COLUMN + 1;
        var step = (int)((elapsedMs - GLINT_START_MS) / ((GLINT_END_MS - GLINT_START_MS) / STEPS));

        return GLINT_FIRST_COLUMN + Math.Min(step, STEPS - 1);
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + BobRows(elapsedMs) + headBobRows;
        var ring = new PixelLayer(RingArt, LEFT_X, top, Opacity(elapsedMs));
        var glint = GlintColumn(elapsedMs);

        if (glint < 0)
            return [ring];

        return [ring, new PixelLayer(GlintArt, LEFT_X + glint, top + RingPixels.Count - 1)];
    }
}
```

- [ ] **Step 5: Create `LightbulbEmote.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     An idea: a grey bulb pops up above the head, flickers on twice, then stays lit yellow with short rays.
/// </summary>
public sealed class LightbulbEmote : PixelArtEmote
{
    /// <summary>Byte 3 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 3;

    public const int PREVIEW_FRAME = 1004;
    public const float TOTAL_MS = 1600f;

    /// <summary>How long the bulb takes to rise into place.</summary>
    public const float RISE_MS = 150f;

    /// <summary>Rows below its resting place the bulb starts from. More than 3 would put it on the scalp.</summary>
    public const int RISE_ROWS = 3;

    /// <summary>From here on the bulb is lit and its rays show.</summary>
    public const float STEADY_MS = 620f;

    /// <summary>The bulb is 7 wide, x52-58, centred on the head.</summary>
    public const int LEFT_X = 52;

    /// <summary>The bulb is 10 tall, rows 12-21, leaving rows 22-23 clear above the head.</summary>
    public const int TOP_Y = 12;

    /// <summary>The rays map is 3 wider than the bulb on each side and starts 3 rows above it.</summary>
    private const int RAYS_MARGIN = 3;

    /// <summary>The two flickers before the bulb holds steady: lit from Start to End.</summary>
    private static readonly (float Start, float End)[] Flickers = [(300f, 380f), (460f, 540f)];

    public static IReadOnlyList<string> BulbPixels { get; } =
    [
        "..ggg..",
        ".ghggg.",
        "ghggggg",
        "ggggggg",
        "ggggggg",
        ".ggggg.",
        "..ggg..",
        "..mmm..",
        "..MMM..",
        "...m..."
    ];

    public static IReadOnlyList<string> RaysPixels { get; } =
    [
        "......y......",
        ".y....y....y.",
        "..y.......y..",
        ".............",
        ".............",
        "yy.........yy",
        "............."
    ];

    private static readonly Color Metal = new(118, 118, 128);
    private static readonly Color MetalDark = new(84, 84, 94);

    private static readonly PixelArt UnlitArt = PixelArt.FromPalette(
        "lightbulb:unlit",
        BulbPixels,
        new Dictionary<char, Color>
        {
            ['g'] = new(150, 152, 164),
            ['h'] = new(196, 198, 210),
            ['m'] = Metal,
            ['M'] = MetalDark
        });

    private static readonly PixelArt LitArt = PixelArt.FromPalette(
        "lightbulb:lit",
        BulbPixels,
        new Dictionary<char, Color>
        {
            ['g'] = new(255, 222, 82),
            ['h'] = new(255, 244, 176),
            ['m'] = Metal,
            ['M'] = MetalDark
        });

    private static readonly PixelArt RaysArt = PixelArt.FromPalette(
        "lightbulb:rays",
        RaysPixels,
        new Dictionary<char, Color> { ['y'] = new(255, 236, 120) });

    public static LightbulbEmote Instance { get; } = new();

    private LightbulbEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Lightbulb";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Lit, with rays.</summary>
    public override float IconTimeMs => STEADY_MS;

    /// <summary>Rows below its resting place the bulb sits: <see cref="RISE_ROWS" /> at the start, 0 once risen.</summary>
    public static int RiseOffset(float elapsedMs)
    {
        if (elapsedMs >= RISE_MS)
            return 0;

        var progress = Math.Clamp(elapsedMs / RISE_MS, 0f, 1f);

        return (int)Math.Ceiling(RISE_ROWS * (1f - progress));
    }

    /// <summary>True while the bulb is lit: during each flicker, and steadily from <see cref="STEADY_MS" />.</summary>
    public static bool IsLit(float elapsedMs)
    {
        if (elapsedMs >= STEADY_MS)
            return true;

        foreach (var (start, end) in Flickers)
            if ((elapsedMs >= start) && (elapsedMs < end))
                return true;

        return false;
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + RiseOffset(elapsedMs) + headBobRows;
        var bulb = new PixelLayer(IsLit(elapsedMs) ? LitArt : UnlitArt, LEFT_X, top);

        if (elapsedMs < STEADY_MS)
            return [bulb];

        return [bulb, new PixelLayer(RaysArt, LEFT_X - RAYS_MARGIN, top - RAYS_MARGIN)];
    }
}
```

- [ ] **Step 6: Create `AngerSteamEmote.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Fuming: puffs of steam rise from both sides of the head at ear height, drifting outward, growing and fading.
/// </summary>
public sealed class AngerSteamEmote : PixelArtEmote
{
    /// <summary>Byte 4 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 4;

    public const int PREVIEW_FRAME = 1005;
    public const float TOTAL_MS = 1600f;

    /// <summary>How long one puff lives, from ear to gone.</summary>
    public const float PUFF_LIFE_MS = 700f;

    /// <summary>Where the left-hand puffs are born: two columns outside the head (x50-60), at ear height.</summary>
    public const int LEFT_ORIGIN_X = CustomEmoteGeometry.HEAD_LEFT_X - 2;

    /// <summary>Where the right-hand puffs are born.</summary>
    public const int RIGHT_ORIGIN_X = CustomEmoteGeometry.HEAD_RIGHT_X + 2;

    public const int ORIGIN_Y = 30;

    /// <summary>Rows a puff rises over its life.</summary>
    public const int RISE_ROWS = 8;

    /// <summary>Columns a puff drifts away from the head over its life.</summary>
    public const int DRIFT_COLUMNS = 4;

    /// <summary>Fraction of its life after which a puff starts to fade.</summary>
    public const float FADE_FROM = 0.6f;

    /// <summary>One puff per side starts at each of these moments.</summary>
    private static readonly float[] PuffStartsMs = [0f, 400f, 800f];

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['p'] = new Color(212, 212, 218),
        ['q'] = new Color(168, 168, 180)
    };

    /// <summary>A puff's three sizes, smallest first. The bottom row is shaded.</summary>
    private static readonly PixelArt[] Stages =
    [
        PixelArt.FromPalette("anger-steam:small", [".p.", "ppp", ".q."], Palette),
        PixelArt.FromPalette("anger-steam:medium", [".pp.", "pppp", "pppp", ".qq."], Palette),
        PixelArt.FromPalette("anger-steam:large", [".ppp.", "ppppp", "ppppp", ".qqq."], Palette)
    ];

    public static AngerSteamEmote Instance { get; } = new();

    private AngerSteamEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Anger Steam";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The first pair of puffs at medium size, still fully opaque.</summary>
    public override float IconTimeMs => 250f;

    /// <summary>One puff at a moment in its life.</summary>
    /// <param name="IsLeft">True for the puffs on the left of the unflipped sprite.</param>
    /// <param name="CenterX">Composite X of the puff's centre, before head bob.</param>
    /// <param name="CenterY">Composite Y of the puff's centre, before head bob.</param>
    /// <param name="Stage">Index into the puff sizes: 0 small, 1 medium, 2 large.</param>
    /// <param name="Opacity">1 until the puff starts to fade, then down to 0.</param>
    public readonly record struct Puff(bool IsLeft, int CenterX, int CenterY, int Stage, float Opacity);

    /// <summary>Every puff alive at this moment, left and right of each pair together.</summary>
    public static IEnumerable<Puff> Puffs(float elapsedMs)
    {
        foreach (var start in PuffStartsMs)
        {
            var age = elapsedMs - start;

            if ((age < 0f) || (age >= PUFF_LIFE_MS))
                continue;

            var life = age / PUFF_LIFE_MS;
            var stage = Math.Min(Stages.Length - 1, (int)(life * Stages.Length));
            var rise = (int)MathF.Round(RISE_ROWS * life);
            var drift = (int)MathF.Round(DRIFT_COLUMNS * life);
            var opacity = life < FADE_FROM ? 1f : Math.Clamp(1f - (life - FADE_FROM) / (1f - FADE_FROM), 0f, 1f);

            yield return new Puff(true, LEFT_ORIGIN_X - drift, ORIGIN_Y - rise, stage, opacity);
            yield return new Puff(false, RIGHT_ORIGIN_X + drift, ORIGIN_Y - rise, stage, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var layers = new List<PixelLayer>(6);

        foreach (var puff in Puffs(elapsedMs))
        {
            var art = Stages[puff.Stage];

            layers.Add(
                new PixelLayer(art, puff.CenterX - art.Width / 2, puff.CenterY - art.Height / 2 + headBobRows, puff.Opacity));
        }

        return layers;
    }
}
```

`Compose` at a moment with no live puffs returns an empty list, but only after 1500 ms. The shared test only needs the list to be non-empty at `IconTimeMs` (250 ms) and empty at `DurationMs`.

- [ ] **Step 7: Create `PartyHatEmote.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Party time: a striped cone hat falls onto the head, and a burst of confetti flies off its tip when it lands.
/// </summary>
public sealed class PartyHatEmote : PixelArtEmote
{
    /// <summary>Byte 5 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 5;

    public const int PREVIEW_FRAME = 1006;
    public const float TOTAL_MS = 1800f;

    /// <summary>How long the hat takes to fall — the same as the sunglasses.</summary>
    public const float DROP_MS = 240f;

    /// <summary>Rows above its resting place the hat starts from.</summary>
    public const int START_ROW_OFFSET = -10;

    /// <summary>How long the confetti flies, from the landing.</summary>
    public const float CONFETTI_MS = 400f;

    /// <summary>The hat is 9 wide, x51-59, centred on the head.</summary>
    public const int LEFT_X = 51;

    /// <summary>The hat is 10 tall, rows 16-25. Its brim sits over the head's top rows (24-25).</summary>
    public const int TOP_Y = 16;

    /// <summary>Downward pull on the confetti, in pixels per second squared.</summary>
    private const float GRAVITY = 420f;

    /// <summary>Where the confetti comes from: the cone's tip, relative to the hat's top-left.</summary>
    private const int TIP_OFFSET_X = 4;

    private const int TIP_OFFSET_Y = 2;

    private static readonly Color Pink = new(236, 82, 152);
    private static readonly Color Blue = new(78, 146, 236);

    public static IReadOnlyList<string> HatPixels { get; } =
    [
        "....y....",
        "...yYy...",
        "....a....",
        "...aaa...",
        "...bbb...",
        "..bbbbb..",
        "..aaaaa..",
        ".aaaaaaa.",
        ".bbbbbbb.",
        "eeeeeeeee"
    ];

    private static readonly PixelArt HatArt = PixelArt.FromPalette(
        "party-hat:hat",
        HatPixels,
        new Dictionary<char, Color>
        {
            ['y'] = new(255, 226, 92),
            ['Y'] = new(232, 178, 40),
            ['a'] = Pink,
            ['b'] = Blue,
            ['e'] = new(250, 200, 60)
        });

    //one instance per colour: an art key must never name two different PixelArt objects
    private static readonly PixelArt PinkDot = PixelArt.Dot("party-hat:confetti-pink", Pink);
    private static readonly PixelArt BlueDot = PixelArt.Dot("party-hat:confetti-blue", Blue);

    /// <summary>Each confetti piece: launch velocity in pixels per second, and its colour.</summary>
    private static readonly (float Vx, float Vy, PixelArt Art)[] Confetti =
    [
        (-55f, -70f, PinkDot),
        (55f, -70f, BlueDot),
        (-30f, -95f, PixelArt.Dot("party-hat:confetti-yellow", new Color(255, 214, 64))),
        (30f, -95f, PixelArt.Dot("party-hat:confetti-green", new Color(86, 200, 112))),
        (-75f, -30f, PixelArt.Dot("party-hat:confetti-orange", new Color(250, 146, 52))),
        (75f, -30f, PixelArt.Dot("party-hat:confetti-purple", new Color(164, 96, 224))),
        (-12f, -110f, BlueDot),
        (12f, -110f, PinkDot)
    ];

    public static PartyHatEmote Instance { get; } = new();

    private PartyHatEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Party Hat";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The hat on the head, the confetti gone.</summary>
    public override float IconTimeMs => DROP_MS + CONFETTI_MS;

    /// <summary>Rows above its resting place the hat sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once landed.</summary>
    public static int RowOffset(float elapsedMs)
    {
        if (elapsedMs >= DROP_MS)
            return 0;

        var progress = Math.Clamp(elapsedMs / DROP_MS, 0f, 1f);

        //ceil rather than round, as with the sunglasses: the hat only reaches row offset 0 at the landing itself
        var offset = (int)Math.Ceiling(START_ROW_OFFSET * (1f - progress));

        return Math.Clamp(offset, START_ROW_OFFSET, 0);
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + RowOffset(elapsedMs) + headBobRows;
        var layers = new List<PixelLayer>(1 + Confetti.Length) { new(HatArt, LEFT_X, top) };
        var sinceLanding = elapsedMs - DROP_MS;

        if ((sinceLanding < 0f) || (sinceLanding >= CONFETTI_MS))
            return layers;

        var seconds = sinceLanding / 1000f;

        //the last 30% of the flight fades out
        var opacity = Math.Clamp((CONFETTI_MS - sinceLanding) / (CONFETTI_MS * 0.3f), 0f, 1f);
        var tipX = LEFT_X + TIP_OFFSET_X;
        var tipY = TOP_Y + TIP_OFFSET_Y + headBobRows;

        foreach (var (vx, vy, art) in Confetti)
        {
            var x = tipX + (int)MathF.Round(vx * seconds);
            var y = tipY + (int)MathF.Round(vy * seconds + 0.5f * GRAVITY * seconds * seconds);

            layers.Add(new PixelLayer(art, x, y, opacity));
        }

        return layers;
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet build Chaos.Client.slnx` → 0 errors.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/NewCustomEmoteTests/*"` → 19 passed, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/CustomEmotes/HeartEyesEmote.cs", "Chaos.Client.Rendering/CustomEmotes/HaloEmote.cs", "Chaos.Client.Rendering/CustomEmotes/LightbulbEmote.cs", "Chaos.Client.Rendering/CustomEmotes/AngerSteamEmote.cs", "Chaos.Client.Rendering/CustomEmotes/PartyHatEmote.cs", "Tests/Chaos.Client.Tests/NewCustomEmoteTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/NewCustomEmoteTests/*\"", "acceptanceCriteria": ["Empty at DurationMs, non-empty and opaque at IconTimeMs", "No near-white colours", "Unique art keys", "Every layer follows head bob", "Heart Eyes 4 shrinks per second", "Halo fade/bob/glint behaviour", "Lightbulb rise/flicker/rays behaviour", "Anger Steam puff counts, drift and ageing", "Party Hat drop and confetti window", "NewCustomEmoteTests pass"], "modelTier": "standard"}
```

---

### Task 4: Preview sheet and art sign-off

**Goal:** Render every new emote over a real aisling face into one PNG. The user approves the art (or asks for changes) before the emotes are wired in.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- Create (throwaway, not committed): `<scratchpad>/EmotePreview/EmotePreview.csproj`, `<scratchpad>/EmotePreview/Program.cs`
- Modify only if the user asks for changes: the five emote files from Task 3 and `NewCustomEmoteTests.cs`

**Acceptance Criteria:**
- [ ] `<scratchpad>/EmotePreview/emote-preview.png` exists. It has 10 rows (5 emotes × unflipped/flipped) and 10 time columns, at 4× scale.
- [ ] The coordinator has looked at the PNG itself (Read tool) before showing it.
- [ ] The user has explicitly approved the art in chat. Any requested changes are made, re-rendered and approved.
- [ ] If art changed, `NewCustomEmoteTests` still pass.

**Verify:** `dotnet run --project <scratchpad>/EmotePreview -- <scratchpad>/EmotePreview/emote-preview.png` → prints the output path. User approval is quoted in the close note.

**Steps:**

- [ ] **Step 1: Create the preview project.** `<scratchpad>` is this session's scratchpad directory. Write `EmotePreview.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos.Client.Rendering\Chaos.Client.Rendering.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write `Program.cs`.** It reads emot01 and the body palette straight from the game archives with DALib. It never touches `DataContext`, so no graphics device is needed:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using DALib.Data;
using DALib.Drawing;
using SkiaSharp;

var dataPath = Environment.GetEnvironmentVariable("DA_PATH") ?? @"C:\Users\Michael\Documents\Unora\Unora Files";
var outPath = args.Length > 0 ? args[0] : "emote-preview.png";

using var legend = DataArchive.FromFile(Path.Combine(dataPath, "legend.dat"));
using var khanpal = DataArchive.FromFile(Path.Combine(dataPath, "khanpal.dat"));

var emot = EpfFile.FromArchive("emot01.epf", legend);
var palettes = Palette.FromArchive("palm", khanpal);
var palette = palettes.TryGetValue(0, out var white) ? white : palettes.Values.First();
using var face = Graphics.RenderImage(emot[CustomEmoteGeometry.PLAIN_FACE_FRAME], palette);

PixelArtEmote[] emotes =
[
    HeartEyesEmote.Instance,
    HaloEmote.Instance,
    LightbulbEmote.Instance,
    AngerSteamEmote.Instance,
    PartyHatEmote.Instance
];

const int STEPS = 10;
const int SCALE = 4;

//crop window in composite space: wide enough for confetti and steam, tall enough for the bulb's rays
const int CROP_X = 28;
const int CROP_Y = 4;
const int CROP_W = 54;
const int CROP_H = 44;

//110 wide so a horizontal flip about the canvas centre maps x to AislingRenderer.MirrorX(x) = 109 - x
const int CANVAS_W = AislingRenderer.FLIP_PIVOT_X * 2;
const int CANVAS_H = 85;

var cellW = CROP_W * SCALE;
var cellH = CROP_H * SCALE;

using var sheet = new SKBitmap(cellW * STEPS, cellH * emotes.Length * 2);
using var sheetCanvas = new SKCanvas(sheet);
sheetCanvas.Clear(new SKColor(40, 44, 52));

var nearest = new SKSamplingOptions(SKFilterMode.Nearest);
var crop = SKRect.Create(CROP_X, CROP_Y, CROP_W, CROP_H);

for (var e = 0; e < emotes.Length; e++)
for (var s = 0; s < STEPS; s++)
{
    var t = emotes[e].DurationMs * s / STEPS;

    using var frame = new SKBitmap(CANVAS_W, CANVAS_H);

    using (var canvas = new SKCanvas(frame))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(face, AislingRenderer.LAYER_OFFSET_PADDING, 0);

        foreach (var layer in emotes[e].Compose(t, 0))
        {
            using var art = PixelSprite.ToBitmap(layer.Art);

            using var paint = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity * 255f, 0f, 255f))
            };

            canvas.DrawBitmap(art, layer.X, layer.Y, paint);
        }
    }

    using var flipped = new SKBitmap(CANVAS_W, CANVAS_H);

    using (var canvas = new SKCanvas(flipped))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(-1, 1, CANVAS_W / 2f, 0);
        canvas.DrawBitmap(frame, 0, 0);
    }

    using var frameImage = SKImage.FromBitmap(frame);
    using var flippedImage = SKImage.FromBitmap(flipped);

    sheetCanvas.DrawImage(frameImage, crop, SKRect.Create(s * cellW, e * 2 * cellH, cellW, cellH), nearest);
    sheetCanvas.DrawImage(flippedImage, crop, SKRect.Create(s * cellW, (e * 2 + 1) * cellH, cellW, cellH), nearest);
}

using var png = SKImage.FromBitmap(sheet).Encode(SKEncodedImageFormat.Png, 100);
using (var file = File.Create(outPath))
    png.SaveTo(file);

Console.WriteLine(Path.GetFullPath(outPath));
```

- [ ] **Step 3: Run it**

Run: `dotnet run --project <scratchpad>/EmotePreview -- <scratchpad>/EmotePreview/emote-preview.png`
Expected: the full PNG path is printed.

- [ ] **Step 4: Look at the sheet yourself** with the Read tool. Check that each emote sits where intended: hearts on the eyes, halo and bulb above the head, steam beside it, hat on top. Check that the flipped rows mirror correctly. Fix obvious placement errors in the Task 3 constants, then re-render before showing the user.

- [ ] **Step 5: Show the user and ask for sign-off.** Give the PNG path. Explain the layout: rows run Heart Eyes, Halo, Lightbulb, Anger Steam, Party Hat, each unflipped then flipped; columns are 10 evenly spaced moments across each emote. Ask whether to go ahead or what to change. Apply any changes to the pixel maps and constants, re-run Steps 3–5, and re-run `NewCustomEmoteTests` until the user approves.

```json:metadata
{"files": ["<scratchpad>/EmotePreview/EmotePreview.csproj", "<scratchpad>/EmotePreview/Program.cs"], "verifyCommand": "dotnet run --project <scratchpad>/EmotePreview -- <scratchpad>/EmotePreview/emote-preview.png", "acceptanceCriteria": ["emote-preview.png exists with 10 rows x 10 columns at 4x", "Coordinator viewed the PNG before showing it", "User explicitly approved the art in chat", "NewCustomEmoteTests still pass after any art change"], "modelTier": "standard", "userGate": true, "tags": ["user-gate"]}
```

---

### Task 5: Register the five emotes

**Goal:** Add the approved emotes to `CustomEmoteRegistry`, so they appear in the catalog and wheel and play in the world.

**Files:**
- Modify: `Chaos.Client.Rendering/CustomEmotes/CustomEmoteRegistry.cs`
- Test: `Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs`, `Tests/Chaos.Client.Tests/EmoteCatalogTests.cs`

**Acceptance Criteria:**
- [ ] `CustomEmoteRegistry.All` lists, in order: Sunglasses, Middle Finger, Heart Eyes, Halo, Lightbulb, Anger Steam, Party Hat.
- [ ] `EmoteCatalog.All` has 40 entries.
- [ ] The full test suite passes.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `CustomEmoteRegistryTests.cs`:

```csharp
    [Test]
    public async Task The_registry_lists_all_seven_in_catalog_order()
    {
        CustomEmoteRegistry.All.Select(e => e.Name)
                           .Should()
                           .Equal("Sunglasses", "Middle Finger", "Heart Eyes", "Halo", "Lightbulb", "Anger Steam", "Party Hat");

        await Task.CompletedTask;
    }
```

Add to `EmoteCatalogTests.cs`:

```csharp
    [Test]
    public async Task The_catalog_has_forty_emotes()
    {
        EmoteCatalog.All.Should().HaveCount(40);
        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CustomEmoteRegistryTests/*"`
Expected: `The_registry_lists_all_seven_in_catalog_order` FAILS, because only 2 names are listed.

- [ ] **Step 3: Register them.** Replace `All` in `CustomEmoteRegistry.cs`:

```csharp
    public static IReadOnlyList<ICustomEmote> All { get; } =
    [
        SunglassesEmote.Instance,
        MiddleFingerEmote.Instance,
        HeartEyesEmote.Instance,
        HaloEmote.Instance,
        LightbulbEmote.Instance,
        AngerSteamEmote.Instance,
        PartyHatEmote.Instance
    ];
```

- [ ] **Step 4: Run everything**

Run: `dotnet build Chaos.Client.slnx` → 0 errors.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/CustomEmotes/CustomEmoteRegistry.cs", "Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs", "Tests/Chaos.Client.Tests/EmoteCatalogTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["Registry lists the seven emotes in order", "EmoteCatalog.All has 40 entries", "Full suite passes"], "modelTier": "mechanical"}
```

---

### Task 6: In-game check

**Goal:** The user plays every custom emote from the wheel in the running client and confirms how it looks and times.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- Modify only if the user reports a problem: the emote file concerned, plus its tests.

**Acceptance Criteria:**
- [ ] `dotnet build Chaos.Client.slnx` succeeds right before the hand-off (output captured).
- [ ] The user confirms in chat that all five new emotes appear in the wheel's catalog with their icons, and play in the world.
- [ ] The user confirms that Sunglasses and Middle Finger still look and time as before.
- [ ] Any problem the user reports is fixed, the suite re-run and passing, and the user re-confirms.

**Verify:** the user's confirmation, quoted in the close note, plus a passing `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` after any fix

**Steps:**

- [ ] **Step 1: Build**

Run: `dotnet build Chaos.Client.slnx` → 0 errors.

- [ ] **Step 2: Hand off to the user** with this checklist:
  1. Start the client (`dotnet run --project Chaos.Client/Chaos.Client.csproj`, with `DA_PATH` set as in `launchSettings.json`) and log in.
  2. Open the emote wheel's slot editor. Check that Heart Eyes, Halo, Lightbulb, Anger Steam and Party Hat appear after Middle Finger, with icons.
  3. Put them on the wheel and play each one standing still, facing both ways.
  4. Play one while another player (or a second client) watches. Check that they see it too.
  5. Play Sunglasses and Middle Finger. Check that they are unchanged.

- [ ] **Step 3: Fix and re-check** anything the user reports, re-run the full suite, and repeat Step 2 for the affected emotes.

```json:metadata
{"files": [], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["Solution builds right before hand-off", "User confirms the five new emotes appear with icons and play in the world", "User confirms Sunglasses and Middle Finger unchanged", "Reported problems fixed, suite passing, user re-confirms"], "modelTier": "standard", "userGate": true, "tags": ["user-gate"]}
```

---

### Task 7: Commit the full implementation

**Goal:** One commit on a new feature branch with the whole change set, spec and plan included, and nothing unrelated.

**Files:**
- No code changes.

**Acceptance Criteria:**
- [ ] The current branch is `feat/custom-emotes`.
- [ ] `git show --stat HEAD` lists the spec, the plan and its `.tasks.json`, the `CustomEmotes/` folder, the two moved and two deleted files, the rewired client files and the tests.
- [ ] `Chaos.Client/Properties/launchSettings.json` and `Chaos-Server` are still modified and uncommitted.
- [ ] The full test suite passes on the committed tree.

**Verify:** `git status --short && git show --stat HEAD` → only `launchSettings.json` and `Chaos-Server` left modified

**Steps:**

- [ ] **Step 1: Run the full suite one last time**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass.

- [ ] **Step 2: Branch and stage exactly the change set**

```bash
git switch -c feat/custom-emotes
git add docs/superpowers/specs/2026-09-23-custom-emotes-design.md \
        docs/superpowers/plans/2026-09-23-custom-emotes.md \
        docs/superpowers/plans/2026-09-23-custom-emotes.md.tasks.json \
        Chaos.Client.Rendering/CustomEmotes \
        Chaos.Client.Rendering/UiRenderer.cs \
        Chaos.Client/GlobalUsings.cs \
        Chaos.Client/Models/WorldEntity.cs \
        Chaos.Client/Screens/WorldScreen.Update.cs \
        Chaos.Client/Screens/WorldScreen.ServerHandlers.cs \
        Chaos.Client/Screens/WorldScreen.Draw.cs \
        Chaos.Client/ChaosGame.cs \
        Chaos.Client/Definitions/EmoteCatalog.cs \
        Tests/Chaos.Client.Tests/CustomEmoteCoreTests.cs \
        Tests/Chaos.Client.Tests/CustomEmoteRegistryTests.cs \
        Tests/Chaos.Client.Tests/NewCustomEmoteTests.cs \
        Tests/Chaos.Client.Tests/EmoteCatalogTests.cs \
        Tests/Chaos.Client.Tests/SunglassesEmoteTests.cs \
        Tests/Chaos.Client.Tests/MiddleFingerEmoteTests.cs
git status --short
```

Expected: the `git mv` / `git rm` from Task 2 are already staged. Only `Chaos.Client/Properties/launchSettings.json` and `Chaos-Server` remain unstaged. Stage any other file this plan changed that is left over. Leave anything unrelated alone.

- [ ] **Step 3: Commit**

```bash
git commit -m "$(cat <<'EOF'
Add five custom emotes on a shared custom-emote system

Heart Eyes, Halo, Lightbulb, Anger Steam and Party Hat join Sunglasses and
Middle Finger. Every client-side emote is now an ICustomEmote in one
registry, with one clock on WorldEntity and one CustomEmoteRenderer, so a
new emote is one class plus one registry line.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
git show --stat HEAD
git status --short
```

```json:metadata
{"files": [], "verifyCommand": "git status --short && git show --stat HEAD", "acceptanceCriteria": ["On branch feat/custom-emotes", "HEAD contains spec, plan, CustomEmotes folder, moved/deleted files, rewired client files and tests", "launchSettings.json and Chaos-Server remain uncommitted", "Full suite passes"], "modelTier": "mechanical"}
```

# More Map Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add nine new map effects and make Rain run down walls and splash on floors.

**Architecture:** The server adds nine `MapFlags` bits and a third theatre page. The client adds `AmbientOverlayBase`, new `IAmbientOverlay` renderers, and `MapSurface`, which records wall faces, floor and steam vents once per map. Wall water and steam are pure simulations with thin drawing classes, so tests need no graphics device.

**Tech Stack:** C# 14, .NET 10, MonoGame, TUnit, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-09-more-map-effects-design.md`. All preset numbers come from the spec.

## Global Constraints

- Don't change `IAmbientOverlay` or any `Chaos.*` library signature. Adding enum members is allowed.
- Packet size and `CONSTANTS.CLIENT_VERSION` don't change.
- Additive and alpha renderers use straight-alpha textures and tint with `new Color(r, g, b, a)`, like `ParticleRenderer`.
- Strip width equals the step in `AuroraRenderer`, or stripes appear.
- World to screen is `world + worldOrigin`. `worldOrigin` is already an absolute screen position.
- Run only the tests that cover the change.
- Don't add comments that restate code. Test code has no explanatory comments.
- Work on `feat/more-map-effects` branches. Never touch the shared checkouts' `main`.

**User decisions (already made):**
- Effects: Sunbeams, Moonbeams, Dappled Light, Aurora, Aurora Glow, Shooting Stars, Meteors, Rainbow, Rising Steam.
- Crows are out.
- Meteors appear every 3-6 seconds.
- Rain gets wall water automatically, in the "wet walls, realistic drops" look, with no separate setting.
- No maps get these effects in this work.

## Review Focus

1. A dark map has both the Rain and Snow bits set. It must not show wall water. Test in Task 3.
2. A map with no walls, no floor or no vents must draw nothing and not throw. Test in Task 2 and Task 6.
3. Changing maps must not carry drops or steam to the new map. Test in Task 6.
4. A zero-size viewport during a resize must not throw. Test in Task 3.
5. A large town left running for a long time must stay within every cap. Test in Task 6.

---

### Task 1: Server flags and third theatre page

**Goal:** Add the nine map flags and a working third theatre page.

**Files:**
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs`
- Modify: `Chaos-Server/Chaos/Utilities/MapFlagVisibility.cs`
- Modify: `Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs`
- Modify: `Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs:810-836, 931-936`
- Test: `Chaos-Server/Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`, `Chaos-Server/Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, `Chaos-Server/Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`

**Acceptance Criteria:**
- [ ] `MapFlags` has the nine flags on bits 33-41.
- [ ] `MapFlagVisibility.ATMOSPHERE` includes all nine.
- [ ] Page 2 has 15 effects and page 3 has the 10 effects from spec section 6, in that order.
- [ ] Page 2 rows end with Clear All, More Effects, Back. Page 3 rows end with Clear All, Back.
- [ ] The three test classes pass.

**Verify:** `dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)|(TheatreStageEffectsTests)/*"` → all pass

**Steps:**

- [ ] **Step 1: Add the tests.** Add the nine flags to `AmbientFlags`, `EVERY_EFFECT_BUT_SNOW` and `ALL_EFFECTS`. In `TheatreStageEffectsTests`, change the existing page check at about line 194 to allow pages 1-3, adjust any page 2 count, and add:

```csharp
[Test]
public void Page2_HasFifteenEffects()
    => TheatreStageEffects.All.Count(effect => effect.Page == 2).Should().Be(15);

[Test]
public void Page3_ListsTheLightAndSkyEffectsInOrder()
    => TheatreStageEffects.All
                          .Where(effect => effect.Page == 3)
                          .Select(effect => effect.Name)
                          .Should()
                          .Equal(
                              "Balloons",
                              "Sunbeams",
                              "Moonbeams",
                              "Dappled Light",
                              "Aurora",
                              "Aurora Glow",
                              "Shooting Stars",
                              "Meteors",
                              "Rainbow",
                              "Rising Steam");

[Test]
public void ClearAll_StopsAPage3Effect()
    => TheatreStageEffects.ClearAll(MapFlags.Meteors | MapFlags.Fog).HasFlag(MapFlags.Meteors).Should().BeFalse();
```

- [ ] **Step 2: Run the tests and confirm they fail to compile.**

- [ ] **Step 3: Add the flags** after `Balloons = 1UL << 32` in `MapFlags`:

```csharp
Sunbeams = 1UL << 33,
Moonbeams = 1UL << 34,
DappledLight = 1UL << 35,
Aurora = 1UL << 36,
AuroraGlow = 1UL << 37,
ShootingStars = 1UL << 38,
Meteors = 1UL << 39,
Rainbow = 1UL << 40,
RisingSteam = 1UL << 41
```

OR the same nine into `MapFlagVisibility.ATMOSPHERE`, after `MapFlags.Balloons`.

- [ ] **Step 4: Change `TheatreStageEffects.All`.** Replace the Balloons entry at the end with:

```csharp
new("Balloons", MapFlags.Balloons, 3),
new("Sunbeams", MapFlags.Sunbeams, 3),
new("Moonbeams", MapFlags.Moonbeams, 3),
new("Dappled Light", MapFlags.DappledLight, 3),
new("Aurora", MapFlags.Aurora, 3),
new("Aurora Glow", MapFlags.AuroraGlow, 3),
new("Shooting Stars", MapFlags.ShootingStars, 3),
new("Meteors", MapFlags.Meteors, 3),
new("Rainbow", MapFlags.Rainbow, 3),
new("Rising Steam", MapFlags.RisingSteam, 3)
```

- [ ] **Step 5: Change `SuomiTheatreScript`.** In `OnDisplaying`, add `case "suomitheatre_stageeffects3":` beside the two page cases, and replace the page calculation and the final `if`/`else`:

```csharp
var page = thisPage switch
{
    "suomitheatre_stageeffects3" => 3,
    "suomitheatre_stageeffects2" => 2,
    _ => 1
};
```

```csharp
if (page < 3)
    Subject.AddOption(TheatreStageEffects.MORE_TEXT, page == 1 ? "suomitheatre_stageeffects2" : "suomitheatre_stageeffects3");

if (page > 1)
    Subject.AddOption(TheatreStageEffects.BACK_TEXT, page == 3 ? "suomitheatre_stageeffects2" : "suomitheatre_stageeffects");
```

In `OnNext`, add `case "suomitheatre_stageeffects3":` beside the two existing page cases.

- [ ] **Step 6: Run the Verify command and confirm all pass.**

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos/Utilities/MapFlagVisibility.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs"], "verifyCommand": "dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)|(TheatreStageEffectsTests)/*\"", "acceptanceCriteria": ["nine flags on bits 33-41", "ATMOSPHERE includes all nine", "page 2 has 15 effects and page 3 has 10", "menu rows match the spec", "three test classes pass"], "modelTier": "standard"}
```

### Task 2: MapSurface

**Goal:** Record wall faces, floor tiles and steam vents for a map, with no graphics device.

**Files:**
- Create: `Chaos.Client.Rendering/MapSurface.cs`
- Test: `Tests/Chaos.Client.Tests/MapSurfaceTests.cs`

**Acceptance Criteria:**
- [ ] A lone wall tile gives no faces. A two-tile run along X gives three faces.
- [ ] Doors, walls and blocked ground are not floor.
- [ ] Sprites under 45 px give no faces.
- [ ] `IsFloorAtWorld` at a tile's center answers for that tile.
- [ ] Two builds give the same vents, and a 64x64 floor map has at least one.
- [ ] An all-wall map and a zero-floor map give empty lists without throwing.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/MapSurfaceTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add the same `MapFile`/`MapTile` using directive that `WorldScreen.Map.cs` uses.

```csharp
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class MapSurfaceTests
{
    private const int WALL = 100;
    private const int DOOR = 200;
    private const int WATER = 5;
    private const int FLOOR = 1;

    private static MapSurface Build(MapFile map, int height = 60)
        => MapSurface.Build(7, map, id => id == WALL, id => id == DOOR, id => id == WATER, _ => height);

    private static MapFile Floors(int width = 8, int height = 8)
    {
        var map = new MapFile(width, height);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                map.Tiles[x, y] = new MapTile { Background = FLOOR };

        return map;
    }

    private static void Wall(MapFile map, int x, int y)
        => map.Tiles[x, y] = new MapTile { Background = FLOOR, LeftForeground = WALL, RightForeground = WALL };

    [Test]
    public void LoneWall_GivesNoFaces()
    {
        var map = Floors();
        Wall(map, 3, 3);

        Build(map).WallFaces.Should().BeEmpty();
    }

    [Test]
    public void TwoTileRun_SkipsTheFaceHiddenByTheNextWall()
    {
        var map = Floors();
        Wall(map, 3, 3);
        Wall(map, 4, 3);

        Build(map).WallFaces.Should().HaveCount(3);
    }

    [Test]
    public void ShortSprites_GiveNoFaces()
    {
        var map = Floors();
        Wall(map, 3, 3);
        Wall(map, 4, 3);

        Build(map, 44).WallFaces.Should().BeEmpty();
    }

    [Test]
    public void DoorNextToWall_DoesNotMakeARun()
    {
        var map = Floors();
        Wall(map, 3, 3);
        map.Tiles[4, 3] = new MapTile { Background = FLOOR, LeftForeground = DOOR };

        var surface = Build(map);

        surface.WallFaces.Should().BeEmpty();
        surface.IsFloor(4, 3).Should().BeFalse();
    }

    [Test]
    public void Floor_ExcludesWallsAndBlockedGround()
    {
        var map = Floors();
        Wall(map, 1, 1);
        map.Tiles[2, 2] = new MapTile { Background = WATER };

        var surface = Build(map);

        surface.IsFloor(1, 1).Should().BeFalse();
        surface.IsFloor(2, 2).Should().BeFalse();
        surface.IsFloor(3, 3).Should().BeTrue();
        surface.IsFloor(-1, 0).Should().BeFalse();
        surface.IsFloor(8, 0).Should().BeFalse();
    }

    [Test]
    public void IsFloorAtWorld_ReadsTheTileUnderItsCenter()
    {
        var map = Floors();
        Wall(map, 2, 5);
        var surface = Build(map);

        var wallCenter = Camera.TileToWorld(2, 5, 8) + new Vector2(28, 14);
        var floorCenter = Camera.TileToWorld(5, 2, 8) + new Vector2(28, 14);

        surface.IsFloorAtWorld(wallCenter.X, wallCenter.Y).Should().BeFalse();
        surface.IsFloorAtWorld(floorCenter.X, floorCenter.Y).Should().BeTrue();
    }

    [Test]
    public void SteamVents_AreStableAcrossBuilds()
    {
        var first = Build(Floors(64, 64)).SteamVents;
        var second = Build(Floors(64, 64)).SteamVents;

        first.Should().NotBeEmpty();
        first.Should().Equal(second);
    }

    [Test]
    public void AllWallMap_HasNoFloorOrVents()
    {
        var map = Floors(4, 4);

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                Wall(map, x, y);

        var surface = Build(map);

        surface.SteamVents.Should().BeEmpty();
        surface.IsFloor(1, 1).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile.**

- [ ] **Step 3: Write `MapSurface.cs`.**

```csharp
using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering;

/// <summary>One 28 px wide wall face. Its foot at column c is at <c>BaseWorldY + BaseSlope * c</c>.</summary>
public readonly record struct WallFace(float WorldX, float BaseWorldY, float BaseSlope, int Height);

/// <summary>What ambient effects need to know about a map's tiles, built once per map.</summary>
public sealed class MapSurface
{
    public const int FACE_WIDTH = 28;
    private const int MIN_FACE_HEIGHT = 45;
    private const int VENT_SPACING = 60;
    private const int HALF_TILE_W = 28;
    private const int HALF_TILE_H = 14;

    private readonly bool[,] Floor;
    private readonly int Width;

    public int MapId { get; }
    public int Height { get; }
    public IReadOnlyList<WallFace> WallFaces { get; }
    public IReadOnlyList<Point> SteamVents { get; }

    private MapSurface(int mapId, bool[,] floor, int width, int height, List<WallFace> faces, List<Point> vents)
    {
        MapId = mapId;
        Floor = floor;
        Width = width;
        Height = height;
        WallFaces = faces;
        SteamVents = vents;
    }

    public static MapSurface Build(
        int mapId,
        MapFile map,
        Func<int, bool> isWall,
        Func<int, bool> isDoor,
        Func<int, bool> isBlockedGround,
        Func<int, int> foregroundHeight)
    {
        var width = map.Width;
        var height = map.Height;
        var walls = new bool[width, height];
        var floor = new bool[width, height];
        var vents = new List<Point>();

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var tile = map.Tiles[x, y];
                var door = isDoor(tile.LeftForeground) || isDoor(tile.RightForeground);
                var wall = !door && (isWall(tile.LeftForeground) || isWall(tile.RightForeground));

                walls[x, y] = wall;
                floor[x, y] = !door && !wall && !isBlockedGround(tile.Background);

                if (floor[x, y] && (StableHash(mapId, x, y) % VENT_SPACING == 0))
                    vents.Add(new Point(x, y));
            }

        bool WallAt(int x, int y) => (x >= 0) && (y >= 0) && (x < width) && (y < height) && walls[x, y];

        var faces = new List<WallFace>();

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!walls[x, y] || !(WallAt(x - 1, y) || WallAt(x + 1, y) || WallAt(x, y - 1) || WallAt(x, y + 1)))
                    continue;

                var tile = map.Tiles[x, y];
                var world = Camera.TileToWorld(x, y, height);

                if (isWall(tile.LeftForeground) && !WallAt(x, y + 1) && (foregroundHeight(tile.LeftForeground) >= MIN_FACE_HEIGHT))
                    faces.Add(new WallFace(world.X, world.Y + 14, 0.5f, foregroundHeight(tile.LeftForeground)));

                if (isWall(tile.RightForeground) && !WallAt(x + 1, y) && (foregroundHeight(tile.RightForeground) >= MIN_FACE_HEIGHT))
                    faces.Add(new WallFace(world.X + FACE_WIDTH, world.Y + 28, -0.5f, foregroundHeight(tile.RightForeground)));
            }

        return new MapSurface(mapId, floor, width, height, faces, vents);
    }

    public bool IsFloor(int tileX, int tileY)
        => (tileX >= 0) && (tileY >= 0) && (tileX < Width) && (tileY < Height) && Floor[tileX, tileY];

    public bool IsFloorAtWorld(float worldX, float worldY)
    {
        var xMinusY = ((worldX - HALF_TILE_W) / HALF_TILE_W) - (Height - 1);
        var xPlusY = (worldY - HALF_TILE_H) / HALF_TILE_H;

        return IsFloor((int)MathF.Round((xPlusY + xMinusY) / 2f), (int)MathF.Round((xPlusY - xMinusY) / 2f));
    }

    private static uint StableHash(int mapId, int x, int y)
    {
        unchecked
        {
            var hash = ((uint)mapId * 73856093u) ^ ((uint)x * 19349663u) ^ ((uint)y * 83492791u);
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;

            return hash;
        }
    }
}
```

- [ ] **Step 4: Run the Verify command and confirm all pass.**

```json:metadata
{"files": ["Chaos.Client.Rendering/MapSurface.cs", "Tests/Chaos.Client.Tests/MapSurfaceTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/MapSurfaceTests/*\"", "acceptanceCriteria": ["lone wall gives no faces", "two-tile run gives three faces", "non-floor tiles excluded", "short sprites give no faces", "world lookup matches tile", "vents stable and non-empty", "empty cases do not throw"], "modelTier": "mechanical"}
```

### Task 3: Overlay base class, rain check and map wiring

**Goal:** Give new overlays a shared base, fix the Rain check, and deliver `MapSurface` to overlays.

**Files:**
- Create: `Chaos.Client.Rendering/AmbientOverlayBase.cs`, `Chaos.Client.Rendering/IMapAwareOverlay.cs`, `Chaos.Client.Rendering/AmbientTextures.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs`, `Chaos.Client.Rendering/MapRenderer.cs`, `Chaos.Client/Screens/WorldScreen.Map.cs:172, 306`, `Chaos.Client/Screens/WorldScreen.GuildHallEditor.cs:266`
- Test: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`

**Acceptance Criteria:**
- [ ] `AmbientEffects.IsOn` is true for real Rain and false for Darkness and None.
- [ ] `AmbientOverlayBase.Update` with a 0x0 viewport doesn't throw and doesn't advance the effect.
- [ ] `SetMapSurface` reaches every `IMapAwareOverlay`.
- [ ] The surface is cleared when a map starts loading, built after `PreloadMapTiles`, and rebuilt after a guild hall edit.
- [ ] `ReleaseInactiveTextures` releases `AmbientOverlayBase` overlays.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/AmbientEffectsTests/*"` → all pass except `EveryEffectFlag_HasAnOverlay`, which passes after Task 6

**Steps:**

- [ ] **Step 1: Add the failing tests to `AmbientEffectsTests`.** Add `using Microsoft.Xna.Framework;` and `using Microsoft.Xna.Framework.Graphics;` to the file.

```csharp
private sealed class CountingOverlay : AmbientOverlayBase
{
    public int Steps;
    public override BlendState BlendState => BlendState.Additive;
    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin) { }
    public override void ReleaseTextures() { }
    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin) => Steps++;
}

[Test]
[Arguments(MapFlags.Rain, true)]
[Arguments(MapFlags.Darkness, false)]
[Arguments(MapFlags.None, false)]
[Arguments(MapFlags.Snow, false)]
public void IsOn_TreatsOnlyRealRainAsRain(MapFlags flags, bool expected)
    => AmbientEffects.IsOn(flags, MapFlags.Rain).Should().Be(expected);

[Test]
public void IsOn_UsesHasFlagForOtherEffects()
    => AmbientEffects.IsOn(MapFlags.Fog | MapFlags.Meteors, MapFlags.Meteors).Should().BeTrue();

[Test]
public void Update_WithEmptyViewport_DoesNotStep()
{
    using var overlay = new CountingOverlay();
    overlay.SetActive(true, immediate: true);

    overlay.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1)), Rectangle.Empty, Vector2.Zero);

    overlay.Steps.Should().Be(0);
}

[Test]
public void Update_FadesIn_ThenSteps()
{
    using var overlay = new CountingOverlay();
    overlay.SetActive(true);

    overlay.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1)), new Rectangle(0, 0, 640, 310), Vector2.Zero);

    overlay.IsActive.Should().BeTrue();
    overlay.Steps.Should().Be(1);
}
```

- [ ] **Step 2: Create `IMapAwareOverlay.cs`.**

```csharp
namespace Chaos.Client.Rendering;

public interface IMapAwareOverlay : IAmbientOverlay
{
    void SetSurface(MapSurface? surface);
}
```

- [ ] **Step 3: Create `AmbientOverlayBase.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

/// <summary>Fade handling shared by the procedural ambient overlays.</summary>
public abstract class AmbientOverlayBase : IAmbientOverlay
{
    private bool Active;

    protected float EffectAlpha { get; private set; }
    protected float Clock { get; private set; }
    protected bool Spawning => Active;
    protected virtual float FadeSeconds => 2f;

    public abstract BlendState BlendState { get; }

    public bool IsActive => Active || (EffectAlpha > 0f);

    public void SetActive(bool on, bool immediate = false)
    {
        if ((on && !IsActive) || (immediate && !on))
            ResetState();

        Active = on;

        if (immediate)
            EffectAlpha = on ? 1f : 0f;
    }

    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (dt <= 0f)
            return;

        var step = dt / FadeSeconds;
        EffectAlpha = Active ? MathF.Min(1f, EffectAlpha + step) : MathF.Max(0f, EffectAlpha - step);

        if (!IsActive || (viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        Clock += dt;
        Step(dt, viewport, worldOrigin);
    }

    public abstract void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin);

    public abstract void ReleaseTextures();

    public void Dispose()
    {
        ReleaseTextures();
        GC.SuppressFinalize(this);
    }

    protected virtual void ResetState() { }

    protected virtual void Step(float dt, Rectangle viewport, Vector2 worldOrigin) { }

    protected static float ViewScale(Rectangle viewport) => MathF.Max(1f, viewport.Width / 640f);

    protected static Color Tint(int r, int g, int b, float alpha)
        => new(r, g, b, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
}
```

- [ ] **Step 4: Create `AmbientTextures.cs`.** All textures are white with the effect in the alpha channel, except `Gradient`.

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

internal static class AmbientTextures
{
    public static Texture2D Create(GraphicsDevice device, int width, int height, Func<int, int, Color> pixel)
    {
        var data = new Color[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                data[(y * width) + x] = pixel(x, y);

        var texture = new Texture2D(device, width, height);
        texture.SetData(data);

        return texture;
    }

    public static Texture2D Pixel(GraphicsDevice device) => Create(device, 1, 1, static (_, _) => Color.White);

    public static Texture2D SoftDot(GraphicsDevice device, int size = 64)
        => Create(device, size, size, (x, y) =>
        {
            var dx = (x + 0.5f - (size / 2f)) / (size / 2f);
            var dy = (y + 0.5f - (size / 2f)) / (size / 2f);
            var d = MathF.Sqrt((dx * dx) + (dy * dy));
            var a = d < 0.5f ? MathHelper.Lerp(1f, 0.45f, d / 0.5f) : d < 1f ? MathHelper.Lerp(0.45f, 0f, (d - 0.5f) / 0.5f) : 0f;

            return White(a);
        });

    public static Texture2D Ring(GraphicsDevice device)
        => Create(device, 64, 32, static (x, y) =>
        {
            var dx = (x + 0.5f - 32f) / 32f;
            var dy = (y + 0.5f - 16f) / 16f;
            var d = MathF.Sqrt((dx * dx) + (dy * dy));

            return White(MathHelper.Clamp(1f - (MathF.Abs(d - 0.94f) / 0.06f), 0f, 1f));
        });

    public static Texture2D VerticalAlpha(GraphicsDevice device, int width, int height, params (float At, float Alpha)[] stops)
        => Create(device, width, height, (_, y) => White(Sample(stops, y / (height - 1f))));

    public static Texture2D Gradient(GraphicsDevice device, int length, int thickness, params (float At, Color Color)[] stops)
        => Create(device, length, thickness, (x, _) =>
        {
            var at = x / (length - 1f);

            for (var i = 1; i < stops.Length; i++)
                if (at <= stops[i].At)
                    return Color.Lerp(stops[i - 1].Color, stops[i].Color, (at - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));

            return stops[^1].Color;
        });

    public static float Sample((float At, float Alpha)[] stops, float at)
    {
        for (var i = 1; i < stops.Length; i++)
            if (at <= stops[i].At)
                return MathHelper.Lerp(stops[i - 1].Alpha, stops[i].Alpha, (at - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));

        return stops[^1].Alpha;
    }

    public static Color White(float alpha) => new(255, 255, 255, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
}
```

- [ ] **Step 5: Change `AmbientEffects`.** In `Apply`, use `overlay.SetActive(IsOn(flags, flag), immediate);`. In `ReleaseInactiveTextures`, add a case for `AmbientOverlayBase`. Add these members:

```csharp
internal static bool IsOn(MapFlags flags, MapFlags flag)
    => flag == MapFlags.Rain ? (flags & MapFlags.Darkness) == MapFlags.Rain : flags.HasFlag(flag);

public void SetMapSurface(MapSurface? surface)
{
    foreach (var (_, overlay) in Overlays)
        if (overlay is IMapAwareOverlay aware)
            aware.SetSurface(surface);
}
```

```csharp
case AmbientOverlayBase procedural:
    procedural.ReleaseTextures();

    break;
```

- [ ] **Step 6: Add to `MapRenderer`.**

```csharp
/// <summary>Pixel height of a foreground tile sprite, or 0 when it has no image.</summary>
public int GetForegroundHeight(int tileId)
{
    if (CyclingManager is not null && CyclingManager.FgOverrides.TryGetValue(tileId, out var cycling))
        return cycling.SourceRect.Height;

    if (FgAtlas?.TryGetRegion(tileId) is { } region)
        return region.SourceRect.Height;

    return GetOrCreateFgTexture(tileId)?.Height ?? 0;
}
```

- [ ] **Step 7: Wire `WorldScreen`.** Add this method to `WorldScreen.Map.cs`:

```csharp
private void RefreshAmbientMapSurface()
{
    var ground = DataContext.Tiles.GroundAttributes;

    AmbientEffects.SetMapSurface(
        MapSurface.Build(
            CurrentMapId,
            MapFile!,
            IsTileWall,
            id => DoorTable.IsDoorTileId(id),
            id => ground.TryGetValue(id, out var attribute) && attribute.IsWalkBlocking,
            MapRenderer.GetForegroundHeight));
}
```

- Call `AmbientEffects.SetMapSurface(null);` right after `MapPreloaded = false;` at `WorldScreen.Map.cs:172`.
- Call `RefreshAmbientMapSurface();` right after `Lighting.SetMapLanterns(MapFile);` at `WorldScreen.Map.cs:306`. Tile sprites are loaded by then.
- Call it again after `BuildPathfinder(MapFile)` at `WorldScreen.GuildHallEditor.cs:266`, inside the same `if`. Add braces to that `if`.
- If the `Func<int, bool>` arguments don't convert because the tile ids are `short`, wrap with a cast lambda such as `id => DoorTable.IsDoorTileId((short)id)`.

- [ ] **Step 8: Run the Verify command.** `EveryEffectFlag_HasAnOverlay` may fail until Task 6. Every other test must pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/AmbientOverlayBase.cs", "Chaos.Client.Rendering/IMapAwareOverlay.cs", "Chaos.Client.Rendering/AmbientTextures.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Chaos.Client.Rendering/MapRenderer.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "Chaos.Client/Screens/WorldScreen.GuildHallEditor.cs", "Tests/Chaos.Client.Tests/AmbientEffectsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/AmbientEffectsTests/*\"", "acceptanceCriteria": ["IsOn true for real Rain only", "empty viewport does not step", "SetMapSurface reaches map-aware overlays", "surface cleared, built and rebuilt at the named places", "procedural overlays release textures"], "modelTier": "standard"}
```

### Task 4: Light and rainbow renderers

**Goal:** Add Sunbeams, Moonbeams, Dappled Light and Rainbow.

**Files:**
- Create: `Chaos.Client.Rendering/LightShaftRenderer.cs`, `Chaos.Client.Rendering/DappleRenderer.cs`, `Chaos.Client.Rendering/RainbowRenderer.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs`
- Test: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`

**Acceptance Criteria:**
- [ ] The presets use the spec section 4 numbers.
- [ ] Dapple positions are drawn relative to `worldOrigin`.
- [ ] New renderers hold no textures until first drawn.
- [ ] The client builds.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/AmbientEffectsTests/LightPresets*"` → all pass

**Steps:**

- [ ] **Step 1: Add the failing test.**

```csharp
[Test]
public void LightPresets_MatchTheApprovedLooks()
{
    LightShaftRenderer.Sunbeams.Count.Should().Be(6);
    LightShaftRenderer.Moonbeams.Count.Should().Be(5);
    LightShaftRenderer.Moonbeams.Tint.Should().Be(new Color(170, 200, 255));
}
```

- [ ] **Step 2: Write `LightShaftRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record LightShaftStyle(
    Color Tint,
    int Count,
    float WidthMin,
    float WidthMax,
    float AlphaMin,
    float AlphaMax,
    float Base,
    float Depth,
    float FreqMin,
    float FreqMax,
    float DriftMin,
    float DriftMax);

public sealed class LightShaftRenderer : AmbientOverlayBase
{
    public static LightShaftStyle Sunbeams { get; } = new(new Color(255, 232, 170), 6, 50f, 120f, 0.4f, 0.7f, 0.6f, 0.4f, 0.08f, 0.18f, 2f, 6f);
    public static LightShaftStyle Moonbeams { get; } = new(new Color(170, 200, 255), 5, 50f, 110f, 0.35f, 0.6f, 0.65f, 0.35f, 0.06f, 0.14f, 1.5f, 4f);

    private const float ANGLE = 0.42f;
    private const float LENGTH = 430f;
    private const float WRAP_END = 840f;
    private const float WRAP_START = 40f;

    private sealed class Beam
    {
        public float X, Width, Alpha, Phase, Freq, Drift;
    }

    private readonly LightShaftStyle Style;
    private readonly Beam[] Beams;
    private Texture2D? Shaft;

    public LightShaftRenderer(LightShaftStyle style)
    {
        Style = style;
        var random = new Random();

        float Roll(float min, float max) => min + ((max - min) * random.NextSingle());

        Beams = Enumerable.Range(0, style.Count)
                          .Select(_ => new Beam
                          {
                              X = Roll(60f, 800f),
                              Width = Roll(style.WidthMin, style.WidthMax),
                              Alpha = Roll(style.AlphaMin, style.AlphaMax),
                              Phase = Roll(0f, MathHelper.TwoPi),
                              Freq = Roll(style.FreqMin, style.FreqMax),
                              Drift = Roll(style.DriftMin, style.DriftMax)
                          })
                          .ToArray();
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Shaft is not null;

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        foreach (var beam in Beams)
        {
            beam.X += beam.Drift * dt;

            if (beam.X > WRAP_END)
                beam.X = WRAP_START;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Shaft ??= AmbientTextures.Create(spriteBatch.GraphicsDevice, 64, 256, static (x, y) =>
        {
            var across = 1f - MathF.Abs((x / 63f * 2f) - 1f);

            return AmbientTextures.White(across * (1f - (y / 255f)));
        });

        var scale = ViewScale(viewport);

        foreach (var beam in Beams)
        {
            var alpha = EffectAlpha * beam.Alpha * (Style.Base + (Style.Depth * MathF.Sin((MathHelper.TwoPi * beam.Freq * Clock) + beam.Phase)));
            var position = new Vector2(viewport.X + (beam.X * scale), viewport.Y - (40f * scale));
            var size = new Vector2(beam.Width * scale / Shaft.Width, LENGTH * scale / Shaft.Height);

            spriteBatch.Draw(
                Shaft,
                position,
                null,
                new Color(Style.Tint.R, Style.Tint.G, Style.Tint.B, (byte)Math.Clamp((int)(255f * alpha), 0, 255)),
                ANGLE,
                new Vector2(Shaft.Width / 2f, 0f),
                size,
                SpriteEffects.None,
                0f);
        }
    }

    public override void ReleaseTextures()
    {
        Shaft?.Dispose();
        Shaft = null;
    }
}
```

- [ ] **Step 3: Write `DappleRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class DappleRenderer : AmbientOverlayBase
{
    private const int COUNT = 26;
    private const float AREA_W = 900f;
    private const float AREA_H = 600f;
    private const float DRIFT_X = 5f;
    private const float DRIFT_Y = 2f;

    private sealed class Spot
    {
        public float X, Y, Radius, Alpha, Phase, Freq;
    }

    private readonly Spot[] Spots;
    private Texture2D? Dot;
    private float Offset;

    public DappleRenderer()
    {
        var random = new Random();

        float Roll(float min, float max) => min + ((max - min) * random.NextSingle());

        Spots = Enumerable.Range(0, COUNT)
                          .Select(_ => new Spot
                          {
                              X = Roll(0f, AREA_W),
                              Y = Roll(0f, AREA_H),
                              Radius = Roll(30f, 80f),
                              Alpha = Roll(0.35f, 0.65f),
                              Phase = Roll(0f, MathHelper.TwoPi),
                              Freq = Roll(0.25f, 0.7f)
                          })
                          .ToArray();
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Dot is not null;

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin) => Offset += dt;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Dot ??= AmbientTextures.SoftDot(spriteBatch.GraphicsDevice);

        var originX = worldOrigin.X - viewport.X + (Offset * DRIFT_X);
        var originY = worldOrigin.Y - viewport.Y + (Offset * DRIFT_Y);

        foreach (var spot in Spots)
        {
            var pulse = 0.55f
                        + (0.3f * MathF.Sin((MathHelper.TwoPi * spot.Freq * Clock) + spot.Phase))
                        + (0.15f * MathF.Sin((MathHelper.TwoPi * spot.Freq * 2.7f * Clock) + spot.Phase));
            var alpha = EffectAlpha * spot.Alpha * pulse;
            var x = viewport.X + Wrap(spot.X + originX, AREA_W) - 100f;
            var y = viewport.Y + Wrap(spot.Y + originY, AREA_H) - 100f;

            var destination = new Rectangle((int)(x - spot.Radius), (int)(y - (spot.Radius / 2f)), (int)(spot.Radius * 2f), (int)spot.Radius);

            if (destination.Intersects(viewport))
                spriteBatch.Draw(Dot, destination, new Color(255, 226, 150, (byte)Math.Clamp((int)(255f * alpha), 0, 255)));
        }
    }

    private static float Wrap(float value, float size) => ((value % size) + size) % size;

    public override void ReleaseTextures()
    {
        Dot?.Dispose();
        Dot = null;
    }
}
```

- [ ] **Step 4: Write `RainbowRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class RainbowRenderer : AmbientOverlayBase
{
    private const float INNER = 300f;
    private const float OUTER = 345f;

    private static readonly Color[] Bands =
    [
        new(255, 60, 60),
        new(255, 150, 40),
        new(255, 240, 70),
        new(80, 230, 90),
        new(70, 150, 255),
        new(150, 80, 240)
    ];

    private static readonly (float At, float Alpha)[] Falloff = [(0f, 1f), (0.6f, 0.7f), (1f, 0f)];

    private Texture2D? Arc;

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Arc is not null;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        if ((Arc is null) || (Arc.Width != viewport.Width) || (Arc.Height != viewport.Height))
        {
            ReleaseTextures();
            Arc = Build(spriteBatch.GraphicsDevice, viewport.Width, viewport.Height);
        }

        var alpha = EffectAlpha * (0.34f + (0.12f * MathF.Sin(0.5f * Clock)));

        spriteBatch.Draw(Arc, viewport, new Color(255, 255, 255, (byte)Math.Clamp((int)(255f * alpha), 0, 255)));
    }

    private static Texture2D Build(GraphicsDevice device, int width, int height)
    {
        var scale = MathF.Max(1f, width / 640f);
        var inner = INNER * scale;
        var outer = OUTER * scale;
        var cx = width * 0.55f;
        var cy = height * 1.25f;

        return AmbientTextures.Create(device, width, height, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            var at = (d - inner) / (outer - inner);

            if (at is < 0f or > 1f)
                return Color.Transparent;

            var color = BandColor(at, out var edge);
            var alpha = edge * AmbientTextures.Sample(Falloff, y / (height - 1f));

            return new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
        });
    }

    private static Color BandColor(float at, out float edge)
    {
        edge = at < 0.1f ? at / 0.1f : at > 0.9f ? (1f - at) / 0.1f : 1f;

        var band = MathHelper.Clamp((at - 0.1f) / 0.8f, 0f, 1f) * (Bands.Length - 1);
        var index = Math.Min((int)band, Bands.Length - 2);

        return Color.Lerp(Bands[index], Bands[index + 1], band - index);
    }

    public override void ReleaseTextures()
    {
        Arc?.Dispose();
        Arc = null;
    }
}
```

- [ ] **Step 5: Add to `Overlays`** in the positions from spec section 5.

```csharp
(MapFlags.DappledLight, new DappleRenderer()),
```

```csharp
(MapFlags.Rainbow, new RainbowRenderer()),
(MapFlags.Sunbeams, new LightShaftRenderer(LightShaftRenderer.Sunbeams)),
(MapFlags.Moonbeams, new LightShaftRenderer(LightShaftRenderer.Moonbeams)),
```

- [ ] **Step 6: Run the Verify command and confirm it passes.**

```json:metadata
{"files": ["Chaos.Client.Rendering/LightShaftRenderer.cs", "Chaos.Client.Rendering/DappleRenderer.cs", "Chaos.Client.Rendering/RainbowRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Tests/Chaos.Client.Tests/AmbientEffectsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/AmbientEffectsTests/LightPresets*\"", "acceptanceCriteria": ["presets match the spec", "dapples are anchored with worldOrigin", "textures made on first draw", "client builds"], "modelTier": "mechanical"}
```

### Task 5: Aurora and streak renderers

**Goal:** Add Aurora, Aurora Glow, Shooting Stars and Meteors.

**Files:**
- Create: `Chaos.Client.Rendering/AuroraRenderer.cs`, `Chaos.Client.Rendering/StreakRenderer.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs`
- Test: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`

**Acceptance Criteria:**
- [ ] Meteors use a 3-6 second gap. Shooting Stars use 5-12 seconds.
- [ ] Aurora strip width equals its step: 4 for Aurora and 6 for Aurora Glow.
- [ ] Both effects are in `Overlays` in the spec order.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/AmbientEffectsTests/Sky*"` → all pass

**Steps:**

- [ ] **Step 1: Add the failing tests.**

```csharp
[Test]
public void SkyTiming_MatchesTheApprovedRates()
{
    StreakRenderer.Meteors.GapMin.Should().Be(3f);
    StreakRenderer.Meteors.GapMax.Should().Be(6f);
    StreakRenderer.ShootingStars.GapMin.Should().Be(5f);
    StreakRenderer.ShootingStars.GapMax.Should().Be(12f);
}

[Test]
public void SkyStrips_HaveNoOverlap()
{
    AuroraRenderer.Curtains.Step.Should().Be(4);
    AuroraRenderer.Glow.Step.Should().Be(6);
}
```

- [ ] **Step 2: Write `AuroraRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record AuroraStyle(bool Curtain, int Step);

public sealed class AuroraRenderer : AmbientOverlayBase
{
    public static AuroraStyle Curtains { get; } = new(true, 4);
    public static AuroraStyle Glow { get; } = new(false, 6);

    private static readonly Color[] Colors = [new(90, 255, 160), new(70, 220, 230), new(170, 110, 255)];
    private static readonly float[] Phases = [0f, 2.1f, 4.2f];
    private static readonly float[] Bases = [125f, 95f, 150f];
    private static readonly float[] Speeds = [0.3f, -0.22f, 0.17f];

    private readonly AuroraStyle Style;
    private Texture2D? Strip;

    public AuroraRenderer(AuroraStyle style) => Style = style;

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Strip is not null;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Strip ??= Style.Curtain
            ? AmbientTextures.VerticalAlpha(spriteBatch.GraphicsDevice, 8, 128, (0f, 0f), (0.75f, 0.5f), (0.93f, 1f), (1f, 0f))
            : AmbientTextures.VerticalAlpha(spriteBatch.GraphicsDevice, 8, 128, (0f, 0f), (0.5f, 1f), (1f, 0f));

        var scale = ViewScale(viewport);
        var width = viewport.Width / scale;

        for (var ribbon = 0; ribbon < Colors.Length; ribbon++)
            for (var x = 0f; x < width; x += Style.Step)
                DrawStrip(spriteBatch, viewport, scale, ribbon, x);
    }

    private void DrawStrip(SpriteBatch spriteBatch, Rectangle viewport, float scale, int ribbon, float x)
    {
        var ph = Phases[ribbon];
        var t = Clock;
        float top, height, alpha;

        if (Style.Curtain)
        {
            var y = Bases[ribbon] + (22f * MathF.Sin((0.012f * x) + (0.35f * t) + ph)) + (10f * MathF.Sin((0.031f * x) - (0.6f * t) + (2f * ph)));
            height = 110f + (30f * MathF.Sin((0.02f * x) + (0.2f * t) + ph));
            top = y - height;
            var s = 0.5f + (0.5f * MathF.Sin((0.017f * x) + (0.5f * t) + ph));
            alpha = (s * s * 0.55f) + 0.1f;
        }
        else
        {
            var fullHeight = viewport.Height / scale;
            var sp = Speeds[ribbon];
            top = (0.5f * fullHeight) + (70f * MathF.Sin((0.008f * x) + (sp * t) + ph)) - (0.65f * fullHeight);
            height = 1.3f * fullHeight;
            var s = 0.5f + (0.5f * MathF.Sin((0.011f * x) + (1.7f * sp * t) + ph));
            alpha = (s * s * 0.26f) + 0.03f;
        }

        var destination = new Rectangle(
            viewport.X + (int)MathF.Round(x * scale),
            viewport.Y + (int)MathF.Round(top * scale),
            (int)MathF.Ceiling(Style.Step * scale),
            (int)MathF.Round(height * scale));

        var c = Colors[ribbon];
        spriteBatch.Draw(Strip!, destination, new Color(c.R, c.G, c.B, (byte)Math.Clamp((int)(255f * alpha * EffectAlpha), 0, 255)));
    }

    public override void ReleaseTextures()
    {
        Strip?.Dispose();
        Strip = null;
    }
}
```

- [ ] **Step 3: Write `StreakRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record StreakStyle(
    float GapMin,
    float GapMax,
    float Speed,
    float Life,
    float Tail,
    float Width,
    float StartXMin,
    float StartYMin,
    float StartYMax,
    (float At, Color Color)[] TailColors,
    bool Burning);

public sealed class StreakRenderer : AmbientOverlayBase
{
    public static StreakStyle ShootingStars { get; } = new(
        5f, 12f, 615f, 0.55f, 90f, 1.5f, 0.4f, -10f, 0.4f,
        [(0f, new Color(235, 245, 255, 255)), (1f, new Color(160, 190, 255, 0))],
        false);

    public static StreakStyle Meteors { get; } = new(
        3f, 6f, 295f, 1.3f, 130f, 3f, 0.5f, -20f, 0.25f,
        [(0f, new Color(255, 240, 200, 255)), (0.3f, new Color(255, 170, 90, 178)), (1f, new Color(255, 90, 40, 0))],
        true);

    private const float DIR_X = -0.86f;
    private const float DIR_Y = 0.5f;
    private const float SPARK_EVERY = 0.03f;
    private const int MAX_SPARKS = 80;

    private sealed class Streak
    {
        public float X, Y, Age, NextSpark;
    }

    private sealed class Spark
    {
        public float X, Y, VX, VY, Age, Life;
    }

    private readonly StreakStyle Style;
    private readonly Random Random = new();
    private readonly List<Streak> Streaks = [];
    private readonly List<Spark> Sparks = [];
    private Texture2D? Tail;
    private Texture2D? Head;
    private Texture2D? Pixel;
    private float NextSpawn;

    public StreakRenderer(StreakStyle style)
    {
        Style = style;
        NextSpawn = Roll(0.5f, style.GapMin);
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Tail is not null;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    protected override void ResetState()
    {
        Streaks.Clear();
        Sparks.Clear();
        NextSpawn = Roll(0.5f, Style.GapMin);
    }

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        NextSpawn -= dt;

        if ((NextSpawn <= 0f) && Spawning)
        {
            NextSpawn = Roll(Style.GapMin, Style.GapMax);
            Streaks.Add(new Streak
            {
                X = Roll(viewport.Width * Style.StartXMin, viewport.Width * 1.1f),
                Y = Roll(Style.StartYMin, viewport.Height * Style.StartYMax)
            });
        }

        for (var i = Streaks.Count - 1; i >= 0; i--)
        {
            var streak = Streaks[i];
            streak.Age += dt;

            if (streak.Age >= Style.Life)
            {
                Streaks.RemoveAt(i);

                continue;
            }

            streak.X += DIR_X * Style.Speed * dt;
            streak.Y += DIR_Y * Style.Speed * dt;
            streak.NextSpark -= dt;

            if (Style.Burning && (streak.NextSpark <= 0f) && (Sparks.Count < MAX_SPARKS))
            {
                streak.NextSpark = SPARK_EVERY;
                Sparks.Add(new Spark
                {
                    X = streak.X + Roll(-3f, 3f),
                    Y = streak.Y + Roll(-3f, 3f),
                    VX = Roll(-14f, 14f),
                    VY = Roll(20f, 55f),
                    Life = Roll(0.4f, 0.8f)
                });
            }
        }

        for (var i = Sparks.Count - 1; i >= 0; i--)
        {
            var spark = Sparks[i];
            spark.Age += dt;

            if (spark.Age >= spark.Life)
            {
                Sparks.RemoveAt(i);

                continue;
            }

            spark.X += spark.VX * dt;
            spark.Y += spark.VY * dt;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        var device = spriteBatch.GraphicsDevice;
        Tail ??= AmbientTextures.Gradient(device, 128, 4, Style.TailColors);
        Head ??= AmbientTextures.SoftDot(device, 32);
        Pixel ??= AmbientTextures.Pixel(device);

        var angle = MathF.Atan2(-DIR_Y, -DIR_X);
        var tailScale = new Vector2(Style.Tail / Tail.Width, Style.Width / Tail.Height);

        foreach (var streak in Streaks)
        {
            var fade = MathF.Sin(MathF.PI * streak.Age / Style.Life) * EffectAlpha;
            var position = new Vector2(viewport.X + streak.X, viewport.Y + streak.Y);

            spriteBatch.Draw(Tail, position, null, new Color(255, 255, 255, (byte)Math.Clamp((int)(255f * fade), 0, 255)), angle, new Vector2(0f, Tail.Height / 2f), tailScale, SpriteEffects.None, 0f);

            if (Style.Burning)
                spriteBatch.Draw(Head, position - new Vector2(16f, 16f), Tint(255, 190, 110, fade * 0.8f));
        }

        foreach (var spark in Sparks)
            spriteBatch.Draw(
                Pixel,
                new Rectangle(viewport.X + (int)spark.X, viewport.Y + (int)spark.Y, 2, 2),
                Tint(255, 190, 110, (1f - (spark.Age / spark.Life)) * EffectAlpha));
    }

    public override void ReleaseTextures()
    {
        Tail?.Dispose();
        Tail = null;
        Head?.Dispose();
        Head = null;
        Pixel?.Dispose();
        Pixel = null;
    }
}
```

- [ ] **Step 4: Add to `Overlays`,** after the Task 4 Moonbeams entry and before `Lightning`.

```csharp
(MapFlags.AuroraGlow, new AuroraRenderer(AuroraRenderer.Glow)),
(MapFlags.Aurora, new AuroraRenderer(AuroraRenderer.Curtains)),
(MapFlags.ShootingStars, new StreakRenderer(StreakRenderer.ShootingStars)),
(MapFlags.Meteors, new StreakRenderer(StreakRenderer.Meteors)),
```

- [ ] **Step 5: Run the Verify command and confirm it passes.**

```json:metadata
{"files": ["Chaos.Client.Rendering/AuroraRenderer.cs", "Chaos.Client.Rendering/StreakRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Tests/Chaos.Client.Tests/AmbientEffectsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/AmbientEffectsTests/Sky*\"", "acceptanceCriteria": ["meteor gap 3-6 seconds, shooting star gap 5-12 seconds", "aurora strip width equals step", "overlays added in spec order"], "modelTier": "mechanical"}
```

### Task 6: Steam and wall water

**Goal:** Add Rising Steam and the Rain upgrade, both anchored to the map.

**Files:**
- Create: `Chaos.Client.Rendering/WetWallSimulation.cs`, `Chaos.Client.Rendering/WetWallRenderer.cs`, `Chaos.Client.Rendering/SteamRenderer.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs`
- Test: `Tests/Chaos.Client.Tests/WetWallSimulationTests.cs`

**Acceptance Criteria:**
- [ ] A long run on a large map stays within every cap.
- [ ] No drop exists at or below its wall's foot after a step.
- [ ] `Clear` empties everything, and `SetSurface` calls it.
- [ ] A null or empty surface doesn't spawn drops or rings and doesn't throw.
- [ ] Nothing spawns while `spawning` is false.
- [ ] `EveryEffectFlag_HasAnOverlay` passes.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/(WetWallSimulationTests)|(AmbientEffectsTests)/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add the same `MapFile`/`MapTile` using directive that `WorldScreen.Map.cs` uses.

```csharp
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class WetWallSimulationTests
{
    private static readonly Rectangle Everywhere = new(-4000, -4000, 12000, 12000);

    private static MapSurface Town(int size = 40)
    {
        var map = new MapFile(size, size);

        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                map.Tiles[x, y] = new MapTile { Background = 1, LeftForeground = (short)((y % 4 == 0) ? 100 : 0), RightForeground = (short)((y % 4 == 0) ? 100 : 0) };

        return MapSurface.Build(1, map, id => id == 100, _ => false, _ => false, _ => 60);
    }

    private static MapSurface Empty()
    {
        var map = new MapFile(4, 4);

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                map.Tiles[x, y] = new MapTile { Background = 9 };

        return MapSurface.Build(1, map, _ => false, _ => false, id => id == 9, _ => 60);
    }

    private static WetWallSimulation Run(MapSurface? surface, int steps, bool spawning = true)
    {
        var simulation = new WetWallSimulation(new Random(5));

        for (var i = 0; i < steps; i++)
            simulation.Step(0.05f, Everywhere, surface, spawning);

        return simulation;
    }

    [Test]
    public void LongRun_StaysWithinEveryCap()
    {
        var simulation = Run(Town(), 4000);

        simulation.Drops.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_DROPS);
        simulation.Streaks.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_STREAKS);
        simulation.Beads.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_BEADS);
        simulation.Rings.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_RINGS);
        simulation.Splashes.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_SPLASHES);
        simulation.Patches.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_PATCHES);
    }

    [Test]
    public void Town_MakesDropsAndRings()
    {
        var simulation = Run(Town(), 200);

        simulation.Drops.Should().NotBeEmpty();
        simulation.Rings.Should().NotBeEmpty();
    }

    [Test]
    public void Drops_AlwaysStayAboveTheirWallFoot()
    {
        var simulation = new WetWallSimulation(new Random(9));
        var surface = Town();

        for (var i = 0; i < 1000; i++)
        {
            simulation.Step(0.05f, Everywhere, surface, true);
            simulation.Drops.Should().OnlyContain(drop => drop.Y < drop.Base);
        }
    }

    [Test]
    public void NullOrEmptySurface_MakesNothing()
    {
        Run(null, 200).Drops.Should().BeEmpty();
        Run(Empty(), 200).Rings.Should().BeEmpty();
    }

    [Test]
    public void NotSpawning_MakesNothing()
        => Run(Town(), 200, spawning: false).Drops.Should().BeEmpty();

    [Test]
    public void Clear_EmptiesEverything()
    {
        var simulation = Run(Town(), 200);

        simulation.Clear();

        simulation.Drops.Should().BeEmpty();
        simulation.Streaks.Should().BeEmpty();
        simulation.Rings.Should().BeEmpty();
        simulation.Beads.Should().BeEmpty();
        simulation.Splashes.Should().BeEmpty();
        simulation.Patches.Should().BeEmpty();
    }

    [Test]
    public void Renderer_DropsWaterWhenTheMapChanges()
    {
        using var renderer = new WetWallRenderer();
        renderer.SetSurface(Town());
        renderer.SetActive(true, immediate: true);

        for (var i = 0; i < 200; i++)
            renderer.Update(new GameTime(TimeSpan.FromSeconds(i), TimeSpan.FromSeconds(0.05)), new Rectangle(0, 0, 2000, 900), Vector2.Zero);

        renderer.Simulation.Drops.Should().NotBeEmpty();

        renderer.SetSurface(null);

        renderer.Simulation.Drops.Should().BeEmpty();
    }

    [Test]
    public void OffscreenWalls_MakeNoDrops()
    {
        var simulation = new WetWallSimulation(new Random(5));

        for (var i = 0; i < 200; i++)
            simulation.Step(0.05f, new Rectangle(90000, 90000, 640, 310), Town(), true);

        simulation.Drops.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile.**

- [ ] **Step 3: Write `WetWallSimulation.cs`.** Everything is in world pixels.

```csharp
using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering;

/// <summary>Water on walls and floors for rainy maps. Holds no graphics resources.</summary>
public sealed class WetWallSimulation
{
    public const int MAX_DROPS = 180;
    public const int MAX_STREAKS = 1200;
    public const int MAX_BEADS = 200;
    public const int MAX_RINGS = 150;
    public const int MAX_SPLASHES = 200;
    public const int MAX_PATCHES = 120;

    private const float RING_EVERY = 0.055f;
    private const float DROP_EVERY = 0.09f;
    private const float FACE_REFRESH = 0.25f;

    public sealed class Drop
    {
        public float X, Y, Base, Hold, V, Size, LastWet, NextMeander;
    }

    public record struct Fade(float X, float Y, float Size, float Age, float Life, float Alpha);

    public record struct Splash(float X, float Y, float VX, float VY, float Age, float Life);

    private readonly Random Random;
    private readonly List<WallFace> VisibleFaces = [];
    private float NextRing;
    private float NextDrop;
    private float NextFaceRefresh;

    public List<Drop> Drops { get; } = [];
    public List<Fade> Streaks { get; } = [];
    public List<Fade> Beads { get; } = [];
    public List<Fade> Rings { get; } = [];
    public List<Fade> Patches { get; } = [];
    public List<Splash> Splashes { get; } = [];

    public WetWallSimulation(Random random) => Random = random;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    public void Clear()
    {
        Drops.Clear();
        Streaks.Clear();
        Beads.Clear();
        Rings.Clear();
        Patches.Clear();
        Splashes.Clear();
        VisibleFaces.Clear();
        NextFaceRefresh = 0f;
    }

    public void Step(float dt, Rectangle visibleWorld, MapSurface? surface, bool spawning)
    {
        if (spawning && surface is not null)
            Spawn(dt, visibleWorld, surface);

        for (var i = Drops.Count - 1; i >= 0; i--)
            if (StepDrop(Drops[i], dt))
                RemoveSwapBack(Drops, i);

        Age(Streaks, dt);
        Age(Beads, dt);
        Age(Rings, dt);
        Age(Patches, dt);

        for (var i = Splashes.Count - 1; i >= 0; i--)
        {
            var s = Splashes[i];
            s.Age += dt;

            if (s.Age >= s.Life)
            {
                RemoveSwapBack(Splashes, i);

                continue;
            }

            s.VY += 240f * dt;
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            Splashes[i] = s;
        }
    }

    private void Spawn(float dt, Rectangle visibleWorld, MapSurface surface)
    {
        NextFaceRefresh -= dt;

        if (NextFaceRefresh <= 0f)
        {
            NextFaceRefresh = FACE_REFRESH;
            RefreshFaces(visibleWorld, surface);
        }

        NextRing -= dt;

        while (NextRing <= 0f)
        {
            NextRing += RING_EVERY;
            var x = Roll(visibleWorld.Left, visibleWorld.Right);
            var y = Roll(visibleWorld.Top, visibleWorld.Bottom);

            if (surface.IsFloorAtWorld(x, y))
            {
                AddRing(x, y, Roll(8f, 15f));

                if (Random.NextSingle() < 0.35f)
                    AddSplashes(x, y, 2, 42f);
            }
        }

        NextDrop -= dt;

        while (NextDrop <= 0f)
        {
            NextDrop += DROP_EVERY;

            if ((VisibleFaces.Count > 0) && (Drops.Count < MAX_DROPS))
                AddDrop(VisibleFaces[Random.Next(VisibleFaces.Count)]);
        }
    }

    private void RefreshFaces(Rectangle visibleWorld, MapSurface surface)
    {
        VisibleFaces.Clear();

        foreach (var face in surface.WallFaces)
        {
            var foot = MathF.Max(face.BaseWorldY, face.BaseWorldY + (face.BaseSlope * MapSurface.FACE_WIDTH));

            if ((face.WorldX + MapSurface.FACE_WIDTH >= visibleWorld.Left)
                && (face.WorldX <= visibleWorld.Right)
                && (foot >= visibleWorld.Top)
                && (foot - face.Height <= visibleWorld.Bottom))
                VisibleFaces.Add(face);
        }
    }

    private void AddDrop(WallFace face)
    {
        var column = Random.Next(MapSurface.FACE_WIDTH);
        var foot = face.BaseWorldY + (face.BaseSlope * column);

        Drops.Add(new Drop
        {
            X = face.WorldX + column,
            Y = foot - face.Height + Roll(0f, face.Height * 0.3f),
            Base = foot,
            Hold = Roll(0.2f, 1.1f),
            Size = Roll(1f, 1.3f),
            LastWet = float.MinValue
        });
    }

    private bool StepDrop(Drop drop, float dt)
    {
        if (drop.Hold > 0f)
        {
            drop.Hold -= dt;
            drop.Size = MathF.Min(1.9f, drop.Size + (dt * 0.7f));

            if (drop.Hold <= 0f)
                drop.V = Roll(35f, 65f);

            return false;
        }

        var before = drop.Y;
        drop.V = MathF.Min(180f, drop.V + (280f * drop.Size * dt));
        drop.Y += drop.V * dt;
        drop.Size = MathF.Max(0.9f, drop.Size - (dt * 0.35f));

        if (drop.Y >= drop.NextMeander)
        {
            drop.NextMeander = drop.Y + Roll(7f, 16f);
            drop.X += Random.Next(4) switch { 0 => -1, 1 => 1, _ => 0 };
        }

        if (drop.Y - drop.LastWet >= 6f)
        {
            drop.LastWet = drop.Y;

            if (Streaks.Count < MAX_STREAKS)
                Streaks.Add(new Fade(drop.X, MathF.Floor(before), 7f, 0f, Roll(3.5f, 6.5f), Roll(0.14f, 0.26f)));

            if ((Random.NextSingle() < 0.28f) && (Beads.Count < MAX_BEADS))
                Beads.Add(new Fade(drop.X, MathF.Floor(drop.Y - 4f), 2f, 0f, Roll(1.1f, 2.8f), Roll(0.55f, 0.9f)));
        }

        if (drop.Y >= drop.Base)
        {
            AddRing(drop.X, drop.Base + 1f, Roll(7f, 11f));
            AddSplashes(drop.X, drop.Base, 3, 58f);

            if (Patches.Count < MAX_PATCHES)
                Patches.Add(new Fade(drop.X, drop.Base + 1f, Roll(5f, 10f), 0f, Roll(4f, 7f), 0.2f));

            return true;
        }

        if (Random.NextSingle() < (dt * 1.5f))
        {
            drop.Hold = Roll(0.12f, 0.6f);
            drop.V = 0f;
        }

        return false;
    }

    private void AddRing(float x, float y, float radius)
    {
        if (Rings.Count < MAX_RINGS)
            Rings.Add(new Fade(x, y, radius, 0f, Roll(0.5f, 0.85f), 0.55f));
    }

    private void AddSplashes(float x, float y, int count, float up)
    {
        for (var i = 0; (i < count) && (Splashes.Count < MAX_SPLASHES); i++)
            Splashes.Add(new Splash(x, y, Roll(-24f, 24f), Roll(-up, -up * 0.45f), 0f, Roll(0.22f, 0.4f)));
    }

    private static void Age(List<Fade> list, float dt)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var item = list[i];
            item.Age += dt;

            if (item.Age >= item.Life)
                RemoveSwapBack(list, i);
            else
                list[i] = item;
        }
    }

    private static void RemoveSwapBack<T>(List<T> list, int index)
    {
        list[index] = list[^1];
        list.RemoveAt(list.Count - 1);
    }
}
```

- [ ] **Step 4: Write `WetWallRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class WetWallRenderer : AmbientOverlayBase, IMapAwareOverlay
{
    internal WetWallSimulation Simulation { get; } = new(new Random());
    private MapSurface? Surface;
    private Texture2D? Pixel;
    private Texture2D? RingTexture;
    private Texture2D? Patch;

    public override BlendState BlendState => BlendState.NonPremultiplied;

    public bool HasAllocatedTextures => Pixel is not null;

    public void SetSurface(MapSurface? surface)
    {
        Simulation.Clear();
        Surface = surface;
    }

    protected override void ResetState() => Simulation.Clear();

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        var visible = new Rectangle(viewport.X - (int)worldOrigin.X, viewport.Y - (int)worldOrigin.Y, viewport.Width, viewport.Height);

        Simulation.Step(dt, visible, Surface, Spawning);
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        var device = spriteBatch.GraphicsDevice;
        Pixel ??= AmbientTextures.Pixel(device);
        RingTexture ??= AmbientTextures.Ring(device);
        Patch ??= AmbientTextures.SoftDot(device);

        var ox = (int)worldOrigin.X;
        var oy = (int)worldOrigin.Y;

        foreach (var p in Simulation.Patches)
        {
            var k = p.Age / p.Life;
            spriteBatch.Draw(Patch, new Rectangle((int)(p.X - p.Size) + ox, (int)(p.Y - (p.Size / 2f)) + oy, (int)(p.Size * 2f), (int)p.Size), Tint(4, 10, 30, p.Alpha * (1f - k) * EffectAlpha));
        }

        foreach (var s in Simulation.Streaks)
            spriteBatch.Draw(Pixel, new Rectangle((int)s.X + ox, (int)s.Y + oy, 2, (int)s.Size), Tint(4, 10, 30, s.Alpha * (1f - (s.Age / s.Life)) * EffectAlpha));

        foreach (var r in Simulation.Rings)
        {
            var k = r.Age / r.Life;
            DrawRing(spriteBatch, r.X + ox, r.Y + oy, r.Size * k, 0.55f * (1f - k));

            if (k > 0.3f)
                DrawRing(spriteBatch, r.X + ox, r.Y + oy, r.Size * (k - 0.3f), 0.3f * (1f - k));
        }

        foreach (var b in Simulation.Beads)
            spriteBatch.Draw(Pixel, new Rectangle((int)b.X + ox, (int)b.Y + oy, 1, 2), Tint(226, 238, 252, b.Alpha * (1f - (b.Age / b.Life)) * EffectAlpha));

        foreach (var drop in Simulation.Drops)
            DrawDrop(spriteBatch, drop, ox, oy);

        foreach (var s in Simulation.Splashes)
            spriteBatch.Draw(Pixel, new Rectangle((int)s.X + ox, (int)s.Y + oy, 1, 1), Tint(226, 238, 252, 0.85f * (1f - (s.Age / s.Life)) * EffectAlpha));
    }

    private void DrawRing(SpriteBatch spriteBatch, float x, float y, float radius, float alpha)
    {
        if (radius < 1f)
            return;

        spriteBatch.Draw(RingTexture!, new Rectangle((int)(x - radius), (int)(y - (radius / 2f)), (int)(radius * 2f), (int)radius), Tint(205, 225, 245, alpha * EffectAlpha));
    }

    private void DrawDrop(SpriteBatch spriteBatch, WetWallSimulation.Drop drop, int ox, int oy)
    {
        var x = (int)drop.X + ox;
        var y = (int)drop.Y + oy;
        var big = drop.Size >= 1.4f;

        if (drop.Hold <= 0f)
        {
            var tail = MathF.Min(18f, drop.V * 0.12f);

            for (var n = 1; n < tail; n += 2)
                spriteBatch.Draw(Pixel!, new Rectangle(x, y - n - 2, 1, 2), Tint(214, 232, 250, 0.55f * (1f - (n / tail)) * EffectAlpha));
        }

        var body = Tint(176, 205, 235, 0.8f * EffectAlpha);
        spriteBatch.Draw(Pixel!, new Rectangle(x, y - 3, 1, 2), body);
        spriteBatch.Draw(Pixel!, new Rectangle(x - (big ? 1 : 0), y - 1, big ? 3 : 2, 2), body);

        if (big)
            spriteBatch.Draw(Pixel!, new Rectangle(x, y + 1, 1, 1), body);

        spriteBatch.Draw(Pixel!, new Rectangle(x, y - 1, 1, 1), Tint(248, 252, 255, 0.95f * EffectAlpha));
    }

    public override void ReleaseTextures()
    {
        Pixel?.Dispose();
        Pixel = null;
        RingTexture?.Dispose();
        RingTexture = null;
        Patch?.Dispose();
        Patch = null;
    }
}
```

- [ ] **Step 5: Write `SteamRenderer.cs`.**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class SteamRenderer : AmbientOverlayBase, IMapAwareOverlay
{
    private const float EMIT_EVERY = 0.27f;
    private const int MAX_VENTS = 16;
    private const int MAX_PUFFS = 150;
    private const float VENT_MARGIN = 80f;

    private sealed class Puff
    {
        public float X, Y, VX, VY, Age, Life, Start;
    }

    private readonly Random Random = new();
    private readonly List<Puff> Puffs = [];
    private MapSurface? Surface;
    private Texture2D? Dot;
    private float NextEmit;

    public override BlendState BlendState => BlendState.NonPremultiplied;

    public bool HasAllocatedTextures => Dot is not null;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    public void SetSurface(MapSurface? surface)
    {
        Puffs.Clear();
        Surface = surface;
    }

    protected override void ResetState() => Puffs.Clear();

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        NextEmit -= dt;

        if ((NextEmit <= 0f) && Spawning && Surface is not null)
        {
            NextEmit = EMIT_EVERY;
            Emit(viewport, worldOrigin, Surface);
        }

        for (var i = Puffs.Count - 1; i >= 0; i--)
        {
            var puff = Puffs[i];
            puff.Age += dt;

            if (puff.Age >= puff.Life)
            {
                Puffs.RemoveAt(i);

                continue;
            }

            puff.X += puff.VX * dt;
            puff.Y += puff.VY * dt;
        }
    }

    private void Emit(Rectangle viewport, Vector2 worldOrigin, MapSurface surface)
    {
        var mapHeight = surface.Height;
        var left = viewport.Left - worldOrigin.X - VENT_MARGIN;
        var right = viewport.Right - worldOrigin.X + VENT_MARGIN;
        var top = viewport.Top - worldOrigin.Y - VENT_MARGIN;
        var bottom = viewport.Bottom - worldOrigin.Y + VENT_MARGIN;
        var emitted = 0;

        foreach (var vent in surface.SteamVents)
        {
            var world = Camera.TileToWorld(vent.X, vent.Y, mapHeight) + new Vector2(28f, 14f);

            if ((world.X < left) || (world.X > right) || (world.Y < top) || (world.Y > bottom))
                continue;

            if (Puffs.Count >= MAX_PUFFS)
                return;

            Puffs.Add(new Puff
            {
                X = world.X + Roll(-8f, 8f),
                Y = world.Y,
                VX = Roll(-6f, 9f),
                VY = Roll(-42f, -26f),
                Life = Roll(1.6f, 2.8f),
                Start = Roll(10f, 18f)
            });

            if (++emitted >= MAX_VENTS)
                return;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Dot ??= AmbientTextures.SoftDot(spriteBatch.GraphicsDevice);

        foreach (var puff in Puffs)
        {
            var k = puff.Age / puff.Life;
            var size = puff.Start * (1f + (3.2f * k));
            var alpha = 0.42f * MathF.Sin(MathF.PI * MathF.Min(1f, 1.15f * k)) * EffectAlpha;
            var x = puff.X + worldOrigin.X - (size / 2f);
            var y = puff.Y + worldOrigin.Y - (size / 2f);

            spriteBatch.Draw(Dot, new Rectangle((int)x, (int)y, (int)size, (int)size), Tint(240, 240, 245, alpha));
        }
    }

    public override void ReleaseTextures()
    {
        Dot?.Dispose();
        Dot = null;
    }
}
```

- [ ] **Step 6: Add to `Overlays`** in the positions from spec section 5.

```csharp
(MapFlags.Rain, new WetWallRenderer()),
```

immediately after the `DappledLight` entry, and:

```csharp
(MapFlags.RisingSteam, new SteamRenderer()),
```

immediately before the `Fog` entry.

- [ ] **Step 7: Run the Verify command and confirm all pass.**

```json:metadata
{"files": ["Chaos.Client.Rendering/WetWallSimulation.cs", "Chaos.Client.Rendering/WetWallRenderer.cs", "Chaos.Client.Rendering/SteamRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Tests/Chaos.Client.Tests/WetWallSimulationTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/(WetWallSimulationTests)|(AmbientEffectsTests)/*\"", "acceptanceCriteria": ["long run stays within every cap", "drops stay above their wall foot", "Clear empties and SetSurface calls it", "null or empty surface spawns nothing", "nothing spawns while not spawning", "EveryEffectFlag_HasAnOverlay passes"], "modelTier": "standard"}
```

### Task 7: Unora dialog and docs

**Goal:** Add the third theatre dialog and document the new effects.

**Files:**
- Create: `../Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects3.json`
- Modify: `../Unora/docs/ambient-map-effects.md`, `docs/ai/isometric.md`

**Acceptance Criteria:**
- [ ] The dialog JSON parses and has `templateKey` `suomitheatre_stageeffects3`.
- [ ] The doc lists the nine effects and the Rain upgrade, shows bits 33-41 as used, and says bits 42-63 (22 slots) are free.
- [ ] `isometric.md` mentions wall water and `MapSurface` in the ambient draw-order line.

**Verify:** `python -c "import json;d=json.load(open('../Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects3.json'));print(d['templateKey'])"` → `suomitheatre_stageeffects3`

**Steps:**

- [ ] **Step 1: Create the dialog file.**

```json
{
  "options": [],
  "scriptKeys": [
    "suomitheatre"
  ],
  "scriptVars": {},
  "templateKey": "suomitheatre_stageeffects3",
  "text": "Light and sky! Choose an effect to start it on the stage, or choose it again to stop it.",
  "type": "DialogMenu"
}
```

- [ ] **Step 2: Update `ambient-map-effects.md`.** Add one effects-table row for each flag with this text: Sunbeams "Warm slanted shafts of light"; Moonbeams "Cool blue slanted shafts"; DappledLight "Patches of sun fixed to the ground"; Aurora "Rippling ribbons across the top of the view"; AuroraGlow "Faint colored light over the whole view"; ShootingStars "Thin streaks every 5-12 seconds"; Meteors "Burning streaks with sparks every 3-6 seconds"; Rainbow "A faint arc that slowly brightens and dims"; RisingSteam "Puffs from fixed floor tiles". Add a Rain note: rainy maps also get wall water, floor rings and splashes, and a dark map never does. Change the first free bit from 31 to 42. Change "33 more slots" to "22 more slots. Bits 42-63 are free."

- [ ] **Step 3: Update `isometric.md` line 25.** After "fog, lightning, mists, particles", add "wall water and steam (anchored to tiles through `MapSurface`)".

```json:metadata
{"files": ["../Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects3.json", "../Unora/docs/ambient-map-effects.md", "docs/ai/isometric.md"], "verifyCommand": "python -c \"import json;d=json.load(open('../Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects3.json'));print(d['templateKey'])\"", "acceptanceCriteria": ["dialog parses with the right key", "doc lists effects and free bits", "isometric note updated"], "modelTier": "mechanical"}
```

### Task 8: Commit the full implementation

**Goal:** Make one commit per repo on the feature branches.

**Files:**
- Modify: no new source. Commits the working trees of `Chaos-Server`, `Chaos.Client` and `Unora`.

**Acceptance Criteria:**
- [ ] Server, client and Unora each have one new commit on `feat/more-map-effects`.
- [ ] The client commit moves its `Chaos-Server` pointer to the server commit.
- [ ] Nothing is merged or pushed.

**Verify:** `git log -1 --oneline feat/more-map-effects` in each repo → the new commit

**Steps:**

- [ ] **Step 1: Commit the server** by explicit path, with the message `feat: nine new map effect flags and third theatre page`.

- [ ] **Step 2: Commit the client** by explicit path, including the spec, this plan and the `Chaos-Server` pointer, with the message `feat: light, sky, steam and wet wall map effects`.

- [ ] **Step 3: Commit Unora** by explicit path, with the message `feat: third theatre effects page and effect docs`.

```json:metadata
{"files": [], "verifyCommand": "git log -1 --oneline feat/more-map-effects", "acceptanceCriteria": ["one commit per repo", "client pointer moved to server commit", "nothing merged or pushed"], "modelTier": "mechanical"}
```

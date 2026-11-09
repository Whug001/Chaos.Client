# More map effects: light, sky, steam and wet walls

Designed 2026-11-09. It continues `2026-09-25-more-ambient-effects-design.md`. Bats and the other Halloween effects shipped separately. Crows were previewed and rejected. Heat shimmer and water wobble remain unbuilt and are not part of this work.

The previews the user approved are in `.superpowers/brainstorm/304628-1794216901/content/`: `new-effects-looks.html`, `more-effect-ideas-v2.html` and `rain-on-walls-v2.html`. Where a number below is unclear, the preview's JavaScript is the reference.

## Goal

- Nine new map flags: Sunbeams, Moonbeams, DappledLight, Aurora, AuroraGlow, ShootingStars, Meteors, Rainbow and RisingSteam.
- The existing Rain effect gains water running down walls, rings and splashes on the floor.
- Every new effect can be started from the Suomi theatre.

## Decisions

| Question | Decision |
|---|---|
| Structure | New renderers behind the existing `IAmbientOverlay`. |
| Wall water | Part of Rain, with no new flag. Every rainy map gets it. |
| Layering | Drawn on top of the scene like every other overlay. Water can briefly cross a character or an object in front of a wall. Accepted. |
| Meteor rate | Every 3-6 seconds, a little more often than the 4-8 seconds previewed. |
| Maps | No map gets these effects in this work. |
| Client version | Unchanged. The packet size doesn't change, and a client ignores bits it has no overlay for. |

## 1. Flags (`Chaos-Server`)

Add to `MapFlags` in `Chaos.DarkAges/Definitions/Enums.cs`, after `Balloons = 1UL << 32`:

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

`MapFlagVisibility.ATMOSPHERE` adds all nine, so `/atmosphere` hides them. It already covers Rain.

## 2. Map knowledge (`Chaos.Client.Rendering`)

New class `MapSurface`, built once per map from the `MapFile`. It holds no GPU resources.

```csharp
public sealed class MapSurface
{
    public static MapSurface Build(int mapId, MapFile map, Func<int, bool> isWall, Func<int, bool> isDoor, Func<int, bool> isBlockedGround, Func<int, int> foregroundHeight);
    public int MapId { get; }
    public IReadOnlyList<WallFace> WallFaces { get; }
    public bool IsFloor(int tileX, int tileY);
    public IReadOnlyList<Point> SteamVents { get; }
}

public readonly record struct WallFace(float WorldX, float BaseWorldY, float BaseSlope, int Height);
```

- `WorldScreen.Map.cs` passes its existing `IsTileWall`, `DoorTable.IsDoorTileId` and ground attribute checks. `foregroundHeight` is a new public `MapRenderer.GetForegroundHeight(tileId)`, which reads the same atlas region or texture `DrawSingleFgTile` uses.
- **Floor:** no wall foreground, no door, and a background that isn't walk-blocking.
- **Wall run:** at least two wall tiles side by side along the X or Y axis. A lone wall tile, such as a lamp post, is not used.
- **Wall face:** the half-sprite of a wall tile in a run, with height at least 45 px. A left-foreground sprite is skipped when the tile at `y + 1` is also a wall. A right-foreground sprite is skipped when the tile at `x + 1` is also a wall. Those faces are hidden behind other walls.
- **Base line:** each face is 28 px wide. For column `c` in 0..27, a left face's base is `worldY + 14 + c / 2`. A right face's base is `worldY + 28 - c / 2`. Check both against real walls in game, and correct the slope sign if a drop stops above or below the wall's foot.
- **Steam vents:** floor tiles where a hash of `(mapId, x, y)` modulo 60 is 0. The hash must be stable, so don't use `HashCode.Combine`. Every player sees the same spots on every visit.
- A map with no floor or no walls gives empty lists. The effects then simply draw nothing.

New interface beside `IAmbientOverlay`:

```csharp
public interface IMapAwareOverlay : IAmbientOverlay
{
    void SetSurface(MapSurface? surface);
}
```

`AmbientEffects.SetMapSurface(MapSurface?)` passes it to each overlay that implements `IMapAwareOverlay`. `WorldScreen` calls it where the map file is loaded, before `AmbientEffects.Apply`. It clears the surface when the map is unloaded.

## 3. Rain fix

`MapFlags.Darkness` equals `Rain | Snow`, so `flags.HasFlag(MapFlags.Rain)` is true on every dark map. `AmbientEffects.Apply` must use this check for an overlay whose flag is Rain:

```csharp
(flags & MapFlags.Darkness) == MapFlags.Rain
```

Every other flag keeps `HasFlag`. Add a test for this.

## 4. Renderers

All new renderers implement `IAmbientOverlay`, fade with `FadeSeconds` like `MistRenderer`, make their textures on first use, and release them in `Dispose` and through the existing `ReleaseInactiveTextures` path. Sizes are in the game's 640-wide viewport pixels. Scale nothing unless the viewport is wider than 640, then scale positions by `viewport.Width / 640`.

### LightShaftRenderer (Sunbeams, Moonbeams), additive

Texture 64x256: alpha is a triangle across the width, and fades from 1 at the top to 0 at the bottom. Each beam is drawn from `(beam.X, -40)`, rotated 0.42 rad, with size `(Width, 430)`. Alpha is `A * (Base + Depth * sin(τ * F * clock + P))`. X moves right at `D` px/sec and wraps from 840 to 40.

| Style | Tint | Beams | Width | A | Base / Depth | F (Hz) | D (px/sec) |
|---|---|---|---|---|---|---|---|
| Sunbeams | 255,232,170 | 6 | 50-120 | 0.4-0.7 | 0.6 / 0.4 | 0.08-0.18 | 2-6 |
| Moonbeams | 170,200,255 | 5 | 50-110 | 0.35-0.6 | 0.65 / 0.35 | 0.06-0.14 | 1.5-4 |

### DappleRenderer (DappledLight), additive

26 soft ellipses, color 255,226,150, radius 30-80, drawn `2r` by `r`. Alpha is `A * (0.55 + 0.3 * sin(τ * F * clock + P) + 0.15 * sin(τ * F * 2.7 * clock + P))`, with `A` 0.35-0.65 and `F` 0.25-0.7. Positions live in a repeating 900x600 world-space area and are drawn relative to `worldOrigin`, so patches stay fixed to the map. They drift 5, 2 px/sec.

### AuroraRenderer (Aurora, AuroraGlow), additive

Three ribbons, each drawn as vertical strips across the viewport. Strip width must equal the step, or overlapping strips show as stripes.

Colors: 90,255,160, then 70,220,230, then 170,110,255. Phases 0, 2.1, 4.2.

- **Aurora:** step 4. Texture 8x128 with stops 0: alpha 0, 0.75: alpha 0.5, 0.93: alpha 1, 1: alpha 0. Bases 125, 95, 150. `y = base + 22 sin(0.012x + 0.35t + ph) + 10 sin(0.031x - 0.6t + 2ph)`. `h = 110 + 30 sin(0.02x + 0.2t + ph)`. `s = 0.5 + 0.5 sin(0.017x + 0.5t + ph)`. Alpha is `s² * 0.55 + 0.1`. Draw at `(x, y - h)`, size `(4, h)`.
- **AuroraGlow:** step 6. Texture 8x128 with stops 0: alpha 0, 0.5: alpha 1, 1: alpha 0. Speeds 0.3, -0.22, 0.17. `y = 0.5H + 70 sin(0.008x + sp * t + ph) - 0.65H`. `s = 0.5 + 0.5 sin(0.011x + 1.7 * sp * t + ph)`. Alpha is `s² * 0.26 + 0.03`. Draw at `(x, y)`, size `(6, 1.3H)`.

### StreakRenderer (ShootingStars, Meteors), additive

Spawn at `x` in `0.4W..1.1W` (`0.5W..1.1W` for Meteors) and `y` in `-10..0.4H` (`-20..0.25H`). Direction is down-left along `(-0.86, 0.5)`. Fade is `sin(π * age / life)`.

| Style | Gap (sec) | Speed (px/sec) | Life (sec) | Tail (px) | Width | Colors |
|---|---|---|---|---|---|---|
| ShootingStars | 5-12 | 615 | 0.55 | 90 | 1.5 | 235,245,255 fading to 160,190,255 |
| Meteors | 3-6 | 295 | 1.3 | 130 | 3 | 255,240,200, then 255,170,90 at 30%, then 255,90,40 |

Meteors also draw a soft 32 px head in 255,190,110 at `fade * 0.8`. They drop a spark every 0.03 sec: 2x2 px, 255,190,110, life 0.4-0.8 sec, velocity X -14..14 and Y 20..55, alpha falling from 1 to 0.

### RainbowRenderer (Rainbow), additive

One viewport-sized texture. Center `(0.55W, 1.25H)`, ring radius 300 to 345. Colors in order: 255,60,60, then 255,150,40, then 255,240,70, then 80,230,90, then 70,150,255, then 150,80,240, with alpha 0 just outside each end. Vertically, alpha multiplies by 1 at the top, 0.7 at 60% and 0 at the bottom. Draw alpha is `0.34 + 0.12 sin(0.5t)`. Rebuild the texture if the viewport size changes.

### SteamRenderer (RisingSteam), alpha blend, `IMapAwareOverlay`

Uses `SteamVents`, in world space. Only vents on screen emit, up to 16. Each emits a puff every 0.27 sec. Puff: start within 8 px of the vent, velocity X -6..9 and Y -42..-26, life 1.6-2.8 sec, start size 10-18, size `s * (1 + 3.2k)` where `k = age / life`. Color 240,240,245 soft dot, alpha `0.42 sin(π * min(1, 1.15k))`. Cap of 150 puffs.

### WetWallRenderer (Rain), alpha blend, `IMapAwareOverlay`

Everything is stored in world space. Every 0.25 sec the renderer rebuilds its list of wall faces on screen, from `MapSurface.WallFaces`. When no face is on screen, it still draws floor rings.

Caps: 180 drops, 1200 wet streaks, 200 beads, 150 rings, 200 splash pixels, 120 damp patches. Stop spawning at a cap.

- **Floor rings:** one attempt every 0.055 sec at a random viewport point. Skip unless `IsFloor` holds for the tile under it. Radius 8-15, life 0.5-0.85 sec. Ellipse height is half the width. Color 205,225,245. Alpha `0.55(1 - k)`. After `k` 0.3, also draw an inner ring of radius `r(k - 0.3)` at `0.3(1 - k)`. Draw rings with a 1-pixel ring texture or short line segments, whichever the implementer finds cheaper. 35% of floor rings also make 2 splash pixels with upward speed up to 42.
- **Drops:** one every 0.09 sec on a random on-screen face and column, starting in the top 30% of the face.
  - Start with `hold` 0.2-1.1 sec and `size` 1-1.3.
  - While held, `size` grows by 0.7/sec to at most 1.9.
  - On release, `v` is 35-65. Then `v += 280 * size * dt`, capped at 180, and `size` falls by 0.35/sec to at least 0.9.
  - Each 7-16 px travelled, the X column moves by -1, 0, 0 or +1, chosen at random.
  - Moving drops stop for 0.12-0.6 sec, at a chance of 1.5 per second, and `v` resets to 0.
- **Wet streaks:** each 6 px travelled, add a 2x7 px rectangle, color 4,10,30, alpha 0.14-0.26 fading to 0 over 3.5-6.5 sec. 28% of the time, also leave a 1x2 px bead, color 226,238,252, alpha 0.55-0.9, life 1.1-2.8 sec.
- **Drop shape:** upper part 1x2 px at `y - 3`, body 2x2 px at `y - 1`, or 3x2 px when `size >= 1.4`, plus 1 px at `y + 1` when large. Color 176,205,235 at alpha 0.8. Highlight 1 px at `y - 1`, color 248,252,255, alpha 0.95. A moving drop has a 1 px tail of `min(18, v * 0.12)` px, 214,232,250, alpha `0.55(1 - n / tail)`.
- **At the base:** add a ring of radius 7-11, 3 splash pixels with upward speed up to 58, and a dark ellipse (color 4,10,30, alpha `0.2(1 - k)`, radius 5-10, life 4-7 sec).
- **Splash pixels:** velocity X -24..24, Y from `-up` to `-0.45 * up`, gravity 240, life 0.22-0.4 sec, alpha `0.85(1 - k)`.

Draw order inside the renderer: damp patches, wet streaks, rings, beads, drops, splashes.

The existing `WeatherRenderer` rain streaks don't change.

## 5. Draw order

In `AmbientEffects.Overlays`, keep existing entries in their relative order and add:

| New entry | Position |
|---|---|
| `DappledLight` | Right after `CloudShadows` |
| `Rain` (wet walls) | Right after `DappledLight` |
| `Rainbow`, `Sunbeams`, `Moonbeams`, `AuroraGlow`, `Aurora`, `ShootingStars`, `Meteors` | In that order, right before `Lightning` |
| `RisingSteam` | Right before `Fog` |

## 6. Theatre third page

Page 2 currently has 16 effects, plus Clear All and Back, making 18 rows. That's the panel's limit, so adding "More Effects" needs one effect to move.

Server, `TheatreStageEffects`:

- Move Balloons to page 3, leaving 15 effects on page 2.
- Page 3, in order: Balloons, Sunbeams, Moonbeams, Dappled Light, Aurora, Aurora Glow, Shooting Stars, Meteors, Rainbow, Rising Steam.

Server, `suomiTheatreScript`:

- Page 2 rows end with Clear All, More Effects (to `suomitheatre_stageeffects3`) and Back (to page 1).
- New page 3 cases in `OnDisplaying` and `OnNext`, matching page 2's pattern. Rows end with Clear All and Back, which goes to page 2.

Unora: new `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects3.json`, copied from the second page's file with `templateKey` changed and this text: "Light and sky! Choose an effect to start it on the stage, or choose it again to stop it."

## 7. Testing

Server (`Tests/Chaos.Tests`):

- `MapEffectsPacketConverterTests`: the nine flags round-trip.
- `MapFlagVisibilityTests`: the nine flags are included.
- `TheatreStageEffectsTests`: page 2 has 15 effects, page 3 has the 10 above, and Clear All stops a page-3 effect.

Client (`Tests/Chaos.Client.Tests`):

- `MapSurface.Build`: a lone wall tile gives no faces, a two-tile run does, a short sprite is skipped, doors are skipped, and hidden faces are skipped.
- `MapSurface`: an empty map gives empty lists, and steam vents are the same on two builds.
- `AmbientEffects`: the Rain overlay is on for Rain, and off for Darkness and None.
- The `CoveredFlags` guard covers the nine new flags.

In game, with the server and client closed before building because they lock the build:

- On a test map, use `/mapFlag add` for each new flag, and compare with its preview.
- On a rainy town map, check that drops stay on building walls, stop at each wall's foot, skip doors, and rings stay off walls. Check how trees look.
- On a dark map, confirm no wall water appears.
- Walk around, and confirm dappled light, steam and wall water stay fixed to the map.
- Change maps, and confirm nothing carries over and no error appears.
- Watch the frame rate on the largest rainy town map with wall water on.
- Theatre: page 2 reaches page 3, and Back and Clear All work on page 3.
- `/atmosphere` hides the new effects.

## 8. Docs

- `Unora/docs/ambient-map-effects.md`: add the nine effects and the Rain upgrade, list bits 33-41 as used, and change the free slot count to 22 (bits 42-63).
- `Chaos.Client/CLAUDE.md`: no change unless a listed convention changes.
- `Chaos.Client/docs/ai/isometric.md`: mention wet walls and `MapSurface` in the draw-order note.

## 9. Delivery

- Each repo gets a `feat/more-map-effects` branch in its own worktree.
- One commit per repo at the end: server first, then client with its `Chaos-Server` pointer moved to the server commit, then Unora.
- Nothing is merged or pushed without the user's go-ahead.

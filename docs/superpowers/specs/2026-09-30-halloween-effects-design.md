# Halloween ambient effects: Bats, Ghosts, Harvest Moon

Date: 2026-09-30. Status: design approved in chat; spec awaiting review.

## Goal

Add three Halloween map effects that work like the existing ones (Fog, Leaves, Wisps and the rest):

| Flag | Look (picked from browser previews) |
|---|---|
| `Bats` | Look 1B. Groups of 4–8 black bats, 19 px wide, cross the screen and dart about. A quiet gap of 8–20 s follows each group. |
| `Ghosts` | Look 2A. A few pale sheet ghosts with dark eyes float upward, sway, and fade fully in and out. Their hems ripple. |
| `HarvestMoon` | Look 3A. An orange evening wash that breathes slowly, dark edges, and pumpkin-orange sparks that rise and flicker gently. |

The preview page is `.superpowers/brainstorm/836-1790811084/content/halloween-looks.html` in the main Chaos.Client
checkout. Its JavaScript holds the exact sprites and numbers this spec copies.

## Decisions already made

- The effects are switched on per map by flags, the same as every other effect. There is no seasonal or date-based
  switch.
- No map gets the flags now. Admins can try them with `/mapFlag add Bats`.
- The Suomi theatre stage-effects menu lists all three on page 2.
- The Mileth festival list does **not** get them.
- None of the three go in the F4 "Triggering or Unsettling Map Effects" section. None of them flash. The Harvest Moon
  sparks flicker at 1–2.5 times a second, slower than Embers (3–7), which is not in that section either.

## Part 1: server (`Chaos.Client/Chaos-Server`)

1. `Chaos.DarkAges/Definitions/Enums.cs`, `MapFlags`: add
   - `Bats = 1UL << 28`
   - `Ghosts = 1UL << 29`
   - `HarvestMoon = 1UL << 30`
2. `Chaos/Utilities/MapFlagVisibility.cs`: add the three flags to the ambient set, after `Drips`. This makes
   `/atmosphere` hide them along with the other effects.
3. `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs`: add three page-2 entries after Drips:
   `new("Bats", MapFlags.Bats, 2)`, `new("Ghosts", MapFlags.Ghosts, 2)` and
   `new("Harvest Moon", MapFlags.HarvestMoon, 2)`. Page 2 then holds 14 of its 16 slots.
4. Tests: update `Tests/Chaos.Tests/MapFlagVisibilityTests.cs` and `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`
   where they list effects or count page 2. Add the three flags to the `AmbientFlags` list in
   `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`. The packet code itself needs no change, because
   it already carries all 8 bytes.

No packet change and no CLIENT_VERSION bump. An older client receives the new bits and draws nothing for them.

## Part 2: client (`Chaos.Client.Rendering`)

### 2a. Pixel sprite helper (new: `SpriteGrid.cs`)

A static helper that turns a text grid into a texture:

- `'X'` becomes opaque white. The draw call tints it with the particle's or bat's color.
- `'o'` becomes an opaque dark detail color, passed in by the caller. The draw tint multiplies it too. The ghost tint
  is near-white, so the eyes stay close to the dark color. Bats use no `'o'`.
- `'.'` becomes clear.

Signature: `Texture2D Build(GraphicsDevice device, IReadOnlyList<string> rows, Color dark)`. A pure function
`Color[] ToPixels(IReadOnlyList<string> rows, Color dark)` does the work so tests can check it without a graphics
device. All rows in one sprite must be the same width; `ToPixels` throws `ArgumentException` otherwise.

### 2b. Harvest Moon (`MistStyle.HarvestMoon`, `ParticleStyle.HarvestSparks`)

`MistStyle.HarvestMoon`:

| Field | Value |
|---|---|
| WashColor / WashAlpha | (120, 55, 10) / 0.22 |
| Pulse / PulseDepth / PulsePeriod | Sine / 0.12 / 6 s |
| LayerTint | (230, 120, 40) |
| Layers | (3.5, 0.14, (4, 0)), (2.5, 0.18, (7, -1)) |
| Seed / NoiseKnee / NoiseRange | 1031 / 0.45 / 0.45 |
| VignetteColor / VignetteAlpha | (25, 8, 0) / 0.45 |

`ParticleStyle.HarvestSparks`: Count 22, Dot shape, additive, colors (255, 140, 30), (255, 170, 50), (255, 110, 20),
size 2–3, velocity (-6, -22) to (6, -10), sway (12, 0) at 0.4–0.9 Hz, alpha 0.6–1, Twinkle 0.5 at 1–2.5 Hz,
HaloScale 3, HaloAlpha 0.3.

### 2c. Ghosts (`ParticleShape.Ghost`, `ParticleStyle.Ghosts`)

A new `ParticleShape.Ghost` in `ParticleRenderer`:

- Two textures built by `SpriteGrid` from the two 11×13 ghost frames below. The dark detail color is (36, 34, 58).
- The frame alternates at 2.2 per second. Each particle's `SwayPhase` offsets its frame so the ghosts don't ripple in
  step.
- Drawn at scale 1, rotation 0, at a position rounded to whole pixels so the sprite stays crisp.
- The existing halo (soft dot behind the particle) is reused.

```
frame 0         frame 1
....XXX....     ....XXX....
..XXXXXXX..     ..XXXXXXX..
.XXXXXXXXX.     .XXXXXXXXX.
.XXoXXXoXX.     .XXoXXXoXX.
.XXoXXXoXX.     .XXoXXXoXX.
XXXXXXXXXXX     XXXXXXXXXXX
XXXXXoXXXXX     XXXXXoXXXXX
XXXXXXXXXXX     XXXXXXXXXXX
XXXXXXXXXXX     XXXXXXXXXXX
XXXXXXXXXXX     XXXXXXXXXXX
XXXXXXXXXXX     XXXXXXXXXXX
XX.XXX.XXX.     .XXX.XXX.XX
X...X...X..     ..X...X...X
```

`ParticleStyle.Ghosts`: Count 7, Ghost shape, not additive, one color (225, 232, 255), size 1 (unused by this shape),
velocity (-5, -14) to (5, -7), sway (14, 4) at 0.12–0.25 Hz, alpha 0.55–0.55, Twinkle 1 at 0.07–0.14 Hz with
TwinkleSharpness 2 (the ghost fades fully out, then back in), HaloScale 32 / HaloAlpha 0.18 on a size-1 particle
gives the 32 px glow from the preview. FadeSeconds 2.

The halo draws in the style's blend state, which is normal (not additive) here. The preview drew it additively. The
difference on screen is small; tune HaloAlpha in game if needed.

### 2d. Bats (new: `FlyByRenderer`, `FlyByStyle`, `FlyByFlock`)

Three pieces, split so the flight logic can be tested without a graphics device:

- **`FlyByStyle`** (record-like class with `init` properties, same pattern as `ParticleStyle`), plus the `Bats` preset:

  | Field | Bats value |
  |---|---|
  | Frames | the three 19-px bat frames below |
  | FrameCycle | 0, 1, 2, 1 |
  | Color / Alpha | (18, 10, 22) / 0.95 |
  | GapSeconds | 8–20 |
  | FirstGroupSeconds | 2–6 (after the effect switches on) |
  | GroupSize | 4–8 |
  | Speed | 95–135 px/s, each bat ×0.9–1.1 |
  | Spread | 80 px along the flight line, ±36 px across it |
  | Drift | vertical speed shared by the group, -14 to 14 px/s |
  | Wave | 4 px amplitude at 1.8 Hz, each bat ×0.7–1.3 amplitude and ×0.8–1.2 rate |
  | Dart | every 0.15–0.45 s, a new random push of up to ±40 px/s across and ±55 px/s up or down |
  | FlapRate | 4–5 cycles per second |
  | FadeSeconds | 1.5 |

- **`FlyByFlock`**: plain C#, no MonoGame graphics types except `Vector2`. Takes the style and a `Random`. Holds the list
  of bats and the gap timer. `Update(float dt, Vector2 viewportSize, Vector2 cameraShift, bool spawning)`:
  - Counts the timer down while `spawning` is true. At zero it spawns a group and rolls a new gap.
  - A group enters from the left or right edge (random), 30 px outside the viewport, at a height between 30 px and
    70% of the viewport height.
  - Each bat moves by its velocity, the group drift, its wave and its current dart push, plus `cameraShift`, so bats
    stay over the map when the player walks.
  - A bat is removed once it is 140 px past the far edge. A camera jump bigger than the viewport (teleport) is ignored,
    as in `ParticleRenderer`.
  - `Bats` exposes each bat's position and current frame index for drawing.
- **`FlyByRenderer : IAmbientOverlay`**: owns a `FlyByFlock`, fades the whole effect in and out like the other
  renderers, and draws each bat's frame texture (built by `SpriteGrid`) at a rounded position. When switched off, no
  new groups spawn; bats already in the air finish crossing while the effect fades. `SetActive(false, immediate: true)`
  clears the flock, so a map change never carries bats over. Blend state is `NonPremultiplied`.

```
frame 0 (wings up)       frame 1 (level)          frame 2 (wings down)
X.................X      ........X.X........      ........X.X........
XX...............XX      ........XXX........      ........XXX........
XXX.............XXX      XXX....XXXXX....XXX      .......XXXXX.......
.XXX....X.X....XXX.      XXXXXXXXXXXXXXXXXXX      ....XXXXXXXXXXX....
..XXXX..XXX..XXXX..      .XXXXXXXXXXXXXXXXX.      ..XXXXXX.XXX.XXXXX.
...XXXXXXXXXXXXX...      ..X..X..XXX..X..X..      .XXXX....XXX...XXXX
.......XXXXX.......      ........XXX........      XXX.......X......XX
........XXX........      .........X.........      XX................X
.........X.........
```

### 2e. Wiring (`AmbientEffects.cs`)

Add to the `Overlays` list:

- `(MapFlags.HarvestMoon, new MistRenderer(MistStyle.HarvestMoon))` with the tints, after `BloodMoon`.
- `(MapFlags.HarvestMoon, new ParticleRenderer(ParticleStyle.HarvestSparks))`, `(MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts))`
  and `(MapFlags.Bats, new FlyByRenderer(FlyByStyle.Bats))` at the end, so they draw over every haze. Bats go last.

The existing test `EveryEffectFlag_HasAnOverlay` then passes only if all three are wired.

The client picks up the new flags through its `Chaos-Server` submodule, so the client branch moves the submodule to the
server commit from part 1.

## Part 3: tests

Client (`Tests/Chaos.Client.Tests`):

- `SpriteGridTests`: `'X'`, `'o'` and `'.'` map to white, the dark color and clear; uneven rows throw; every bat and
  ghost frame in the presets has even rows.
- `FlyByFlockTests`, with a seeded `Random` and fixed time steps:
  - No bats before the first-group timer runs out; a group of 4–8 appears after it.
  - Every bat of a group leaves and is removed within a bounded time (viewport width plus 280 px, over the slowest
    speed, plus margin).
  - The next group waits at least the minimum gap.
  - With `spawning` false, no new group appears, and bats in the air still leave.
  - A camera shift moves every bat by the same amount; a shift bigger than the viewport is ignored.
- `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay` covers the wiring with no edit.

Server: the two test files named in part 1.

Manual check in game: stand on a map, `/mapFlag add Bats`, `Ghosts`, `HarvestMoon` one at a time and together, walk
around, then `/mapFlag remove` each and change maps. Try the theatre menu page 2.

## Part 4: docs

- `Unora/docs/ambient-map-effects.md`: add the three rows to the effects table and mention the new fly-by renderer
  in the "adding effects" part.
- `Chaos.Client/CLAUDE.md`: add Bats, Ghosts and HarvestMoon to the `AmbientEffects` line (bits 8–30) and name
  `FlyByRenderer`.

## Repos and branches

| Repo | Branch | Holds |
|---|---|---|
| `Chaos-Server` | `feat/halloween-effects` | flags, visibility, theatre, server tests |
| `Chaos.Client` | `feat/halloween-effects` | renderers, presets, wiring, client tests, CLAUDE.md, this spec, submodule bump |
| `Unora` | `feat/halloween-effects` | `docs/ambient-map-effects.md` |

Each branch lives in its own worktree under `C:\Users\Michael\Documents\GitHub\worktrees\`, because other sessions share
the main checkouts. Merge order: server, then client (after pointing its submodule at the merged server commit), then
Unora.

## Out of scope

- Putting the flags on any map.
- The Mileth festival list.
- Crows, and the rest of the earlier "part 2" list (sunbeams, aurora, shooting stars). `FlyByRenderer` is built so a
  crows preset can be added later.

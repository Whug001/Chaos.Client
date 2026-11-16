# Thanksgiving ambient effects: Turkey Feathers, Geese Shadows, Wheat Chaff

Date: 2026-11-16. Status: design approved in chat; spec awaiting review.

## Goal

Add three Thanksgiving map effects that work like the existing ones, and switch them on for the two Thanksgiving
event maps.

| Flag | Look (picked from browser previews) |
|---|---|
| `TurkeyFeathers` | Look 1A. About 10 barred turkey feathers (bronze, russet, grey-brown, cream tips) see-sawing down: they swing side to side, tilt with the swing, and drop fastest at the bottom of each swing. |
| `GeeseShadows` | Look 2B, shadows only, crisp. The geese fly above the camera and are never drawn. Their V of goose-shaped shadows, black at 18% opacity, slides across the ground every 15–35 s. |
| `WheatChaff` | Look 3B. Golden chaff flecks on a gusting breeze, pale wind lines that show only during strong gusts, the odd tumbling wheat head, and a faint gold haze. |

The preview pages are `.superpowers/brainstorm/thanksgiving/thanksgiving-looks.html` and `geese-shadows.html` in the
main Chaos.Client checkout. Their JavaScript holds the motion this spec copies.

## Decisions already made

- The effects are switched on per map by flags, like every other effect. There is no date-based switch.
- `harvest_field` (Gobbler King) and `ThanksgivingChallenge` get all three flags. No other map does.
- The Suomi theatre stage-effects menu lists all three on page 3.
- Mileth and the festival list do **not** get them.
- None of the three go in the F4 "Triggering or Unsettling Map Effects" section. Nothing flashes; the wind lines fade
  in and out over about a second.
- Hearth Glow (look 4) was dropped.

## Part 1: server (`Chaos.Client/Chaos-Server`)

1. `Chaos.DarkAges/Definitions/Enums.cs`, `MapFlags`: add after `RisingSteam`
   - `TurkeyFeathers = 1UL << 42`
   - `GeeseShadows = 1UL << 43`
   - `WheatChaff = 1UL << 44`
2. `Chaos/Utilities/MapFlagVisibility.cs`: add the three flags to the ambient set, after `RisingSteam`, so
   `/atmosphere` hides them.
3. `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs`: add page-3 entries after Rising Steam:
   `new("Turkey Feathers", MapFlags.TurkeyFeathers, 3)`, `new("Geese", MapFlags.GeeseShadows, 3)` and
   `new("Wheat Chaff", MapFlags.WheatChaff, 3)`. Page 3 then holds 13 of its 16 slots.
4. Tests: update `Tests/Chaos.Tests/MapFlagVisibilityTests.cs` and `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`
   where they list effects or count page 3. Add the three flags to the `AmbientFlags` list in
   `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`.

No packet change and no CLIENT_VERSION bump. An older client receives the new bits and draws nothing for them.

## Part 2: client (`Chaos.Client.Rendering`)

### 2a. Sprite grids (`SpriteGrid.cs`)

Add three grid sets next to `BatFrames` and `GhostFrames`. All rows within a sprite are the same width.

`FeatherBody` (14×5). `'X'` takes the particle's colour; `'o'` takes the dark detail colour (110, 100, 95), which the
tint multiplies into a darker shade of the same feather colour, giving the bars:

```
...XXooXXoo...
..XXXooXXooX..
..XXXooXXooX..
..XXXooXXooX..
...XXooXXoo...
```

`FeatherTip` (14×5), drawn over the body with the same transform in cream (232, 222, 198): the bare quill on the left,
the pale tip on the right.

```
..............
............X.
XX..........XX
............X.
..............
```

`GooseFrames`, three 17×11 frames, top-down, heading right. `'o'` is the tail, neck and head. Frame 0 is wings fully
spread; frames 1 and 2 are the downstroke seen from above (shorter span). For the shadow both `'X'` and `'o'` come out
black.

```
frame 0              frame 1              frame 2
....XX...........    .................    .................
.....XXXX........    .....XX..........    .................
.....XXXXX.......    .....XXXXX.......    .....XX..........
......XXXXX......    ......XXXXX......    ......XXXXX......
....XXXXXXX....o.    ....XXXXXXX....o.    ....XXXXXXX....o.
ooXXXXXXXXXXooooo    ooXXXXXXXXXXooooo    ooXXXXXXXXXXooooo
....XXXXXXX......    ....XXXXXXX......    ....XXXXXXX......
......XXXXX......    ......XXXXX......    ......XXXXX......
.....XXXXX.......    .....XXXXX.......    .....XX..........
.....XXXX........    .....XX..........    .................
....XX...........    .................    .................
```

`WheatHead` (14×3). `'X'` takes the particle's colour (gold); `'o'` is the stalk, dark colour (150, 120, 60).

```
.X.X.X.X......
XXXXXXXXoooooo
.X.X.X.X......
```

### 2b. Turkey Feathers (`ParticleShape.Feather`, `ParticleStyle.SeeSaw`, `ParticleStyle.TurkeyFeathers`)

New `ParticleShape.Feather`: two textures built by `SpriteGrid` from `FeatherBody` and `FeatherTip`. The body draws
in the particle colour and the tip in cream at the particle's alpha, both with the same position, rotation and a scale
of `Size / 14` on both axes. Positions are rounded to whole pixels.

New `ParticleStyle.SeeSaw` (float, radians, default 0). When it is above zero:

- Let `angle` be the particle's sway angle, as already computed in `CurrentVelocity`.
- Rotation is `SeeSaw * sin(angle)`, so the feather tilts the way it is swinging. `Spin` is ignored, and there is no
  tumble squash.
- The Y part of the base velocity is multiplied by `0.5 + 0.9 * |sin(angle)|`, so the feather drops fastest mid-swing
  (where the sideways speed peaks) and nearly hangs at each end.

Put the rotation and the drop factor in two small internal static functions, so tests can check them without a
graphics device.

`ParticleStyle.TurkeyFeathers`:

| Field | Value |
|---|---|
| Count | 10 |
| Shape | Feather, not additive |
| Colors | (128, 80, 42), (150, 72, 36), (112, 94, 74) |
| Size | 11–14 |
| Velocity | (2, 9) to (6, 15) |
| Sway | (40, 0) at 0.35–0.55 Hz |
| SeeSaw | 0.55 |
| Alpha | 0.95–0.95 |

### 2c. Geese Shadows (`FlyByStyle` additions, `FlyByStyle.GooseShadows`)

Two new `FlyByStyle` options:

- `Formation` (new enum `FlyByFormation { Scatter, Vee }`, default `Scatter`, which is today's behaviour).
  - With `Vee`, `FlyByFlock.SpawnGroup` places flyer 0 at the front of the group line. It picks a random split
    `lead` between 2 and `count - 3` (both ends included), then places flyers 1..lead on one arm and the rest on the
    other. The k-th bird on an arm sits `k * VeeAlong` px behind the leader and `k * VeeAcross` px to its side, plus
    `±VeeJitter` px of random offset on each axis.
  - `SpreadAlong` and `SpreadAcross` are not used for placement. `HasLeft` uses the group's actual depth (the largest
    along offset, stored on the flock at spawn) where it used `SpreadAlong`, so a long V isn't removed while its tail
    is still on screen.
  - New fields: `VeeAlong` (13), `VeeAcross` (8), `VeeJitter` (1.5).
- `FlipWithDirection` (bool, default false). When true, `FlyByRenderer` draws flyers with `Direction < 0` using
  `SpriteEffects.FlipHorizontally`.

`FlyByStyle.GooseShadows`:

| Field | Value |
|---|---|
| Frames / FrameCycle | `GooseFrames` / 0, 1, 2, 1 |
| Color / Alpha | black / 0.18 |
| Formation | Vee (13 along, 8 across, jitter 1.5) |
| FlipWithDirection | true |
| FirstGroupSeconds | 4–10 |
| GapSeconds | 15–35 |
| GroupSize | 7–11 |
| Speed | 34–44 px/s, SpeedJitter 0 (so the V holds its shape) |
| Drift | ±5 px/s |
| Wave | 1.2 px at 0.2 Hz |
| Dart | off (DartIntervalMax 0) |
| FlapRate | 2.2–2.8 cycles per second |
| BandTop / BandBottom | 30 px / 0.75 |
| FadeSeconds | 1.5 |

The renderer builds the frame textures with `SpriteGrid.Build(device, rows, Color.Black)` as it already does, so
`'o'` is black too and the whole shadow is one tone.

### 2d. Wheat Chaff (`ParticleStyle` gust fields, `ParticleShape.WheatHead`, three presets, `MistStyle.Chaff`)

New gust fields on `ParticleStyle`:

- `GustLow`, `GustHigh` (floats, both default 1). The X part of the base velocity is multiplied by
  `lerp(GustLow, GustHigh, g)`, where `g` is the shared gust curve below. At the defaults nothing changes.
- `GustGate` (float, default 0). When above zero, each particle's alpha is multiplied by
  `smoothstep(GustGate, GustGate + 0.15, g)`, so the particle shows only during strong gusts.

The gust curve is a pure static function, `ParticleRenderer.Gust(double seconds)`:
`clamp(0.5 + 0.3 * sin(0.83 t) + 0.2 * sin(1.9 t + 1.3), 0, 1)`. It is driven by total game time
(`gameTime.TotalGameTime`), not each renderer's own clock, so the three chaff layers gust together.

New `ParticleShape.WheatHead`: one `SpriteGrid` texture from `WheatHead`, drawn at scale 1, rotated by the particle's
rotation (it spins like a leaf but does not squash).

Presets, all for the `WheatChaff` flag:

| Field | `ChaffFlecks` | `ChaffWindLines` | `ChaffHeads` |
|---|---|---|---|
| Count | 70 | 6 | 2 |
| Shape | Streak | Streak | WheatHead |
| Colors | (232, 192, 96), (212, 162, 62), (246, 218, 140), (184, 132, 52) | (255, 244, 214) | (226, 184, 80) |
| Size | 2–3 | 18–40 | 14 (unused by this shape) |
| Velocity | (42, 1) to (78, 5) | (70, 0) to (120, 0) | (45, 0) to (70, 4) |
| Sway | (0, 8) at 0.25–0.5 Hz | none | (0, 10) at 0.5 Hz |
| Spin | 0 | 0 | 7 |
| Alpha | 0.55–0.9 | 0.16–0.16 | 1–1 |
| GustLow / GustHigh | 0.5 / 1.67 | 0.5 / 1.67 | 0.5 / 1.67 |
| GustGate | 0 | 0.62 | 0 |

`MistStyle.Chaff`: WashColor (200, 150, 60) at WashAlpha 0.07, LayerTint (230, 190, 110), one layer
`new MistLayer(3.5f, 0.10f, new Vector2(20f, 0f))`, Seed 5050, NoiseKnee 0.5.

### 2e. Wiring (`AmbientEffects.cs`)

Add to the `Overlays` list:

- `(MapFlags.GeeseShadows, new FlyByRenderer(FlyByStyle.GooseShadows))` right after `CloudShadows`, because the
  shadows lie on the ground under every haze.
- `(MapFlags.WheatChaff, new MistRenderer(MistStyle.Chaff))` after `Dust`.
- `(MapFlags.WheatChaff, new ParticleRenderer(ParticleStyle.ChaffFlecks))`,
  `(MapFlags.WheatChaff, new ParticleRenderer(ParticleStyle.ChaffWindLines))`,
  `(MapFlags.WheatChaff, new ParticleRenderer(ParticleStyle.ChaffHeads))` and
  `(MapFlags.TurkeyFeathers, new ParticleRenderer(ParticleStyle.TurkeyFeathers))` after `HarvestSparks`.

`EveryEffectFlag_HasAnOverlay` then passes only if all three flags are wired.

The client picks up the new flags through its `Chaos-Server` submodule, so the client branch moves the submodule to the
server commit from part 1.

## Part 3: data (`Unora`)

`Data/Configuration/MapInstances/Temuair/Events/Thanksgiving/harvest_field/instance.json` and
`Data/Configuration/MapInstances/Temuair/Events/ThanksgivingChallenge/Instance.json`: change `"flags": "None"` to
`"flags": "TurkeyFeathers, GeeseShadows, WheatChaff"`.

This needs the server from part 1 deployed first. An older server fails to parse the unknown flag names.

## Part 4: tests

Client (`Tests/Chaos.Client.Tests`):

- `SpriteGridTests`: every new grid has even rows.
- Feather see-saw: the rotation is 0 at the ends of the swing and ±SeeSaw mid-swing, with the sign following the
  sideways velocity; the drop factor is 0.5 at the ends and 1.4 mid-swing.
- `FlyByFlockTests`, with a seeded `Random`:
  - With `Vee`, a group of n has one leader and two arms whose offsets are k × (VeeAlong, VeeAcross) within the
    jitter, and every bird is behind the leader.
  - Every bird of a Vee group leaves and is removed within a bounded time (viewport width plus the group depth plus
    the margins, over the slowest speed, plus margin); none is removed while still on screen.
  - `Scatter` behaviour is unchanged (the existing bat tests still pass).
- Gust: `Gust(t)` stays in [0, 1] over a long sample, and reaches above 0.62 and below 0.4 within a minute. The
  gate factor is 0 below `GustGate` and 1 at or above `GustGate + 0.15`. With the default GustLow/High the X speed
  is unchanged.
- `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay` covers the wiring with no edit.

Server: the three test files named in part 1.

Manual check in game: on a field map, `/mapFlag add TurkeyFeathers`, `GeeseShadows`, `WheatChaff` one at a time and
together. Walk around and check that the shadows stay over the map and flip with direction. Then `/mapFlag remove`
each, change maps, and try the theatre menu page 3. After Part 3, enter Harvest Field and the Thanksgiving Challenge.

## Part 5: docs

- `Unora/docs/ambient-map-effects.md`: add the three rows to the effects table. Mention the `Vee` formation and the
  gust fields in the "adding effects" part.

## Repos and branches

| Repo | Branch | Holds |
|---|---|---|
| `Chaos-Server` | `feat/thanksgiving-effects` | flags, visibility, theatre, server tests |
| `Chaos.Client` | `feat/thanksgiving-effects` | sprites, renderer changes, presets, wiring, client tests, this spec, submodule bump |
| `Unora` | `feat/thanksgiving-effects` | the two instance.json files, `docs/ambient-map-effects.md` |

Each branch lives in its own worktree under `~/Documents/GitHub/worktrees/` (`thanksgiving-server`,
`thanksgiving-client`, `thanksgiving-unora`), because other sessions share the main checkouts. Merge order: server,
then client (after pointing its submodule at the merged server commit), then Unora.

## Out of scope

- Hearth Glow / woodsmoke.
- Drawing the geese themselves, and goose sounds.
- Golden feathers.
- Any map other than the two event maps; Mileth and the festival list.

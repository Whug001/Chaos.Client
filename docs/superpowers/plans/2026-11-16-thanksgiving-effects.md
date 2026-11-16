# Thanksgiving Ambient Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three new map effects (`TurkeyFeathers`, `GeeseShadows`, `WheatChaff`), listed in the theatre and admin-trinket menus and switched on for the two Thanksgiving event maps.

**Architecture:** The server adds three `MapFlags` bits and lists them in the shared stage-effects table. The client extends the existing `ParticleRenderer`, `FlyByRenderer`/`FlyByFlock` and `MistRenderer` with small options (see-saw, V formation, flip, gusts) plus presets, and wires them in `AmbientEffects`. Unora turns the flags on in two `instance.json` files.

**Tech Stack:** C# 14 / .NET 10, MonoGame, TUnit + FluentAssertions (client), the server's existing test project.

**Spec:** `docs/superpowers/specs/2026-11-16-thanksgiving-effects-design.md` (client worktree). Every number below comes from it; when this plan and the spec disagree, the spec wins.

## Global Constraints

- Flag bits: `TurkeyFeathers = 1UL << 42`, `GeeseShadows = 1UL << 43`, `WheatChaff = 1UL << 44`.
- Menu labels: "Turkey Feathers", "Geese", "Wheat Chaff", all on page 3.
- No packet change, no CLIENT_VERSION bump.
- Defaults of every new style field leave existing presets unchanged (`SeeSaw = 0`, `GustLow = GustHigh = 1`, `GustGate = 0`, `Formation = Scatter`, `FlipWithDirection = false`).
- Worktrees (all under `~/Documents/GitHub/worktrees/`, branch `feat/thanksgiving-effects` in each): `thanksgiving-server` (Chaos-Server), `thanksgiving-client` (Chaos.Client, already created), `thanksgiving-unora` (Unora). Never edit the main checkouts.
- Client tests: `dotnet build Chaos.Client.slnx` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Filter with `-- --treenode-filter "/*/*/<Class>/*"`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Merge order: server, then client (submodule moved to the merged server commit), then Unora. Push only when the user asks.

## Review Focus

1. **Map change mid-flight**: changing maps while a V of shadows is crossing must clear them (`SetActive(false, immediate: true)` → `Flock.Reset()`). Existing behaviour; Task 4 adds a Vee test that `Reset` empties the flock.
2. **Player walking with the geese**: a V pushed back past its entry edge must still be removed, using the V's depth, not `SpreadAlong` (0 for geese). Pinned in Task 4.
3. **Existing presets unchanged**: Leaves, Bats, Sand and the other presets must move exactly as before. Pinned by the default-value tests in Tasks 3–5 and the existing bat tests.
4. **Gust layers out of step**: the three chaff layers must read one gust value from total game time, not their own clocks, or the wind lines show during lulls. Pinned in Task 5 (`Gust` is a pure function of seconds; the renderer stores the level from `gameTime.TotalGameTime`).
5. **Dark maps**: `TheatreStageEffects.Available` hides weather on dark maps. The new effects are not in `WEATHER`, so they must still list on a dark map. Pinned in Task 1.

---

### Task 1: Server flags, visibility, theatre and trinket menus

**Files** (in `~/Documents/GitHub/worktrees/thanksgiving-server`):
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (`MapFlags`, after `RisingSteam`)
- Modify: `Chaos/Utilities/MapFlagVisibility.cs:51` (ambient set)
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs:69`
- Test: `Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`, `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`, `Tests/Chaos.Tests/Admin/AdminTrinketMapFlagsTests.cs`

**Interfaces:**
- Produces: `MapFlags.TurkeyFeathers`, `MapFlags.GeeseShadows`, `MapFlags.WheatChaff` (used by client Task 6 and Unora Task 7).

- [ ] **Step 1: Create the worktree**

```bash
cd ~/Documents/GitHub/Chaos.Client/Chaos-Server && git fetch -q origin
git worktree add ../../worktrees/thanksgiving-server -b feat/thanksgiving-effects origin/master
```

- [ ] **Step 2: Write the failing tests**
  - `MapFlagVisibilityTests`: add the three flags to the expected ambient set at line 46.
  - `TheatreStageEffectsTests`: add them to the expected page-3 set at line 47, and update any page-3 count from 10 to 13.
  - `MapEffectsPacketConverterTests`: add them to `AmbientFlags` after `MapFlags.RisingSteam`.
  - `TheatreStageEffectsTests`: new `Thanksgiving_effects_list_on_a_dark_map`. Asserts that `Available(MapFlags.Darkness, 3)` contains all three.
  - `AdminTrinketMapFlagsTests`: new `Page_three_lists_the_thanksgiving_effects`, built like `Page_three_lists_the_newest_effects_and_a_way_back`. Asserts that options "Start Turkey Feathers", "Start Geese" and "Start Wheat Chaff" exist. New `Choosing_start_turkey_feathers_turns_it_on`, built like `Choosing_start_aurora_on_page_three_turns_aurora_on`. Asserts `mapInstance.Flags.HasFlag(MapFlags.TurkeyFeathers)`.

- [ ] **Step 3: Run the server tests and confirm they fail.** They should fail to compile on the missing enum members.

- [ ] **Step 4: Implement.** Add the three enum members (bits in Global Constraints). Add them to the `MapFlagVisibility` ambient set after `RisingSteam`. Add `new("Turkey Feathers", MapFlags.TurkeyFeathers, 3)`, `new("Geese", MapFlags.GeeseShadows, 3)` and `new("Wheat Chaff", MapFlags.WheatChaff, 3)` after Rising Steam in `TheatreStageEffects`.

- [ ] **Step 5: Run the full server test project and confirm it passes.**

- [ ] **Step 6: Commit** — `feat: Thanksgiving map flags (turkey feathers, geese shadows, wheat chaff)`.

---

### Task 2: Sprite grids

**Files:**
- Modify: `Chaos.Client.Rendering/SpriteGrid.cs` (next to `BatFrames` / `GhostFrames`)
- Test: `Tests/Chaos.Client.Tests/SpriteGridTests.cs`

**Interfaces:**
- Produces: `SpriteGrid.FeatherBody` and `SpriteGrid.FeatherTip` (`string[]`, 14×5); `SpriteGrid.GooseFrames` (`IReadOnlyList<string[]>`, 3 frames of 17×11); `SpriteGrid.WheatHead` (`string[]`, 14×3). The exact rows are in spec §2a.

- [ ] **Step 1: Write the failing tests**
  - `ThanksgivingGrids_HaveEvenRows`: `ToPixels(grid, Color.Black)` does not throw for `FeatherBody`, `FeatherTip`, `WheatHead` and each `GooseFrames` entry.
  - `ThanksgivingGrids_HaveTheSpecSizes`: the sizes are 14×5, 14×5, 14×3 and three frames of 17×11.
  - `GooseFrames_SpanShrinksFrameToFrame`: the count of non-`'.'` cells is strictly decreasing across frames 0, 1, 2.
- [ ] **Step 2: Run them and confirm they fail** (missing members).
- [ ] **Step 3: Add the four grids**, copied verbatim from spec §2a.
- [ ] **Step 4: Run them and confirm they pass.**
- [ ] **Step 5: Commit** — `feat: sprite grids for feathers, geese and wheat heads`.

---

### Task 3: Turkey Feathers (Feather shape, see-saw)

**Files:**
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (enum `ParticleShape`, a `SeeSaw` property, the `TurkeyFeathers` preset)
- Modify: `Chaos.Client.Rendering/ParticleRenderer.cs` (`CurrentVelocity`, `ShapeTransform`, `GetShapeTexture`, `Draw`, `ReleaseTextures`/`Dispose`)
- Create: `Tests/Chaos.Client.Tests/ParticleSeeSawTests.cs`

**Interfaces:**
- Consumes: `SpriteGrid.FeatherBody`, `SpriteGrid.FeatherTip`.
- Produces:
  - `ParticleShape.Feather`;
  - `ParticleStyle.SeeSaw` (`float`, default 0);
  - `ParticleStyle.TurkeyFeathers`;
  - `internal static float ParticleRenderer.SeeSawTilt(float seeSaw, float swayAngle)`, which returns `seeSaw * MathF.Sin(swayAngle)`;
  - `internal static float ParticleRenderer.SeeSawDrop(float swayAngle)`, which returns `0.5f + 0.9f * MathF.Abs(MathF.Sin(swayAngle))`.

- [ ] **Step 1: Write the failing tests** in `ParticleSeeSawTests`:
  - `Tilt_IsZeroAtTheEndsOfTheSwing`: `SeeSawTilt(0.55f, 0f)` and `SeeSawTilt(0.55f, MathF.PI)` are each within 1e-5 of 0.
  - `Tilt_PeaksMidSwing_FollowingTheSidewaysVelocity`: `SeeSawTilt(0.55f, MathF.PI / 2)` is ≈ 0.55, and `SeeSawTilt(0.55f, 3 * MathF.PI / 2)` is ≈ -0.55.
  - `Drop_IsSlowAtTheEnds_FastMidSwing`: `SeeSawDrop(0f)` is ≈ 0.5, and `SeeSawDrop(MathF.PI / 2)` is ≈ 1.4.
  - `ExistingPresets_HaveNoSeeSaw`: `ParticleStyle.Leaves.SeeSaw` and `ParticleStyle.Petals.SeeSaw` are both 0.
  - `TurkeyFeathers_MatchesSpec`: Count 10, Shape Feather, SeeSaw 0.55, Size 11–14, Sway (40, 0).
- [ ] **Step 2: Run them and confirm they fail.**
- [ ] **Step 3: Implement.**
  - Add `Feather` to `ParticleShape`, with a doc comment.
  - Add `SeeSaw` with its summary from spec §2b.
  - Add the `TurkeyFeathers` preset with the values from the spec §2b table.
  - In `CurrentVelocity`, when `Style.SeeSaw > 0`, multiply the base Y velocity by `SeeSawDrop(angle)` before adding the sway.
  - In the `Update` loop, when `Style.SeeSaw > 0`, set `p.Rotation = SeeSawTilt(Style.SeeSaw, angle)` instead of adding the spin.
  - `ShapeTransform` returns `(p.Rotation, new Vector2(p.Size / 14f))` for `Feather`.
  - Build the textures with `SpriteGrid.Build(device, FeatherBody, new Color(110, 100, 95))` and `SpriteGrid.Build(device, FeatherTip, Color.White)`. Cache them in `FeatherTextures` (a `Texture2D[]?`).
  - In `Draw`, after the body, draw the tip texture with the same position (rounded to whole pixels), rotation, origin and scale, tinted `new Color(232, 222, 198)` at the particle's alpha. Mirror the balloon-detail block.
  - Dispose and release `FeatherTextures` wherever `GhostTextures` is disposed or released.
- [ ] **Step 4: Build and run the client tests.** Confirm the new tests pass and the existing suite still passes.
- [ ] **Step 5: Commit** — `feat: see-saw feather particles and the TurkeyFeathers preset`.

---

### Task 4: Geese Shadows (Vee formation, flip)

**Files:**
- Modify: `Chaos.Client.Rendering/FlyByStyle.cs` (new enum `FlyByFormation`, the Vee fields, `FlipWithDirection`, the `GooseShadows` preset)
- Modify: `Chaos.Client.Rendering/FlyByFlock.cs` (`SpawnGroup`, `HasLeft`)
- Modify: `Chaos.Client.Rendering/FlyByRenderer.cs` (`Draw` flips)
- Test: `Tests/Chaos.Client.Tests/FlyByFlockTests.cs`, `Tests/Chaos.Client.Tests/FlyByRendererTests.cs`

**Interfaces:**
- Consumes: `SpriteGrid.GooseFrames`.
- Produces:
  - `enum FlyByFormation { Scatter, Vee }`;
  - on `FlyByStyle`: `Formation`, `VeeAlong` (13), `VeeAcross` (8), `VeeJitter` (1.5), `FlipWithDirection`, and the `GooseShadows` preset;
  - `internal static SpriteEffects FlyByRenderer.EffectsFor(FlyByStyle style, int direction)`;
  - `float FlyByFlock.GroupDepth`, the largest along-offset of the latest group (0 for Scatter, where `SpreadAlong` keeps applying).

- [ ] **Step 1: Write the failing tests** in `FlyByFlockTests`, using a `NewGeese(seed)` helper over `FlyByStyle.GooseShadows`. Each runs over seeds 0–19:
  - `Vee_HasOneLeaderAndTwoArms`: after the first spawn, project each bird onto the flight line, relative to the frontmost bird. Every other bird is `k * 13 ± 1.5` behind it and `k * 8 ± 1.5` to one side, for some whole k ≥ 1. Each side has at least 1 bird. The count is 7–11.
  - `Vee_EveryBirdLeavesWithinBound`: after the spawn, stop spawning. Within `(640 + GroupDepth + EntryMargin + ExitMargin) / 34 + 2` seconds the flock is empty, and no bird whose x is inside [0, 640] was removed earlier.
  - `Vee_PushedBackByTheCamera_IsRemoved`: feed a camera shift of `-direction * 30` px each step, pushing the V back past its entry edge. The flock empties within 60 s.
  - `Vee_ResetClearsFlyers`: `Reset()` empties the flock mid-crossing.
  - In `FlyByRendererTests`: `EffectsFor_FlipsLeftFlyersOnlyWhenAsked`. With `GooseShadows`, direction -1 gives `FlipHorizontally` and +1 gives `None`. With `Bats`, both give `None`.
- [ ] **Step 2: Run them and confirm they fail.**
- [ ] **Step 3: Implement.**
  - Add the style fields and the `GooseShadows` preset from the spec §2c table. `SpreadAlong` and `SpreadAcross` stay 0, and `DartIntervalMax` is 0.
  - In `SpawnGroup`, when the formation is `Vee`, pick `lead = Rng.Next(2, count - 2)`. Flyer 0 sits at `(entryX, lineY)`. Flyer i ≤ lead is on arm -1 with k = i; the rest are on arm +1 with k = i - lead. Position each at `entryX - direction * (k * VeeAlong + jitter)`, `lineY + arm * (k * VeeAcross) + jitter`, where each jitter is `Roll(-VeeJitter, VeeJitter)`. All birds share one speed, since the jitter is 0 for this preset. Set `GroupDepth` to the largest `k * VeeAlong + VeeJitter`.
  - In `HasLeft`, when the formation is `Vee`, use `GroupDepth` where it uses `SpreadAlong`.
  - In `FlyByRenderer.Draw`, call the `spriteBatch.Draw` overload that takes `SpriteEffects`, passing `EffectsFor(Style, flyer.Direction)`.
- [ ] **Step 4: Build and run the client tests.** The new tests and all existing bat tests should pass.
- [ ] **Step 5: Commit** — `feat: V-formation fly-bys and the GooseShadows preset`.

---

### Task 5: Wheat Chaff (gusts, wheat heads, haze)

**Files:**
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (`GustLow`, `GustHigh`, `GustGate`, `ParticleShape.WheatHead`, presets `ChaffFlecks`, `ChaffWindLines`, `ChaffHeads`)
- Modify: `Chaos.Client.Rendering/ParticleRenderer.cs`
- Modify: `Chaos.Client.Rendering/MistStyle.cs` (`Chaff` preset)
- Create: `Tests/Chaos.Client.Tests/ParticleGustTests.cs`

**Interfaces:**
- Consumes: `SpriteGrid.WheatHead`.
- Produces:
  - `public static float ParticleRenderer.Gust(double seconds)`: `clamp(0.5 + 0.3 sin(0.83 t) + 0.2 sin(1.9 t + 1.3), 0, 1)`;
  - `internal static float ParticleRenderer.GustSpeedFactor(ParticleStyle style, float gust)`: `lerp(GustLow, GustHigh, gust)`;
  - `internal static float ParticleRenderer.GustAlphaFactor(ParticleStyle style, float gust)`: 1 when `GustGate <= 0`, otherwise `smoothstep(GustGate, GustGate + 0.15, gust)`;
  - the presets `ParticleStyle.ChaffFlecks`, `ChaffWindLines` and `ChaffHeads`, and `MistStyle.Chaff`.

- [ ] **Step 1: Write the failing tests** in `ParticleGustTests`:
  - `Gust_StaysInRange`: sample `t` from 0 to 600 s in 0.05 s steps; every value is within [0, 1].
  - `Gust_HasStrongGustsAndLulls`: within the first 60 s, some value is above 0.62 and some is below 0.4.
  - `Defaults_LeaveSpeedAndAlphaUnchanged`: for `ParticleStyle.Leaves` and g ∈ {0, 0.5, 1}, `GustSpeedFactor` and `GustAlphaFactor` are both 1.
  - `SpeedFactor_LerpsBetweenLowAndHigh`: for `ChaffFlecks`, the factor at g = 0 is 0.5 and at g = 1 is 1.67.
  - `AlphaGate_HidesBelowAndShowsAbove`: for `ChaffWindLines`, the factor at g = 0.6 is 0, at g = 0.77 is 1, and at g = 0.695 is strictly between 0 and 1.
  - `ChaffPresets_MatchSpec`: the counts are 70, 6 and 2; the shapes are Streak, Streak and WheatHead; `ChaffHeads.Spin` is 7.
- [ ] **Step 2: Run them and confirm they fail.**
- [ ] **Step 3: Implement.**
  - Add the fields and presets, with the values from the spec §2d table, plus `MistStyle.Chaff` from spec §2d.
  - In `ParticleRenderer.Update`, store `GustLevel = Gust(gameTime.TotalGameTime.TotalSeconds)` once per frame.
  - `CurrentVelocity` multiplies the base X velocity by `GustSpeedFactor(Style, GustLevel)`. The Streak angle already follows the velocity.
  - `Draw` multiplies the alpha by `GustAlphaFactor(Style, GustLevel)`.
  - `WheatHead` builds one texture with `SpriteGrid.Build(device, WheatHead, new Color(150, 120, 60))`, cached and disposed like `FeatherTextures`. `ShapeTransform` returns `(p.Rotation, Vector2.One)`.
- [ ] **Step 4: Build and run the client tests and confirm they pass.**
- [ ] **Step 5: Commit** — `feat: gusting wheat chaff particles, wheat heads and haze`.

---

### Task 6: Submodule bump and wiring

**Files:**
- Modify: `Chaos-Server` (submodule pointer → the Task 1 commit on `feat/thanksgiving-effects`)
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs` (`Overlays`)
- Test: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs` (no edit; `EveryEffectFlag_HasAnOverlay` covers it)

**Interfaces:**
- Consumes: the `MapFlags` from Task 1; the presets from Tasks 3–5.

- [ ] **Step 1: Move the submodule to the Task 1 commit:** `cd Chaos-Server && git fetch ../../thanksgiving-server feat/thanksgiving-effects && git checkout FETCH_HEAD`.
- [ ] **Step 2: Build and run the client tests.** Confirm `EveryEffectFlag_HasAnOverlay` fails for the three new flags.
- [ ] **Step 3: Wire the overlays** at the positions in spec §2e:
  - GooseShadows right after CloudShadows;
  - the Chaff mist after Dust;
  - ChaffFlecks, ChaffWindLines, ChaffHeads and TurkeyFeathers after HarvestSparks.

  Update the draw-order comment to mention the goose shadows lying on the ground.
- [ ] **Step 4: Build and run the client tests and confirm they all pass.**
- [ ] **Step 5: Manual check.** Run the local playtest server and client (see the user's local playtest setup memory), then on a field map:
  - `/mapFlag add` each flag, one at a time and then together;
  - walk around: the shadows stay over the map, the V flips when it flies left, and the feathers see-saw;
  - `/mapFlag remove` each flag;
  - change maps;
  - open page 3 of the theatre menu and of the admin trinket's Map Flags menu.

  Report anything that looks off rather than retuning numbers unasked.
- [ ] **Step 6: Commit** — `feat: wire the Thanksgiving map effects`, including the submodule pointer. Then run `graphify update .`.

---

### Task 7: Unora data and docs

**Files** (in `~/Documents/GitHub/worktrees/thanksgiving-unora`):
- Modify: `Data/Configuration/MapInstances/Temuair/Events/Thanksgiving/harvest_field/instance.json`
- Modify: `Data/Configuration/MapInstances/Temuair/Events/ThanksgivingChallenge/Instance.json`
- Modify: `docs/ambient-map-effects.md`

- [ ] **Step 1: Create the worktree:** `cd ~/Documents/GitHub/Unora && git fetch -q origin && git worktree add ../worktrees/thanksgiving-unora -b feat/thanksgiving-effects origin/main`.
- [ ] **Step 2: Set `"flags": "TurkeyFeathers, GeeseShadows, WheatChaff"`** in both instance files.
- [ ] **Step 3: Docs.** Add the three rows to the effects table in `docs/ambient-map-effects.md`, and a short note on `Formation = Vee`, `FlipWithDirection`, `SeeSaw` and the gust fields in its "adding effects" part.
- [ ] **Step 4: Verify.** Start the local playtest server on the Task 1 server branch with this data. It loads with no map-instance parse errors, and Harvest Field and the Thanksgiving Challenge show all three effects.
- [ ] **Step 5: Commit** — `feat: Thanksgiving effects on the event maps`.

---

### Task 8: Merge in order (only when the user says to)

- [ ] Merge the server branch to master, then point the client submodule at the merged server commit and commit `chore: bump Chaos-Server (Thanksgiving map flags)`.
- [ ] Merge the client branch, then the Unora branch. The server must be deployed before the Unora data, or the server fails to parse the new flag names.

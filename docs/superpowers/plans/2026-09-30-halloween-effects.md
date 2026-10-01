# Halloween Ambient Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three per-map ambient effects — Bats, Ghosts and Harvest Moon — as `MapFlags` bits 28–30, drawn by the client and offered on page 2 of the Suomi theatre menu.

**Architecture:** The server gains three flag bits and lists them wherever effects are listed (the /atmosphere hide set, the theatre menu, three tests). The client gains a small helper that turns text pixel grids into textures, two new presets for the existing mist and particle renderers (Harvest Moon), a new pixel-sprite particle shape (Ghosts), and a new renderer for groups of flyers crossing the screen (Bats). The bat flight logic lives in a plain class (`FlyByFlock`) so tests can run it without a graphics device.

**Tech Stack:** C# / .NET, MonoGame (client), TUnit + FluentAssertions (tests).

**Spec:** `docs/superpowers/specs/2026-09-30-halloween-effects-design.md` (in the client worktree).

## Global Constraints

- **Worktrees only.** Other Claude sessions share the main checkouts. All edits happen in:
  - server: `C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server` (branch `feat/halloween-effects`, created in Task 1)
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\halloween-effects-client` (branch `feat/halloween-effects`, already exists)
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\halloween-effects-unora` (branch `feat/halloween-effects`, created in Task 7)
  Never edit, stage, stash, reset or switch branches in `C:\Users\Michael\Documents\GitHub\Chaos.Client`, its `Chaos-Server` submodule, or `C:\Users\Michael\Documents\GitHub\Unora`.
- **Client builds point at the server worktree.** The client worktree's `Chaos-Server` submodule folder is empty. Every client build passes `-p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server`.
- **One build at a time.** Never build the server and client at the same moment. Run tasks one after another, not in parallel.
- **Tests run with `dotnet run`, not `dotnet test`.** Filter with `--treenode-filter`.
- **Do not commit** in Tasks 1–7. Leave changes in the working trees. Task 8 makes one commit per repo.
- **Exact looks.** Preset numbers and sprite grids are copied from the spec, which copied them from the browser previews the user picked (1B, 2A, 3A). Do not retune them.
- **Flag values:** `Bats = 1UL << 28`, `Ghosts = 1UL << 29`, `HarvestMoon = 1UL << 30`.
- **No CLIENT_VERSION bump** and no packet change.

**User decisions (already made):**
- Effects switch on per map by flag, like every other effect; no seasonal switch.
- Looks: Bats 1B (bigger, darting groups), Ghosts 2A (sheet ghosts), Harvest Moon 3A (warm).
- Bats fly by in groups with quiet gaps (not always on screen).
- Offered in the Suomi theatre menu (page 2) only. Not on any map yet. Not in the Mileth festival list.
- None of the three go in the F4 "Triggering or Unsettling Map Effects" section.

---

## File map

**Server worktree**
- Modify `Chaos.DarkAges/Definitions/Enums.cs` — three new `MapFlags` values.
- Modify `Chaos/Utilities/MapFlagVisibility.cs` — `ATMOSPHERE` hides them.
- Modify `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs` — page-2 entries.
- Modify tests: `Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`, `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`.

**Client worktree** (`Chaos.Client.Rendering/`)
- Create `SpriteGrid.cs` — text grid → pixels/texture; holds the bat and ghost frames.
- Modify `MistStyle.cs` — `HarvestMoon` preset.
- Modify `ParticleStyle.cs` — `Ghost` shape, `HarvestSparks` and `Ghosts` presets.
- Modify `ParticleRenderer.cs` — draws the `Ghost` shape.
- Create `FlyByStyle.cs` — tunables + `Bats` preset.
- Create `FlyByFlock.cs` — `Flyer` and the flight logic (no graphics).
- Create `FlyByRenderer.cs` — `IAmbientOverlay` that fades and draws a `FlyByFlock`.
- Modify `AmbientEffects.cs` — wire the three flags.
- Tests: create `Tests/Chaos.Client.Tests/SpriteGridTests.cs`, `HalloweenPresetsTests.cs`, `FlyByFlockTests.cs`.
- Modify `CLAUDE.md`.

**Unora worktree**
- Modify `docs/ambient-map-effects.md`.

---

### Task 1: Server flags, visibility and theatre menu

**Goal:** The server knows `Bats`, `Ghosts` and `HarvestMoon`, `/atmosphere` hides them, and the theatre offers them on page 2.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (the `MapFlags` enum, ends with `Drips = 1UL << 27`)
- Modify: `Chaos/Utilities/MapFlagVisibility.cs` (`ATMOSPHERE` constant)
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs` (`All` list)
- Test: `Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`, `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`

All paths are relative to the server worktree.

**Acceptance Criteria:**
- [ ] `MapFlags.Bats == 1UL << 28`, `MapFlags.Ghosts == 1UL << 29`, `MapFlags.HarvestMoon == 1UL << 30`.
- [ ] `MapFlagVisibility.ATMOSPHERE` contains all three.
- [ ] `TheatreStageEffects.All` ends with "Bats", "Ghosts", "Harvest Moon", all on page 2; page 2 has 14 effects.
- [ ] `MapFlagVisibilityTests`, `TheatreStageEffectsTests` and `MapEffectsPacketConverterTests` all pass.

**Verify:** from the server worktree, `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(MapFlagVisibilityTests)|(TheatreStageEffectsTests)|(MapEffectsPacketConverterTests)/*"` → all tests pass, 0 failed.

**Steps:**

- [ ] **Step 1: Create the server worktree**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server -c core.longpaths=true worktree add -b feat/halloween-effects C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server master
```

Expected: `Preparing worktree (new branch 'feat/halloween-effects')`. Run every later server command from `C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server`.

- [ ] **Step 2: Add the flags**

In `Chaos.DarkAges/Definitions/Enums.cs`, change the end of `MapFlags` from:

```csharp
    Wisps = 1UL << 26,
    Drips = 1UL << 27
}
```

to:

```csharp
    Wisps = 1UL << 26,
    Drips = 1UL << 27,
    Bats = 1UL << 28,
    Ghosts = 1UL << 29,
    HarvestMoon = 1UL << 30
}
```

- [ ] **Step 3: Update the tests to expect the new effects**

`Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, in `EVERY_EFFECT_BUT_SNOW`, change the last line `| MapFlags.Drips;` to:

```csharp
                                                   | MapFlags.Drips
                                                   | MapFlags.Bats
                                                   | MapFlags.Ghosts
                                                   | MapFlags.HarvestMoon;
```

`Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`, in `AmbientFlags`, change the last entry `MapFlags.Drips` to:

```csharp
        MapFlags.Drips,
        MapFlags.Bats,
        MapFlags.Ghosts,
        MapFlags.HarvestMoon
```

`Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`:

1. In `ALL_EFFECTS`, change the last line `| MapFlags.Drips;` to:

```csharp
                                         | MapFlags.Drips
                                         | MapFlags.Bats
                                         | MapFlags.Ghosts
                                         | MapFlags.HarvestMoon;
```

2. In `Available_PageTwo_IsTheNewEffectsInOrder`, change the last argument `MapFlags.Drips);` to:

```csharp
                                  MapFlags.Drips,
                                  MapFlags.Bats,
                                  MapFlags.Ghosts,
                                  MapFlags.HarvestMoon);
```

3. In `Available_WhileLightsOff_KeepsAllOfPageTwo`, change `.HaveCount(11);` to `.HaveCount(14);`.

- [ ] **Step 4: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(MapFlagVisibilityTests)|(TheatreStageEffectsTests)|(MapEffectsPacketConverterTests)/*"`

Expected: FAIL. `Atmosphere_CoversEveryEffectFlag` and `ForViewer_HideAtmosphere_RemovesEveryEffect` fail (the new flags are not in `ATMOSPHERE`). `All_OffersEveryAmbientEffect`, `Available_PageTwo_IsTheNewEffectsInOrder` and `Available_WhileLightsOff_KeepsAllOfPageTwo` fail (the theatre doesn't list them). The packet tests pass already.

- [ ] **Step 5: Add the flags to `ATMOSPHERE`**

In `Chaos/Utilities/MapFlagVisibility.cs`, change the last line of `ATMOSPHERE` from `| MapFlags.Drips;` to:

```csharp
                                       | MapFlags.Drips
                                       | MapFlags.Bats
                                       | MapFlags.Ghosts
                                       | MapFlags.HarvestMoon;
```

- [ ] **Step 6: Add the theatre entries**

In `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs`, change the end of `All` from:

```csharp
        new("Wisps", MapFlags.Wisps, 2),
        new("Drips", MapFlags.Drips, 2)
    ];
```

to:

```csharp
        new("Wisps", MapFlags.Wisps, 2),
        new("Drips", MapFlags.Drips, 2),
        new("Bats", MapFlags.Bats, 2),
        new("Ghosts", MapFlags.Ghosts, 2),
        new("Harvest Moon", MapFlags.HarvestMoon, 2)
    ];
```

- [ ] **Step 7: Run the tests to see them pass**

Run the command from Step 4. Expected: every test in the three classes passes, 0 failed.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos/Utilities/MapFlagVisibility.cs", "Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs", "Tests/Chaos.Tests/MapFlagVisibilityTests.cs", "Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs", "Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/(MapFlagVisibilityTests)|(TheatreStageEffectsTests)|(MapEffectsPacketConverterTests)/*\"", "acceptanceCriteria": ["MapFlags.Bats/Ghosts/HarvestMoon are bits 28/29/30", "ATMOSPHERE contains all three", "TheatreStageEffects.All ends with Bats, Ghosts, Harvest Moon on page 2; page 2 has 14", "MapFlagVisibilityTests, TheatreStageEffectsTests, MapEffectsPacketConverterTests pass"], "modelTier": "mechanical"}
```

---

### Task 2: Pixel sprite helper

**Goal:** A `SpriteGrid` helper turns text grids into pixel colors and textures, and holds the bat and ghost frames.

**Files:**
- Create: `Chaos.Client.Rendering/SpriteGrid.cs`
- Test: `Tests/Chaos.Client.Tests/SpriteGridTests.cs`

All paths are relative to the client worktree.

**Acceptance Criteria:**
- [ ] `SpriteGrid.ToPixels` maps `'X'` to `Color.White`, `'o'` to the dark color with alpha 255, `'.'` to `Color.Transparent`, row by row from the top.
- [ ] `ToPixels` throws `ArgumentException` for no rows, uneven rows, an empty row, or any other character.
- [ ] `SpriteGrid.BatFrames` holds three frames, every row 19 characters; `SpriteGrid.GhostFrames` holds two frames of 13 rows × 11 characters.
- [ ] `SpriteGridTests` passes.

**Verify:** from the client worktree, `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server` then `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpriteGridTests/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tests/Chaos.Client.Tests/SpriteGridTests.cs`:

```csharp
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class SpriteGridTests
{
    private static readonly Color Dark = new(36, 34, 58);

    [Test]
    public void ToPixels_MapsEachCharacter()
        => SpriteGrid.ToPixels(["Xo."], Dark)
                      .Should()
                      .Equal(Color.White, new Color(36, 34, 58, 255), Color.Transparent);

    [Test]
    public void ToPixels_LaysRowsOutTopToBottom()
        => SpriteGrid.ToPixels(["X.", ".X"], Dark)
                      .Should()
                      .Equal(Color.White, Color.Transparent, Color.Transparent, Color.White);

    [Test]
    public void ToPixels_NoRows_Throws()
    {
        var act = () => SpriteGrid.ToPixels([], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_UnevenRows_Throws()
    {
        var act = () => SpriteGrid.ToPixels(["XX", "X"], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_EmptyRow_Throws()
    {
        var act = () => SpriteGrid.ToPixels([""], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_UnknownCharacter_Throws()
    {
        var act = () => SpriteGrid.ToPixels(["X#"], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void BatFrames_AreThreeValidFrames19PixelsWide()
    {
        SpriteGrid.BatFrames
                   .Should()
                   .HaveCount(3);

        foreach (var frame in SpriteGrid.BatFrames)
        {
            frame.Should()
                 .OnlyContain(row => row.Length == 19);

            SpriteGrid.ToPixels(frame, Dark)
                       .Should()
                       .NotBeEmpty();
        }
    }

    [Test]
    public void GhostFrames_AreTwoValidFrames11By13()
    {
        SpriteGrid.GhostFrames
                   .Should()
                   .HaveCount(2);

        foreach (var frame in SpriteGrid.GhostFrames)
        {
            frame.Should()
                 .HaveCount(13)
                 .And
                 .OnlyContain(row => row.Length == 11);

            SpriteGrid.ToPixels(frame, Dark)
                       .Should()
                       .NotBeEmpty();
        }
    }
}
```

- [ ] **Step 2: Build to see it fail**

Run: `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server`

Expected: FAIL with `CS0103: The name 'SpriteGrid' does not exist in the current context`.

- [ ] **Step 3: Write the helper**

Create `Chaos.Client.Rendering/SpriteGrid.cs`:

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Builds small pixel-art textures from text grids, one string per row, top to bottom. <c>'X'</c> is opaque white,
///     tinted at draw time. <c>'o'</c> is an opaque dark detail color chosen by the caller (the draw tint multiplies it
///     too). <c>'.'</c> is clear. Also holds the bat and ghost frames the Halloween effects draw.
/// </summary>
public static class SpriteGrid
{
    /// <summary>Bat frames, 19 px wide: wings up, level, down. Played 0, 1, 2, 1 per flap.</summary>
    public static IReadOnlyList<string[]> BatFrames { get; } =
    [
        [
            "X.................X",
            "XX...............XX",
            "XXX.............XXX",
            ".XXX....X.X....XXX.",
            "..XXXX..XXX..XXXX..",
            "...XXXXXXXXXXXXX...",
            ".......XXXXX.......",
            "........XXX........",
            ".........X........."
        ],
        [
            "........X.X........",
            "........XXX........",
            "XXX....XXXXX....XXX",
            "XXXXXXXXXXXXXXXXXXX",
            ".XXXXXXXXXXXXXXXXX.",
            "..X..X..XXX..X..X..",
            "........XXX........",
            ".........X........."
        ],
        [
            "........X.X........",
            "........XXX........",
            ".......XXXXX.......",
            "....XXXXXXXXXXX....",
            "..XXXXXX.XXX.XXXXX.",
            ".XXXX....XXX...XXXX",
            "XXX.......X......XX",
            "XX................X"
        ]
    ];

    /// <summary>Sheet-ghost frames, 11 x 13, differing only in the hem. <c>'o'</c> marks the eyes and mouth.</summary>
    public static IReadOnlyList<string[]> GhostFrames { get; } =
    [
        [
            "....XXX....",
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            ".XXoXXXoXX.",
            ".XXoXXXoXX.",
            "XXXXXXXXXXX",
            "XXXXXoXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XX.XXX.XXX.",
            "X...X...X.."
        ],
        [
            "....XXX....",
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            ".XXoXXXoXX.",
            ".XXoXXXoXX.",
            "XXXXXXXXXXX",
            "XXXXXoXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXX.XXX.XX",
            "..X...X...X"
        ]
    ];

    /// <summary>
    ///     The grid's pixels, row by row from the top. Throws <see cref="ArgumentException" /> for no rows, rows of
    ///     different or zero width, or a character other than X, o and dot.
    /// </summary>
    public static Color[] ToPixels(IReadOnlyList<string> rows, Color dark)
    {
        if (rows.Count == 0)
            throw new ArgumentException("A sprite needs at least one row.", nameof(rows));

        var width = rows[0].Length;

        if ((width == 0) || rows.Any(row => row.Length != width))
            throw new ArgumentException("Every sprite row must have the same, non-zero width.", nameof(rows));

        var detail = new Color(dark.R, dark.G, dark.B, (byte)255);
        var pixels = new Color[width * rows.Count];

        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < width; x++)
                pixels[(y * width) + x] = rows[y][x] switch
                {
                    'X'       => Color.White,
                    'o'       => detail,
                    '.'       => Color.Transparent,
                    var other => throw new ArgumentException($"Unknown sprite character '{other}'.", nameof(rows))
                };

        return pixels;
    }

    /// <summary>Builds the grid as a texture. The caller owns and disposes it.</summary>
    public static Texture2D Build(GraphicsDevice device, IReadOnlyList<string> rows, Color dark)
    {
        var pixels = ToPixels(rows, dark);
        var texture = new Texture2D(device, rows[0].Length, rows.Count);
        texture.SetData(pixels);

        return texture;
    }
}
```

- [ ] **Step 4: Build and run the test to see it pass**

Run: `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server`
then: `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpriteGridTests/*"`

Expected: 8 tests pass, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/SpriteGrid.cs", "Tests/Chaos.Client.Tests/SpriteGridTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server && dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/SpriteGridTests/*\"", "acceptanceCriteria": ["ToPixels maps X to White, o to dark with alpha 255, . to Transparent, rows top to bottom", "ToPixels throws ArgumentException for no rows, uneven rows, empty row, unknown character", "BatFrames: 3 frames, rows 19 wide; GhostFrames: 2 frames, 13 rows x 11", "SpriteGridTests passes"], "modelTier": "mechanical"}
```

---

### Task 3: Harvest Moon presets and wiring

**Goal:** The `HarvestMoon` flag draws an orange mist and orange sparks, using the existing renderers.

**Files:**
- Modify: `Chaos.Client.Rendering/MistStyle.cs` (add a preset at the end of the class)
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (add a preset at the end of the class)
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs` (`Overlays` list)
- Test: `Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs` (create)

**Acceptance Criteria:**
- [ ] `MistStyle.HarvestMoon` and `ParticleStyle.HarvestSparks` exist with the spec's numbers.
- [ ] `AmbientEffects.CoveredFlags` contains `MapFlags.HarvestMoon`: a mist entry right after `BloodMoon`'s mist, and a sparks entry after `Drips`.
- [ ] `HarvestSparks.TwinkleFreqMax` (2.5) is below `Embers.TwinkleFreqMin` (3), checked by a test.
- [ ] `HalloweenPresetsTests` passes. `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay` still fails, naming only `Ghosts` and `Bats`.

**Verify:** build as in Task 2, then `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/HalloweenPresetsTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs`:

```csharp
using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class HalloweenPresetsTests
{
    //the F4 flicker section leaves Harvest Moon out because its sparks flicker slower than Embers, which that section
    //also leaves out. keep it that way
    [Test]
    public void HarvestSparks_FlickerSlowerThanEmbers()
        => ParticleStyle.HarvestSparks
                        .TwinkleFreqMax
                        .Should()
                        .BeLessThan(ParticleStyle.Embers.TwinkleFreqMin);
}
```

- [ ] **Step 2: Build to see it fail**

Run the Task 2 build command. Expected: FAIL with `CS0117: 'ParticleStyle' does not contain a definition for 'HarvestSparks'`.

- [ ] **Step 3: Add the mist preset**

In `Chaos.Client.Rendering/MistStyle.cs`, add after the `Heat` preset, before the class's closing `}`:

```csharp

    /// <summary>Orange evening wash breathing slowly, faint amber haze, dark edges. Paired with rising orange sparks.</summary>
    public static MistStyle HarvestMoon { get; } = new()
    {
        WashColor = new Color(120, 55, 10),
        WashAlpha = 0.22f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.12f,
        PulsePeriod = 6f,
        LayerTint = new Color(230, 120, 40),
        Layers =
        [
            new MistLayer(3.5f, 0.14f, new Vector2(4f, 0f)),
            new MistLayer(2.5f, 0.18f, new Vector2(7f, -1f))
        ],
        Seed = 1031,
        NoiseKnee = 0.45f,
        NoiseRange = 0.45f,
        VignetteColor = new Color(25, 8, 0),
        VignetteAlpha = 0.45f
    };
```

- [ ] **Step 4: Add the sparks preset**

In `Chaos.Client.Rendering/ParticleStyle.cs`, add after the `Drips` preset, before the class's closing `}`:

```csharp

    /// <summary>
    ///     Pumpkin-orange sparks rising and flickering gently. Paired with the HarvestMoon mist. The flicker stays slower
    ///     than <see cref="Embers" /> so the effect can stay out of the F4 flicker section.
    /// </summary>
    public static ParticleStyle HarvestSparks { get; } = new()
    {
        Count = 22,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 140, 30), new Color(255, 170, 50), new Color(255, 110, 20)],
        SizeMin = 2f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-6f, -22f),
        VelocityMax = new Vector2(6f, -10f),
        Sway = new Vector2(12f, 0f),
        SwayFreqMin = 0.4f,
        SwayFreqMax = 0.9f,
        AlphaMin = 0.6f,
        AlphaMax = 1f,
        Twinkle = 0.5f,
        TwinkleFreqMin = 1f,
        TwinkleFreqMax = 2.5f,
        HaloScale = 3f,
        HaloAlpha = 0.3f
    };
```

- [ ] **Step 5: Wire the flag**

In `Chaos.Client.Rendering/AmbientEffects.cs`, in `Overlays`:

1. After `(MapFlags.BloodMoon, new MistRenderer(MistStyle.BloodMoon)),` add:

```csharp
        (MapFlags.HarvestMoon, new MistRenderer(MistStyle.HarvestMoon)),
```

2. Change the last entry `(MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips))` to:

```csharp
        (MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips)),
        (MapFlags.HarvestMoon, new ParticleRenderer(ParticleStyle.HarvestSparks))
```

- [ ] **Step 6: Build and run the tests**

Run the Task 2 build command, then:
`dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(HalloweenPresetsTests)|(AmbientEffectsTests)/*"`

Expected: `HarvestSparks_FlickerSlowerThanEmbers` passes. `EveryEffectFlag_HasAnOverlay` fails, and its message lists only `Bats` and `Ghosts` (Tasks 4 and 6 fix it). `ExtendedFlags_CoverEveryBitAboveTheLowByte` passes.

```json:metadata
{"files": ["Chaos.Client.Rendering/MistStyle.cs", "Chaos.Client.Rendering/ParticleStyle.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server && dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/HalloweenPresetsTests/*\"", "acceptanceCriteria": ["MistStyle.HarvestMoon and ParticleStyle.HarvestSparks exist with the spec's numbers", "HarvestMoon mist after BloodMoon mist; HarvestSparks after Drips in Overlays", "test: HarvestSparks.TwinkleFreqMax < Embers.TwinkleFreqMin", "HalloweenPresetsTests passes; EveryEffectFlag_HasAnOverlay names only Ghosts and Bats"], "modelTier": "mechanical"}
```

---

### Task 4: Ghost particle shape and preset

**Goal:** `ParticleRenderer` can draw the pixel sheet ghost, and the `Ghosts` flag shows a few ghosts floating up and fading.

**Files:**
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (`ParticleShape` enum; new `Ghosts` preset)
- Modify: `Chaos.Client.Rendering/ParticleRenderer.cs` (constants, a texture field, `Draw`, `GetShapeTexture`, `Dispose`, new methods)
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs` (`Overlays` list)
- Test: `Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs` (add tests)

**Acceptance Criteria:**
- [ ] `ParticleShape.Ghost` exists; `ParticleRenderer` draws it from `SpriteGrid.GhostFrames` at scale 1, at whole-pixel positions, with eye color (36, 34, 58).
- [ ] `ParticleRenderer.GhostFrameAt(clock, phase)` returns `(int)(clock * 2.2 + phase) % 2`.
- [ ] `ParticleStyle.Ghosts` has the spec's numbers (Twinkle 1, TwinkleSharpness 2, HaloScale 32, HaloAlpha 0.18, FadeSeconds 2).
- [ ] Ghost textures are disposed in `Dispose`.
- [ ] `Overlays` has `(MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts))` after the HarvestSparks entry.
- [ ] `HalloweenPresetsTests` passes; `EveryEffectFlag_HasAnOverlay` now names only `Bats`.

**Verify:** build as in Task 2, then `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/HalloweenPresetsTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

In `Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs`, add inside the class, after `HarvestSparks_FlickerSlowerThanEmbers`:

```csharp

    [Test]
    public void GhostFrameAt_AlternatesAtTheHemRate()
    {
        ParticleRenderer.GhostFrameAt(0f, 0f)
                        .Should()
                        .Be(0);

        //0.5 s x 2.2 frames/s = 1.1 -> frame 1
        ParticleRenderer.GhostFrameAt(0.5f, 0f)
                        .Should()
                        .Be(1);

        //1 s x 2.2 = 2.2 -> frame 2, which wraps to 0
        ParticleRenderer.GhostFrameAt(1f, 0f)
                        .Should()
                        .Be(0);
    }

    [Test]
    public void GhostFrameAt_PhaseOffsetsTheRipple()
        => ParticleRenderer.GhostFrameAt(0f, 1.2f)
                           .Should()
                           .Be(1, "each ghost's phase shifts its hem so they don't ripple in step");

    [Test]
    public void Ghosts_FadeFullyOutBetweenAppearances()
        => ParticleStyle.Ghosts
                        .Twinkle
                        .Should()
                        .Be(1f, "a twinkle of 1 takes each ghost to zero opacity at the bottom of its fade");
```

- [ ] **Step 2: Build to see it fail**

Run the Task 2 build command. Expected: FAIL with `CS0117: 'ParticleRenderer' does not contain a definition for 'GhostFrameAt'` and `'ParticleStyle' does not contain a definition for 'Ghosts'`.

- [ ] **Step 3: Add the shape**

In `Chaos.Client.Rendering/ParticleStyle.cs`, change the end of the `ParticleShape` enum from:

```csharp
    /// <summary>Thin line aligned to its motion — blowing sand.</summary>
    Streak
}
```

to:

```csharp
    /// <summary>Thin line aligned to its motion — blowing sand.</summary>
    Streak,

    /// <summary>
    ///     Pixel-art sheet ghost with a rippling hem, drawn 1:1 from <see cref="SpriteGrid.GhostFrames" />. Size and
    ///     spin are ignored.
    /// </summary>
    Ghost
}
```

- [ ] **Step 4: Add the preset**

In `Chaos.Client.Rendering/ParticleStyle.cs`, add after the `HarvestSparks` preset (added in Task 3), before the class's closing `}`:

```csharp

    /// <summary>
    ///     A few pale sheet ghosts floating slowly upward, swaying, and fading fully out and back in. The halo on a
    ///     size-1 particle is a 32 px glow.
    /// </summary>
    public static ParticleStyle Ghosts { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 7,
        Shape = ParticleShape.Ghost,
        Colors = [new Color(225, 232, 255)],
        SizeMin = 1f,
        SizeMax = 1f,
        VelocityMin = new Vector2(-5f, -14f),
        VelocityMax = new Vector2(5f, -7f),
        Sway = new Vector2(14f, 4f),
        SwayFreqMin = 0.12f,
        SwayFreqMax = 0.25f,
        AlphaMin = 0.55f,
        AlphaMax = 0.55f,
        Twinkle = 1f,
        TwinkleFreqMin = 0.07f,
        TwinkleFreqMax = 0.14f,
        TwinkleSharpness = 2f,
        HaloScale = 32f,
        HaloAlpha = 0.18f
    };
```

- [ ] **Step 5: Teach `ParticleRenderer` the ghost shape**

In `Chaos.Client.Rendering/ParticleRenderer.cs`:

1. After the line `private const float WING_PALE = 0.55f;         // how far the wing color moves from the particle color to white` add:

```csharp
    private const float GHOST_FRAME_RATE = 2.2f;   // ghost hem frames per second

    private static readonly Color GhostDetail = new(36, 34, 58); // ghost eyes and mouth
```

2. After the field `private Texture2D? WingTexture;` add:

```csharp
    private Texture2D[]? GhostTextures;
```

3. In `Draw`, inside the `foreach (var p in Particles)` loop, find:

```csharp
            if (Style.WingScale > 0f)
            {
                WingTexture ??= BuildWingTexture(device);
                DrawWings(spriteBatch, in p, position, alpha);
            }

            var (rotation, scale) = ShapeTransform(in p, texture);
```

and change it to:

```csharp
            if (Style.WingScale > 0f)
            {
                WingTexture ??= BuildWingTexture(device);
                DrawWings(spriteBatch, in p, position, alpha);
            }

            if (Style.Shape == ParticleShape.Ghost)
            {
                DrawGhost(spriteBatch, in p, position, alpha);

                continue;
            }

            var (rotation, scale) = ShapeTransform(in p, texture);
```

4. In `Dispose`, after the lines `WingTexture?.Dispose();` and `WingTexture = null;` add:

```csharp

        if (GhostTextures is not null)
            foreach (var ghostTexture in GhostTextures)
                ghostTexture.Dispose();

        GhostTextures = null;
```

5. Replace `GetShapeTexture` with this version. The only change is the `Ghost` line, which builds the ghost frames before the draw loop needs them:

```csharp
    private Texture2D GetShapeTexture(GraphicsDevice device)
        => Style.Shape switch
        {
            ParticleShape.Square or ParticleShape.Streak => PixelTexture ??= BuildPixelTexture(device),
            ParticleShape.Leaf                           => LeafTexture ??= BuildLeafTexture(device),
            ParticleShape.Bubble                         => BubbleTexture ??= BuildBubbleTexture(device),
            ParticleShape.Ghost                          => (GhostTextures ??= BuildGhostTextures(device))[0],
            _                                            => SoftDotTexture ??= BuildSoftDotTexture(device)
        };
```

6. After the `DrawWingPair` method, add:

```csharp

    /// <summary>
    ///     The ghost frame shown at <paramref name="clock" /> seconds for a particle with sway phase
    ///     <paramref name="phase" />. The phase shifts each ghost's hem so they don't ripple in step.
    /// </summary>
    public static int GhostFrameAt(float clock, float phase)
        => (int)((clock * GHOST_FRAME_RATE) + phase) % SpriteGrid.GhostFrames.Count;

    //1:1 pixel sprite at a whole-pixel position, so it stays crisp
    private void DrawGhost(SpriteBatch spriteBatch, in Particle p, Vector2 position, float alpha)
    {
        var frame = GhostTextures![GhostFrameAt(Clock, p.SwayPhase)];
        var topLeft = new Vector2(MathF.Round(position.X - (frame.Width / 2f)), MathF.Round(position.Y - (frame.Height / 2f)));

        spriteBatch.Draw(frame, topLeft, WithAlpha(p.Color, alpha));
    }

    private static Texture2D[] BuildGhostTextures(GraphicsDevice device)
        => SpriteGrid.GhostFrames
                      .Select(rows => SpriteGrid.Build(device, rows, GhostDetail))
                      .ToArray();
```

- [ ] **Step 6: Wire the flag**

In `Chaos.Client.Rendering/AmbientEffects.cs`, change the last entry of `Overlays` from `(MapFlags.HarvestMoon, new ParticleRenderer(ParticleStyle.HarvestSparks))` to:

```csharp
        (MapFlags.HarvestMoon, new ParticleRenderer(ParticleStyle.HarvestSparks)),
        (MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts))
```

- [ ] **Step 7: Build and run the tests**

Run the Task 2 build command, then:
`dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(HalloweenPresetsTests)|(AmbientEffectsTests)/*"`

Expected: all four `HalloweenPresetsTests` pass. `EveryEffectFlag_HasAnOverlay` still fails, and its message lists only `Bats` (Task 6 fixes it).

```json:metadata
{"files": ["Chaos.Client.Rendering/ParticleStyle.cs", "Chaos.Client.Rendering/ParticleRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server && dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/HalloweenPresetsTests/*\"", "acceptanceCriteria": ["ParticleShape.Ghost drawn from SpriteGrid.GhostFrames at scale 1, whole-pixel positions, eye color (36,34,58)", "GhostFrameAt(clock, phase) == (int)(clock*2.2 + phase) % 2", "ParticleStyle.Ghosts has the spec's numbers", "ghost textures disposed in Dispose", "Ghosts overlay wired after HarvestSparks", "HalloweenPresetsTests passes; EveryEffectFlag_HasAnOverlay names only Bats"], "modelTier": "standard"}
```

---

### Task 5: Fly-by style and flock logic

**Goal:** `FlyByStyle` (with the `Bats` preset) and `FlyByFlock` spawn groups of flyers, move them across the viewport, and remove them after they leave, all testable without graphics.

**Files:**
- Create: `Chaos.Client.Rendering/FlyByStyle.cs`
- Create: `Chaos.Client.Rendering/FlyByFlock.cs`
- Test: `Tests/Chaos.Client.Tests/FlyByFlockTests.cs`

**Acceptance Criteria:**
- [ ] No flyers exist before the first-group delay (2–6 s after spawning starts); then one group of 4–8 appears, entering from outside the left or right edge.
- [ ] Each later group waits 8–20 s after the previous one.
- [ ] With spawning off, no new group appears and every flyer is removed within 30 s.
- [ ] A camera shift moves every flyer by exactly that shift; a shift larger than the viewport is ignored.
- [ ] `Reset` removes every flyer.
- [ ] `FrameOf` always returns an index into `Style.Frames`.
- [ ] `FlyByFlockTests` passes.

**Verify:** build as in Task 2, then `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/FlyByFlockTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Client.Tests/FlyByFlockTests.cs`:

```csharp
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class FlyByFlockTests
{
    private const float STEP = 0.05f;
    private static readonly Vector2 Viewport = new(640f, 480f);

    private static FlyByFlock NewFlock(int seed) => new(FlyByStyle.Bats, new Random(seed));

    //advances the flock in STEP-sized frames and returns the times (s) at which a new group spawned
    private static List<float> Run(FlyByFlock flock, float seconds, bool spawning = true)
    {
        var spawnTimes = new List<float>();
        var groups = flock.GroupsSpawned;

        for (var t = STEP; t <= seconds + 0.0001f; t += STEP)
        {
            flock.Update(STEP, Viewport, Vector2.Zero, spawning);

            if (flock.GroupsSpawned != groups)
            {
                spawnTimes.Add(t);
                groups = flock.GroupsSpawned;
            }
        }

        return spawnTimes;
    }

    [Test]
    public void NoFlyers_BeforeTheFirstGroupDelay()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var flock = NewFlock(seed);

            Run(flock, FlyByStyle.Bats.FirstGroupMin - 0.1f);

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: the first group comes {FlyByStyle.Bats.FirstGroupMin}+ s after switching on");
        }
    }

    [Test]
    public void FirstGroup_IsFourToEightFlyersEnteringFromOffScreen()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var flock = NewFlock(seed);

            while (flock.GroupsSpawned == 0)
                Run(flock, STEP);

            flock.Flyers
                 .Should()
                 .HaveCountGreaterThanOrEqualTo(4)
                 .And
                 .HaveCountLessThanOrEqualTo(8);

            flock.Flyers
                 .Should()
                 .OnlyContain(f => (f.Position.X < 0f) || (f.Position.X > Viewport.X), $"seed {seed}: groups enter from off screen");
        }
    }

    [Test]
    public void Groups_ArriveWithinTheFirstDelayThenTheGap()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var spawnTimes = Run(NewFlock(seed), 90f);

            spawnTimes.Should()
                      .HaveCountGreaterThanOrEqualTo(4, $"seed {seed}: 90 s holds at least four groups");

            spawnTimes[0]
                .Should()
                .BeInRange(FlyByStyle.Bats.FirstGroupMin - STEP, FlyByStyle.Bats.FirstGroupMax + STEP);

            for (var i = 1; i < spawnTimes.Count; i++)
                (spawnTimes[i] - spawnTimes[i - 1]).Should()
                                                   .BeInRange(
                                                       FlyByStyle.Bats.GapMin - STEP,
                                                       FlyByStyle.Bats.GapMax + STEP,
                                                       $"seed {seed}: gap before group {i}");
        }
    }

    [Test]
    public void SpawningOff_NoNewGroup_AndFlyersLeave()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var flock = NewFlock(seed);

            while (flock.GroupsSpawned == 0)
                Run(flock, STEP);

            var groups = flock.GroupsSpawned;

            Run(flock, 30f, spawning: false);

            flock.GroupsSpawned
                 .Should()
                 .Be(groups, $"seed {seed}: no group spawns while spawning is off");

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: every flyer crosses and is removed within 30 s");
        }
    }

    [Test]
    public void CameraShift_MovesEveryFlyerByTheShift()
    {
        var still = NewFlock(7);
        var panned = NewFlock(7);

        while (still.GroupsSpawned == 0)
        {
            still.Update(STEP, Viewport, Vector2.Zero, true);
            panned.Update(STEP, Viewport, Vector2.Zero, true);
        }

        var shift = new Vector2(10f, -6f);
        still.Update(STEP, Viewport, Vector2.Zero, true);
        panned.Update(STEP, Viewport, shift, true);

        panned.Flyers
              .Should()
              .HaveCount(still.Flyers.Count);

        for (var i = 0; i < still.Flyers.Count; i++)
        {
            (panned.Flyers[i].Position.X - still.Flyers[i].Position.X).Should()
                                                                       .BeApproximately(shift.X, 0.001f);

            (panned.Flyers[i].Position.Y - still.Flyers[i].Position.Y).Should()
                                                                       .BeApproximately(shift.Y, 0.001f);
        }
    }

    [Test]
    public void CameraJumpBiggerThanTheViewport_IsIgnored()
    {
        var still = NewFlock(11);
        var teleported = NewFlock(11);

        while (still.GroupsSpawned == 0)
        {
            still.Update(STEP, Viewport, Vector2.Zero, true);
            teleported.Update(STEP, Viewport, Vector2.Zero, true);
        }

        still.Update(STEP, Viewport, Vector2.Zero, true);
        teleported.Update(STEP, Viewport, new Vector2(5000f, 0f), true);

        teleported.Flyers
                  .Select(f => f.Position)
                  .Should()
                  .Equal(still.Flyers.Select(f => f.Position));
    }

    [Test]
    public void Reset_RemovesEveryFlyer()
    {
        var flock = NewFlock(3);

        while (flock.GroupsSpawned == 0)
            Run(flock, STEP);

        flock.Reset();

        flock.Flyers
             .Should()
             .BeEmpty();
    }

    [Test]
    public void FrameOf_IsAlwaysAValidFrame()
    {
        var flock = NewFlock(5);

        for (var i = 0; i < 400; i++)
        {
            flock.Update(STEP, Viewport, Vector2.Zero, true);

            foreach (var flyer in flock.Flyers)
                flock.FrameOf(flyer)
                     .Should()
                     .BeInRange(0, FlyByStyle.Bats.Frames.Count - 1);
        }
    }
}
```

- [ ] **Step 2: Build to see it fail**

Run the Task 2 build command. Expected: FAIL with `CS0246: The type or namespace name 'FlyByFlock' could not be found`.

- [ ] **Step 3: Write the style**

Create `Chaos.Client.Rendering/FlyByStyle.cs`:

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Tunables for one <see cref="FlyByRenderer" /> effect: groups of small pixel sprites that cross the screen now and
///     then, with quiet gaps between. Every "Min/Max" pair is rolled uniformly. Adjust for feel and rebuild to apply.
/// </summary>
public sealed record FlyByStyle
{
    /// <summary>Seconds for the whole effect to fade fully in or out.</summary>
    public float FadeSeconds { get; init; } = 1.5f;

    /// <summary>The animation frames, as <see cref="SpriteGrid" /> text grids.</summary>
    public IReadOnlyList<string[]> Frames { get; init; } = [];

    /// <summary>Frame indices played in order, once per flap cycle.</summary>
    public int[] FrameCycle { get; init; } = [0];

    /// <summary>Tint of every flyer (RGB only).</summary>
    public Color Color { get; init; } = Color.Black;

    /// <summary>Opacity of every flyer when fully faded in [0..1].</summary>
    public float Alpha { get; init; } = 1f;

    /// <summary>Seconds from switching on to the first group.</summary>
    public float FirstGroupMin { get; init; } = 2f;

    public float FirstGroupMax { get; init; } = 6f;

    /// <summary>Seconds from one group to the next.</summary>
    public float GapMin { get; init; } = 8f;

    public float GapMax { get; init; } = 20f;

    /// <summary>Flyers per group, both ends included.</summary>
    public int GroupMin { get; init; } = 4;

    public int GroupMax { get; init; } = 8;

    /// <summary>Group speed across the screen, px/sec.</summary>
    public float SpeedMin { get; init; } = 100f;

    public float SpeedMax { get; init; } = 100f;

    /// <summary>Each flyer's speed is the group's times 1 plus or minus this.</summary>
    public float SpeedJitter { get; init; } = 0.1f;

    /// <summary>How far behind the front of its group a flyer may start, px.</summary>
    public float SpreadAlong { get; init; }

    /// <summary>How far above or below its group's line a flyer may start, px.</summary>
    public float SpreadAcross { get; init; }

    /// <summary>Largest up or down speed a group shares, px/sec. Rolled between minus and plus this.</summary>
    public float DriftMax { get; init; }

    /// <summary>Up-and-down bob height in px. Each flyer rolls 0.7 to 1.3 times this.</summary>
    public float WaveAmplitude { get; init; }

    /// <summary>Bob cycles per second. Each flyer rolls 0.8 to 1.2 times this.</summary>
    public float WaveFreq { get; init; } = 1f;

    /// <summary>Seconds between darts. A max of zero turns darting off.</summary>
    public float DartIntervalMin { get; init; }

    public float DartIntervalMax { get; init; }

    /// <summary>Largest dart push in px/sec: sideways (X) and up or down (Y).</summary>
    public Vector2 DartSpeed { get; init; }

    /// <summary>Flap cycles per second.</summary>
    public float FlapMin { get; init; } = 3f;

    public float FlapMax { get; init; } = 3f;

    /// <summary>Px outside the viewport edge where a group enters.</summary>
    public float EntryMargin { get; init; } = 30f;

    /// <summary>Px past the far edge where a flyer is removed.</summary>
    public float ExitMargin { get; init; } = 140f;

    /// <summary>Highest start line, px from the top of the viewport.</summary>
    public float BandTop { get; init; } = 30f;

    /// <summary>Lowest start line, as a fraction of the viewport height.</summary>
    public float BandBottom { get; init; } = 0.7f;

    // ============================================================
    // Presets — one per map flag
    // ============================================================

    /// <summary>Groups of 4-8 black bats, 19 px wide, darting across every 8-20 s.</summary>
    public static FlyByStyle Bats { get; } = new()
    {
        FadeSeconds = 1.5f,
        Frames = SpriteGrid.BatFrames,
        FrameCycle = [0, 1, 2, 1],
        Color = new Color(18, 10, 22),
        Alpha = 0.95f,
        FirstGroupMin = 2f,
        FirstGroupMax = 6f,
        GapMin = 8f,
        GapMax = 20f,
        GroupMin = 4,
        GroupMax = 8,
        SpeedMin = 95f,
        SpeedMax = 135f,
        SpeedJitter = 0.1f,
        SpreadAlong = 80f,
        SpreadAcross = 36f,
        DriftMax = 14f,
        WaveAmplitude = 4f,
        WaveFreq = 1.8f,
        DartIntervalMin = 0.15f,
        DartIntervalMax = 0.45f,
        DartSpeed = new Vector2(40f, 55f),
        FlapMin = 4f,
        FlapMax = 5f
    };
}
```

- [ ] **Step 4: Write the flock**

Create `Chaos.Client.Rendering/FlyByFlock.cs`:

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One sprite in a <see cref="FlyByFlock" />. Its position is relative to the viewport's top-left.</summary>
public sealed class Flyer
{
    internal Flyer() { }

    public Vector2 Position { get; internal set; }

    /// <summary>+1 while flying right, -1 while flying left.</summary>
    public int Direction { get; internal init; }

    internal float VelocityX { get; init; }
    internal float Drift { get; init; }
    internal float WaveAmplitude { get; init; }
    internal float WaveFreq { get; init; }
    internal float WavePhase { get; init; }
    internal float FlapRate { get; init; }
    internal float FlapPhase { get; init; }
    internal float Age { get; set; }
    internal float DartTimer { get; set; }
    internal Vector2 Dart { get; set; }
}

/// <summary>
///     The flight logic behind <see cref="FlyByRenderer" />, with no graphics: while spawning, a group of flyers enters
///     from the left or right edge after a random delay, crosses with a bob and random darts, and is removed past the
///     far edge. Camera movement shifts every flyer, so they fly over the MAP. Touched only on the game-loop thread.
/// </summary>
public sealed class FlyByFlock
{
    private readonly FlyByStyle Style;
    private readonly Random Rng;
    private readonly List<Flyer> FlyerList = [];
    private float? GroupTimer; // seconds to the next group; null while not spawning

    public FlyByFlock(FlyByStyle style, Random rng)
    {
        Style = style;
        Rng = rng;
    }

    public IReadOnlyList<Flyer> Flyers => FlyerList;

    /// <summary>How many groups have spawned since this flock was made. Tests use it to time the gaps.</summary>
    public int GroupsSpawned { get; private set; }

    /// <summary>Removes every flyer and restarts the first-group delay. Used on map change.</summary>
    public void Reset()
    {
        FlyerList.Clear();
        GroupTimer = null;
    }

    /// <summary>The index into <see cref="FlyByStyle.Frames" /> that <paramref name="flyer" /> shows now.</summary>
    public int FrameOf(Flyer flyer)
    {
        var cycle = Style.FrameCycle;
        var step = (int)(((flyer.Age * flyer.FlapRate) + flyer.FlapPhase) * cycle.Length);

        return cycle[step % cycle.Length];
    }

    /// <summary>
    ///     Advances every flyer by <paramref name="dt" /> seconds. While <paramref name="spawning" /> is true, counts
    ///     down to the next group. Turning spawning off forgets the countdown, so switching back on waits the first-group
    ///     delay again. A camera shift bigger than the viewport is a teleport and is ignored.
    /// </summary>
    public void Update(float dt, Vector2 viewportSize, Vector2 cameraShift, bool spawning)
    {
        if (dt <= 0f)
            return;

        if ((MathF.Abs(cameraShift.X) > viewportSize.X) || (MathF.Abs(cameraShift.Y) > viewportSize.Y))
            cameraShift = Vector2.Zero;

        if (spawning)
        {
            GroupTimer = (GroupTimer ?? Roll(Style.FirstGroupMin, Style.FirstGroupMax)) - dt;

            if (GroupTimer <= 0f)
            {
                SpawnGroup(viewportSize);
                GroupTimer = Roll(Style.GapMin, Style.GapMax);
            }
        }
        else
            GroupTimer = null;

        for (var i = FlyerList.Count - 1; i >= 0; i--)
        {
            var flyer = FlyerList[i];
            Move(flyer, dt, cameraShift);

            if (HasLeft(flyer, viewportSize.X))
                FlyerList.RemoveAt(i);
        }
    }

    private void SpawnGroup(Vector2 viewportSize)
    {
        var direction = Rng.Next(2) == 0 ? 1 : -1;
        var entryX = direction > 0 ? -Style.EntryMargin : viewportSize.X + Style.EntryMargin;
        var lineY = Roll(Style.BandTop, MathF.Max(Style.BandTop, viewportSize.Y * Style.BandBottom));
        var speed = Roll(Style.SpeedMin, Style.SpeedMax);
        var drift = Roll(-Style.DriftMax, Style.DriftMax);
        var count = Rng.Next(Style.GroupMin, Style.GroupMax + 1);

        for (var i = 0; i < count; i++)
            FlyerList.Add(
                new Flyer
                {
                    Direction = direction,
                    Position = new Vector2(
                        entryX - (direction * Roll(0f, Style.SpreadAlong)),
                        lineY + Roll(-Style.SpreadAcross, Style.SpreadAcross)),
                    VelocityX = direction * speed * Roll(1f - Style.SpeedJitter, 1f + Style.SpeedJitter),
                    Drift = drift,
                    WaveAmplitude = Style.WaveAmplitude * Roll(0.7f, 1.3f),
                    WaveFreq = Style.WaveFreq * Roll(0.8f, 1.2f),
                    WavePhase = Roll(0f, MathF.Tau),
                    FlapRate = Roll(Style.FlapMin, Style.FlapMax),
                    FlapPhase = Roll(0f, 1f)
                });

        GroupsSpawned++;
    }

    //velocity plus the group's drift, the bob (the derivative of a sine, so the height swings by WaveAmplitude) and the
    //current dart, plus the camera shift
    private void Move(Flyer flyer, float dt, Vector2 cameraShift)
    {
        flyer.Age += dt;

        if (Style.DartIntervalMax > 0f)
        {
            flyer.DartTimer -= dt;

            if (flyer.DartTimer <= 0f)
            {
                flyer.DartTimer = Roll(Style.DartIntervalMin, Style.DartIntervalMax);
                flyer.Dart = new Vector2(Roll(-Style.DartSpeed.X, Style.DartSpeed.X), Roll(-Style.DartSpeed.Y, Style.DartSpeed.Y));
            }
        }

        var angle = (MathF.Tau * flyer.WaveFreq * flyer.Age) + flyer.WavePhase;
        var bob = flyer.WaveAmplitude * MathF.Tau * flyer.WaveFreq * MathF.Cos(angle);
        var velocity = new Vector2(flyer.VelocityX + flyer.Dart.X, flyer.Drift + flyer.Dart.Y + bob);

        flyer.Position += cameraShift + (velocity * dt);
    }

    private bool HasLeft(Flyer flyer, float viewportWidth)
        => flyer.Direction > 0 ? flyer.Position.X > viewportWidth + Style.ExitMargin : flyer.Position.X < -Style.ExitMargin;

    private float Roll(float min, float max) => min + ((float)Rng.NextDouble() * (max - min));
}
```

- [ ] **Step 5: Build and run the tests**

Run the Task 2 build command, then:
`dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/FlyByFlockTests/*"`

Expected: 8 tests pass, 0 failed.

`FlyByStyle.cs` and `FlyByFlock.cs` link to `FlyByRenderer` in doc comments, and that class arrives in Task 6. An unresolved doc link is normally a warning. If the build instead stops with error `CS1574`, change those two links to `<c>FlyByRenderer</c>` and say so in your report.

```json:metadata
{"files": ["Chaos.Client.Rendering/FlyByStyle.cs", "Chaos.Client.Rendering/FlyByFlock.cs", "Tests/Chaos.Client.Tests/FlyByFlockTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server && dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/FlyByFlockTests/*\"", "acceptanceCriteria": ["no flyers before first-group delay; then 4-8 entering from off screen", "later groups 8-20 s apart", "spawning off: no new group, all flyers removed within 30 s", "camera shift moves every flyer by the shift; shift larger than viewport ignored", "Reset removes every flyer", "FrameOf returns a valid frame index", "FlyByFlockTests passes"], "modelTier": "mechanical"}
```

---

### Task 6: Fly-by renderer and Bats wiring

**Goal:** `FlyByRenderer` fades a `FlyByFlock` in and out and draws its flyers, and the `Bats` flag uses it.

**Files:**
- Create: `Chaos.Client.Rendering/FlyByRenderer.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs` (`Overlays` list and the draw-order comment above it)

**Acceptance Criteria:**
- [ ] `FlyByRenderer` implements `IAmbientOverlay` with `BlendState.NonPremultiplied`.
- [ ] `SetActive(on, immediate: true)` clears the flock; switching on after a full fade-out also clears it; switching off stops new groups while the effect fades over `FadeSeconds`.
- [ ] Each flyer draws its current frame, tinted `Style.Color` at `Style.Alpha` times the fade, at a whole-pixel position inside the viewport.
- [ ] Frame textures are built on first draw and disposed in `Dispose`.
- [ ] `Overlays` ends with `(MapFlags.Bats, new FlyByRenderer(FlyByStyle.Bats))`.
- [ ] The full client test suite passes, including `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay`.

**Verify:** build as in Task 2, then `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → 0 failed.

**Steps:**

- [ ] **Step 1: Confirm the test that covers this task fails**

Run the Task 2 build command, then:
`dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/AmbientEffectsTests/*"`

Expected: `EveryEffectFlag_HasAnOverlay` FAILS, naming `Bats`.

- [ ] **Step 2: Write the renderer**

Create `Chaos.Client.Rendering/FlyByRenderer.cs`:

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Full-viewport overlay for things that fly past in groups now and then — the bats. The flight itself is a
///     <see cref="FlyByFlock" />; this class fades the whole effect in and out and draws each flyer's current frame as a
///     1:1 pixel sprite. Switched off, no new group starts, and the flyers already in the air fade with the effect.
///     Touched only on the game-loop thread.
/// </summary>
public sealed class FlyByRenderer : IAmbientOverlay
{
    private readonly FlyByStyle Style;
    private readonly FlyByFlock Flock;

    private Texture2D[]? FrameTextures;
    private Vector2? LastWorldOrigin;
    private bool Active;
    private float EffectAlpha; // 0..1 current fade level

    public FlyByRenderer(FlyByStyle style)
    {
        Style = style;
        Flock = new FlyByFlock(style, new Random());
    }

    /// <inheritdoc />
    public BlendState BlendState => BlendState.NonPremultiplied;

    /// <inheritdoc />
    public bool IsActive => Active || (EffectAlpha > 0f);

    /// <inheritdoc />
    public void SetActive(bool on, bool immediate = false)
    {
        //starting from nothing (a map change, or switched on after a full fade-out): drop any flyers left over, so a
        //stale group never resumes mid-screen
        if (immediate || (on && !IsActive))
        {
            Flock.Reset();
            LastWorldOrigin = null;
        }

        Active = on;

        if (immediate)
            EffectAlpha = on ? 1f : 0f;
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (dt <= 0f)
            return;

        var step = dt / Style.FadeSeconds;
        EffectAlpha = Active ? MathF.Min(1f, EffectAlpha + step) : MathF.Max(0f, EffectAlpha - step);

        if (!IsActive || (viewport.Width <= 0) || (viewport.Height <= 0))
        {
            //forget the camera so the next activation doesn't shift everything by the distance walked meanwhile
            LastWorldOrigin = null;

            return;
        }

        var cameraShift = LastWorldOrigin is { } last ? worldOrigin - last : Vector2.Zero;
        LastWorldOrigin = worldOrigin;

        Flock.Update(dt, new Vector2(viewport.Width, viewport.Height), cameraShift, Active);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if ((EffectAlpha <= 0f) || (Flock.Flyers.Count == 0))
            return;

        var device = spriteBatch.GraphicsDevice;

        FrameTextures ??= Style.Frames
                               .Select(rows => SpriteGrid.Build(device, rows, Color.Black))
                               .ToArray();

        var alpha = (byte)Math.Clamp((int)(255f * Style.Alpha * EffectAlpha), 0, 255);
        var color = new Color(Style.Color.R, Style.Color.G, Style.Color.B, alpha);

        foreach (var flyer in Flock.Flyers)
        {
            var texture = FrameTextures[Flock.FrameOf(flyer)];

            var topLeft = new Vector2(
                viewport.X + MathF.Round(flyer.Position.X - (texture.Width / 2f)),
                viewport.Y + MathF.Round(flyer.Position.Y - (texture.Height / 2f)));

            spriteBatch.Draw(texture, topLeft, color);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (FrameTextures is not null)
            foreach (var texture in FrameTextures)
                texture.Dispose();

        FrameTextures = null;
    }
}
```

If Task 5 changed the crefs in `FlyByStyle.cs` to `<c>FlyByRenderer</c>`, change them back to `<see cref="FlyByRenderer" />`.

- [ ] **Step 3: Wire the flag**

In `Chaos.Client.Rendering/AmbientEffects.cs`:

1. Change the last entry of `Overlays` from `(MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts))` to:

```csharp
        (MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts)),
        (MapFlags.Bats, new FlyByRenderer(FlyByStyle.Bats))
```

2. Change the comment above `Overlays` from:

```csharp
    //draw order, back to front: cloud shadows first because they lie on the ground, then tints and mists, lightning
    //flashing through them, fog over the flash (as in the original storm), then particles last so glows and falling
    //things read on top of every haze
```

to:

```csharp
    //draw order, back to front: cloud shadows first because they lie on the ground, then tints and mists, lightning
    //flashing through them, fog over the flash (as in the original storm), then particles so glows and falling
    //things read on top of every haze, and bats last of all, flying over everything
```

- [ ] **Step 4: Build and run the whole client suite**

Run the Task 2 build command, then:
`dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`

Expected: 0 failed, including `EveryEffectFlag_HasAnOverlay`. If a test unrelated to this plan fails, report its name and message to the coordinator. Do not stash, reset or switch branches to compare against `main`.

```json:metadata
{"files": ["Chaos.Client.Rendering/FlyByRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server && dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["FlyByRenderer implements IAmbientOverlay, NonPremultiplied", "immediate SetActive and on-after-full-fade clear the flock; off stops new groups while fading", "flyers drawn at current frame, Style.Color at Style.Alpha x fade, whole-pixel positions", "frame textures built on first draw, disposed in Dispose", "Overlays ends with Bats FlyByRenderer", "full client suite 0 failed incl. EveryEffectFlag_HasAnOverlay"], "modelTier": "mechanical"}
```

---

### Task 7: Docs

**Goal:** The Unora effects guide and the client's CLAUDE.md describe the three new effects and the fly-by renderer.

**Files:**
- Modify: `docs/ambient-map-effects.md` (Unora worktree)
- Modify: `CLAUDE.md` (client worktree, the `AmbientEffects` bullet)

**Acceptance Criteria:**
- [ ] The Unora guide's effects table has rows for `Bats`, `Ghosts` and `HarvestMoon`.
- [ ] The guide's bit table shows 28–30 as the Halloween set and 31–63 as free; the "Adding a new effect" example and the Limits section say the first free bit is 31 (33 slots left).
- [ ] The guide's renderer table lists `FlyByRenderer`, and the particle `Shape` row lists `Ghost`.
- [ ] The client CLAUDE.md `AmbientEffects` bullet names Bats, Ghosts and HarvestMoon, says bits 8-30, and names `FlyByRenderer`.

**Verify:** `grep -c "FlyByRenderer\|HarvestMoon" docs/ambient-map-effects.md` (Unora worktree) → at least 4; `grep -c "FlyByRenderer" CLAUDE.md` (client worktree) → 1.

**Steps:**

- [ ] **Step 1: Create the Unora worktree**

```bash
git -C C:/Users/Michael/Documents/GitHub/Unora -c core.longpaths=true worktree add -b feat/halloween-effects C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-unora main
```

Expected: `Preparing worktree (new branch 'feat/halloween-effects')`.

- [ ] **Step 2: Edit `docs/ambient-map-effects.md` in the Unora worktree**

Make these exact changes:

1. After the paragraph that starts "A second set followed later that month:" add a new paragraph:

```markdown
A Halloween set followed on 2026-09-30: Bats, Ghosts and HarvestMoon.
```

2. In "The effects" table, after the `Drips` row, add:

```markdown
| `Bats` | Groups of 4–8 black bats cross the screen and dart about, with a quiet gap of 8–20 seconds between groups | `FlyByRenderer` + `FlyByStyle.Bats` |
| `Ghosts` | A few pale pixel sheet ghosts float upward, sway, and fade fully in and out | `ParticleStyle.Ghosts` |
| `HarvestMoon` | Orange evening wash breathing slowly, dark edges, orange sparks rising and flickering gently | `MistStyle.HarvestMoon` + `ParticleStyle.HarvestSparks` |
```

3. Replace the line `No art files are involved. Every texture is generated in code when the effect first draws.` with:

```markdown
No art files are involved. Every texture is generated in code when the effect first draws. The bat and ghost sprites
are small pixel grids written as text in `SpriteGrid.cs`.
```

4. In the bit table, replace the row `| 28–63 | — | **Free.** New effects go here. |` with:

```markdown
| 28–30 | `Bats`, `Ghosts`, `HarvestMoon` | The Halloween set |
| 31–63 | — | **Free.** New effects go here. |
```

5. Replace `There are three renderers. Each implements `IAmbientOverlay`:` with `There are four renderers. Each implements `IAmbientOverlay`:`, and after the `LightningRenderer` row of that table add:

```markdown
| `FlyByRenderer` | Groups of small pixel sprites that cross the screen now and then, with quiet gaps between. The flight logic is in `FlyByFlock`, which tests run without graphics. | A `FlyByStyle` preset |
```

6. In the particle fields table, replace the `Shape` row's description `` `Square`, `Dot` (soft glow), `Leaf`, `Petal`, `Bubble` or `Streak` (a line along its motion) `` with:

```markdown
`Square`, `Dot` (soft glow), `Leaf`, `Petal`, `Bubble`, `Streak` (a line along its motion) or `Ghost` (the pixel sheet ghost, drawn 1:1; size and spin are ignored)
```

7. In "1. Add the flag (server repo)", replace:

~~~markdown
In `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs`, add the next free bit to `MapFlags`. Bits 17–27 hold the
second set of effects, so the first free one is 28. Write it as a `ulong` shift:

```csharp
    Drips = 1UL << 27,
    Snowglow = 1UL << 28
```
~~~

with:

~~~markdown
In `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs`, add the next free bit to `MapFlags`. Bits 28–30 hold the
Halloween set, so the first free one is 31. Write it as a `ulong` shift:

```csharp
    HarvestMoon = 1UL << 30,
    Snowglow = 1UL << 31
```
~~~

8. In "Limits", replace `New effects belong in bits 28–63.` with `New effects belong in bits 31–63.`, and replace `**36 more slots.** Bits 28–63 are free.` with `**33 more slots.** Bits 31–63 are free.`

- [ ] **Step 3: Edit the client `CLAUDE.md`**

In the client worktree's `CLAUDE.md`, in the bullet that starts ``- **`AmbientEffects`** --``:

1. Replace `Heat, Wisps and Drips (`MapFlags` bits 8-27;` with `Heat, Wisps, Drips, Bats, Ghosts and HarvestMoon (`MapFlags` bits 8-30;`.
2. Replace `` `ParticleRenderer` (procedural particles, optionally with flapping wings, tuned by `ParticleStyle` presets) `` with:

```markdown
`ParticleRenderer` (procedural particles, optionally with flapping wings, or the pixel sheet ghost, tuned by `ParticleStyle` presets), `FlyByRenderer` (groups of pixel sprites crossing now and then — the bats — tuned by `FlyByStyle` presets; the flight logic is the graphics-free `FlyByFlock`)
```

- [ ] **Step 4: Check**

Run in the Unora worktree: `grep -c "FlyByRenderer\|HarvestMoon" docs/ambient-map-effects.md` → 4 or more.
Run in the client worktree: `grep -c "FlyByRenderer" CLAUDE.md` → 1.

```json:metadata
{"files": ["docs/ambient-map-effects.md", "CLAUDE.md"], "verifyCommand": "grep -c \"FlyByRenderer\\|HarvestMoon\" C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-unora/docs/ambient-map-effects.md && grep -c FlyByRenderer C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-client/CLAUDE.md", "acceptanceCriteria": ["Unora guide effects table has Bats, Ghosts, HarvestMoon rows", "bit table: 28-30 Halloween set, 31-63 free; add-effect example and Limits say first free bit 31, 33 slots", "renderer table lists FlyByRenderer; Shape row lists Ghost", "client CLAUDE.md AmbientEffects bullet names the three flags, bits 8-30, FlyByRenderer"], "modelTier": "mechanical"}
```

---

### Task 8: Commit the full implementation

**Goal:** One commit per repo on `feat/halloween-effects`, with the client's `Chaos-Server` pointer set to the server commit.

**Files:**
- All files changed in Tasks 1–7, plus this plan and its `.tasks.json`.

**Acceptance Criteria:**
- [ ] The server worktree has one new commit on `feat/halloween-effects` holding exactly the six Task 1 files.
- [ ] The client worktree has one new commit holding the Task 2–7 client files, the plan, its `.tasks.json`, the spec edit, and `Chaos-Server` pointing at the server commit.
- [ ] The Unora worktree has one new commit holding `docs/ambient-map-effects.md`.
- [ ] `git status --short` in each worktree shows nothing from this plan left unstaged.

**Verify:** `git -C <each worktree> log --oneline -1` shows the new commit; `git -C C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-client ls-tree HEAD Chaos-Server` shows the server commit's hash.

**Steps:**

- [ ] **Step 1: Commit the server**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-server
git add Chaos.DarkAges/Definitions/Enums.cs Chaos/Utilities/MapFlagVisibility.cs Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs Tests/Chaos.Tests/MapFlagVisibilityTests.cs Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs
git commit -m "Add the Bats, Ghosts and Harvest Moon map effects

Map flags 28-30, hidden by /atmosphere, offered on page 2 of the Suomi
theatre menu.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git rev-parse HEAD
```

Note the printed hash as `<server-sha>`.

- [ ] **Step 2: Commit the client**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-client
git update-index --cacheinfo 160000,<server-sha>,Chaos-Server
git add Chaos.Client.Rendering/SpriteGrid.cs Chaos.Client.Rendering/MistStyle.cs Chaos.Client.Rendering/ParticleStyle.cs Chaos.Client.Rendering/ParticleRenderer.cs Chaos.Client.Rendering/FlyByStyle.cs Chaos.Client.Rendering/FlyByFlock.cs Chaos.Client.Rendering/FlyByRenderer.cs Chaos.Client.Rendering/AmbientEffects.cs Tests/Chaos.Client.Tests/SpriteGridTests.cs Tests/Chaos.Client.Tests/HalloweenPresetsTests.cs Tests/Chaos.Client.Tests/FlyByFlockTests.cs CLAUDE.md
git add -f docs/superpowers/specs/2026-09-30-halloween-effects-design.md docs/superpowers/plans/2026-09-30-halloween-effects.md docs/superpowers/plans/2026-09-30-halloween-effects.md.tasks.json
git commit -m "Draw the Bats, Ghosts and Harvest Moon map effects

Bats fly past in groups (new FlyByRenderer), ghosts are a pixel-sprite
particle shape, and Harvest Moon is a mist plus sparks. Moves Chaos-Server
to <server-sha>.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Replace both `<server-sha>` placeholders with the hash from Step 1 before running.

- [ ] **Step 3: Commit Unora**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-unora
git add docs/ambient-map-effects.md
git commit -m "Document the Bats, Ghosts and Harvest Moon map effects

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Check**

Run `git status --short` in each of the three worktrees. Expected: no file from this plan listed. (A Unora worktree may show `Custom Client Mods/.../obj` build outputs; leave them.)

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/halloween-effects-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["server: one commit with the six Task 1 files", "client: one commit with Task 2-7 files, plan, tasks.json, spec, Chaos-Server at the server commit", "Unora: one commit with docs/ambient-map-effects.md", "no plan file left unstaged in any worktree"], "modelTier": "mechanical"}
```

---

## After the plan

Not part of this plan, and left to the user:

- Merging the three branches (server, then client, then Unora) and pushing.
- The in-game check: `/mapFlag add Bats`, `Ghosts`, `HarvestMoon` one at a time and together; walk around; remove each; change maps; try theatre menu page 2.
- Putting the flags on any map.

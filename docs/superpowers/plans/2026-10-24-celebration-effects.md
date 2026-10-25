# Celebration Map Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Confetti and Balloons map effects. GMs switch each one on alone, Mileth festivals turn both on, and both appear in the theatre menu and the F4 hide list.

**Architecture:** Two new `MapFlags` bits live in the shared `Chaos.DarkAges` code. The server turns them on through the existing `/mapFlag` command, through a new town setting `fairEffects` that `TownFestivalScript` applies while a festival runs, and through the theatre's stage-effects menu. The client draws each one with a `ParticleRenderer` preset that uses a new particle shape. Two new F4 boxes can hide them.

**Tech Stack:** C# 14 / .NET 10, MonoGame, TUnit + FluentAssertions + Moq (server and client tests).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-24-celebration-effects-design.md`

## Global Constraints

- The flag values are exactly `Confetti = 1UL << 31` and `Balloons = 1UL << 32`.
- The theatre's page 2 ends up with exactly 16 effects (`MAX_EFFECTS_PER_PAGE`). Confetti and Balloons go after Harvest Moon, in that order.
- The confetti colours are `#ff4d6d`, `#ffd23f`, `#3ec1ff`, `#6be37a`, `#c77dff`, `#ff9f1c` and `#ffffff`. Balloons use the first six.
- `TownFestivalScript` never removes a flag it did not add. This is checked flag by flag, not for the whole set at once.
- No `CLIENT_VERSION` bump.
- Paths: the server repo is `C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server`, the client is `C:\Users\Michael\Documents\GitHub\Chaos.Client`, and Unora is `C:\Users\Michael\Documents\GitHub\Unora`.
- Server tests: `dotnet run --project Tests/Chaos.Tests` from the server repo (`dotnet test` does not work on this SDK). Client tests: `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Both are TUnit. Pass `-- --treenode-filter "/*/*/<ClassName>/*"` to run one class.
- Commit strategy is at-end. Implementers do not commit. Task 6 makes the commits.
- No em dashes in JSON.

**User decisions (already made):**
- "A and B for Fair. Make them seperate effects for the admin switch though. Don't want C." This means confetti and balloons are separate flags, and there are no sparkle bursts.
- Fair placement option 1: the effects come on only during Mileth festivals, because the fair yard is part of the Mileth map.
- GMs switch them on with the existing `/mapFlag` command (the user picked options 1 and 3).
- "yes add them there as well": both go in the Suomi theatre's stage-effects menu.
- The user approved the F4 hide boxes and the plan to skip the `CLIENT_VERSION` bump, as part of the design.

## Review Focus

1. **A festival is already running when the server starts.** The first check after startup should turn on the festival effect and both fair effects. Task 2 covers this with `A_festival_running_at_startup_applies_everything_on_the_first_check`.
2. **A GM removes Balloons with `/mapFlag` in the middle of a festival.** Balloons should stay off, and the festival ending must not turn it back on or break the other flags. Task 2 covers this with `A_fair_effect_removed_mid_festival_stays_off`.
3. **The map already has Confetti on its own (a GM or the map file) when a festival starts.** Confetti should stay on after the festival ends, while Balloons and the festival effect come and go. Task 2 covers this with `A_flag_the_map_already_has_is_left_on`.
4. **A balloon reaches the screen edge.** A balloon is taller than the other particles, so it must wrap fully off screen. It should never pop in or out while visible. Task 4 covers this with `Balloons_StayInsideTheWrapMargin`.
5. **A player ticks "Disable confetti" while standing in the fair.** The confetti should go away at once, like the other hide boxes do. Task 5 covers this: `MapEffectFilter.Keys` gains both keys, and the shared `Expected` map checks them.

---

### Task 1: Server flags, atmosphere list and theatre menu

**Goal:** `MapFlags` gains Confetti and Balloons. `/atmosphere` hides them, the packet round-trips them, and the theatre offers them on page 2.

**Files:**
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs` (the `MapFlags` enum, after `HarvestMoon = 1UL << 30`)
- Modify: `Chaos-Server/Chaos/Utilities/MapFlagVisibility.cs` (`ATMOSPHERE`)
- Modify: `Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs` (`All`, after Harvest Moon)
- Test: `Chaos-Server/Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, `Chaos-Server/Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`, `Chaos-Server/Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`

**Acceptance Criteria:**
- [ ] `MapFlags.Confetti == 1UL << 31` and `MapFlags.Balloons == 1UL << 32`.
- [ ] `MapFlagVisibility.ATMOSPHERE` includes both.
- [ ] `TheatreStageEffects.Available(MapFlags.None, 2)` ends with Confetti, then Balloons, and has 16 entries.
- [ ] The MapFlagVisibility, MapEffectsPacketConverter and TheatreStageEffects test classes pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"` → all pass. Then do the same for `MapFlagVisibilityTests` and `MapEffectsPacketConverterTests`.

**Steps:**

- [ ] **Step 1: Update the three tests first**

In `MapFlagVisibilityTests.cs`, the effect list ends `| MapFlags.HarvestMoon;` (about line 35). Change it to:

```csharp
                                                   | MapFlags.HarvestMoon
                                                   | MapFlags.Confetti
                                                   | MapFlags.Balloons;
```

In `Networking/MapEffectsPacketConverterTests.cs`, the flag array ends with `MapFlags.HarvestMoon` (about line 38). Change it to:

```csharp
        MapFlags.HarvestMoon,
        MapFlags.Confetti,
        MapFlags.Balloons
```

In `Theatre/TheatreStageEffectsTests.cs`, make three changes:
- The combined list ending `| MapFlags.HarvestMoon;` (about line 36) gets `| MapFlags.Confetti | MapFlags.Balloons` added before the semicolon, in the same one-flag-per-line layout.
- The page-2 `.Equal(...)` list ending `MapFlags.HarvestMoon);` (about line 159) becomes `MapFlags.HarvestMoon, MapFlags.Confetti, MapFlags.Balloons);`, keeping one flag per line.
- `Available_WhileLightsOff_KeepsAllOfPageTwo` changes `.HaveCount(14);` to `.HaveCount(16);`.

- [ ] **Step 2: Run the tests and watch them fail to compile**

Run: `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"`
Expected: a build error, because `MapFlags` does not contain a definition for `Confetti`.

- [ ] **Step 3: Add the flags**

In `Chaos.DarkAges/Definitions/Enums.cs`, change `HarvestMoon = 1UL << 30` to end with a comma, and add the two new flags after it. Match the doc comment style of the flags above it:

```csharp
    HarvestMoon = 1UL << 30,

    /// <summary>
    ///     Party-coloured paper strips tumbling down. Mileth festivals turn it on for the fair
    /// </summary>
    Confetti = 1UL << 31,

    /// <summary>
    ///     Balloons drifting slowly up. Mileth festivals turn it on for the fair
    /// </summary>
    Balloons = 1UL << 32
```

In `Chaos/Utilities/MapFlagVisibility.cs`, change `| MapFlags.HarvestMoon;` to:

```csharp
                                       | MapFlags.HarvestMoon
                                       | MapFlags.Confetti
                                       | MapFlags.Balloons;
```

In `TheatreStageEffects.cs`, change `new("Harvest Moon", MapFlags.HarvestMoon, 2)` to:

```csharp
        new("Harvest Moon", MapFlags.HarvestMoon, 2),
        new("Confetti", MapFlags.Confetti, 2),
        new("Balloons", MapFlags.Balloons, 2)
```

- [ ] **Step 4: Run the three test classes and check they pass**

Run each, one at a time:
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"`
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/MapFlagVisibilityTests/*"`
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/MapEffectsPacketConverterTests/*"`

Expected: all pass.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos/Utilities/MapFlagVisibility.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs", "Chaos-Server/Tests/Chaos.Tests/MapFlagVisibilityTests.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs", "Chaos-Server/Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests -- --treenode-filter \"/*/*/TheatreStageEffectsTests/*\"", "acceptanceCriteria": ["MapFlags.Confetti == 1UL << 31 and MapFlags.Balloons == 1UL << 32", "MapFlagVisibility.ATMOSPHERE includes both", "Theatre page 2 ends Confetti, Balloons and has 16 entries", "MapFlagVisibility, MapEffectsPacketConverter and TheatreStageEffects tests pass"], "modelTier": "mechanical"}
```

---

### Task 2: Festival fair effects on the server

**Goal:** A town's `fairEffects` setting loads into `TownConfig.FairEffects`. `TownFestivalScript` applies those effects, along with the festival effect, while a festival runs, and removes only what it added, checked flag by flag.

**Files:**
- Modify: `Chaos-Server/Chaos/Services/Towns/TownConfig.cs`:
  - `TownConfig`: add a `FairEffects` property next to `FairVendors` (about line 102);
  - the schema class: add `List<string>? FairEffects` next to `FairVendors` (about line 182).
- Modify: `Chaos-Server/Chaos/Services/Towns/TownCatalog.cs`: parse the list after the fair vendor loop (about line 296), and set it in the `built with { ... }` block next to `FairVendors = vendors`.
- Modify: `Chaos-Server/Chaos/Services/Towns/TownGovernmentService.cs`: add a `FairEffects(string)` method next to `FairVendors(string)` (about line 274).
- Modify: `Chaos-Server/Chaos/Scripting/MapScripts/Temuair/TownFestivalScript.cs`: the class summary, and the flag block in `Update`.
- Test: `Chaos-Server/Tests/Chaos.Tests/Towns/TownCatalogTests.cs` (append), and a new file `Chaos-Server/Tests/Chaos.Tests/Towns/TownFestivalEffectsTests.cs`.

**Acceptance Criteria:**
- [ ] With no `fairEffects`, `FairEffects` is `MapFlags.None`. With `["Confetti", "balloons"]`, it is `Confetti | Balloons`.
- [ ] A blank, numeric, `None` or unknown name skips the file with an error containing `fair effect`.
- [ ] A festival adds Petals, Confetti and Balloons. Its end removes all three.
- [ ] A flag the map already had before the festival is still on after the festival ends.
- [ ] A fair effect a GM removed mid-festival stays off after the festival ends, and the other flags clear.
- [ ] A festival running at startup applies everything on the first check.
- [ ] All `TownCatalogTests`, `TownFestivalEffectsTests` and `TownFestivalVendorsTests` pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TownFestivalEffectsTests/*"` → 5 pass. Then run `TownCatalogTests` and `TownFestivalVendorsTests` the same way, and check that every test passes.

**Steps:**

- [ ] **Step 1: Write the catalog tests**

Append these inside `TownCatalogTests`, before the closing brace:

```csharp
    private const string FAIR_EFFECTS = "\"shopSharePercent\": 8, \"fairEffects\": [\"Confetti\", \"balloons\"]";

    [Test]
    public void Fair_effects_default_to_none()
    {
        var load = Parse(("mileth.json", MILETH));

        load.Towns.Single().FairEffects.Should().Be(MapFlags.None);
    }

    [Test]
    public void Fair_effects_are_read()
    {
        var load = Parse(("mileth.json", MILETH.Replace("\"shopSharePercent\": 8", FAIR_EFFECTS)));

        load.Errors.Should().BeEmpty();
        load.Towns.Single().FairEffects.Should().Be(MapFlags.Confetti | MapFlags.Balloons);
    }

    [Test]
    [Arguments("\"Sparkles\"")]
    [Arguments("\"\"")]
    [Arguments("\"3\"")]
    [Arguments("\"None\"")]
    [Arguments("null")]
    public void A_bad_fair_effect_skips_the_file(string replace)
    {
        var load = Parse(("mileth.json", MILETH.Replace("\"shopSharePercent\": 8", FAIR_EFFECTS.Replace("\"balloons\"", replace))));

        load.Towns.Should().BeEmpty();
        load.Errors.Should().ContainSingle().Which.Should().Contain("fair effect");
    }

    [Test]
    public void Fair_effects_are_found_by_the_festival_map()
    {
        var town = TownTestSupport.Mileth() with { FairEffects = MapFlags.Confetti };
        var service = TownTestSupport.CreateService(out _, out _, out _, out _, town);

        service.FairEffects("MILETH").Should().Be(MapFlags.Confetti);
        service.FairEffects("rucesion").Should().Be(MapFlags.None);
    }
```

If `MapFlags` is not already imported in that file, add `using Chaos.DarkAges.Definitions;` to its `#region` usings.

- [ ] **Step 2: Write the festival script tests**

Create `Tests/Chaos.Tests/Towns/TownFestivalEffectsTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Scripting.MapScripts.Temuair;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Towns;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
#endregion

namespace Chaos.Tests.Towns;

public sealed class TownFestivalEffectsTests
{
    private static readonly TimeSpan Check = TimeSpan.FromSeconds(5);
    private const MapFlags FAIR = MapFlags.Confetti | MapFlags.Balloons;

    private sealed class World
    {
        public required MapInstance Map { get; init; }
        public required TownConfig Town { get; init; }
        public required TownGovernmentState State { get; init; }
        public required TownFestivalScript Script { get; init; }

        public void StartFestival()
            => State.For(Town).Festival = new TownTimedEffect
            {
                StartUtc = TownTestSupport.Start.UtcDateTime,
                EndUtc = TownTestSupport.Start.UtcDateTime.AddDays(1),
                Effect = "Petals"
            };

        public void EndFestival() => State.For(Town).Festival = null;
    }

    private static World Create(MapFlags fairEffects = FAIR, MapFlags mapOwn = MapFlags.None, bool festivalAtStartup = false)
    {
        var map = MockMapInstance.Create("mileth", "Mileth", 60, 80);
        map.Flags = mapOwn;
        var town = TownTestSupport.Mileth() with { FestivalMapId = map.InstanceId, FairEffects = fairEffects };
        var service = TownTestSupport.CreateService(out var state, out _, out _, out _, town);
        var world = new World
        {
            Map = map,
            Town = town,
            State = state,
            Script = new TownFestivalScript(map, service, new Mock<IMerchantFactory>().Object, NullLogger<TownFestivalScript>.Instance)
        };

        if (festivalAtStartup)
            world.StartFestival();

        return world;
    }

    [Test]
    public void A_festival_adds_its_effect_and_the_fair_effects_and_its_end_removes_them()
    {
        var world = Create();
        world.StartFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.Petals | FAIR);

        world.EndFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.None);
    }

    [Test]
    public void A_flag_the_map_already_has_is_left_on()
    {
        var world = Create(mapOwn: MapFlags.Confetti);
        world.StartFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.Petals | FAIR);

        world.EndFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.Confetti);
    }

    [Test]
    public void A_town_without_fair_effects_adds_only_the_festival_effect()
    {
        var world = Create(fairEffects: MapFlags.None);
        world.StartFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.Petals);
    }

    [Test]
    public void A_festival_running_at_startup_applies_everything_on_the_first_check()
    {
        var world = Create(festivalAtStartup: true);
        world.Script.Update(TimeSpan.FromMilliseconds(1));

        world.Map.Flags.Should().Be(MapFlags.Petals | FAIR);
    }

    [Test]
    public void A_fair_effect_removed_mid_festival_stays_off()
    {
        var world = Create();
        world.StartFestival();
        world.Script.Update(Check);
        world.Map.Flags &= ~MapFlags.Balloons;

        world.Script.Update(Check);
        world.Map.Flags.Should().Be(MapFlags.Petals | MapFlags.Confetti);

        world.EndFestival();
        world.Script.Update(Check);

        world.Map.Flags.Should().Be(MapFlags.None);
    }
}
```

If `MapInstance.Flags` has no public setter on the mock, set it the way `MapFlagCommand` does (`source.MapInstance.Flags |= ...`). That command shows the property is settable.

- [ ] **Step 3: Run the tests and watch them fail to compile**

Run: `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TownFestivalEffectsTests/*"`
Expected: a build error, because `TownConfig` does not contain a definition for `FairEffects`.

- [ ] **Step 4: Add the config field, the schema field and the parsing**

In `TownConfig.cs`, add this after the `FairVendors` property of `TownConfig`:

```csharp
    /// <summary>Map effects the festival map script adds while a festival runs, on top of the festival's own effect.</summary>
    public MapFlags FairEffects { get; init; }
```

Add this to the schema class, after `public List<TownFairVendorSchema>? FairVendors { get; set; }`:

```csharp
    public List<string>? FairEffects { get; set; }
```

In `TownCatalog.cs`, add this right after the `foreach (var vendor in schema.FairVendors ?? [])` loop closes, before `var built = new TownConfig`:

```csharp
        var fairEffects = MapFlags.None;

        foreach (var name in schema.FairEffects ?? [])
        {
            if (string.IsNullOrWhiteSpace(name)
                || int.TryParse(name, out _)
                || !Enum.TryParse<MapFlags>(name, true, out var flag)
                || !Enum.IsDefined(flag)
                || (flag == MapFlags.None))
                return $"fair effect '{name}' is not a map effect";

            fairEffects |= flag;
        }
```

In the `built = built with { ... }` block, change `FairVendors = vendors,` to:

```csharp
            FairVendors = vendors,
            FairEffects = fairEffects,
```

- [ ] **Step 5: Add the service method**

In `TownGovernmentService.cs`, add this after the `FairVendors(string mapInstanceId)` method:

```csharp
    /// <summary>The fair effects of the town whose festival map this is; none for any other map.</summary>
    public MapFlags FairEffects(string mapInstanceId) => Catalog.ByFestivalMap(mapInstanceId)?.FairEffects ?? MapFlags.None;
```

- [ ] **Step 6: Apply the fair effects in the festival script, checking each flag on its own**

In `TownFestivalScript.Update`, replace the block from `var wanted = festival?.Effect ?? MapFlags.None;` through `Applied = wanted;` with:

```csharp
        var wanted = festival is null ? MapFlags.None : festival.Effect | Towns.FairEffects(Subject.InstanceId);

        if (wanted == Requested)
            return;

        Requested = wanted;
        var before = Subject.Flags;
        Subject.Flags &= ~Applied;

        //a flag the map already has on its own is not ours to remove later
        wanted &= ~Subject.Flags;
        Subject.Flags |= wanted;
        Applied = wanted;
```

Keep the lines after it (`if (Subject.Flags == before) return;` and the refresh loop) unchanged.

In the class summary, change "adds the festival's map effect while it runs" to "adds the festival's map effect, and the town's fair effects, while it runs". Change "Only the flag this script added is ever removed" to "Only the flags this script added are ever removed".

- [ ] **Step 7: Run the tests and check they pass**

Run each, one at a time:
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TownFestivalEffectsTests/*"`, expected 5 pass;
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TownCatalogTests/*"`, expected all pass;
- `dotnet run --project Tests/Chaos.Tests -- --treenode-filter "/*/*/TownFestivalVendorsTests/*"`, expected all pass.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Towns/TownConfig.cs", "Chaos-Server/Chaos/Services/Towns/TownCatalog.cs", "Chaos-Server/Chaos/Services/Towns/TownGovernmentService.cs", "Chaos-Server/Chaos/Scripting/MapScripts/Temuair/TownFestivalScript.cs", "Chaos-Server/Tests/Chaos.Tests/Towns/TownCatalogTests.cs", "Chaos-Server/Tests/Chaos.Tests/Towns/TownFestivalEffectsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests -- --treenode-filter \"/*/*/TownFestivalEffectsTests/*\"", "acceptanceCriteria": ["Missing fairEffects gives MapFlags.None; [Confetti, balloons] gives Confetti|Balloons", "Blank, numeric, None or unknown name skips the file with an error containing 'fair effect'", "Festival adds Petals+Confetti+Balloons; end removes all three", "A flag the map already had stays on after the festival", "A fair effect removed mid-festival stays off and the rest clear at the end", "A festival running at startup applies everything on the first check", "TownCatalogTests, TownFestivalEffectsTests and TownFestivalVendorsTests all pass"], "modelTier": "standard"}
```

---

### Task 3: Mileth fair effects setting in Unora

**Goal:** Mileth's town file turns on Confetti and Balloons during its festivals.

**Files:**
- Modify: `Unora/Data/Configuration/Towns/mileth.json` (add a line after `"festivalEffects": [...]`)

**Acceptance Criteria:**
- [ ] `mileth.json` has `"fairEffects": ["Confetti", "Balloons"]` and still parses as JSON.
- [ ] No other town file has `fairEffects`.

**Verify:** `python -I -c "import json; print(json.load(open('Data/Configuration/Towns/mileth.json', encoding='utf-8-sig'))['fairEffects'])"` (run from the Unora repo) → `['Confetti', 'Balloons']`

**Steps:**

- [ ] **Step 1: Add the setting**

Find the line that starts `"festivalEffects": [` in `mileth.json`. Add this as the next key, keeping the file's indentation and comma style:

```json
  "fairEffects": ["Confetti", "Balloons"],
```

- [ ] **Step 2: Check it parses**

Run the Verify command. Expected: `['Confetti', 'Balloons']`.

```json:metadata
{"files": ["Unora/Data/Configuration/Towns/mileth.json"], "verifyCommand": "python -I -c \"import json; print(json.load(open('Data/Configuration/Towns/mileth.json', encoding='utf-8-sig'))['fairEffects'])\"", "acceptanceCriteria": ["mileth.json has fairEffects [Confetti, Balloons] and parses", "No other town file has fairEffects"], "modelTier": "mechanical"}
```

---

### Task 4: Client confetti and balloon looks

**Goal:** The client draws Confetti as tumbling paper strips and Balloons as rising balloons with a shine and a string, and `AmbientEffects` covers both flags.

**Files:**
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs`: two new `ParticleShape` values, and two presets after `HarvestSparks`.
- Modify: `Chaos.Client.Rendering/ParticleRenderer.cs`: the constants, the texture fields, `Draw`, `ShapeTransform`, `GetShapeTexture`, `Dispose`, and two texture builders.
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs` (`Overlays`, before the Ghosts entry)
- Modify: `CLAUDE.md` (the `AmbientEffects` bullet)
- Test: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`

**Acceptance Criteria:**
- [ ] `EveryEffectFlag_HasAnOverlay` passes with Confetti and Balloons in `MapFlags`.
- [ ] `Balloons_StayInsideTheWrapMargin` passes: half the drawn balloon height at `SizeMax` is at most `WRAP_MARGIN`.
- [ ] The balloon body is tinted by the particle colour, while the shine and string are drawn light grey, untinted.
- [ ] The client test project builds with no new warnings.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/AmbientEffectsTests/*"` → 3 pass

**Steps:**

The client compiles the shared code from its `Chaos-Server` submodule. It needs the Task 1 server change, so this task runs after Task 1 and against that server tree.

- [ ] **Step 1: Write the wrap-margin test**

Add to `AmbientEffectsTests`:

```csharp
    [Test]
    public void Balloons_StayInsideTheWrapMargin()
        => (ParticleStyle.Balloons.SizeMax / ParticleRenderer.BALLOON_TEX_W * ParticleRenderer.BALLOON_TEX_H / 2f)
           .Should()
           .BeLessThanOrEqualTo(ParticleRenderer.WRAP_MARGIN, "a balloon that wraps while still on screen pops in and out");
```

- [ ] **Step 2: Run it and watch it fail**

Run the Verify build command.
Expected: a build error, because `ParticleStyle` does not contain `Balloons`, and `ParticleRenderer` does not contain `BALLOON_TEX_W` or `WRAP_MARGIN` (that constant is private).

- [ ] **Step 3: Add the shapes and the presets**

In `ParticleStyle.cs`, inside `enum ParticleShape`, add these after `Streak,`:

```csharp
    /// <summary>Paper strip that spins and flips as it falls — confetti.</summary>
    Confetti,

    /// <summary>Round balloon with a hanging string; the body takes the particle colour, the shine and string stay pale.</summary>
    Balloon,
```

After the `HarvestSparks` preset, add:

```csharp
    /// <summary>Party-coloured paper strips tumbling down and flipping. Mileth festivals turn it on for the fair.</summary>
    public static ParticleStyle Confetti { get; } = new()
    {
        Count = 70,
        Shape = ParticleShape.Confetti,
        Colors =
        [
            new Color(255, 77, 109), new Color(255, 210, 63), new Color(62, 193, 255), new Color(107, 227, 122),
            new Color(199, 125, 255), new Color(255, 159, 28), Color.White
        ],
        SizeMin = 4f,
        SizeMax = 7f,
        VelocityMin = new Vector2(-6f, 18f),
        VelocityMax = new Vector2(6f, 34f),
        Sway = new Vector2(10f, 0f),
        SwayFreqMin = 0.2f,
        SwayFreqMax = 0.3f,
        Spin = 6f,
        AlphaMin = 0.9f,
        AlphaMax = 1f
    };

    /// <summary>A few balloons drifting slowly up with a gentle sway. Mileth festivals turn it on for the fair.</summary>
    public static ParticleStyle Balloons { get; } = new()
    {
        Count = 9,
        Shape = ParticleShape.Balloon,
        Colors =
        [
            new Color(255, 77, 109), new Color(255, 210, 63), new Color(62, 193, 255), new Color(107, 227, 122),
            new Color(199, 125, 255), new Color(255, 159, 28)
        ],
        SizeMin = 12f,
        SizeMax = 16f,
        VelocityMin = new Vector2(0f, -24f),
        VelocityMax = new Vector2(0f, -14f),
        Sway = new Vector2(6f, 0f),
        SwayFreqMin = 0.1f,
        SwayFreqMax = 0.15f,
        AlphaMin = 1f,
        AlphaMax = 1f
    };
```

For Balloon, `Size` is the balloon's width in pixels. `SizeMax` is 16, which makes a balloon 40 px tall in total, so half of it is 20 px. That fits inside the 24 px wrap margin.

- [ ] **Step 4: Teach the renderer the two shapes**

In `ParticleRenderer.cs`, make these changes in order:

a) In the constants, change `private const float WRAP_MARGIN = 24f;` to `internal const float WRAP_MARGIN = 24f;`, keeping its comment. Add these after `WING_TEX_H`:

```csharp
    internal const int BALLOON_TEX_W = 16;         // balloon body + string texture; the body fills the top half
    internal const int BALLOON_TEX_H = 40;
    private static readonly Color BalloonDetailColor = new(240, 240, 240);
```

b) Add these texture fields after `private Texture2D? WingTexture;`:

```csharp
    private Texture2D? BalloonTexture;
    private Texture2D? BalloonDetailTexture;
```

c) In `Draw`, right after the main `spriteBatch.Draw(texture, ...)` call inside the loop, add:

```csharp
            if (Style.Shape == ParticleShape.Balloon)
            {
                BalloonDetailTexture ??= BuildBalloonDetailTexture(device);

                spriteBatch.Draw(
                    BalloonDetailTexture,
                    position,
                    null,
                    WithAlpha(BalloonDetailColor, alpha),
                    rotation,
                    textureCenter,
                    scale,
                    SpriteEffects.None,
                    0f);
            }
```

d) In `ShapeTransform`, add this switch arm after the `Petal` arm. Confetti uses the 1×1 pixel texture, so its scale is in pixels:

```csharp
            ParticleShape.Confetti => (p.Rotation, new Vector2(p.Size * 0.55f, p.Size * tumble)),
```

Balloon uses the default arm, `(0f, new Vector2(p.Size / texture.Width))`, so it needs no new arm.

e) In `GetShapeTexture`, change the first arm to `ParticleShape.Square or ParticleShape.Streak or ParticleShape.Confetti => PixelTexture ??= BuildPixelTexture(device),`. Add this arm before the `_` arm:

```csharp
            ParticleShape.Balloon                        => BalloonTexture ??= BuildBalloonTexture(device),
```

f) In `Dispose`, after the `WingTexture` lines, add:

```csharp
        BalloonTexture?.Dispose();
        BalloonTexture = null;
        BalloonDetailTexture?.Dispose();
        BalloonDetailTexture = null;
```

g) Add these builders after `BuildWingTexture`. In the u, v coordinates, v runs from -1 at the top to 1 at the bottom. One v unit is 20 px and one u unit is 8 px.

```csharp
    //body: an oval in the top half, 1.2 times taller than wide, with a soft one-pixel edge. tinted by the particle colour
    private static Texture2D BuildBalloonTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            BALLOON_TEX_W,
            BALLOON_TEX_H,
            (u, v) =>
            {
                var bv = (v + 0.52f) / 0.48f;
                var r = MathF.Sqrt((u * u) + (bv * bv));

                return Math.Clamp((1f - r) * 6f, 0f, 1f);
            });

    //drawn pale over the body: a small shine up-left on the body and a thin wavy string from the knot to the bottom
    private static Texture2D BuildBalloonDetailTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            BALLOON_TEX_W,
            BALLOON_TEX_H,
            (u, v) =>
            {
                var hu = (u + 0.35f) / 0.25f;
                var hv = (v + 0.72f) / 0.1f;
                var shine = Math.Clamp(1f - MathF.Sqrt((hu * hu) + (hv * hv)), 0f, 1f) * 0.8f;

                if (v < -0.04f)
                    return shine;

                var stringX = 0.18f * MathF.Sin((v + 0.04f) * 7f);

                return Math.Clamp(1f - (MathF.Abs(u - stringX) / 0.13f), 0f, 1f) * 0.7f;
            });
```

- [ ] **Step 5: Wire the flags**

In `AmbientEffects.cs`'s `Overlays`, add these before `(MapFlags.Ghosts, new ParticleRenderer(ParticleStyle.Ghosts)),`:

```csharp
        (MapFlags.Confetti, new ParticleRenderer(ParticleStyle.Confetti)),
        (MapFlags.Balloons, new ParticleRenderer(ParticleStyle.Balloons)),
```

- [ ] **Step 6: Update CLAUDE.md**

In the `AmbientEffects` bullet of `CLAUDE.md`, change "Bats, Ghosts and HarvestMoon (`MapFlags` bits 8-30;" to "Bats, Ghosts, HarvestMoon, Confetti and Balloons (`MapFlags` bits 8-32;".

- [ ] **Step 7: Build and test**

Run the Verify commands. Expected: the build has no new warnings, and the 3 `AmbientEffectsTests` pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/ParticleStyle.cs", "Chaos.Client.Rendering/ParticleRenderer.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "CLAUDE.md", "Tests/Chaos.Client.Tests/AmbientEffectsTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/AmbientEffectsTests/*\"", "acceptanceCriteria": ["EveryEffectFlag_HasAnOverlay passes with Confetti and Balloons", "Balloons_StayInsideTheWrapMargin passes", "Balloon body tinted by particle colour; shine and string drawn pale, untinted", "Client test project builds with no new warnings"], "modelTier": "standard"}
```

---

### Task 5: F4 hide boxes for confetti and balloons

**Goal:** F4's "Triggering or Unsettling Map Effects" section gets two new boxes, "Disable Confetti" and "Disable Balloons". They are saved per player and hide each effect at once.

**Files:**
- Modify: `Chaos.Client/Systems/ClientSettings.cs` (the properties around line 139, the load switch around line 342, the save lines around line 601)
- Modify: `Chaos.Client/ViewModel/SettingDefinition.cs` (the `SettingKey` enum around line 103, the definitions around line 275)
- Modify: `Chaos.Client/Systems/MapEffectFilter.cs` (`Keys` and `HiddenByPlayer`)
- Test: `Tests/Chaos.Client.Tests/MapEffectFilterTests.cs` (the `Expected` map)

**Acceptance Criteria:**
- [ ] `ClientSettings.HideConfetti` and `HideBalloons` load from `Settings.cfg` and save to it as `HideConfetti : 0|1` and `HideBalloons : 0|1`.
- [ ] Both boxes show in the MapEffects section, after "Disable Blizzard" and before "Reduce Arena Fall Motion", and default to unticked.
- [ ] Ticking each one strips its flag in `MapEffectFilter.Visible`.
- [ ] All `MapEffectFilterTests` pass.

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/MapEffectFilterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Extend the test's map**

In `MapEffectFilterTests.cs`, change the end of `Expected` to:

```csharp
        [SettingKey.HideBlizzard] = MapFlags.Blizzard,
        [SettingKey.HideConfetti] = MapFlags.Confetti,
        [SettingKey.HideBalloons] = MapFlags.Balloons
    };
```

- [ ] **Step 2: Run it and watch it fail**

Run the Verify build command. Expected: a build error, because `SettingKey` does not contain `HideConfetti`.

- [ ] **Step 3: Add the settings in the four places**

In `ClientSettings.cs`:
- After `public static bool HideBlizzard { get; set; }`, add `public static bool HideConfetti { get; set; }` and `public static bool HideBalloons { get; set; }` on their own lines.
- In the load switch, after the `case "HideBlizzard":` block, add:

```csharp
                    case "HideConfetti":
                        HideConfetti = value == "1";

                        break;

                    case "HideBalloons":
                        HideBalloons = value == "1";

                        break;
```

- In the save block, after the `HideBlizzard` line, add:

```csharp
            writer.WriteLine($"HideConfetti : {(HideConfetti ? 1 : 0)}");
            writer.WriteLine($"HideBalloons : {(HideBalloons ? 1 : 0)}");
```

In `SettingDefinition.cs`:
- In the `SettingKey` enum, after `HideBlizzard,`, add `HideConfetti,` and `HideBalloons,`.
- After the `SettingKey.HideBlizzard` definition, add:

```csharp
        new(
            SettingKey.HideConfetti,
            "Disable Confetti",
            SettingSection.MapEffects,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.HideConfetti,
            Set: v => ClientSettings.HideConfetti = v),
        new(
            SettingKey.HideBalloons,
            "Disable Balloons",
            SettingSection.MapEffects,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.HideBalloons,
            Set: v => ClientSettings.HideBalloons = v),
```

In `MapEffectFilter.cs`:
- In `Keys`, change `SettingKey.HideBlizzard` to `SettingKey.HideBlizzard,`, then add `SettingKey.HideConfetti,` and `SettingKey.HideBalloons` after it.
- In `HiddenByPlayer`, after the `HideBlizzard` check, add:

```csharp
            if (ClientSettings.HideConfetti)
                hidden |= MapFlags.Confetti;

            if (ClientSettings.HideBalloons)
                hidden |= MapFlags.Balloons;
```

- [ ] **Step 4: Build and test**

Run the Verify commands. Expected: all `MapEffectFilterTests` pass.

```json:metadata
{"files": ["Chaos.Client/Systems/ClientSettings.cs", "Chaos.Client/ViewModel/SettingDefinition.cs", "Chaos.Client/Systems/MapEffectFilter.cs", "Tests/Chaos.Client.Tests/MapEffectFilterTests.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/MapEffectFilterTests/*\"", "acceptanceCriteria": ["HideConfetti and HideBalloons load and save in Settings.cfg", "Both boxes in MapEffects after Disable Blizzard, before Reduce Arena Fall Motion, default unticked", "Ticking each strips its flag in MapEffectFilter.Visible", "MapEffectFilterTests pass"], "modelTier": "mechanical"}
```

---

### Task 6: Commit the full implementation

**Goal:** Each repo gets one commit: server, Unora, and client. The client commit points its `Chaos-Server` submodule at the new server commit.

**Files:**
- Commit: every file from Tasks 1-5, in its own repo.

**Acceptance Criteria:**
- [ ] The full server test run passes. The only failures allowed are ones that already fail on master before this change; list any of those by name.
- [ ] The full client test run passes.
- [ ] Each repo has exactly one new commit, and only this plan's files are in each.
- [ ] The client commit's `Chaos-Server` gitlink equals the new server commit.

**Verify:** `git -C Chaos-Server log --oneline -1`, then `git ls-tree HEAD Chaos-Server` (from the client repo) → the same short SHA.

**Steps:**

The checkouts are shared with other sessions. Stage files only by explicit path. Never use `add -A`, `stash`, `reset` or `clean`. Write each commit message file to the session scratchpad with a unique name. If a local `Chaos.exe` locks the server build, run the full test suites on FSOS, or in a worktree, instead of stopping that server.

- [ ] **Step 1: Run both full test suites**

From the server repo, run `dotnet run --project Tests/Chaos.Tests`. From the client repo, run `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj` and then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Expected: all pass.

- [ ] **Step 2: Commit the server**

From the server repo, stage the Task 1 and Task 2 files by path. Commit with this message:

```
Confetti and Balloons map effects; Mileth festivals add the fair effects

Two new MapFlags (bits 31, 32) for /mapFlag, /atmosphere and the theatre's
page 2. A town's fairEffects go on with its festival effect, and the
festival script now leaves alone each flag the map already had.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 3: Commit Unora**

From the Unora repo, stage `Data/Configuration/Towns/mileth.json` and commit with this message:

```
Mileth festivals turn on Confetti and Balloons

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 4: Commit the client with the submodule pointer**

From the client repo, stage the Task 4 and Task 5 files by path, plus `Chaos-Server`, which is the gitlink to the Step 2 commit. Commit with this message:

```
Confetti and Balloons overlays with F4 hide boxes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 5: Check the gitlink**

Run the Verify commands. Expected: the two SHAs match.

```json:metadata
{"files": [], "verifyCommand": "git ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["Full server test run passes (pre-existing failures named)", "Full client test run passes", "One new commit per repo with only this plan's files", "Client Chaos-Server gitlink equals the new server commit"], "modelTier": "standard"}
```

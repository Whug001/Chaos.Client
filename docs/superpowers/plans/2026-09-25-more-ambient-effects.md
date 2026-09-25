# More Ambient Effects (Part 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Widen the map-effect flags to 8 bytes, add 11 new ambient effects to the client (one with fluttering wings), and offer them on a second page of the Suomi theatre's stage-effects menu.

**Architecture:** The server gains 11 `MapFlags` bits (17–27) and sends every bit above the low byte in an 8-byte `SetMapEffects` packet. The client draws each flag with presets for its existing `MistRenderer` and `ParticleRenderer`. `ParticleRenderer` gains optional wings. The theatre menu splits into two pages, keyed by a page number on each `StageEffect`.

**Tech Stack:** C# 14 / .NET 10, MonoGame 3.8 (client), TUnit + FluentAssertions (tests), JSON dialog templates (Unora).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-more-ambient-effects-design.md`

## Global Constraints

- **Worktrees only.** All edits happen in these three worktrees, made in Task 1:
  - `C:\Users\Michael\Documents\GitHub\worktrees\ambient-server` (Chaos-Server)
  - `C:\Users\Michael\Documents\GitHub\worktrees\ambient-client` (Chaos.Client)
  - `C:\Users\Michael\Documents\GitHub\worktrees\ambient-unora` (Unora)

  Never edit, stage, stash, reset or switch branches in the shared checkouts (`GitHub\Chaos.Client`, `GitHub\Chaos.Client\Chaos-Server`, `GitHub\Unora`). Other Claude sessions work there.
- **No commits until Task 8.** This project commits once per repo at the end. Implementers leave all changes in the worktree.
- **Builds.** Before any build, check that no `Chaos.exe` or Chaos.Client game process is running (`Get-Process | Where-Object { $_.Path -like '*Chaos*' }`). If one is, stop and ask the user to close it. Never kill it. Never run two builds at once.
- **The client builds against the server worktree.** Every client build passes `-p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server`.
- **Serena paths.** Serena's tools take worktree paths relative to `C:\Users\Michael\Documents\GitHub`, for example `worktrees/ambient-server/Chaos/Utilities/MapFlagVisibility.cs`.
- **Exact values.** Flag bits, preset numbers, wing geometry and menu text are exactly as written in this plan and the spec. Don't retune them.
- **No map changes.** No `instance.json` file changes. Effects are reachable only through `/mapFlag` and the theatre.
- **Never commit** `Chaos/appsettings.json` or `Chaos.Client/Properties/launchSettings.json`, and never commit anything under the Unora worktree's `Data/Saved` or `Data/LocalStorage`.
- Nothing is merged or pushed. Integration happens later, with the user's go-ahead.

**User decisions (already made):**
- The work is split into three parts. This plan is part 1: wider flags plus 11 preset effects. Part 2 (sunbeams, aurora, shooting stars, bats and crows) and part 3 (heat shimmer and water wobble) get their own specs.
- The flags widen from 4 bytes to 8 in this part.
- Looks were approved from browser previews. Dust uses the stronger version, Cloud shadows the medium one (7A), and Wisps the small fairy wings (10A).
- The theatre gets a second page, not categories.
- No effects go on any map yet.
- The spec was approved as written.

---

## File map

| Repo (worktree) | File | Change |
|---|---|---|
| server | `Chaos.DarkAges/Definitions/Enums.cs` | `MapFlags : ulong`, 11 new flags |
| server | `Chaos.DarkAges/Definitions/CONSTANTS.cs` | `CLIENT_VERSION` up by one |
| server | `Chaos.Networking/Entities/Server/SetMapEffectsArgs.cs` | `ExtendedFlags` becomes `ulong` |
| server | `Chaos.Networking/Converters/Server/SetMapEffectsConverter.cs` | read and write 8 bytes |
| server | `Chaos/Networking/ChaosWorldClient.cs` | `SendMapInfo` masks as `ulong` |
| server | `Chaos/Utilities/MapFlagVisibility.cs` | `ATMOSPHERE` gets the new flags |
| server | `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs` | page numbers, new effects |
| server | `Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` | page-2 display and pick |
| server | `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs` | `ulong`, new flags, bit-40 round trip |
| server | `Tests/Chaos.Tests/MapFlagVisibilityTests.cs` | new flags, coverage guard |
| server | `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs` | page tests |
| client | `Chaos.Client.Rendering/ParticleStyle.cs` | wing settings, 9 particle presets |
| client | `Chaos.Client.Rendering/ParticleRenderer.cs` | wing drawing |
| client | `Chaos.Client.Rendering/MistStyle.cs` | 9 mist presets |
| client | `Chaos.Client.Rendering/AmbientEffects.cs` | 8-byte mask, overlays, `CoveredFlags` |
| client | `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs` | new guard tests |
| client | `CLAUDE.md` | AmbientEffects line |
| unora | `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json` | new page-2 dialog |
| unora | `docs/ambient-map-effects.md` | new effects, 8-byte flags, wings, theatre page |

---

### Task 1: Make the three worktrees and check the baseline

**Goal:** Three `feat/more-ambient-effects` worktrees exist on the agreed bases, and both solutions build there before any change.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\ambient-server`, `...\ambient-client`, `...\ambient-unora` (git worktrees)

**Acceptance Criteria:**
- [ ] `git -C <worktree> branch --show-current` prints `feat/more-ambient-effects` in all three
- [ ] Server worktree HEAD is `f90d470f3`, Unora worktree HEAD is `ad2031769`, and client worktree HEAD contains `2ba6bee`
- [ ] `dotnet build Chaos.slnx` succeeds in the server worktree
- [ ] The client test project builds against the server worktree

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/ambient-server log --oneline -1` → `f90d470f3 ...`

**Steps:**

- [ ] **Step 1: Check the branch name is free and the bases exist**

```bash
cd /c/Users/Michael/Documents/GitHub
for r in Chaos.Client Chaos.Client/Chaos-Server Unora; do git -C $r branch --list feat/more-ambient-effects; done
git -C Chaos.Client/Chaos-Server cat-file -t f90d470f3
git -C Unora cat-file -t ad2031769
git -C Chaos.Client merge-base --is-ancestor 2ba6bee main && echo "client main has the spec"
```

Expected: no branch names printed, `commit` twice, then `client main has the spec`. If the branch already exists anywhere, stop and report it.

- [ ] **Step 2: Make the worktrees**

`core.longpaths` is needed for the server (long Cthonic Demise paths) and Unora (a long crafting path). Pass it with `-c` only. Never write it to the repo config.

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add -b feat/more-ambient-effects C:/Users/Michael/Documents/GitHub/worktrees/ambient-server f90d470f3
git -C Chaos.Client worktree add -b feat/more-ambient-effects C:/Users/Michael/Documents/GitHub/worktrees/ambient-client main
git -C Unora -c core.longpaths=true worktree add -b feat/more-ambient-effects C:/Users/Michael/Documents/GitHub/worktrees/ambient-unora ad2031769
```

The client worktree's `Chaos-Server` submodule folder is empty. That is expected, because the client builds against the server worktree through `UnoraServerPath`.

- [ ] **Step 3: Check the branches and bases**

```bash
for w in ambient-server ambient-client ambient-unora; do git -C C:/Users/Michael/Documents/GitHub/worktrees/$w log --oneline -1; git -C C:/Users/Michael/Documents/GitHub/worktrees/$w branch --show-current; done
```

Expected: `f90d470f3 ...`, then client main's HEAD (at or after `2ba6bee`), then `ad2031769 ...`, each on `feat/more-ambient-effects`.

- [ ] **Step 4: Baseline builds**

First check no game server or client is running (see Global Constraints). Then:

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server && dotnet build Chaos.slnx
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server
```

Expected: both `Build succeeded`. Record any warnings, so later tasks can show they added none.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/ambient-server log --oneline -1", "acceptanceCriteria": ["All three worktrees on feat/more-ambient-effects", "Server at f90d470f3, Unora at ad2031769, client contains 2ba6bee", "Server solution builds", "Client test project builds against the server worktree"], "modelTier": "mechanical"}
```

---

### Task 2: Widen MapFlags to 8 bytes and add the 11 flags (server)

**Goal:** `MapFlags` is a `ulong` with Gloom through Drips at bits 17–27. The effect packet carries 8 bytes, `/atmosphere` hides the new flags, and the client version is one higher.

**Files (all in `worktrees/ambient-server`):**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (the `MapFlags` enum, near line 1473)
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs` (line 23, `CLIENT_VERSION`)
- Modify: `Chaos.Networking/Entities/Server/SetMapEffectsArgs.cs`
- Modify: `Chaos.Networking/Converters/Server/SetMapEffectsConverter.cs`
- Modify: `Chaos/Networking/ChaosWorldClient.cs` (`SendMapInfo`)
- Modify: `Chaos/Utilities/MapFlagVisibility.cs` (`ATMOSPHERE`)
- Test: `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`
- Test: `Tests/Chaos.Tests/MapFlagVisibilityTests.cs`

**Acceptance Criteria:**
- [ ] `MapFlags` is declared `: ulong`, and `Gloom` … `Drips` are `1UL << 17` … `1UL << 27` in the spec's order
- [ ] A `SetMapEffectsArgs` with bit 40 set survives a serialize/deserialize round trip
- [ ] `Atmosphere_CoversEveryEffectFlag` passes, so every single-bit flag is in `ATMOSPHERE` or `NOT_EFFECTS`
- [ ] `CLIENT_VERSION` is exactly one higher than before
- [ ] The full `Chaos.Tests` run shows no failures beyond the two known ones (`GiveAbility_AddsAbilityToAisling`, `OnItemDroppedOn_ShouldAddStackableItem_WhenCountIsPositive`)

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)/*"` → all pass

**Steps:**

- [ ] **Step 1: Update the packet converter tests**

In `Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs`, replace the `AmbientFlags` array:

```csharp
    private static readonly MapFlags[] AmbientFlags =
    [
        MapFlags.BloodMoon,
        MapFlags.Sandstorm,
        MapFlags.Miasma,
        MapFlags.Ash,
        MapFlags.Leaves,
        MapFlags.Petals,
        MapFlags.Fireflies,
        MapFlags.Underwater,
        MapFlags.Gloom,
        MapFlags.Radiance,
        MapFlags.Arcane,
        MapFlags.Frost,
        MapFlags.Blizzard,
        MapFlags.Dust,
        MapFlags.CloudShadows,
        MapFlags.SeaSpray,
        MapFlags.Heat,
        MapFlags.Wisps,
        MapFlags.Drips
    ];
```

In `SetMapEffects_round_trips_every_ambient_flag`, change `ExtendedFlags = (uint)all` to `ExtendedFlags = (ulong)all`.

Replace the loop body in `Ambient_flags_sit_above_the_map_info_byte`:

```csharp
        foreach (var flag in AmbientFlags)
            ((ulong)flag & 0xFFUL).Should()
                                   .Be(0UL, $"{flag} must not overlap the byte map info sends");
```

Replace the loop body in `No_new_flag_reads_as_weather_or_darkness`:

```csharp
        foreach (var flag in AmbientFlags.Append(MapFlags.Fog).Append(MapFlags.Lightning))
            ((ulong)flag & (ulong)MapFlags.Darkness).Should()
                                                    .Be(0UL, $"{flag} must not overlap Snow, Rain or Darkness");
```

Add this test at the end of the class:

```csharp
    [Test]
    public async Task SetMapEffects_round_trips_bits_above_31()
    {
        //the packet carried 4 bytes before the flags grew to 8, so a bit this high would have been cut off
        var original = new SetMapEffectsArgs { ExtendedFlags = (1UL << 40) | (ulong)MapFlags.Drips };

        var result = RoundTrip(new SetMapEffectsConverter(), original);

        result.ExtendedFlags
              .Should()
              .Be(original.ExtendedFlags);

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Update the visibility tests**

In `Tests/Chaos.Tests/MapFlagVisibilityTests.cs`, replace `EVERY_EFFECT_BUT_SNOW` and add `NOT_EFFECTS` right after it:

```csharp
    private const MapFlags EVERY_EFFECT_BUT_SNOW = MapFlags.Rain
                                                   | MapFlags.Fog
                                                   | MapFlags.Lightning
                                                   | MapFlags.BloodMoon
                                                   | MapFlags.Sandstorm
                                                   | MapFlags.Miasma
                                                   | MapFlags.Ash
                                                   | MapFlags.Leaves
                                                   | MapFlags.Petals
                                                   | MapFlags.Fireflies
                                                   | MapFlags.Underwater
                                                   | MapFlags.Gloom
                                                   | MapFlags.Radiance
                                                   | MapFlags.Arcane
                                                   | MapFlags.Frost
                                                   | MapFlags.Blizzard
                                                   | MapFlags.Dust
                                                   | MapFlags.CloudShadows
                                                   | MapFlags.SeaSpray
                                                   | MapFlags.Heat
                                                   | MapFlags.Wisps
                                                   | MapFlags.Drips;

    /// <summary>
    ///     The single-bit map flags that are not weather or ambient effects, so /atmosphere leaves them alone
    /// </summary>
    private const MapFlags NOT_EFFECTS = MapFlags.NoTabMap | MapFlags.SnowTileset | MapFlags.NoTownMap;
```

Add this test as the last test in the class:

```csharp
    //a new effect flag left out of ATMOSPHERE would stay on screen after /atmosphere. every single-bit flag must be
    //either an effect ATMOSPHERE hides or listed in NOT_EFFECTS
    [Test]
    public void Atmosphere_CoversEveryEffectFlag()
        => Enum.GetValues<MapFlags>()
               .Where(flag => ulong.IsPow2((ulong)flag))
               .Where(flag => !MapFlagVisibility.ATMOSPHERE.HasFlag(flag) && !NOT_EFFECTS.HasFlag(flag))
               .Should()
               .BeEmpty("every map flag is either an effect /atmosphere hides or listed in NOT_EFFECTS");
```

- [ ] **Step 3: Run the tests and confirm they fail**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)/*"
```

Expected: the build fails. The errors say `MapFlags` has no `Gloom` (and the other new names), and that `ulong` can't convert to `uint` for `ExtendedFlags`.

- [ ] **Step 4: Widen the enum and add the flags**

In `Chaos.DarkAges/Definitions/Enums.cs`, replace the `MapFlags` remarks and declaration, from `/// <remarks>` above `[Flags]` down to the enum's closing brace:

```csharp
/// <remarks>
///     Only the low byte goes out in the map info packet, which every client understands. The bits above it, up to all
///     64, are custom-client ambient effects, sent separately in <c>ServerOpCode.SetMapEffects</c> so the standard
///     client never sees them.
/// </remarks>
[Flags]
public enum MapFlags : ulong
{
    None = 0,
    Snow = 1,
    Rain = 2,
    Darkness = Rain | Snow,
    Fog = 16,
    Lightning = 32,
    NoTabMap = 64,
    SnowTileset = 128,
    BloodMoon = 1 << 8,
    Sandstorm = 1 << 9,
    Miasma = 1 << 10,
    Ash = 1 << 11,
    Leaves = 1 << 12,
    Petals = 1 << 13,
    Fireflies = 1 << 14,
    Underwater = 1 << 15,

    /// <summary>
    ///     Suppresses the town map, the popup opened with T. Separate from <see cref="NoTabMap" />, which only
    ///     covers the Tab overlay -- a map can hide one and keep the other.
    /// </summary>
    NoTownMap = 1 << 16,
    Gloom = 1UL << 17,
    Radiance = 1UL << 18,
    Arcane = 1UL << 19,
    Frost = 1UL << 20,
    Blizzard = 1UL << 21,
    Dust = 1UL << 22,
    CloudShadows = 1UL << 23,
    SeaSpray = 1UL << 24,
    Heat = 1UL << 25,
    Wisps = 1UL << 26,
    Drips = 1UL << 27
}
```

- [ ] **Step 5: Widen the packet**

`Chaos.Networking/Entities/Server/SetMapEffectsArgs.cs`: change the property to:

```csharp
    /// <summary>
    ///     The map's flags with the low byte cleared. The client ORs this onto the map info flags.
    /// </summary>
    public ulong ExtendedFlags { get; set; }
```

`Chaos.Networking/Converters/Server/SetMapEffectsConverter.cs`: change the two method bodies to:

```csharp
    /// <inheritdoc />
    public override SetMapEffectsArgs Deserialize(ref SpanReader reader)
        => new()
        {
            ExtendedFlags = reader.ReadUInt64()
        };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, SetMapEffectsArgs args) => writer.WriteUInt64(args.ExtendedFlags);
```

`Chaos/Networking/ChaosWorldClient.cs`, in `SendMapInfo`, change the `ExtendedFlags` line to:

```csharp
                ExtendedFlags = (ulong)flags & ~0xFFUL
```

- [ ] **Step 6: Hide the new flags with /atmosphere**

`Chaos/Utilities/MapFlagVisibility.cs`: replace `ATMOSPHERE` with:

```csharp
    public const MapFlags ATMOSPHERE = MapFlags.Snow
                                       | MapFlags.Rain
                                       | MapFlags.Fog
                                       | MapFlags.Lightning
                                       | MapFlags.BloodMoon
                                       | MapFlags.Sandstorm
                                       | MapFlags.Miasma
                                       | MapFlags.Ash
                                       | MapFlags.Leaves
                                       | MapFlags.Petals
                                       | MapFlags.Fireflies
                                       | MapFlags.Underwater
                                       | MapFlags.Gloom
                                       | MapFlags.Radiance
                                       | MapFlags.Arcane
                                       | MapFlags.Frost
                                       | MapFlags.Blizzard
                                       | MapFlags.Dust
                                       | MapFlags.CloudShadows
                                       | MapFlags.SeaSpray
                                       | MapFlags.Heat
                                       | MapFlags.Wisps
                                       | MapFlags.Drips;
```

- [ ] **Step 7: Bump the client version**

In `Chaos.DarkAges/Definitions/CONSTANTS.cs`, read the current `CLIENT_VERSION` value (755 when this plan was written; another session may have raised it since), then add one. Example if it is still 755:

```csharp
    public const ushort CLIENT_VERSION = 756;
```

- [ ] **Step 8: Build and fix any other number conversions**

Check no game process is running, then:

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server && dotnet build Chaos.slnx
```

Expected: `Build succeeded`. A search when this plan was written found no other code turning `MapFlags` into `uint` or `int`. `MapInstanceMapperProfile` uses `(byte)obj.Flags`, which still works. If the compiler reports an error about a `MapFlags` conversion, change that `uint` to `ulong`, and list the file in your report so Task 8 stages it.

- [ ] **Step 9: Run the tests**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)/*"
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj
```

Expected: the filtered run passes completely. The full run fails only `GiveAbility_AddsAbilityToAisling` and `OnItemDroppedOn_ShouldAddStackableItem_WhenCountIsPositive`, the two known failures on master. Report the full-run totals.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/SetMapEffectsArgs.cs", "Chaos.Networking/Converters/Server/SetMapEffectsConverter.cs", "Chaos/Networking/ChaosWorldClient.cs", "Chaos/Utilities/MapFlagVisibility.cs", "Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs", "Tests/Chaos.Tests/MapFlagVisibilityTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)/*\"", "acceptanceCriteria": ["MapFlags is ulong with Gloom..Drips at bits 17-27", "Bit 40 survives a SetMapEffects round trip", "Atmosphere_CoversEveryEffectFlag passes", "CLIENT_VERSION is one higher", "Full Chaos.Tests run shows only the two known failures"], "modelTier": "standard"}
```

---

### Task 3: Second page for the theatre stage-effects menu

**Goal:** The Suomi theatre shows today's 12 effects plus "More Effects" on page 1. Page 2 shows the 11 new effects plus "Clear All Effects" and "Back".

**Files:**
- Modify: `worktrees/ambient-server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs`
- Modify: `worktrees/ambient-server/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` (the `suomitheatre_stageeffects` cases in `OnDisplaying`, near line 760, and `OnNext`, near line 873)
- Test: `worktrees/ambient-server/Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`
- Create: `worktrees/ambient-unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json`

**Acceptance Criteria:**
- [ ] `Available(flags, 1)` returns today's 12 effects in today's order, and `Available(flags, 2)` returns the 11 new ones in flag order
- [ ] No page holds more than `MAX_EFFECTS_PER_PAGE` (16) effects
- [ ] `ClearAll` stops page-2 effects and leaves the lights and non-effect flags alone
- [ ] "More Effects" and "Back" don't parse as effects
- [ ] The page-2 dialog JSON exists with template key `suomitheatre_stageeffects2` and script key `suomitheatre`

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Update the theatre tests**

In `Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs`, replace `ALL_EFFECTS`:

```csharp
    private const MapFlags ALL_EFFECTS = MapFlags.Fog
                                         | MapFlags.Lightning
                                         | MapFlags.Snow
                                         | MapFlags.Rain
                                         | MapFlags.BloodMoon
                                         | MapFlags.Sandstorm
                                         | MapFlags.Miasma
                                         | MapFlags.Ash
                                         | MapFlags.Leaves
                                         | MapFlags.Petals
                                         | MapFlags.Fireflies
                                         | MapFlags.Underwater
                                         | MapFlags.Gloom
                                         | MapFlags.Radiance
                                         | MapFlags.Arcane
                                         | MapFlags.Frost
                                         | MapFlags.Blizzard
                                         | MapFlags.Dust
                                         | MapFlags.CloudShadows
                                         | MapFlags.SeaSpray
                                         | MapFlags.Heat
                                         | MapFlags.Wisps
                                         | MapFlags.Drips;
```

Replace the two tests `Available_WhileLightsOn_OffersEveryEffect` and `Available_WhileLightsOff_HidesSnowAndRain` with:

```csharp
    [Test]
    public void Available_PageOne_IsTheFirstTwelveEffectsInOrder()
        => TheatreStageEffects.Available(MapFlags.Fog, 1)
                              .Select(effect => effect.Flag)
                              .Should()
                              .Equal(
                                  MapFlags.Fog,
                                  MapFlags.Lightning,
                                  MapFlags.Snow,
                                  MapFlags.Rain,
                                  MapFlags.BloodMoon,
                                  MapFlags.Sandstorm,
                                  MapFlags.Miasma,
                                  MapFlags.Ash,
                                  MapFlags.Leaves,
                                  MapFlags.Petals,
                                  MapFlags.Fireflies,
                                  MapFlags.Underwater);

    [Test]
    public void Available_PageTwo_IsTheNewEffectsInOrder()
        => TheatreStageEffects.Available(MapFlags.Fog, 2)
                              .Select(effect => effect.Flag)
                              .Should()
                              .Equal(
                                  MapFlags.Gloom,
                                  MapFlags.Radiance,
                                  MapFlags.Arcane,
                                  MapFlags.Frost,
                                  MapFlags.Blizzard,
                                  MapFlags.Dust,
                                  MapFlags.CloudShadows,
                                  MapFlags.SeaSpray,
                                  MapFlags.Heat,
                                  MapFlags.Wisps,
                                  MapFlags.Drips);

    [Test]
    public void Available_WhileLightsOff_HidesSnowAndRain()
        => TheatreStageEffects.Available(MapFlags.Darkness, 1)
                              .Select(effect => effect.Flag)
                              .Should()
                              .NotContain([MapFlags.Snow, MapFlags.Rain])
                              .And
                              .HaveCount(TheatreStageEffects.Available(MapFlags.None, 1).Count() - 2);

    [Test]
    public void Available_WhileLightsOff_KeepsAllOfPageTwo()
        => TheatreStageEffects.Available(MapFlags.Darkness, 2)
                              .Should()
                              .HaveCount(11);

    //the option panel fits 18 rows before it runs off the top of the screen; each page also shows Clear All and a
    //page button
    [Test]
    public void EveryPage_FitsOnScreen()
        => TheatreStageEffects.All
                              .GroupBy(effect => effect.Page)
                              .Should()
                              .OnlyContain(page => page.Count() <= TheatreStageEffects.MAX_EFFECTS_PER_PAGE);

    [Test]
    public void ClearAll_StopsPageTwoEffects()
        => TheatreStageEffects.ClearAll(MapFlags.Fog | MapFlags.Gloom | MapFlags.Wisps)
                              .Should()
                              .Be(MapFlags.None);
```

In `TryParseOption_UnknownText_ReturnsFalse`, add two argument lines after `[Arguments("Clear All Effects")]`:

```csharp
    [Arguments("More Effects")]
    [Arguments("Back")]
```

- [ ] **Step 2: Run the tests and confirm they fail**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"
```

Expected: the build fails. `Available` has no overload that takes a page, and `MAX_EFFECTS_PER_PAGE` and `StageEffect.Page` don't exist.

- [ ] **Step 3: Add pages to TheatreStageEffects**

In `TheatreStageEffects.cs`, replace the constants block (from `public const string CLEAR_ALL_TEXT` down to the `WEATHER` constant) with:

```csharp
    public const string BACK_TEXT = "Back";
    public const string CLEAR_ALL_TEXT = "Clear All Effects";
    public const string MORE_TEXT = "More Effects";

    /// <summary>
    ///     The most effects one menu page may hold. The option panel fits 18 rows before it runs off the top of the
    ///     screen, and every page also shows Clear All and a page button
    /// </summary>
    public const int MAX_EFFECTS_PER_PAGE = 16;

    private const string START_PREFIX = "Start ";
    private const string STOP_PREFIX = "Stop ";

    //snow and rain share their bits with darkness: both together are MapFlags.Darkness
    private const MapFlags WEATHER = MapFlags.Snow | MapFlags.Rain;
```

Replace the `All` property:

```csharp
    public static IReadOnlyList<StageEffect> All { get; } =
    [
        new("Fog", MapFlags.Fog, 1),
        new("Lightning", MapFlags.Lightning, 1),
        new("Snow", MapFlags.Snow, 1),
        new("Rain", MapFlags.Rain, 1),
        new("Blood Moon", MapFlags.BloodMoon, 1),
        new("Sandstorm", MapFlags.Sandstorm, 1),
        new("Miasma", MapFlags.Miasma, 1),
        new("Ash", MapFlags.Ash, 1),
        new("Falling Leaves", MapFlags.Leaves, 1),
        new("Petals", MapFlags.Petals, 1),
        new("Fireflies", MapFlags.Fireflies, 1),
        new("Underwater", MapFlags.Underwater, 1),
        new("Gloom", MapFlags.Gloom, 2),
        new("Radiance", MapFlags.Radiance, 2),
        new("Arcane", MapFlags.Arcane, 2),
        new("Frost", MapFlags.Frost, 2),
        new("Blizzard", MapFlags.Blizzard, 2),
        new("Dust", MapFlags.Dust, 2),
        new("Cloud Shadows", MapFlags.CloudShadows, 2),
        new("Sea Spray", MapFlags.SeaSpray, 2),
        new("Heat", MapFlags.Heat, 2),
        new("Wisps", MapFlags.Wisps, 2),
        new("Drips", MapFlags.Drips, 2)
    ];
```

Replace `Available`:

```csharp
    /// <summary>
    ///     The effects to offer on one menu page. Snow and rain are left out while the lights are off
    /// </summary>
    public static IEnumerable<StageEffect> Available(MapFlags current, int page)
    {
        var onPage = All.Where(effect => effect.Page == page);

        return IsDark(current) ? onPage.Where(effect => (effect.Flag & WEATHER) == MapFlags.None) : onPage;
    }
```

Replace the record at the bottom of the class:

```csharp
    /// <summary>
    ///     One effect in the stage-effects menu, and the menu page (1 or 2) it is listed on
    /// </summary>
    public sealed record StageEffect(string Name, MapFlags Flag, int Page);
```

- [ ] **Step 4: Show page 2 in the theatre script**

In `SuomiTheatreScript.cs`, in `OnDisplaying`, replace the whole `case "suomitheatre_stageeffects":` block (from the `case` line through its closing `}`) with:

```csharp
            case "suomitheatre_stageeffects":
            case "suomitheatre_stageeffects2":
            {
                if (!source.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre"))
                {
                    Subject.Reply(source, "You cannot do this here.");

                    return;
                }

                var flags = source.MapInstance.Flags;
                var thisPage = Subject.Template.TemplateKey.ToLower();
                var page = thisPage == "suomitheatre_stageeffects2" ? 2 : 1;

                //every effect option reopens this page, so a pick flips its label in place
                foreach (var effect in TheatreStageEffects.Available(flags, page))
                    Subject.AddOption(TheatreStageEffects.OptionText(effect, flags), thisPage);

                Subject.AddOption(TheatreStageEffects.CLEAR_ALL_TEXT, thisPage);

                if (page == 1)
                    Subject.AddOption(TheatreStageEffects.MORE_TEXT, "suomitheatre_stageeffects2");
                else
                    Subject.AddOption(TheatreStageEffects.BACK_TEXT, "suomitheatre_stageeffects");

                break;
            }
```

In `OnNext`, replace the `case "suomitheatre_stageeffects":` block with:

```csharp
            case "suomitheatre_stageeffects":
            case "suomitheatre_stageeffects2":
            {
                //"More Effects" and "Back" don't parse as effects, so HandleStageEffects ignores them and the dialog
                //moves on to the other page
                HandleStageEffects(source, optionIndex);

                break;
            }
```

- [ ] **Step 5: Add the page-2 dialog template (Unora)**

Create `worktrees/ambient-unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "suomitheatre"
  ],
  "scriptVars": {},
  "templateKey": "suomitheatre_stageeffects2",
  "text": "More atmosphere! Choose an effect to start it on the stage, or choose it again to stop it.",
  "type": "DialogMenu"
}
```

- [ ] **Step 6: Build and run the tests**

Check no game process is running, then:

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet build Chaos.slnx
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/TheatreStageEffectsTests/*"
```

Expected: `Build succeeded` and every `TheatreStageEffectsTests` test passes.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs", "Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs", "Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs", "Unora: Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/TheatreStageEffectsTests/*\"", "acceptanceCriteria": ["Page 1 is today's 12 in order, page 2 the 11 new in order", "No page over 16 effects", "ClearAll stops page-2 effects, keeps lights and non-effect flags", "More Effects and Back don't parse as effects", "Page-2 dialog JSON exists with the right keys"], "modelTier": "standard"}
```

---

### Task 4: Optional wings on particles (client)

**Goal:** `ParticleStyle` has six wing settings, and `ParticleRenderer` draws flapping wings when `WingScale > 0`. Existing presets are unchanged.

**Files (all in `worktrees/ambient-client`):**
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (after `HaloAlpha`)
- Modify: `Chaos.Client.Rendering/ParticleRenderer.cs`

**Acceptance Criteria:**
- [ ] `ParticleStyle` has `WingScale` (default 0), `WingWidth` (1), `WingAlpha` (0.5), `WingPairs` (1), `WingFlapMin` and `WingFlapMax` (2)
- [ ] `ParticleRenderer` makes a 32x16 wing texture on first use and releases it in `Dispose`
- [ ] Wings draw between the halo and the body, only when `WingScale > 0`
- [ ] No existing preset sets any wing field, so every existing effect draws as before
- [ ] The client test project builds and all client tests pass

**Verify:** `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server` → `Build succeeded`

Wings are drawing code with no game logic, and the texture needs a graphics device. So this task has no unit test. Task 7 checks the wings on screen against the approved preview.

**Steps:**

- [ ] **Step 1: Add the wing settings to ParticleStyle**

In `ParticleStyle.cs`, directly after `public float HaloAlpha { get; init; }`, insert:

```csharp

    /// <summary>Wing length as a multiple of the particle size. Zero means no wings.</summary>
    public float WingScale { get; init; }

    /// <summary>Wing width as a multiple of the particle size.</summary>
    public float WingWidth { get; init; } = 1f;

    /// <summary>Wing opacity as a fraction of the particle's own. Wings are the particle's color, paled toward white.</summary>
    public float WingAlpha { get; init; } = 0.5f;

    /// <summary>1 draws one pair of wings; 2 adds a smaller lower pair, like fairy wings.</summary>
    public int WingPairs { get; init; } = 1;

    /// <summary>Wing beats per second.</summary>
    public float WingFlapMin { get; init; } = 2f;

    public float WingFlapMax { get; init; } = 2f;
```

- [ ] **Step 2: Add constants, the texture field and the particle fields to ParticleRenderer**

In `ParticleRenderer.cs`, after `private const int LEAF_TEX_H = 8;`, insert:

```csharp
    private const int WING_TEX_W = 32;
    private const int WING_TEX_H = 16;
    private const float UPPER_WING_ANGLE = -0.45f; // radians from horizontal for the right wing; negative tilts up
    private const float LOWER_WING_ANGLE = 0.55f;
    private const float LOWER_WING_LENGTH = 0.7f;  // lower wings are this fraction of the upper ones
    private const float LOWER_WING_WIDTH = 0.8f;
    private const float WING_PALE = 0.55f;         // how far the wing color moves from the particle color to white
```

After `private Texture2D? BubbleTexture;`, insert:

```csharp
    private Texture2D? WingTexture;
```

In the `Particle` struct, after `public float TwinklePhase;`, insert:

```csharp
        public float FlapFreq;
        public float FlapPhase;
```

In `Spawn`, inside the `new Particle { ... }` initializer, after `TwinklePhase = Roll(0f, MathF.Tau)`, add a comma and:

```csharp
                FlapFreq = Roll(Style.WingFlapMin, Style.WingFlapMax),
                FlapPhase = Roll(0f, MathF.Tau)
```

- [ ] **Step 3: Draw the wings**

In `Draw`, between the end of the `if (Style.HaloScale > 0f) { ... }` block and `var (rotation, scale) = ShapeTransform(in p, texture);`, insert:

```csharp

            if (Style.WingScale > 0f)
            {
                WingTexture ??= BuildWingTexture(device);
                DrawWings(spriteBatch, in p, position, alpha);
            }
```

After the `ShapeTransform` method, insert:

```csharp
    //wings sit on the body and sweep between nearly edge-on (0.2) and fully open (1). the texture's narrow end is the
    //draw origin, so each wing grows outward from the particle
    private void DrawWings(SpriteBatch spriteBatch, in Particle p, Vector2 position, float alpha)
    {
        var flap = 0.2f + (0.8f * MathF.Abs(MathF.Sin((MathF.Tau * p.FlapFreq * Clock) + p.FlapPhase)));
        var length = p.Size * Style.WingScale * flap;
        var width = p.Size * Style.WingWidth;
        var color = WithAlpha(Color.Lerp(p.Color, Color.White, WING_PALE), alpha * Style.WingAlpha);

        DrawWingPair(spriteBatch, position, color, UPPER_WING_ANGLE, length, width);

        if (Style.WingPairs > 1)
            DrawWingPair(spriteBatch, position, color, LOWER_WING_ANGLE, length * LOWER_WING_LENGTH, width * LOWER_WING_WIDTH);
    }

    //right wing at the given angle, left wing mirrored across the vertical. the wing is symmetric along its length, so
    //turning it to PI - angle mirrors it exactly
    private void DrawWingPair(SpriteBatch spriteBatch, Vector2 position, Color color, float angle, float length, float width)
    {
        var origin = new Vector2(0f, WING_TEX_H / 2f);
        var scale = new Vector2(length / WING_TEX_W, width / WING_TEX_H);

        spriteBatch.Draw(WingTexture!, position, null, color, angle, origin, scale, SpriteEffects.None, 0f);
        spriteBatch.Draw(WingTexture!, position, null, color, MathF.PI - angle, origin, scale, SpriteEffects.None, 0f);
    }
```

- [ ] **Step 4: Build the wing texture**

After the `BuildLeafTexture` method, insert:

```csharp

    //soft-edged teardrop: narrow where it meets the body (left edge), round at the tip, a little brighter toward the tip
    private static Texture2D BuildWingTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            WING_TEX_W,
            WING_TEX_H,
            (u, v) =>
            {
                var along = (u + 1f) / 2f; // 0 at the body end, 1 at the tip
                var half = MathF.Sin(MathF.PI * MathF.Pow(along, 0.7f));

                if (half <= 0f)
                    return 0f;

                var edge = 1f - (MathF.Abs(v) / half);

                return Math.Clamp(edge * 2.5f, 0f, 1f) * (0.55f + (0.45f * along));
            });
```

In `Dispose`, after `BubbleTexture = null;`, insert:

```csharp
        WingTexture?.Dispose();
        WingTexture = null;
```

In the class summary at the top of the file, add one line directly before the closing `/// </summary>`:

```csharp
///     A style can also give each particle a pair (or two) of flapping wings.
```

- [ ] **Step 5: Build and run the client tests**

Check no game process is running, then:

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client
dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --no-ansi
```

Expected: `Build succeeded` with no new warnings compared with Task 1, and every client test passes.

```json:metadata
{"files": ["Chaos.Client.Rendering/ParticleStyle.cs", "Chaos.Client.Rendering/ParticleRenderer.cs"], "verifyCommand": "dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server", "acceptanceCriteria": ["Six wing settings with the stated defaults", "32x16 wing texture built on first use, released in Dispose", "Wings drawn between halo and body only when WingScale > 0", "No existing preset sets a wing field", "Client tests build and pass"], "modelTier": "mechanical"}
```

---

### Task 5: The 11 effects in the client: presets, draw order and guard tests

**Goal:** Every new flag switches on its approved look. `AmbientEffects` masks all 8 bytes, and two guard tests catch an uncovered flag or a narrow mask.

**Files (all in `worktrees/ambient-client`):**
- Create: `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`
- Modify: `Chaos.Client.Rendering/AmbientEffects.cs`
- Modify: `Chaos.Client.Rendering/MistStyle.cs` (after the `Underwater` preset)
- Modify: `Chaos.Client.Rendering/ParticleStyle.cs` (after the `SandGrains` preset)

**Acceptance Criteria:**
- [ ] `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay` passes, and fails if any new overlay line is removed
- [ ] `AmbientEffectsTests.ExtendedFlags_CoverEveryBitAboveTheLowByte` passes (`EXTENDED_FLAGS` is `0xFFFF_FFFF_FFFF_FF00`)
- [ ] The `Overlays` list matches the spec's draw order exactly
- [ ] Preset values match the spec exactly
- [ ] All client tests pass

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --no-ansi --treenode-filter "/*/*/AmbientEffectsTests/*"` → 2 passed

**Steps:**

- [ ] **Step 1: Write the guard tests**

Create `Tests/Chaos.Client.Tests/AmbientEffectsTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class AmbientEffectsTests
{
    //single-bit flags that AmbientEffects doesn't draw: map options, not effects. snow, rain and darkness sit below
    //bit 4 and have their own renderers
    private const MapFlags NOT_AMBIENT = MapFlags.NoTabMap | MapFlags.SnowTileset | MapFlags.NoTownMap;

    [Test]
    public void EveryEffectFlag_HasAnOverlay()
    {
        using var effects = new AmbientEffects();

        Enum.GetValues<MapFlags>()
            .Where(flag => ulong.IsPow2((ulong)flag) && ((ulong)flag >= (ulong)MapFlags.Fog))
            .Where(flag => !NOT_AMBIENT.HasFlag(flag))
            .Where(flag => !effects.CoveredFlags.Contains(flag))
            .Should()
            .BeEmpty("every ambient map flag needs at least one overlay in AmbientEffects");
    }

    [Test]
    public void ExtendedFlags_CoverEveryBitAboveTheLowByte()
        => ((ulong)AmbientEffects.EXTENDED_FLAGS).Should()
                                                  .Be(0xFFFF_FFFF_FFFF_FF00UL, "SetMapEffects carries every bit above the low byte");
}
```

- [ ] **Step 2: Add `CoveredFlags` so the tests compile**

In `AmbientEffects.cs`, directly after the `Overlays` field, insert:

```csharp

    /// <summary>Every map flag that switches on at least one overlay.</summary>
    public IReadOnlyCollection<MapFlags> CoveredFlags => Overlays.Select(entry => entry.Flag).ToHashSet();
```

- [ ] **Step 3: Run the tests and confirm they fail**

Check no game process is running, then:

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client
dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --no-ansi --treenode-filter "/*/*/AmbientEffectsTests/*"
```

Expected: both tests fail. `EveryEffectFlag_HasAnOverlay` lists the 11 new flags, and `ExtendedFlags_CoverEveryBitAboveTheLowByte` reports `0xFFFFFF00`.

- [ ] **Step 4: Widen the mask**

In `AmbientEffects.cs`, replace the `EXTENDED_FLAGS` line with:

```csharp
    public const MapFlags EXTENDED_FLAGS = (MapFlags)0xFFFF_FFFF_FFFF_FF00UL;
```

- [ ] **Step 5: Add the mist presets**

In `MistStyle.cs`, after the `Underwater` preset (before the class's closing brace), insert:

```csharp

    /// <summary>Dark violet dim with slow shadow wisps and heavy dark edges.</summary>
    public static MistStyle Gloom { get; } = new()
    {
        WashColor = new Color(28, 16, 44),
        WashAlpha = 0.32f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.15f,
        PulsePeriod = 7f,
        LayerTint = new Color(50, 30, 70),
        Layers =
        [
            new MistLayer(4.0f, 0.35f, new Vector2(-5f, 1f)),
            new MistLayer(2.6f, 0.40f, new Vector2(8f, -2f))
        ],
        Seed = 6661,
        NoiseKnee = 0.45f,
        NoiseRange = 0.4f,
        VignetteColor = new Color(8, 0, 18),
        VignetteAlpha = 0.6f,
        VignetteInner = 0.5f
    };

    /// <summary>Warm gold glow breathing slowly. Paired with rising golden motes.</summary>
    public static MistStyle Radiance { get; } = new()
    {
        WashColor = new Color(255, 210, 120),
        WashAlpha = 0.10f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.3f,
        PulsePeriod = 6f,
        LayerTint = new Color(255, 236, 180),
        Layers =
        [
            new MistLayer(3.5f, 0.16f, new Vector2(0f, -6f)),
            new MistLayer(2.2f, 0.18f, new Vector2(3f, -10f))
        ],
        Seed = 7777,
        NoiseKnee = 0.45f
    };

    /// <summary>Faint pulsing violet haze. Paired with violet and blue sparks.</summary>
    public static MistStyle Arcane { get; } = new()
    {
        WashColor = new Color(60, 30, 120),
        WashAlpha = 0.14f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.3f,
        PulsePeriod = 4f,
        LayerTint = new Color(150, 110, 230),
        Layers = [new MistLayer(3.0f, 0.14f, new Vector2(-4f, -3f))],
        Seed = 2718,
        NoiseKnee = 0.5f,
        NoiseRange = 0.4f,
        VignetteColor = new Color(20, 0, 40),
        VignetteAlpha = 0.3f,
        VignetteInner = 0.6f
    };

    /// <summary>Cold blue tint with frosted white edges. Paired with pale sparkles.</summary>
    public static MistStyle Frost { get; } = new()
    {
        WashColor = new Color(150, 190, 230),
        WashAlpha = 0.14f,
        LayerTint = new Color(230, 245, 255),
        Layers = [new MistLayer(3.0f, 0.12f, new Vector2(4f, 1f))],
        Seed = 1212,
        NoiseKnee = 0.5f,
        VignetteColor = new Color(220, 238, 255),
        VignetteAlpha = 0.45f,
        VignetteInner = 0.6f
    };

    /// <summary>White-out haze tearing sideways. Paired with snow flakes and streaks.</summary>
    public static MistStyle Blizzard { get; } = new()
    {
        FadeSeconds = 1.5f,
        WashColor = new Color(200, 210, 225),
        WashAlpha = 0.26f,
        LayerTint = new Color(240, 245, 255),
        Layers =
        [
            new MistLayer(3.0f, 0.28f, new Vector2(-120f, 20f)),
            new MistLayer(2.0f, 0.34f, new Vector2(-200f, 30f)),
            new MistLayer(1.3f, 0.24f, new Vector2(-300f, 40f))
        ],
        Seed = 5150,
        NoiseKnee = 0.32f,
        VignetteColor = new Color(230, 238, 250),
        VignetteAlpha = 0.35f,
        VignetteInner = 0.55f
    };

    /// <summary>Faint warm haze. Paired with specks hanging in the air.</summary>
    public static MistStyle Dust { get; } = new()
    {
        WashColor = new Color(170, 150, 110),
        WashAlpha = 0.10f,
        LayerTint = new Color(220, 200, 160),
        Layers = [new MistLayer(3.5f, 0.14f, new Vector2(3f, -1f))],
        Seed = 3030,
        NoiseKnee = 0.5f
    };

    /// <summary>Large soft shadows of passing clouds sliding over the ground. No wash, so no tint.</summary>
    public static MistStyle CloudShadows { get; } = new()
    {
        FadeSeconds = 2f,
        LayerTint = new Color(8, 14, 24),
        Layers = [new MistLayer(5.0f, 0.45f, new Vector2(14f, 6f))],
        Seed = 4040,
        NoiseKnee = 0.45f,
        NoiseRange = 0.35f
    };

    /// <summary>Pale salt mist blowing sideways. Paired with white flecks of spray.</summary>
    public static MistStyle SeaSpray { get; } = new()
    {
        WashColor = new Color(180, 200, 210),
        WashAlpha = 0.08f,
        LayerTint = new Color(235, 245, 250),
        Layers =
        [
            new MistLayer(3.0f, 0.18f, new Vector2(-40f, -2f)),
            new MistLayer(1.8f, 0.20f, new Vector2(-70f, -4f))
        ],
        Seed = 8080,
        NoiseKnee = 0.45f
    };

    /// <summary>Orange tint throbbing gently, with faint haze rising.</summary>
    public static MistStyle Heat { get; } = new()
    {
        WashColor = new Color(200, 110, 40),
        WashAlpha = 0.14f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.25f,
        PulsePeriod = 3f,
        LayerTint = new Color(255, 190, 120),
        Layers =
        [
            new MistLayer(2.5f, 0.12f, new Vector2(0f, -18f)),
            new MistLayer(1.6f, 0.10f, new Vector2(3f, -28f))
        ],
        Seed = 9191,
        NoiseKnee = 0.5f,
        VignetteColor = new Color(120, 40, 0),
        VignetteAlpha = 0.3f,
        VignetteInner = 0.6f
    };
```

- [ ] **Step 6: Add the particle presets**

In `ParticleStyle.cs`, after the `SandGrains` preset (before the class's closing brace), insert:

```csharp

    /// <summary>Golden motes rising and glimmering. Paired with the Radiance mist.</summary>
    public static ParticleStyle RadianceMotes { get; } = new()
    {
        Count = 30,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 220, 120), new Color(255, 240, 180), new Color(255, 200, 90)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-3f, -18f),
        VelocityMax = new Vector2(3f, -8f),
        Sway = new Vector2(6f, 0f),
        SwayFreqMin = 0.3f,
        SwayFreqMax = 0.7f,
        AlphaMin = 0.6f,
        AlphaMax = 1f,
        Twinkle = 0.5f,
        TwinkleFreqMin = 0.5f,
        TwinkleFreqMax = 1.2f,
        TwinkleSharpness = 1.5f,
        HaloScale = 4f,
        HaloAlpha = 0.25f
    };

    /// <summary>Violet and blue sparks drifting up and flickering. Paired with the Arcane mist.</summary>
    public static ParticleStyle ArcaneSparks { get; } = new()
    {
        Count = 35,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(170, 120, 255), new Color(110, 160, 255), new Color(220, 170, 255)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-5f, -25f),
        VelocityMax = new Vector2(5f, -10f),
        Sway = new Vector2(10f, 4f),
        SwayFreqMin = 0.4f,
        SwayFreqMax = 1f,
        AlphaMin = 0.7f,
        AlphaMax = 1f,
        Twinkle = 0.8f,
        TwinkleFreqMin = 1.5f,
        TwinkleFreqMax = 3f,
        TwinkleSharpness = 2.5f,
        HaloScale = 3.5f,
        HaloAlpha = 0.3f
    };

    /// <summary>Pale sparkles drifting down and twinkling. Paired with the Frost mist.</summary>
    public static ParticleStyle FrostSparkles { get; } = new()
    {
        Count = 30,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(190, 225, 255), new Color(235, 245, 255)],
        SizeMin = 1.5f,
        SizeMax = 2.5f,
        VelocityMin = new Vector2(-3f, 4f),
        VelocityMax = new Vector2(3f, 10f),
        Sway = new Vector2(6f, 0f),
        Twinkle = 0.8f,
        TwinkleFreqMin = 0.5f,
        TwinkleFreqMax = 1.5f,
        TwinkleSharpness = 2f,
        HaloScale = 3f,
        HaloAlpha = 0.2f
    };

    /// <summary>Snow streaks driving sideways. Paired with the Blizzard mist and flakes.</summary>
    public static ParticleStyle BlizzardStreaks { get; } = new()
    {
        FadeSeconds = 1.5f,
        Count = 120,
        Shape = ParticleShape.Streak,
        Colors = [new Color(255, 255, 255), new Color(230, 238, 250), new Color(210, 222, 240)],
        SizeMin = 3f,
        SizeMax = 8f,
        VelocityMin = new Vector2(-420f, 60f),
        VelocityMax = new Vector2(-260f, 140f),
        Sway = new Vector2(0f, 15f),
        SwayFreqMin = 1f,
        SwayFreqMax = 2f,
        AlphaMin = 0.5f,
        AlphaMax = 0.9f
    };

    /// <summary>Snow flakes blowing sideways, slower than the streaks. Paired with the Blizzard mist.</summary>
    public static ParticleStyle BlizzardFlakes { get; } = new()
    {
        FadeSeconds = 1.5f,
        Count = 70,
        Shape = ParticleShape.Square,
        Colors = [new Color(255, 255, 255), new Color(235, 242, 252)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-240f, 40f),
        VelocityMax = new Vector2(-150f, 90f),
        Sway = new Vector2(0f, 20f),
        SwayFreqMin = 0.8f,
        SwayFreqMax = 1.6f,
        AlphaMin = 0.6f,
        AlphaMax = 1f
    };

    /// <summary>Warm specks hanging in the air and catching the light. Paired with the Dust mist.</summary>
    public static ParticleStyle DustMotes { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 70,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 235, 200), new Color(240, 220, 190)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-2f, -2f),
        VelocityMax = new Vector2(2f, 3f),
        Sway = new Vector2(4f, 3f),
        SwayFreqMin = 0.05f,
        SwayFreqMax = 0.2f,
        AlphaMin = 0.5f,
        AlphaMax = 0.9f,
        Twinkle = 0.5f,
        TwinkleFreqMin = 0.2f,
        TwinkleFreqMax = 0.5f,
        HaloScale = 2.5f,
        HaloAlpha = 0.3f
    };

    /// <summary>White flecks of spray blowing sideways. Paired with the SeaSpray mist.</summary>
    public static ParticleStyle SeaSprayFlecks { get; } = new()
    {
        Count = 45,
        Shape = ParticleShape.Dot,
        Colors = [new Color(245, 250, 255), new Color(225, 238, 245)],
        SizeMin = 1f,
        SizeMax = 2.5f,
        VelocityMin = new Vector2(-160f, -20f),
        VelocityMax = new Vector2(-90f, 10f),
        Sway = new Vector2(0f, 20f),
        SwayFreqMin = 0.8f,
        SwayFreqMax = 1.5f,
        AlphaMin = 0.4f,
        AlphaMax = 0.8f
    };

    /// <summary>A few blue lights on small fluttering fairy wings, drifting slowly and dimming.</summary>
    public static ParticleStyle Wisps { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 6,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(120, 190, 255), new Color(160, 230, 255), new Color(100, 255, 230)],
        SizeMin = 4f,
        SizeMax = 6f,
        VelocityMin = new Vector2(-6f, -4f),
        VelocityMax = new Vector2(6f, 4f),
        Sway = new Vector2(22f, 16f),
        SwayFreqMin = 0.05f,
        SwayFreqMax = 0.15f,
        AlphaMin = 0.7f,
        AlphaMax = 1f,
        Twinkle = 0.4f,
        TwinkleFreqMin = 0.2f,
        TwinkleFreqMax = 0.4f,
        TwinkleSharpness = 1.5f,
        HaloScale = 6f,
        HaloAlpha = 0.3f,
        WingScale = 1.5f,
        WingWidth = 0.6f,
        WingAlpha = 0.5f,
        WingPairs = 2,
        WingFlapMin = 7f,
        WingFlapMax = 9f
    };

    /// <summary>A few water drops falling from above.</summary>
    public static ParticleStyle Drips { get; } = new()
    {
        Count = 8,
        Shape = ParticleShape.Streak,
        Colors = [new Color(170, 200, 230), new Color(200, 225, 250)],
        SizeMin = 4f,
        SizeMax = 7f,
        VelocityMin = new Vector2(0f, 220f),
        VelocityMax = new Vector2(0f, 320f),
        AlphaMin = 0.35f,
        AlphaMax = 0.6f
    };
```

- [ ] **Step 7: Replace the overlay list and the class summary**

In `AmbientEffects.cs`, replace the comment above `Overlays` and the whole `Overlays` field with:

```csharp
    //draw order, back to front: cloud shadows first because they lie on the ground, then tints and mists, lightning
    //flashing through them, fog over the flash (as in the original storm), then particles last so glows and falling
    //things read on top of every haze
    private readonly (MapFlags Flag, IAmbientOverlay Overlay)[] Overlays =
    [
        (MapFlags.CloudShadows, new MistRenderer(MistStyle.CloudShadows)),
        (MapFlags.Underwater, new MistRenderer(MistStyle.Underwater)),
        (MapFlags.Heat, new MistRenderer(MistStyle.Heat)),
        (MapFlags.Gloom, new MistRenderer(MistStyle.Gloom)),
        (MapFlags.BloodMoon, new MistRenderer(MistStyle.BloodMoon)),
        (MapFlags.Miasma, new MistRenderer(MistStyle.Miasma)),
        (MapFlags.Radiance, new MistRenderer(MistStyle.Radiance)),
        (MapFlags.Arcane, new MistRenderer(MistStyle.Arcane)),
        (MapFlags.Frost, new MistRenderer(MistStyle.Frost)),
        (MapFlags.Dust, new MistRenderer(MistStyle.Dust)),
        (MapFlags.SeaSpray, new MistRenderer(MistStyle.SeaSpray)),
        (MapFlags.Sandstorm, new MistRenderer(MistStyle.Sandstorm)),
        (MapFlags.Blizzard, new MistRenderer(MistStyle.Blizzard)),
        (MapFlags.Lightning, new LightningRenderer()),
        (MapFlags.Fog, new MistRenderer(MistStyle.Fog)),
        (MapFlags.Sandstorm, new ParticleRenderer(ParticleStyle.SandGrains)),
        (MapFlags.Underwater, new ParticleRenderer(ParticleStyle.Bubbles)),
        (MapFlags.Ash, new ParticleRenderer(ParticleStyle.Ash)),
        (MapFlags.Ash, new ParticleRenderer(ParticleStyle.Embers)),
        (MapFlags.Leaves, new ParticleRenderer(ParticleStyle.Leaves)),
        (MapFlags.Petals, new ParticleRenderer(ParticleStyle.Petals)),
        (MapFlags.Fireflies, new ParticleRenderer(ParticleStyle.Fireflies)),
        (MapFlags.Radiance, new ParticleRenderer(ParticleStyle.RadianceMotes)),
        (MapFlags.Arcane, new ParticleRenderer(ParticleStyle.ArcaneSparks)),
        (MapFlags.Frost, new ParticleRenderer(ParticleStyle.FrostSparkles)),
        (MapFlags.Dust, new ParticleRenderer(ParticleStyle.DustMotes)),
        (MapFlags.SeaSpray, new ParticleRenderer(ParticleStyle.SeaSprayFlecks)),
        (MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardFlakes)),
        (MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardStreaks)),
        (MapFlags.Wisps, new ParticleRenderer(ParticleStyle.Wisps)),
        (MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips))
    ];
```

Replace the class's `<summary>` block (above `public sealed class AmbientEffects`) with:

```csharp
/// <summary>
///     Every map-flag-driven ambient overlay — fog, lightning, the tints and mists, and the particle effects — switched
///     on and off together from the current <see cref="MapFlags" />. Several can be on at once (e.g. Leaves +
///     Fireflies, or Rain + Lightning + Fog). Snow and rain stay with <see cref="WeatherRenderer" /> and darkness with
///     <see cref="DarknessRenderer" />. Touched only on the game-loop thread.
/// </summary>
```

Also replace the `EXTENDED_FLAGS` summary with:

```csharp
    /// <summary>
    ///     The flag bits above the low byte. The map info packet carries only the low byte; these arrive separately, as
    ///     8 bytes, in the SetMapEffects packet.
    /// </summary>
```

- [ ] **Step 8: Build and run all client tests**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client
dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --no-ansi
```

Expected: `Build succeeded` with no new warnings, and every test passes, including both `AmbientEffectsTests`.

- [ ] **Step 9: Prove the guard catches a missing overlay**

Temporarily delete the line `(MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips))`, plus the trailing comma on the line above it. Rebuild and run only `AmbientEffectsTests`. Expected: `EveryEffectFlag_HasAnOverlay` fails and names `Drips`. Put the line back exactly, then rebuild and rerun. Expected: both tests pass.

```json:metadata
{"files": ["Tests/Chaos.Client.Tests/AmbientEffectsTests.cs", "Chaos.Client.Rendering/AmbientEffects.cs", "Chaos.Client.Rendering/MistStyle.cs", "Chaos.Client.Rendering/ParticleStyle.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --no-ansi --treenode-filter \"/*/*/AmbientEffectsTests/*\"", "acceptanceCriteria": ["EveryEffectFlag_HasAnOverlay passes and fails when an overlay line is removed", "ExtendedFlags_CoverEveryBitAboveTheLowByte passes", "Overlays list matches the spec draw order", "Preset values match the spec", "All client tests pass"], "modelTier": "standard"}
```

---

### Task 6: Docs for the new effects

**Goal:** The effects doc and the client's `CLAUDE.md` describe the 11 new effects, the 8-byte flags, the wing settings and the theatre's second page.

**Files:**
- Modify: `worktrees/ambient-unora/docs/ambient-map-effects.md`
- Modify: `worktrees/ambient-client/CLAUDE.md` (the `AmbientEffects` bullet under "Rendering Layer")

**Acceptance Criteria:**
- [ ] The effects table lists all 21 effect flags, with the presets that build each one
- [ ] The bit table shows 17–27 as the second set and 28–63 as free, and the text says `MapFlags` is 8 bytes
- [ ] The particle table documents the six wing settings
- [ ] A "The Suomi theatre" section explains the two pages and the 16-per-page limit
- [ ] "Adding a new effect" uses bit 28, and mentions the client guard test and the theatre list
- [ ] "Limits" says 36 free slots, and says new effects belong in bits 28–63
- [ ] The client `CLAUDE.md` AmbientEffects bullet lists the new flags and bits 8–63

**Verify:** `grep -c "Gloom\|CloudShadows\|WingScale\|stageeffects2\|1UL << 28" C:/Users/Michael/Documents/GitHub/worktrees/ambient-unora/docs/ambient-map-effects.md` → 5 or more

**Steps:**

- [ ] **Step 1: Intro**

In `docs/ambient-map-effects.md`, after the sentence ending `Fireflies and Underwater.` in the first paragraph, add:

```markdown
A second set followed later that month: Gloom, Radiance, Arcane, Frost, Blizzard, Dust, CloudShadows, SeaSpray,
Heat, Wisps and Drips. That change also widened the flags to 8 bytes.
```

- [ ] **Step 2: Effects table**

Add these rows at the end of the table under "## The effects":

```markdown
| `Gloom` | Dark violet dim, slow shadow wisps, heavy dark edges | `MistStyle.Gloom` |
| `Radiance` | Warm gold glow breathing slowly, golden motes rising | `MistStyle.Radiance` + `ParticleStyle.RadianceMotes` |
| `Arcane` | Faint pulsing violet haze, violet and blue sparks drifting up | `MistStyle.Arcane` + `ParticleStyle.ArcaneSparks` |
| `Frost` | Cold blue tint, frosted white edges, pale sparkles drifting down | `MistStyle.Frost` + `ParticleStyle.FrostSparkles` |
| `Blizzard` | White-out haze tearing sideways, with snow flakes and streaks driving across | `MistStyle.Blizzard` + `ParticleStyle.BlizzardFlakes` + `ParticleStyle.BlizzardStreaks` |
| `Dust` | Faint warm haze, specks hanging in the air | `MistStyle.Dust` + `ParticleStyle.DustMotes` |
| `CloudShadows` | Large soft cloud shadows sliding over the ground. No tint. | `MistStyle.CloudShadows` |
| `SeaSpray` | Pale salt mist blowing sideways, white flecks of spray | `MistStyle.SeaSpray` + `ParticleStyle.SeaSprayFlecks` |
| `Heat` | Orange tint throbbing gently, faint haze rising | `MistStyle.Heat` |
| `Wisps` | A few blue lights on small fluttering fairy wings | `ParticleStyle.Wisps` |
| `Drips` | A few water drops falling | `ParticleStyle.Drips` |
```

- [ ] **Step 3: Theatre section**

After the "## Putting an effect on a map" section (after the paragraph about `/atmosphere`, before "## How it works"), insert:

```markdown
## The Suomi theatre

A theatre director can start and stop effects on the stage from Thulin's stage-effects menu. The menu has two pages:

- **Page 1** (`suomitheatre_stageeffects`) lists the first 12 effects, then "Clear All Effects" and "More Effects".
- **Page 2** (`suomitheatre_stageeffects2`) lists the rest, then "Clear All Effects" and "Back".

"Clear All Effects" stops the effects on both pages and leaves the lights alone. The list lives in
`TheatreStageEffects.All`, and each entry names its page. A page holds at most 16 effects
(`MAX_EFFECTS_PER_PAGE`), because the option panel fits 18 rows before it runs off the top of the screen. A test fails
if a page grows past that.
```

- [ ] **Step 4: The flag values**

Under "### The flag values", change `It is 4 bytes wide, laid out like this:` to `It is 8 bytes wide (a ulong), laid out like this:`. Replace the table's last row (`| 17–31 | — | **Free.** New effects go here. |`) with:

```markdown
| 17–27 | `Gloom` … `Drips` | The second set of ambient effects |
| 28–63 | — | **Free.** New effects go here. |
```

- [ ] **Step 5: Getting the flags to the client**

In "### Getting the flags to the client", change the `SetMapEffects` bullet's first sentence to:

```markdown
- **`SetMapEffects`** (server opcode 125) sends every bit above the low byte, as 8 bytes. The server sends it
```

Keep the rest of that bullet unchanged.

- [ ] **Step 6: Drawing order**

In "### Drawing", replace the numbered draw-order list with:

```markdown
1. Cloud shadows, which lie on the ground
2. Tints and mists
3. Lightning, flashing through the mists
4. Fog, over the flash
5. Particles, last, so glows and falling things read on top of every haze
```

- [ ] **Step 7: Wing settings**

In the "**Particles (`ParticleStyle`)**" table, add these rows at the end:

```markdown
| `WingScale`, `WingWidth` | Wings on each particle: length and width as multiples of the particle size. A `WingScale` of 0 means no wings. |
| `WingAlpha` | Wing opacity as a fraction of the particle's own. Wings use the particle's color, paled toward white. |
| `WingPairs` | 1 draws one pair of wings. 2 adds a smaller lower pair, like fairy wings. |
| `WingFlapMin`/`Max` | Wing beats per second |
```

- [ ] **Step 8: Adding a new effect**

In "### 1. Add the flag (server repo)", replace the sentence `In \`Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs\`, add the next free bit to \`MapFlags\`. Bit 16 is \`NoTownMap\`, so the first free one is 17:` and the code block after it with:

````markdown
In `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs`, add the next free bit to `MapFlags`. Bits 17–27 hold the
second set of effects, so the first free one is 28. Write it as a `ulong` shift:

```csharp
    Drips = 1UL << 27,
    Snowglow = 1UL << 28
```
````

In the same section, add a fourth item to the numbered list of server changes:

```markdown
4. To offer it in the Suomi theatre, add a `StageEffect` to `TheatreStageEffects.All` with page 2. Page 2 has room
   while it holds fewer than 16 effects.
```

In "### 3. Connect the flag to the look", add after the code block:

```markdown
The client test `AmbientEffectsTests.EveryEffectFlag_HasAnOverlay` fails until the new flag has an overlay here.
```

- [ ] **Step 9: Limits**

In "## Limits":
- In the "Why not the low byte" bullet, change `New effects belong in bits 17–31.` to `New effects belong in bits 28–63.`
- Replace the "Fifteen more slots" bullet with:

```markdown
- **36 more slots.** Bits 28–63 are free. Past that, the flag type and the packet both need to grow again.
```

- Replace the "Code built on the server" bullet with:

```markdown
- **Code built on the server.** Widening `MapFlags` from 1 byte to 4, and later to 8, was a breaking change for any
  code that treats the flags as a number.
```

- [ ] **Step 10: Client CLAUDE.md**

In `worktrees/ambient-client/CLAUDE.md`, replace the whole `- **\`AmbientEffects\`** -- ...` bullet with:

```markdown
- **`AmbientEffects`** -- Map-flag ambient overlays: Fog and Lightning (values 16/32, bits 4 and 5, of the map info byte) plus BloodMoon, Sandstorm, Miasma, Ash, Leaves, Petals, Fireflies, Underwater, Gloom, Radiance, Arcane, Frost, Blizzard, Dust, CloudShadows, SeaSpray, Heat, Wisps and Drips (`MapFlags` bits 8-27; `MapFlags` is 8 bytes, and every bit above the low byte arrives in the separate `SetMapEffects` packet right after map info). Each flag drives one or more `IAmbientOverlay`s: `MistRenderer` (wash + drifting cloud-noise layers + vignette, tuned by `MistStyle` presets), `ParticleRenderer` (procedural particles, optionally with flapping wings, tuned by `ParticleStyle` presets) and `LightningRenderer` (random flash + procedural bolts). Several can be on at once. `CoveredFlags` lists the flags that have an overlay; `AmbientEffectsTests` fails if an effect flag has none.
```

- [ ] **Step 11: Check**

```bash
grep -c "Gloom\|CloudShadows\|WingScale\|stageeffects2\|1UL << 28" C:/Users/Michael/Documents/GitHub/worktrees/ambient-unora/docs/ambient-map-effects.md
grep -c "Wisps and Drips" C:/Users/Michael/Documents/GitHub/worktrees/ambient-client/CLAUDE.md
```

Expected: the first count is 5 or more, and the second is 1.

```json:metadata
{"files": ["Unora: docs/ambient-map-effects.md", "Client: CLAUDE.md"], "verifyCommand": "grep -c \"Gloom\\|CloudShadows\\|WingScale\\|stageeffects2\\|1UL << 28\" C:/Users/Michael/Documents/GitHub/worktrees/ambient-unora/docs/ambient-map-effects.md", "acceptanceCriteria": ["Effects table lists all 21 effect flags", "Bit table shows 17-27 used and 28-63 free; MapFlags is 8 bytes", "Particle table documents the six wing settings", "Suomi theatre section explains two pages and the 16 limit", "Adding a new effect uses bit 28 and mentions the guard test and theatre list", "Limits says 36 free slots and bits 28-63", "Client CLAUDE.md AmbientEffects bullet lists the new flags and bits 8-63"], "modelTier": "standard"}
```

---

### Task 7: In-game check of every new effect and the theatre pages

**Goal:** Each new effect, seen in the running game, matches its approved preview. The theatre pages and `/atmosphere` behave as the spec says.

**Files:** none changed. Screenshots go to the session scratchpad.

**Acceptance Criteria:**
- [ ] A screenshot exists for each of the 11 flags, taken with only that flag added, and each matches its approved preview in color, amount and motion. Wisps show small fluttering wings.
- [ ] With Blizzard on, the F11 debug overlay shows no clear frame-rate drop compared with no effect
- [ ] Theatre page 1 ends with "More Effects". Page 2 lists the 11 new effects and flips one between Start and Stop. "Back" returns to page 1. "Clear All Effects" stops a page-2 effect.
- [ ] After `/atmosphere`, the new effects disappear. Running it again shows them.

**Verify:** screenshots in the scratchpad (`fx-<Flag>.png`), plus the observations above recorded in the task's completion note

This task runs in the coordinating session, not in a subagent. It needs the user: to close any running game, to log in, and to walk to the theatre.

**Steps:**

- [ ] **Step 1: Make sure nothing locks the build**

```powershell
Get-Process | Where-Object { $_.Path -like '*Chaos*' } | Select-Object Id, ProcessName, Path
```

If a server or client from these repos is running, ask the user to close it. Never kill it.

- [ ] **Step 2: Build both**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server && dotnet build Chaos.slnx
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client && dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/ambient-server
```

- [ ] **Step 3: Point the server at the Unora worktree**

The worktree's committed `appsettings.json` names another developer's path. Patch only the built copy, and never the source file. Use Python, because shell escaping mangles the backslashes:

```bash
python - <<'EOF'
p = r"C:/Users/Michael/Documents/GitHub/worktrees/ambient-server/Chaos/bin/Debug/net10.0/appsettings.json"
s = open(p, encoding="utf-8").read()
old = "C:\\\\Users\\\\mewbb\\\\Documents\\\\GitHub\\\\Unora"
new = "C:\\\\Users\\\\Michael\\\\Documents\\\\GitHub\\\\worktrees\\\\ambient-unora"
assert old in s, "StagingDirectory line not found"
open(p, "w", encoding="utf-8").write(s.replace(old, new))
print("patched")
EOF
```

The Unora worktree has the new page-2 dialog and the tracked saved characters. Logging in there writes to the worktree's `Data/Saved`, and Task 8 never stages those files.

- [ ] **Step 4: Start the server, then the client**

Start `Chaos.exe` from `worktrees/ambient-server/Chaos/bin/Debug/net10.0/` in the background, and wait until it logs that it is listening. Then start the client with the local game data:

```powershell
$env:DA_PATH = 'C:\Users\Michael\Documents\Unora\Unora Files'
Start-Process 'C:\Users\Michael\Documents\GitHub\worktrees\ambient-client\Chaos.Client\bin\Debug\net10.0\Chaos.Client.exe'
```

If the client executable is elsewhere, find it under `worktrees/ambient-client/Chaos.Client/bin/`. Ask the user to log in with an admin character and stand on an outdoor map with no effects of its own.

- [ ] **Step 5: Screenshot each effect**

For each flag in the order Gloom, Radiance, Arcane, Frost, Blizzard, Dust, CloudShadows, SeaSpray, Heat, Wisps, Drips:

1. Type `/mapFlag add <Flag>` in the game. Use SendKeys to the focused game window, or ask the user to type it if SendKeys doesn't reach the game after one try.
2. Wait 3 seconds for the fade-in.
3. Take a screenshot of the game window's client area to `<scratchpad>/fx-<Flag>.png` (script below).
4. Type `/mapFlag remove <Flag>`.

Compare each screenshot with its approved preview. Those are the round 1 cards in `.superpowers/brainstorm/3983-1790376389/content/effects-looks.html`, plus `-v2.html` for Dust and Cloud shadows (7A) and `-v3.html` for Wisps (10A). Show the user any effect that looks clearly different.

Screenshot script (PowerShell, from the window-automation memory):

```powershell
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  public struct RECT { public int L, T, R, B; }
  public struct POINT { public int X, Y; }
}
"@
[W]::SetProcessDPIAware() | Out-Null
$p = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.Path -like '*ambient-client*' } | Select-Object -First 1
$h = $p.MainWindowHandle
[W]::SetForegroundWindow($h) | Out-Null
$r = New-Object W+RECT; [W]::GetClientRect($h, [ref]$r) | Out-Null
$o = New-Object W+POINT; [W]::ClientToScreen($h, [ref]$o) | Out-Null
$bmp = New-Object System.Drawing.Bitmap $r.R, $r.B
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($o.X, $o.Y, 0, 0, $bmp.Size)
$bmp.Save('<scratchpad>\fx-<Flag>.png')
```

- [ ] **Step 6: Frame rate with Blizzard**

Press F11 to show the debug overlay. Note the frame rate with no effect, then with `/mapFlag add Blizzard`. Record both numbers. Report a drop of more than about 10% to the user.

- [ ] **Step 7: /atmosphere**

With Gloom and Wisps added, type `/atmosphere`. Both disappear. Type it again, and both come back. Remove both flags afterwards.

- [ ] **Step 8: Theatre**

Ask the user to go to the Suomi theatre as the director and open Thulin's stage-effects menu. Check, with screenshots:

1. Page 1 lists today's 12 effects, then "Clear All Effects" and "More Effects".
2. "More Effects" opens page 2 with the 11 new effects, "Clear All Effects" and "Back". The menu fits on screen.
3. "Start Gloom" turns Gloom on, and the option then reads "Stop Gloom".
4. "Clear All Effects" on page 2 stops it.
5. "Back" returns to page 1.

- [ ] **Step 9: Shut down**

Ask the user to log out. Stop the server you started. Record the results for the acceptance criteria in the task's completion note.

```json:metadata
{"files": [], "verifyCommand": "", "acceptanceCriteria": ["Screenshot per flag matching its approved preview; Wisps show small fluttering wings", "No clear frame-rate drop with Blizzard (F11 overlay numbers recorded)", "Theatre page 1 has More Effects; page 2 lists the 11, flips Start/Stop, Back returns, Clear All stops a page-2 effect", "/atmosphere hides the new effects and shows them again"], "modelTier": "frontier"}
```

---

### Task 8: Commit the full implementation

**Goal:** One commit per repo on `feat/more-ambient-effects`: server first, then the client pointing its `Chaos-Server` submodule at that server commit, then Unora. Nothing is merged or pushed.

**Files:** the files changed by Tasks 2–6, staged by explicit path.

**Acceptance Criteria:**
- [ ] The server worktree has one new commit containing exactly the Task 2 and Task 3 server files (plus any extra file Task 2 had to fix)
- [ ] The client worktree has one new commit with the Task 4–6 client files, and `Chaos-Server` recorded at the new server commit
- [ ] The Unora worktree has one new commit containing only the page-2 dialog JSON and `docs/ambient-map-effects.md`
- [ ] No commit contains `appsettings.json`, `launchSettings.json`, `Data/Saved` or `Data/LocalStorage`
- [ ] `git status --short` in each worktree shows nothing staged afterwards

**Verify:** `git -C <each worktree> show --stat HEAD` lists exactly the expected files

**Steps:**

- [ ] **Step 1: Server commit**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-server
git status --short
git add -- Chaos.DarkAges/Definitions/Enums.cs Chaos.DarkAges/Definitions/CONSTANTS.cs \
  Chaos.Networking/Entities/Server/SetMapEffectsArgs.cs Chaos.Networking/Converters/Server/SetMapEffectsConverter.cs \
  Chaos/Networking/ChaosWorldClient.cs Chaos/Utilities/MapFlagVisibility.cs \
  Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs \
  Tests/Chaos.Tests/Networking/MapEffectsPacketConverterTests.cs Tests/Chaos.Tests/MapFlagVisibilityTests.cs \
  Tests/Chaos.Tests/Theatre/TheatreStageEffectsTests.cs
```

Also add any extra file Task 2 reported fixing. Check that `git diff --cached --name-only` lists only these files. Then write the message to a file in the scratchpad and commit:

```text
Widen map flags to 8 bytes and add 11 ambient effects

MapFlags becomes a ulong, and SetMapEffects now carries 8 bytes. Gloom,
Radiance, Arcane, Frost, Blizzard, Dust, CloudShadows, SeaSpray, Heat,
Wisps and Drips take bits 17-27, and /atmosphere hides them. The Suomi
theatre lists them on a second stage-effects page. The client version
goes up by one, because older clients read only 4 bytes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

```bash
git commit -F <scratchpad>/server-msg.txt
git log --oneline -1
```

Record the new server commit hash as `SERVER_SHA`.

- [ ] **Step 2: Client commit**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-client
git status --short
git add -- Chaos.Client.Rendering/ParticleStyle.cs Chaos.Client.Rendering/ParticleRenderer.cs \
  Chaos.Client.Rendering/MistStyle.cs Chaos.Client.Rendering/AmbientEffects.cs \
  Tests/Chaos.Client.Tests/AmbientEffectsTests.cs CLAUDE.md
git update-index --cacheinfo 160000,$SERVER_SHA,Chaos-Server
git diff --cached --name-only
```

Expected: the six files plus `Chaos-Server`. Commit with this message:

```text
Draw 11 new ambient effects, with wings for wisps

Adds mist and particle presets for Gloom, Radiance, Arcane, Frost,
Blizzard, Dust, CloudShadows, SeaSpray, Heat, Wisps and Drips, and
optional flapping wings on particles. The effect mask now covers all
8 bytes. A guard test fails if an effect flag has no overlay. Points
Chaos-Server at the commit that adds the flags.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 3: Unora commit**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/ambient-unora
git status --short
git add -- Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json docs/ambient-map-effects.md
git diff --cached --name-only
```

Expected: exactly those two files. `git status` may also show `Data/Saved`, `Data/LocalStorage` or `Custom Client Mods/.../obj` changes from the in-game run or background builds. Leave them unstaged. Commit with this message:

```text
Add the theatre's second effects page and document 11 new effects

The Suomi theatre's stage-effects menu gets a second page for the new
ambient effects. The effects doc covers the new flags, the 8-byte flag
layout, particle wings and the two theatre pages.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 4: Check all three**

```bash
for w in ambient-server ambient-client ambient-unora; do git -C C:/Users/Michael/Documents/GitHub/worktrees/$w show --stat --oneline HEAD; git -C C:/Users/Michael/Documents/GitHub/worktrees/$w diff --cached --name-only; done
```

Expected: each HEAD lists only its files, and nothing is left staged. Merging into the shared checkouts is a separate step, taken only with the user's go-ahead. At that point, the shared server checkout's uncommitted `MapFlagVisibilityTests.cs` edit needs the user's OK before it is replaced (see the spec, "Open item for merge time").

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/ambient-server show --stat --oneline HEAD", "acceptanceCriteria": ["One server commit with exactly the Task 2-3 server files", "One client commit with the Task 4-6 client files and Chaos-Server at the server commit", "One Unora commit with only the dialog JSON and the effects doc", "No appsettings, launchSettings, Data/Saved or Data/LocalStorage in any commit", "Nothing left staged"], "modelTier": "mechanical"}
```

# Haunted Theatre Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** During the Halloween window the Suomi theatre is replaced by a redecorated, haunted copy (new map, ghosts, spooky effect flags) that keeps every director tool, and the mirror maze entrance lives there.

**Architecture:** Two map instances, `suomi_theatre` and `suomi_theatre_halloween`, share one static helper, `SuomiTheatre`. It answers "is this map a theatre?" and "which theatre is open now?". Every hard-coded `"suomi_theatre"` goes through it, and each theatre's map script moves players to the open one when the window opens or closes. The new map is made by a Python script that copies `lod346`, swaps the floor and places props, and a preview is rendered with the existing `render_map.cs`.

**Tech Stack:** C# / .NET 10 (Chaos-Server, TUnit + FluentAssertions + Moq), Python 3 + pytest (Unora tools), DALib (`dotnet run` file app) for previews.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-01-haunted-theatre-design.md`. Parent spec: `2026-09-30-mirror-maze-design.md` (section 5).

## Global Constraints

- **Repos and branches.** Work in two worktrees so the dirty `main` checkouts are not touched:
  - Server: `C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-server`, branch `feat/haunted-theatre` from the `master` of `Chaos.Client/Chaos-Server`.
  - Unora: `C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-unora`, branch `feat/haunted-theatre` from `main`.
  - No client change, no `CLIENT_VERSION` change. Never merge or push; the user does that (merge order: server, then Unora).
- **Instance ids** are exactly `suomi_theatre` and `suomi_theatre_halloween`. The Halloween map number is `10269` (free in both `Data/Configuration/MapData` and `UnusedMapData`; `lod10232` is taken in `UnusedMapData`).
- **Same layout as the normal theatre:** the new map is 20x31, copied from `lod346`, so every tile coordinate means the same thing on both maps. Players are moved tile for tile.
- **Protected tiles** never get props or foreground changes: the stage rectangle `(0, 12)` to `(8, 20)`, every reactor and merchant tile of the normal theatre plus their four neighbours, and the mirror wall tile `(5, 2)` with its door tile `(5, 3)` and neighbours. The stage keeps every value it has. The dark floor swap applies everywhere else, protected tiles included, so doorways match the room.
- **`EventPeriod` quirk:** `EventPeriod` works out its start date from the real clock (`DateTime.UtcNow`), not from the date passed in. Tests must not depend on a fixed date being inside the window. Test the window logic with a plain `bool`, and test registration with the date sweep in Task 1.
- **Window:** the existing Halloween `EventPeriod` (Oct 4 06:00 UTC to Nov 4 06:00 UTC). The helper reads it; nothing copies the dates.
- **Tests style:** TUnit (`[Test]`, `[Arguments]`), FluentAssertions, `public sealed class XTests`, in `Tests/Chaos.Tests/Theatre/`.
- **Server test command** (from the server worktree): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(ClassA)|(ClassB)/*"`.
- **Python tests** run from the Unora worktree root: `python -m pytest Tools/HauntedTheatre -q`.
- **Docs:** write plainly, short sentences, no filler. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

**User decisions (already made):**
- Scope: the full spec (new map, theatre swap, ghosts, mirror entrance moved, effect flags on).
- Swap method: two instances and one helper.
- Decorations: a script places a first pass and renders a preview; the user then tweaks the map in ChaosAssetManager.
- Baseline effects `Ghosts` and `HarvestMoon`; `Bats` stays on the director's menu only.
- Director tools all kept; ghosts are harmless wanderers.
- The design in the spec was approved on 2026-10-01.

---

### Task 1: `SuomiTheatre` helper and the Halloween event entry

**Goal:** One static class says which maps are theatres and which theatre is open, and the Halloween `EventPeriod` knows the new instance.

**Files:**
- Create: `Chaos/Services/Theatre/SuomiTheatre.cs` (server worktree)
- Modify: `Chaos/Models/World/EventPeriod.cs` (the Halloween list, currently `hm_road`, `secludedcave`, `macabre_yard`, `mirror_maze`)
- Test: `Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs`

**Acceptance Criteria:**
- [ ] `SuomiTheatre.IsTheatre` is true for `suomi_theatre` and `suomi_theatre_halloween` in any letter case, and false for `suomi_theatre_backstage`, `suomi`, `mirror_maze`, `""` and null.
- [ ] `OpenInstanceId(true)` is `suomi_theatre_halloween` and `OpenInstanceId(false)` is `suomi_theatre`.
- [ ] `Resolve("suomi_theatre", true)` is `suomi_theatre_halloween`; `Resolve("suomi_theatre", false)` is `suomi_theatre`; any other id comes back unchanged (including `suomi_theatre_backstage` and `suomi_theatre_halloween`).
- [ ] `MoveTarget` returns the open theatre's id when the player is on the closed theatre, null when already on the open one, and null for a non-theatre map.
- [ ] `EventPeriod` treats `suomi_theatre_halloween` exactly as it treats `mirror_maze` on every day of an 800-day sweep around today, and `mirror_maze` is active on at least one of those days.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreTests)/*"` → all tests pass, 0 failed.

**Steps:**

- [ ] **Step 1: Create the worktree and branch**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server worktree add C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-server -b feat/haunted-theatre master
```

Work in `C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-server` from here on.

- [ ] **Step 2: Write the failing tests**

Create `Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs`:

```csharp
#region
using Chaos.Definitions;
using Chaos.Models.World;
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class SuomiTheatreTests
{
    [Test]
    [Arguments("suomi_theatre")]
    [Arguments("SUOMI_THEATRE")]
    [Arguments("suomi_theatre_halloween")]
    [Arguments("Suomi_Theatre_Halloween")]
    public void Both_theatres_count_as_the_theatre(string instanceId) => SuomiTheatre.IsTheatre(instanceId).Should().BeTrue();

    [Test]
    [Arguments("suomi_theatre_backstage")]
    [Arguments("suomi")]
    [Arguments("mirror_maze")]
    [Arguments("")]
    public void Other_maps_are_not_the_theatre(string instanceId) => SuomiTheatre.IsTheatre(instanceId).Should().BeFalse();

    [Test]
    public void No_map_id_is_not_the_theatre() => SuomiTheatre.IsTheatre(null).Should().BeFalse();

    [Test]
    public void The_haunted_theatre_is_open_only_inside_the_window()
    {
        SuomiTheatre.OpenInstanceId(true).Should().Be("suomi_theatre_halloween");
        SuomiTheatre.OpenInstanceId(false).Should().Be("suomi_theatre");
    }

    [Test]
    public void Resolve_turns_the_normal_theatre_into_the_open_one()
    {
        SuomiTheatre.Resolve("suomi_theatre", true).Should().Be("suomi_theatre_halloween");
        SuomiTheatre.Resolve("suomi_theatre", false).Should().Be("suomi_theatre");
    }

    [Test]
    [Arguments("suomi_theatre_backstage", true)]
    [Arguments("suomi_theatre_halloween", true)]
    [Arguments("suomi_theatre_halloween", false)]
    [Arguments("mirror_maze", true)]
    public void Resolve_leaves_every_other_map_alone(string instanceId, bool halloweenActive)
        => SuomiTheatre.Resolve(instanceId, halloweenActive).Should().Be(instanceId);

    [Test]
    public void A_player_on_the_closed_theatre_is_moved_to_the_open_one()
    {
        SuomiTheatre.MoveTarget("suomi_theatre", true).Should().Be("suomi_theatre_halloween");
        SuomiTheatre.MoveTarget("suomi_theatre_halloween", false).Should().Be("suomi_theatre");
    }

    [Test]
    public void A_player_on_the_open_theatre_stays()
    {
        SuomiTheatre.MoveTarget("suomi_theatre_halloween", true).Should().BeNull();
        SuomiTheatre.MoveTarget("suomi_theatre", false).Should().BeNull();
    }

    [Test]
    public void Nobody_is_moved_off_a_map_that_is_not_a_theatre()
        => SuomiTheatre.MoveTarget("suomi_theatre_backstage", true).Should().BeNull();

    //EventPeriod reads the real clock, so a fixed date can't be trusted to sit inside the window. Sweeping every day
    //around today still proves the theatre is registered in the Halloween period the maze is in
    [Test]
    public void The_haunted_theatre_follows_the_same_window_as_the_maze()
    {
        var start = DateTime.UtcNow.Date.AddDays(-400);
        var mazeWasActive = false;

        for (var day = 0; day < 800; day++)
        {
            var date = start.AddDays(day)
                            .AddHours(12);

            var maze = EventPeriod.IsSpecificEventActive(date, "mirror_maze", EventType.Halloween);
            var theatre = EventPeriod.IsSpecificEventActive(date, SuomiTheatre.HALLOWEEN_ID, EventType.Halloween);

            theatre.Should()
                   .Be(maze, $"on {date:yyyy-MM-dd} the theatre and the maze share the window");

            mazeWasActive |= maze;
        }

        mazeWasActive.Should()
                     .BeTrue();
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreTests)/*"`
Expected: build error, `SuomiTheatre` does not exist.

- [ ] **Step 4: Write the helper**

Create `Chaos/Services/Theatre/SuomiTheatre.cs`:

```csharp
#region
using Chaos.Definitions;
using Chaos.Extensions.Common;
using Chaos.Models.World;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>
///     Which maps are the Suomi theatre, and which one is open. The normal theatre is open all year except the Halloween
///     window, when its haunted copy replaces it. Both have the same layout and the same director tools.
/// </summary>
public static class SuomiTheatre
{
    public const string HALLOWEEN_ID = "suomi_theatre_halloween";
    public const string NORMAL_ID = "suomi_theatre";

    /// <summary>True for either theatre. The backstage room is not a theatre.</summary>
    public static bool IsTheatre(string? instanceId)
        => !string.IsNullOrEmpty(instanceId) && (instanceId.EqualsI(NORMAL_ID) || instanceId.EqualsI(HALLOWEEN_ID));

    /// <summary>The theatre that is open when the Halloween window is, or is not, running.</summary>
    public static string OpenInstanceId(bool halloweenActive) => halloweenActive ? HALLOWEEN_ID : NORMAL_ID;

    /// <summary>The theatre that is open at <paramref name="nowUtc" />, read from the Halloween event period.</summary>
    public static string OpenInstanceId(DateTime nowUtc)
        => OpenInstanceId(EventPeriod.IsSpecificEventActive(nowUtc, HALLOWEEN_ID, EventType.Halloween));

    /// <summary>
    ///     The map a fixed <c>suomi_theatre</c> destination should really lead to. Every other id is returned as it
    ///     came, so a warp that names the haunted theatre or the backstage room is not redirected.
    /// </summary>
    public static string Resolve(string instanceId, bool halloweenActive)
        => instanceId.EqualsI(NORMAL_ID) ? OpenInstanceId(halloweenActive) : instanceId;

    /// <inheritdoc cref="Resolve(string, bool)" />
    public static string Resolve(string instanceId, DateTime nowUtc)
        => instanceId.EqualsI(NORMAL_ID) ? OpenInstanceId(nowUtc) : instanceId;

    /// <summary>
    ///     Where a player standing on <paramref name="currentId" /> should be moved because that theatre has closed, or
    ///     null when they should stay (already on the open theatre, or not on a theatre at all).
    /// </summary>
    public static string? MoveTarget(string currentId, bool halloweenActive)
    {
        if (!IsTheatre(currentId))
            return null;

        var open = OpenInstanceId(halloweenActive);

        return currentId.EqualsI(open) ? null : open;
    }
}
```

- [ ] **Step 5: Register the haunted theatre in the Halloween period**

In `Chaos/Models/World/EventPeriod.cs`, change the Halloween list from:

```csharp
                [
                    "hm_road",
                    "secludedcave",
                    "macabre_yard",
                    "mirror_maze"
                ]),
```

to (only the Halloween entry, which is the one that starts `"0 6 4 10 *"`, not `HalloweenNight`):

```csharp
                [
                    "hm_road",
                    "secludedcave",
                    "macabre_yard",
                    "mirror_maze",
                    "suomi_theatre_halloween"
                ]),
```

- [ ] **Step 6: Run the tests to see them pass**

Run the command from Step 3. Expected: every test passes (each `[Arguments]` row counts as one), 0 failed.

- [ ] **Step 7: Commit**

```bash
git add Chaos/Services/Theatre/SuomiTheatre.cs Chaos/Models/World/EventPeriod.cs Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs
git commit -m "Add SuomiTheatre: which maps are the theatre and which one is open

The haunted theatre joins the Halloween event period with the maze.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

```json:metadata
{"files": ["Chaos/Services/Theatre/SuomiTheatre.cs", "Chaos/Models/World/EventPeriod.cs", "Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/(SuomiTheatreTests)/*\"", "acceptanceCriteria": ["IsTheatre true for both ids any case, false for backstage/other/null", "OpenInstanceId and Resolve and MoveTarget return the documented values", "haunted theatre shares the maze's Halloween window on an 800-day sweep"], "modelTier": "standard"}
```

---

### Task 2: Route every theatre id through the helper

**Goal:** Nothing outside `SuomiTheatre` hard-codes `"suomi_theatre"` any more, so director tools work on both maps and every way into the theatre lands on the open one.

**Files:**
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` (12 `LoadedFromInstanceId.EqualsI("suomi_theatre")` checks, one `SimpleCache.Get<MapInstance>("suomi_theatre")` at the backstage removal, around line 471)
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusTheatrePortScript.cs` (lines 31, 50, 56, 74)
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Generic/LanternDialogScript.cs` (line 32)
- Modify: `Chaos/Scripting/EffectScripts/TheatreSilenceEffect.cs` (lines 43, 60)
- Modify: `Chaos/Scripting/ReactorTileScripts/WarpScript.cs` (the first line of `OnWalkedOn`)
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs` (the last line of `OnNext`)
- Modify: `Chaos/Messaging/Admin/DisplayMapListCommand.cs` (line 780, the list holding `"suomi_theatre"`)
- Test: `Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs` (one more test)

**Acceptance Criteria:**
- [ ] `rg -n -F '"suomi_theatre"' Chaos --glob "*.cs"` lists only `Chaos/Services/Theatre/SuomiTheatre.cs` and the admin map list in `DisplayMapListCommand.cs`.
- [ ] `WarpScript` and `MirrorDoorDialogScript` look their target map up through `SuomiTheatre.Resolve(<destination map>, DateTime.UtcNow)`, so the backstage exits, the Suomi town door and the maze's return mirror reach the open theatre.
- [ ] `TerminusTheatrePortScript`'s two map lookups and `SuomiTheatreScript`'s backstage-removal lookup use `SuomiTheatre.OpenInstanceId(DateTime.UtcNow)`.
- [ ] The theatre's mirror door question is the warning one from the haunted theatre too (`MirrorDoorScript.StepDialogKey("suomi_theatre_halloween", "mirror_maze")` is `mirrordoor_warn`).
- [ ] The whole server solution builds with 0 errors, and `SuomiTheatreTests`, `MirrorDoorDialogKeyTests`, `SuomiTheatreMapScriptTests`, `TheatreStageEffectsTests`, `TheatreLanternsTests` pass.

**Verify:** `dotnet build Chaos.sln --no-restore -v q` → `Build succeeded` with 0 errors; then `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreTests)|(MirrorDoorDialogKeyTests)|(SuomiTheatreMapScriptTests)|(TheatreStageEffectsTests)|(TheatreLanternsTests)/*"` → 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing test for the haunted theatre's mirror warning**

Add this method to `SuomiTheatreTests` (add `using Chaos.Scripting.ReactorTileScripts.Temauir.MirrorMaze;` to the usings):

```csharp
    //the question a player gets stepping into the maze must be the warning one from either theatre
    [Test]
    public void The_haunted_theatre_mirror_asks_the_warning_question()
        => MirrorDoorScript.StepDialogKey(SuomiTheatre.HALLOWEEN_ID, MirrorDoorScript.MAZE_INSTANCE_ID)
                           .Should()
                           .Be(MirrorDoorScript.WARN_DIALOG_KEY);
```

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreTests)/*"`
Expected: PASS already (the key logic only compares the maze id). It is here to keep that true.

- [ ] **Step 2: Replace the "is this the theatre" checks in `SuomiTheatreScript`**

Add `using Chaos.Services.Theatre;` to the usings if it is not there. Then run this from the server worktree. It rewrites only the exact `"suomi_theatre"` comparisons (the `_backstage` ones end in `_backstage"` and do not match):

```powershell
$path = "Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs"
$text = Get-Content $path -Raw
$text = [regex]::Replace($text, '([\w\.]+)\.LoadedFromInstanceId\.EqualsI\("suomi_theatre"\)', 'SuomiTheatre.IsTheatre($1.LoadedFromInstanceId)')
Set-Content $path $text -NoNewline
```

Then fix the one lookup that sends a player into the theatre (the backstage removal, around line 471). Change:

```csharp
        var mapInstance = SimpleCache.Get<MapInstance>("suomi_theatre");
        invitedPlayer.Aisling.TraverseMap(mapInstance, point);
```

to:

```csharp
        var mapInstance = SimpleCache.Get<MapInstance>(SuomiTheatre.OpenInstanceId(DateTime.UtcNow));
        invitedPlayer.Aisling.TraverseMap(mapInstance, point);
```

Check: `rg -n -F "suomi_theatre\"" Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` → no matches. `rg -c "SuomiTheatre.IsTheatre" Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` → 12.

- [ ] **Step 3: Fix `TerminusTheatrePortScript`**

Add `using Chaos.Services.Theatre;`. Make these four edits.

`GetValidRandomPoint` (line 31):

```csharp
        var map = SimpleCache.Get<MapInstance>(SuomiTheatre.OpenInstanceId(DateTime.UtcNow));
```

The director check (line 50) changes from `x.Aisling.MapInstance.InstanceId.Equals("suomi_theatre", StringComparison.OrdinalIgnoreCase)` to:

```csharp
                    => SuomiTheatre.IsTheatre(x.Aisling.MapInstance.InstanceId)
```

The "not already there" check (line 56) changes from `!source.MapInstance.InstanceId.EqualsI("suomi_theatre")` to:

```csharp
                    && !SuomiTheatre.IsTheatre(source.MapInstance.InstanceId)
```

The portal's destination (line 74):

```csharp
                var map = SimpleCache.Get<MapInstance>(SuomiTheatre.OpenInstanceId(DateTime.UtcNow));
```

- [ ] **Step 4: Fix `LanternDialogScript` and `TheatreSilenceEffect`**

Add `using Chaos.Services.Theatre;` to both.

`LanternDialogScript.cs` line 32 changes from `source.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre")` to:

```csharp
                if (SuomiTheatre.IsTheatre(source.MapInstance.LoadedFromInstanceId)
```

`TheatreSilenceEffect.cs` lines 43 and 60 change from `!Subject.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre")` and `!target.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre")` to:

```csharp
        if (!SuomiTheatre.IsTheatre(Subject.MapInstance.LoadedFromInstanceId))
```

```csharp
        if (!SuomiTheatre.IsTheatre(target.MapInstance.LoadedFromInstanceId))
```

- [ ] **Step 5: Make fixed-destination warps resolve at the moment of use**

`WarpScript.cs`: add `using Chaos.Services.Theatre;`, and change the first line of `OnWalkedOn` from:

```csharp
        var targetMap = SimpleCache.Get<MapInstance>(Destination.Map);
```

to:

```csharp
        //a fixed "suomi_theatre" destination (backstage exits, the town door) leads to whichever theatre is open
        var targetMap = SimpleCache.Get<MapInstance>(SuomiTheatre.Resolve(Destination.Map, DateTime.UtcNow));
```

`MirrorDoorDialogScript.cs`: add `using Chaos.Services.Theatre;`, and change the last line of `OnNext` from:

```csharp
        source.TraverseMap(SimpleCache.Get<MapInstance>(trip.Destination.Map), trip.Destination);
```

to:

```csharp
        //the maze's way back names the normal theatre; inside the Halloween window that is the haunted one
        source.TraverseMap(SimpleCache.Get<MapInstance>(SuomiTheatre.Resolve(trip.Destination.Map, DateTime.UtcNow)), trip.Destination);
```

- [ ] **Step 6: Add the haunted theatre to the admin map list**

In `DisplayMapListCommand.cs`, find the line `"suomi_theatre",` (around line 780) and add a line after it:

```csharp
        "suomi_theatre_halloween",
```

- [ ] **Step 7: Check no hard-coded id is left, then build and test**

Run: `rg -n -F '"suomi_theatre"' Chaos --glob "*.cs"`
Expected: only `Chaos/Services/Theatre/SuomiTheatre.cs` (the constant) and `Chaos/Messaging/Admin/DisplayMapListCommand.cs`.

Run: `dotnet build Chaos.sln --no-restore -v q`
Expected: Build succeeded, 0 errors.

Run the Verify test command above. Expected: 0 failed.

- [ ] **Step 8: Commit**

```bash
git add -A Chaos Tests
git commit -m "Send every theatre check and entrance through SuomiTheatre

Director tools, lanterns and silence work on either theatre, and the Suomi
door, backstage exits, Terminus portal and the maze's return mirror all land
on whichever theatre is open.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusTheatrePortScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/LanternDialogScript.cs", "Chaos/Scripting/EffectScripts/TheatreSilenceEffect.cs", "Chaos/Scripting/ReactorTileScripts/WarpScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs", "Chaos/Messaging/Admin/DisplayMapListCommand.cs", "Tests/Chaos.Tests/Theatre/SuomiTheatreTests.cs"], "verifyCommand": "dotnet build Chaos.sln --no-restore -v q && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/(SuomiTheatreTests)|(MirrorDoorDialogKeyTests)|(SuomiTheatreMapScriptTests)|(TheatreStageEffectsTests)|(TheatreLanternsTests)/*\"", "acceptanceCriteria": ["only SuomiTheatre.cs and the admin map list contain the literal suomi_theatre", "WarpScript and MirrorDoorDialogScript resolve via SuomiTheatre.Resolve", "solution builds, listed test classes pass"], "modelTier": "standard"}
```

---

### Task 3: Move players when the open theatre changes

**Goal:** Each theatre's map script moves everyone to the same tile on the open theatre when the Halloween window opens or closes.

**Files:**
- Modify: `Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs` (constructor, `Update`)
- Modify: `Tests/Chaos.Tests/Theatre/SuomiTheatreMapScriptTests.cs` (the constructor call)

**Acceptance Criteria:**
- [ ] `SuomiTheatreMapScript` takes an `ISimpleCache` as a fourth constructor argument and keeps it in a field.
- [ ] Once a second, in `Update`, the script asks `SuomiTheatre.MoveTarget(Subject.LoadedFromInstanceId, halloweenActive)` and, when it returns an id, moves every Aisling on the map to the same X, Y on that map and stops the rest of that update.
- [ ] The existing test still passes with the new argument (`The_theatre_darkness_does_not_hide_players`), and the solution builds.
- [ ] The decision itself (who is moved, who stays) is covered by Task 1's `MoveTarget` tests; the wiring is covered by the in-game check in Task 7.

**Verify:** `dotnet build Chaos.sln --no-restore -v q` → 0 errors; `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreMapScriptTests)|(SuomiTheatreTests)/*"` → 0 failed.

**Steps:**

- [ ] **Step 1: Update the existing test's constructor call (it will fail to compile first)**

In `SuomiTheatreMapScriptTests.cs`, change the construction to pass a simple cache mock:

```csharp
        _ = new SuomiTheatreMapScript(
            map,
            new Mock<IEffectFactory>().Object,
            new Mock<IStorage<TheatreLightingScenes>>().Object,
            new Mock<ISimpleCache>().Object);
```

(`ISimpleCache` is in `Chaos.Storage.Abstractions`, already imported.)

Run: `dotnet build Chaos.sln --no-restore -v q`
Expected: error, no constructor takes 4 arguments.

- [ ] **Step 2: Add the field and constructor argument**

In `SuomiTheatreMapScript.cs`, add `using Chaos.Definitions;` if it is not there (it is, for `MapFlags`), and `using Chaos.Models.World;` is already present. Change the fields and constructor:

```csharp
    private readonly IEffectFactory EffectFactory;
    private readonly StageLightingRateLimiter RateLimiter = new();
    private readonly IStorage<TheatreLightingScenes> SceneStorage;
    private readonly ISimpleCache SimpleCache;
```

```csharp
    public SuomiTheatreMapScript(
        MapInstance subject,
        IEffectFactory effectFactory,
        IStorage<TheatreLightingScenes> sceneStorage,
        ISimpleCache simpleCache)
        : base(subject)
    {
        EffectFactory = effectFactory;
        SceneStorage = sceneStorage;
        SimpleCache = simpleCache;
```

(keep the rest of the constructor body unchanged).

- [ ] **Step 3: Move players in `Update`**

(`TraverseMap` is an extension used elsewhere in this folder. If it does not resolve, add the `using Chaos.Extensions;` that `WarpScript.cs` has.)

In `Update`, right after the `if (!UpdateTimer.IntervalElapsed) return;` block and before `TrySyncDarkness(DateTime.UtcNow);`, add:

```csharp
        if (TryMoveToOpenTheatre())
            return;
```

Add the method beside `TrySyncDarkness`:

```csharp
    /// <summary>
    ///     When this theatre is the closed one (the Halloween window just opened or closed), moves everyone on it to the
    ///     same tile on the open theatre. The two layouts match, so nobody lands in a wall. Admins and directors are
    ///     moved too. Lighting starts fresh on the other map, as it does after a restart; saved scenes are shared.
    /// </summary>
    private bool TryMoveToOpenTheatre()
    {
        var halloweenActive = EventPeriod.IsSpecificEventActive(DateTime.UtcNow, SuomiTheatre.HALLOWEEN_ID, EventType.Halloween);
        var targetId = SuomiTheatre.MoveTarget(Subject.LoadedFromInstanceId, halloweenActive);

        if (targetId is null)
            return false;

        var target = SimpleCache.Get<MapInstance>(targetId);

        foreach (var aisling in Subject.GetEntities<Aisling>()
                                       .ToList())
            aisling.TraverseMap(target, new Point(aisling.X, aisling.Y));

        return true;
    }
```

- [ ] **Step 4: Build and test**

Run: `dotnet build Chaos.sln --no-restore -v q` → Build succeeded, 0 errors.
Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreMapScriptTests)|(SuomiTheatreTests)/*"` → 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs Tests/Chaos.Tests/Theatre/SuomiTheatreMapScriptTests.cs
git commit -m "Move players to the open theatre when the Halloween window changes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

```json:metadata
{"files": ["Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs", "Tests/Chaos.Tests/Theatre/SuomiTheatreMapScriptTests.cs"], "verifyCommand": "dotnet build Chaos.sln --no-restore -v q && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/(SuomiTheatreMapScriptTests)|(SuomiTheatreTests)/*\"", "acceptanceCriteria": ["map script takes ISimpleCache and moves everyone to the same tile on the open theatre once a second", "existing darkness test passes with the new argument", "solution builds"], "modelTier": "standard"}
```

---

### Task 4: The map decoration tool (floor swap, props, safety checks)

**Goal:** `decorate_theatre.py` copies `lod346` to `lod10269`, swaps the house floor, places props from a table, and refuses to touch anything protected or to change which tiles can reach which.

**Files:**
- Create: `Tools/HauntedTheatre/decorate_theatre.py` (Unora worktree)
- Create: `Tools/HauntedTheatre/test_decorate_theatre.py`

**Acceptance Criteria:**
- [ ] `python Tools/HauntedTheatre/decorate_theatre.py --check` builds the decorated map in memory, prints a summary, writes nothing, and exits 0 when there are no problems.
- [ ] Without `--check`, it writes `Data/Configuration/MapData/lod10269.map` (20x31x6 = 3720 bytes) and `Data/Configuration/Templates/Maps/10269.json` (`width` 20, `height` 31, `templateKey` `"10269"`, `scriptKeys` `[]`).
- [ ] Every tile in the stage rectangle keeps the value it had in `lod346`, and every protected tile keeps its foreground.
- [ ] The only changes outside props are house-floor backgrounds (ids 6929 to 6944, outside the stage) becoming the Macabre checker (`11056` / `11462` by `(x + y) % 2`).
- [ ] A prop placed on a protected tile, on a wall, or on an already-occupied tile is reported as a problem; so is a placement that changes which protected tiles can reach each other.
- [ ] `lod10269` is not in `Unora/UnusedMapData`, and `python -m pytest Tools/HauntedTheatre -q` passes.

**Verify:** `python -m pytest Tools/HauntedTheatre -q` → all passed; `python Tools/HauntedTheatre/decorate_theatre.py --check` → ends with `no problems`, exit 0.

**Steps:**

- [ ] **Step 1: Create the Unora worktree**

```bash
git -C C:/Users/Michael/Documents/GitHub/Unora worktree add C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-unora -b feat/haunted-theatre main
```

Work in `C:/Users/Michael/Documents/GitHub/worktrees/haunted-theatre-unora` from here on. Run the Python commands from that folder.

- [ ] **Step 2: Write the failing tests**

Create `Tools/HauntedTheatre/test_decorate_theatre.py`:

```python
"""Tests for the haunted theatre map tool. Run from the Unora repo root: python -m pytest Tools/HauntedTheatre -q"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import decorate_theatre as d  # noqa: E402

ORIGINAL = d.read_tiles(d.CONFIG / "MapData" / f"lod{d.SOURCE_ID}.map")
PROTECTED = d.protected_tiles()
BUILT = d.build()


def test_the_default_build_has_no_problems():
    assert BUILT.problems == []


def test_the_map_file_is_20_by_31_tiles_of_six_bytes():
    assert len(d.map_bytes(BUILT.tiles)) == 20 * 31 * 6


def test_the_new_map_number_is_free_in_the_archive():
    assert not (d.ROOT / "UnusedMapData" / f"lod{d.MAP_ID}.map").exists()


def test_the_stage_keeps_its_values_and_protected_tiles_keep_their_foreground():
    sx, sy, sw, sh = d.STAGE
    for (x, y) in PROTECTED:
        assert BUILT.tiles[y][x][1:] == ORIGINAL[y][x][1:], (x, y)
        if sx <= x < sx + sw and sy <= y < sy + sh:
            assert BUILT.tiles[y][x] == ORIGINAL[y][x], (x, y)


def test_the_only_changes_outside_props_are_house_floor_backgrounds():
    prop_tiles = {(x, y) for p in d.PROPS for (x, y) in p.tiles}
    for y in range(d.HEIGHT):
        for x in range(d.WIDTH):
            before, after = ORIGINAL[y][x], BUILT.tiles[y][x]
            if before == after or (x, y) in prop_tiles:
                continue
            assert before[1:] == after[1:], f"foreground changed at {(x, y)}"
            assert before[0] in d.HOUSE_FLOOR and after[0] == d.FLOOR[(x + y) % 2], f"background changed at {(x, y)}"


def test_a_prop_on_a_protected_tile_is_a_problem():
    stage_tile = (3, 15)
    problems = d.check(d.decorate([d.Prop("bad", 7000, 7001, [stage_tile])]), PROTECTED)
    assert any("protected" in p for p in problems)


def test_a_prop_on_a_wall_is_a_problem():
    wall = next((x, y) for y in range(d.HEIGHT) for x in range(d.WIDTH)
                if d.is_wall(ORIGINAL[y][x]) and (x, y) not in PROTECTED)
    problems = d.check(d.decorate([d.Prop("bad", 7000, 7001, [wall])]), PROTECTED)
    assert any("wall" in p for p in problems)


def test_props_that_cut_the_hall_in_two_are_a_problem():
    # foreground 1 is a wall in sotp.dat that is never drawn. Filling every free tile with it leaves the protected spots
    # (Thulin's hall, the town doors, the stage) unable to reach each other
    free = [(x, y) for y in range(d.HEIGHT) for x in range(d.WIDTH)
            if (x, y) not in PROTECTED and not d.is_wall(ORIGINAL[y][x]) and not ORIGINAL[y][x][1] and not ORIGINAL[y][x][2]]
    problems = d.check(d.decorate([d.Prop("everything", 1, 0, free)]), PROTECTED)
    assert any("reach" in p for p in problems)
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `python -m pytest Tools/HauntedTheatre -q`
Expected: collection error, `decorate_theatre` is not found.

- [ ] **Step 4: Write the tool**

Create `Tools/HauntedTheatre/decorate_theatre.py`:

```python
"""Dress a copy of the Suomi theatre for Halloween: the map file and its template.
Run from the Unora repo root:

    python Tools/HauntedTheatre/decorate_theatre.py --check   # build in memory, check, print the summary, write nothing
    python Tools/HauntedTheatre/decorate_theatre.py           # write lod10269.map and Templates/Maps/10269.json

The map is a copy of lod346 (20x31). The house floor becomes the dark Macabre checker, and the props in PROPS are placed
on open floor. The stage, every doorway and Thulin's spot, and the mirror wall are protected: nothing there changes, and
nothing placed may change which of those spots can reach which. Run it once, then tweak the map by hand in
ChaosAssetManager; running it again overwrites hand edits.

Preview: dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10269.map 20 31 theatre.png
"""
from __future__ import annotations

import argparse
import json
import os
import re
import struct
import sys
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "Data" / "Configuration"
SOTP = Path(os.environ.get(
    "SOTP_DAT", r"C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server\Chaos\Resources\sotp.dat"))
NORMAL_DIR = CONFIG / "MapInstances" / "Temuair" / "Towns" / "Suomi" / "suomi_theatre"

SOURCE_ID = 346
MAP_ID = 10269
WIDTH, HEIGHT = 20, 31

STAGE = (0, 12, 9, 9)  # x, y, width, height
MIRROR_WALL = (5, 2)  # the theatre mirror hangs on this wall tile; its door is (5, 3)
HOUSE_FLOOR = range(6929, 6945)  # the theatre's house floor backgrounds
FLOOR = (11056, 11462)  # Macabre Mansion checker, by (x + y) % 2
WALL_FLAG = 15  # sotp.dat: all four low bits set means the tile blocks

SOTP_BYTES = SOTP.read_bytes() if SOTP.exists() else b""


@dataclass
class Prop:
    """One decoration: a left and a right foreground tile (0 leaves that slot alone), placed on each of `tiles`."""
    name: str
    left: int
    right: int
    tiles: list[tuple[int, int]]


# Filled in with the user in Task 5 of the plan, from the tile sheet. Each entry is a Prop(...) as above.
PROPS: list[Prop] = []


@dataclass
class Theatre:
    tiles: list
    problems: list[str] = field(default_factory=list)
    placed: list = field(default_factory=list)  # (prop name, (x, y)) for every placement


def read_tiles(path: Path) -> list:
    data = path.read_bytes()
    assert len(data) == WIDTH * HEIGHT * 6, f"{path} is not {WIDTH}x{HEIGHT}"
    return [[list(struct.unpack_from("<hhh", data, (y * WIDTH + x) * 6)) for x in range(WIDTH)] for y in range(HEIGHT)]


def map_bytes(tiles) -> bytes:
    out = bytearray()
    for row in tiles:
        for bg, lf, rf in row:
            out += struct.pack("<hhh", bg, lf, rf)
    return bytes(out)


def blocks(foreground: int) -> bool:
    return foreground > 0 and foreground <= len(SOTP_BYTES) and (SOTP_BYTES[foreground - 1] & WALL_FLAG) == WALL_FLAG


def is_wall(tile) -> bool:
    return blocks(tile[1]) or blocks(tile[2])


def spots(path: Path, key: str) -> list[tuple[int, int]]:
    out = []
    for entry in json.loads(path.read_text(encoding="utf-8")):
        x, y = map(int, re.findall(r"\d+", entry[key]))
        out.append((x, y))
    return out


def protected_tiles() -> set[tuple[int, int]]:
    """The stage, every doorway and merchant of the normal theatre with its four neighbours, and the mirror and its door."""
    sx, sy, sw, sh = STAGE
    out = {(x, y) for x in range(sx, sx + sw) for y in range(sy, sy + sh)}
    centres = spots(NORMAL_DIR / "reactors.json", "source") + spots(NORMAL_DIR / "merchants.json", "spawnPoint")
    centres += [MIRROR_WALL, (MIRROR_WALL[0], MIRROR_WALL[1] + 1)]
    for x, y in centres:
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            if 0 <= x + dx < WIDTH and 0 <= y + dy < HEIGHT:
                out.add((x + dx, y + dy))
    return out


def decorate(props: list[Prop]) -> Theatre:
    tiles = read_tiles(CONFIG / "MapData" / f"lod{SOURCE_ID}.map")
    sx, sy, sw, sh = STAGE
    for y in range(HEIGHT):
        for x in range(WIDTH):
            on_stage = sx <= x < sx + sw and sy <= y < sy + sh
            if not on_stage and tiles[y][x][0] in HOUSE_FLOOR:
                tiles[y][x][0] = FLOOR[(x + y) % 2]
    theatre = Theatre(tiles)
    for prop in props:
        for x, y in prop.tiles:
            if not (0 <= x < WIDTH and 0 <= y < HEIGHT):
                theatre.problems.append(f"{prop.name}: ({x}, {y}) is off the map")
                continue
            tile = tiles[y][x]
            if prop.left:
                tile[1] = prop.left
            if prop.right:
                tile[2] = prop.right
    theatre.placed = [(p.name, t) for p in props for t in p.tiles]
    return theatre


def components(tiles, spots_to_label) -> list[frozenset]:
    """The groups of `spots_to_label` that can walk to each other over non-wall tiles."""
    def walkable(x, y):
        return 0 <= x < WIDTH and 0 <= y < HEIGHT and not is_wall(tiles[y][x])

    groups, seen = [], set()
    for start in sorted(spots_to_label):
        if start in seen or not walkable(*start):
            continue
        comp, queue = {start}, deque([start])
        while queue:
            x, y = queue.popleft()
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if (nx, ny) not in comp and walkable(nx, ny):
                    comp.add((nx, ny))
                    queue.append((nx, ny))
        seen |= comp
        groups.append(frozenset(comp & set(spots_to_label)))
    return groups


def check(theatre: Theatre, protected: set) -> list[str]:
    problems = list(theatre.problems)
    original = read_tiles(CONFIG / "MapData" / f"lod{SOURCE_ID}.map")
    for name, (x, y) in theatre.placed:
        if not (0 <= x < WIDTH and 0 <= y < HEIGHT):
            continue
        if (x, y) in protected:
            problems.append(f"{name}: ({x}, {y}) is a protected tile")
        elif is_wall(original[y][x]):
            problems.append(f"{name}: ({x}, {y}) is already a wall")
        elif original[y][x][1] or original[y][x][2]:
            problems.append(f"{name}: ({x}, {y}) already has something on it")
    if set(components(original, protected)) != set(components(theatre.tiles, protected)):
        problems.append("the props change which protected spots can reach each other")
    return problems


def build() -> Theatre:
    theatre = decorate(PROPS)
    theatre.problems = check(theatre, protected_tiles())
    return theatre


def write(theatre: Theatre) -> None:
    (CONFIG / "MapData" / f"lod{MAP_ID}.map").write_bytes(map_bytes(theatre.tiles))
    template = {"height": HEIGHT, "scriptKeys": [], "templateKey": str(MAP_ID), "width": WIDTH}
    (CONFIG / "Templates" / "Maps" / f"{MAP_ID}.json").write_text(json.dumps(template, indent=2) + "\n", encoding="utf-8")


def summary(theatre: Theatre) -> str:
    swapped = sum(1 for row in theatre.tiles for tile in row if tile[0] in FLOOR)
    lines = [f"map {MAP_ID}: {WIDTH}x{HEIGHT} copied from lod{SOURCE_ID}",
             f"floor tiles on the Macabre checker: {swapped}",
             f"props: {len(PROPS)} kinds, {sum(len(p.tiles) for p in PROPS)} tiles",
             f"protected tiles: {len(protected_tiles())}"]
    lines += [f"PROBLEM: {p}" for p in theatre.problems]
    lines.append("no problems" if not theatre.problems else f"{len(theatre.problems)} problem(s)")
    return "\n".join(lines)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="build and check in memory; write nothing")
    args = ap.parse_args()
    if not SOTP_BYTES:
        print(f"sotp.dat not found at {SOTP}; set the SOTP_DAT environment variable", file=sys.stderr)
        return 2
    theatre = build()
    print(summary(theatre))
    if theatre.problems:
        return 1
    if not args.check:
        write(theatre)
        print(f"wrote lod{MAP_ID}.map and Templates/Maps/{MAP_ID}.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

Note: `sotp.dat` lives in the server repo. The default path is the main checkout's; set `SOTP_DAT` if that moves.

- [ ] **Step 5: Run the tests to see them pass**

Run: `python -m pytest Tools/HauntedTheatre -q`
Expected: 8 passed. If `test_props_that_cut_the_hall_in_two_are_a_problem` fails, first check that `is_wall` treats foreground `1` as blocking (`SOTP_BYTES[0] & 15 == 15`); the mirror maze relies on the same fact.

- [ ] **Step 6: Check the tool runs on the real map**

Run: `python Tools/HauntedTheatre/decorate_theatre.py --check`
Expected output ends with `no problems`; nothing is written (`git status` shows only the two new tool files).

- [ ] **Step 7: Commit**

```bash
git add Tools/HauntedTheatre/decorate_theatre.py Tools/HauntedTheatre/test_decorate_theatre.py
git commit -m "Add the haunted theatre map tool

Copies lod346, swaps the house floor and places props, and refuses to touch the
stage, the doors or the mirror, or to cut one protected spot off from another.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

```json:metadata
{"files": ["Tools/HauntedTheatre/decorate_theatre.py", "Tools/HauntedTheatre/test_decorate_theatre.py"], "verifyCommand": "python -m pytest Tools/HauntedTheatre -q && python Tools/HauntedTheatre/decorate_theatre.py --check", "acceptanceCriteria": ["--check builds in memory and exits 0 with 'no problems'", "protected and stage tiles unchanged; only house floor backgrounds change outside props", "props on protected tiles, walls, occupied tiles or that change reachability are reported", "lod10269 not present in UnusedMapData"], "modelTier": "standard"}
```

---

### Task 5: Choose the decorations with the user and write the map

**Goal:** The `PROPS` table holds a Halloween look the user has seen and approved in a rendered preview, and `lod10269.map` plus its template are written.

**Files:**
- Create: `Tools/HauntedTheatre/tile_sheet.py` (Unora worktree)
- Modify: `Tools/HauntedTheatre/decorate_theatre.py` (the `PROPS` list)
- Create: `Data/Configuration/MapData/lod10269.map`
- Create: `Data/Configuration/Templates/Maps/10269.json`

**Acceptance Criteria:**
- [ ] `tile_sheet.py` writes a test map of every distinct foreground tile used by the Halloween maps (default: `373`, the Macabre Yard), a CSV of which id sits at which tile, and a labelled preview picture.
- [ ] `PROPS` holds at least a graveyard group, candles or pumpkins, and webs or skulls, all on tiles that `decorate_theatre.py --check` accepts.
- [ ] The user has looked at the rendered preview of the decorated theatre and approved it (or asked for changes and the loop repeated).
- [ ] `lod10269.map` is 3720 bytes and `Templates/Maps/10269.json` exists; `python -m pytest Tools/HauntedTheatre -q` still passes.

**Verify:** `python Tools/HauntedTheatre/decorate_theatre.py` → prints `wrote lod10269.map and Templates/Maps/10269.json`; `(Get-Item Data/Configuration/MapData/lod10269.map).Length` → `3720`.

**Steps:**

- [ ] **Step 1: Write the tile sheet tool**

Create `Tools/HauntedTheatre/tile_sheet.py`:

```python
"""Lay candidate foreground tiles out on a test map so they can be looked at.
Run from the Unora repo root:

    python Tools/HauntedTheatre/tile_sheet.py 373 --out sheet      # ids used by map 373 (the Macabre Yard)
    dotnet run Tools/MirrorMaze/render_map.cs -- sheet.map <width> <height> sheet.png
    python Tools/HauntedTheatre/tile_sheet.py --label sheet.png sheet.csv

The first command writes sheet.map and sheet.csv (index, slot, tile id, x, y) and prints the width and height to render
with. The last one draws each index on the picture. If the labels sit a little off, change --dx / --dy.
"""
from __future__ import annotations

import argparse
import csv
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "Data" / "Configuration"
FLOOR = (11056, 11462)
COLUMNS, SPACING = 10, 4


def ids_used(map_id: int) -> set[tuple[str, int]]:
    template = json.loads((CONFIG / "Templates" / "Maps" / f"{map_id}.json").read_text(encoding="utf-8"))
    data = (CONFIG / "MapData" / f"lod{map_id}.map").read_bytes()
    found = set()
    for i in range(template["width"] * template["height"]):
        _, lf, rf = struct.unpack_from("<hhh", data, i * 6)
        if lf > 1:
            found.add(("lf", lf))
        if rf > 1:
            found.add(("rf", rf))
    return found


def lay_out(ids: list[tuple[str, int]]):
    rows = (len(ids) + COLUMNS - 1) // COLUMNS
    width, height = COLUMNS * SPACING + 2, rows * SPACING + 2
    tiles = [[[FLOOR[(x + y) % 2], 0, 0] for x in range(width)] for y in range(height)]
    placed = []
    for i, (slot, tile_id) in enumerate(ids):
        x, y = 2 + (i % COLUMNS) * SPACING, 2 + (i // COLUMNS) * SPACING
        tiles[y][x][1 if slot == "lf" else 2] = tile_id
        placed.append((i, slot, tile_id, x, y))
    return width, height, tiles, placed


def write_sheet(map_ids: list[int], out: Path) -> None:
    ids = sorted(set().union(*(ids_used(m) for m in map_ids)), key=lambda t: (t[1], t[0]))
    width, height, tiles, placed = lay_out(ids)
    data = bytearray()
    for row in tiles:
        for bg, lf, rf in row:
            data += struct.pack("<hhh", bg, lf, rf)
    out.with_suffix(".map").write_bytes(bytes(data))
    with out.with_suffix(".csv").open("w", newline="", encoding="utf-8") as f:
        csv.writer(f).writerows([("index", "slot", "id", "x", "y"), *placed])
    print(f"{len(ids)} tiles on a {width}x{height} sheet -> {out.with_suffix('.map')}")
    print(f"render with: dotnet run Tools/MirrorMaze/render_map.cs -- {out.with_suffix('.map')} {width} {height} {out.with_suffix('.png')}")


def label(png: Path, csv_path: Path, dx: int, dy: int) -> None:
    from PIL import Image, ImageDraw
    rows = list(csv.DictReader(csv_path.open(encoding="utf-8")))
    width = max(int(r["x"]) for r in rows) + 2
    height = max(int(r["y"]) for r in rows) + 2
    im = Image.open(png).convert("RGB")
    top = im.height - (width + height) * 14
    d = ImageDraw.Draw(im)
    for r in rows:
        x, y = int(r["x"]), int(r["y"])
        px = (height - 1 + x - y) * 28 + 28 + dx
        py = (x + y) * 14 + top + dy
        d.text((px, py), r["index"], fill=(255, 255, 0))
    im.save(png)
    print(f"labelled {len(rows)} tiles on {png}")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("maps", nargs="*", type=int, default=[373], help="map numbers to take tile ids from")
    ap.add_argument("--out", type=Path, default=Path("sheet"))
    ap.add_argument("--label", nargs=2, metavar=("PNG", "CSV"))
    ap.add_argument("--dx", type=int, default=0)
    ap.add_argument("--dy", type=int, default=0)
    args = ap.parse_args()
    if args.label:
        label(Path(args.label[0]), Path(args.label[1]), args.dx, args.dy)
    else:
        write_sheet(args.maps, args.out)


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Make the sheet and look at it**

```bash
python Tools/HauntedTheatre/tile_sheet.py 373 --out sheet
dotnet run Tools/MirrorMaze/render_map.cs -- sheet.map <width> <height> sheet.png
python Tools/HauntedTheatre/tile_sheet.py --label sheet.png sheet.csv
```

(Use the width and height the first command prints.) Open `sheet.png`. If the numbers do not sit under their tile, nudge `--dx` / `--dy` and relabel (re-render first, since labelling draws on the picture). Note which ids look like: gravestones and crosses, skulls, candles, pumpkins, webs, dead trees, barrels. `sheet.csv` maps each index back to its tile id and slot. Repeat with other Halloween maps (for example `python Tools/HauntedTheatre/tile_sheet.py 10200 10201 --out sheet2`) if the Yard lacks pumpkins, candles or webs.

Do not commit `sheet.*`.

- [ ] **Step 3: Fill the `PROPS` table**

In `decorate_theatre.py`, replace `PROPS: list[Prop] = []` with entries chosen from the sheet. Use open tiles from this map of the theatre (`#` blocks, `s` is the stage, `R` and `M` are doors and Thulin, `.` is free floor; the tool's `--check` rejects anything protected):

```
   01234567890123456789
 3 #....R.......#####.#
 4 #............#####.#
 5 #...#.#.#.#..#.....#
 6 #............#.....#
 7 #...#.#.#.#..#######
 8 #R..................
 9 #M..#.#.#.#........#
10 #..................#
12 Rssssssss###.#.#....
...
22 ##.........#.#.#....
24 #..#.#.#.#.........#
25 #...................
27 #..................#
29 #..................#
```

The shape of an entry, using one slot id and one tile (swap in the real ids from the sheet):

```python
PROPS: list[Prop] = [
    Prop("graveyard", 6526, 6527, [(15, 22), (17, 24)]),
]
```

Aim for these three groups: a small graveyard corner in the bottom-right rooms, candles or pumpkins along the top hall and beside the stage, and skulls or webs scattered in the bottom hall. Keep the walkways clear. Run `python Tools/HauntedTheatre/decorate_theatre.py --check` after each change; it must end with `no problems`.

- [ ] **Step 4: Render the decorated map and show the user**

```bash
python Tools/HauntedTheatre/decorate_theatre.py
dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10269.map 20 31 theatre-halloween.png
```

Show `theatre-halloween.png` to the user next to the original render (`lod346.map`, same command) and ask what to change, using `AskQuestion`: approve, more or less decoration, a different mood (spooky, cute, creepy), a different floor. Edit `PROPS` and rerun until the user approves. This approval is the gate for this task.

- [ ] **Step 5: Run the tests and commit**

Run: `python -m pytest Tools/HauntedTheatre -q` → all passed.
Run: `(Get-Item Data/Configuration/MapData/lod10269.map).Length` → `3720`.

```bash
git add Tools/HauntedTheatre Data/Configuration/MapData/lod10269.map Data/Configuration/Templates/Maps/10269.json
git commit -m "Add the haunted theatre map

A copy of the Suomi theatre on the dark Macabre floor, with graveyard, candle and
skull props. The user approved the preview.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Make sure `sheet.*` and the preview PNGs are not staged: `git status` should show only the paths above.)

```json:metadata
{"files": ["Tools/HauntedTheatre/tile_sheet.py", "Tools/HauntedTheatre/decorate_theatre.py", "Data/Configuration/MapData/lod10269.map", "Data/Configuration/Templates/Maps/10269.json"], "verifyCommand": "python -m pytest Tools/HauntedTheatre -q && python Tools/HauntedTheatre/decorate_theatre.py --check", "acceptanceCriteria": ["tile_sheet.py writes sheet.map, sheet.csv and a labelled picture for the Halloween maps", "PROPS has graveyard, candle or pumpkin, and web or skull groups and --check ends with 'no problems'", "user approved the rendered preview", "lod10269.map is 3720 bytes and Templates/Maps/10269.json exists"], "modelTier": "standard"}
```

---

### Task 6: The haunted theatre instance, the ghosts, and the mirror's new home

**Goal:** `suomi_theatre_halloween` exists as a map instance with the same doors and Thulin as the normal theatre, harmless wandering ghosts, the maze mirror, and the `Ghosts` and `HarvestMoon` baseline; the normal theatre loses its mirror.

**Files:**
- Modify: `Tools/HauntedTheatre/decorate_theatre.py` (add `build_instance` and write it)
- Modify: `Tools/HauntedTheatre/test_decorate_theatre.py` (instance tests)
- Create: `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/theatre_ghost.json`
- Create: `Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/` (`instance.json`, `reactors.json`, `merchants.json`, `monsters.json`, `mirrors.json`), written by the tool
- Modify: `Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json` (remove the `mirrorDoor` reactor at `(5, 3)`)
- Modify: `Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/mirrors.json` (no segments)

**Acceptance Criteria:**
- [ ] `instance.json` has `instanceId` `suomi_theatre_halloween`, `name` `Garamonde Theatre`, `flags` `Ghosts, HarvestMoon`, `scriptKeys` `suomitheatremap` and `mirrormap`, `templateKey` `10269`, `music` 22.
- [ ] The haunted `reactors.json` holds every reactor of the normal theatre (the two town-door warps, the ten backstage warps, the bulletin board) plus one `mirrorDoor` at `(5, 3)` with destination `mirror_maze:(29, 2)`, `facing` `Up`, `halloweenOnly` true.
- [ ] The haunted `merchants.json` holds Thulin at `(1, 9)` and four `theatre_ghost` spawns; no ghost sits on a protected tile or on a wall.
- [ ] The `theatre_ghost` template is a merchant (no stats, no loot, cannot be fought), sprite 310, script `wander`, `wanderIntervalMs` 1500.
- [ ] The normal theatre has no `mirrorDoor` reactor and an empty `segments` list in `mirrors.json`; every other reactor in it is unchanged.
- [ ] `python -m pytest Tools/HauntedTheatre -q` passes.

**Verify:** `python -m pytest Tools/HauntedTheatre -q` → all passed; `python Tools/HauntedTheatre/decorate_theatre.py` → writes the map and the instance; `rg -n "mirrorDoor" Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json` → no matches.

**Steps:**

- [ ] **Step 1: Write the failing instance tests**

Append to `test_decorate_theatre.py`:

```python
import json  # noqa: E402
import re  # noqa: E402

INSTANCE = d.build_instance()


def test_the_instance_describes_the_haunted_theatre():
    inst = INSTANCE["instance.json"]
    assert inst["instanceId"] == "suomi_theatre_halloween"
    assert inst["name"] == "Garamonde Theatre"
    assert inst["flags"] == "Ghosts, HarvestMoon"
    assert inst["scriptKeys"] == ["suomitheatremap", "mirrormap"]
    assert inst["templateKey"] == str(d.MAP_ID)


def test_the_haunted_reactors_are_the_normal_ones_plus_the_mirror_door():
    normal = json.loads((d.NORMAL_DIR / "reactors.json").read_text(encoding="utf-8"))
    normal = [r for r in normal if "mirrorDoor" not in r["scriptKeys"]]
    haunted = INSTANCE["reactors.json"]
    assert haunted[:len(normal)] == normal
    doors = [r for r in haunted if "mirrorDoor" in r["scriptKeys"]]
    assert len(doors) == 1 and doors[0]["source"] == "(5, 3)"
    assert doors[0]["scriptVars"]["mirrorDoor"] == {
        "facing": "Up", "destination": "mirror_maze:(29, 2)", "halloweenOnly": True}


def test_the_ghosts_stand_on_open_floor_away_from_the_doors():
    ghosts = [m for m in INSTANCE["merchants.json"] if m["merchantTemplateKey"] == "theatre_ghost"]
    assert len(ghosts) == 4
    for g in ghosts:
        x, y = map(int, re.findall(r"\d+", g["spawnPoint"]))
        assert (x, y) not in PROTECTED
        assert not d.is_wall(BUILT.tiles[y][x])


def test_thulin_keeps_his_spot():
    thulin = [m for m in INSTANCE["merchants.json"] if m["merchantTemplateKey"] == "Thulin"]
    assert [t["spawnPoint"] for t in thulin] == ["(1, 9)"]


def test_the_ghost_template_is_a_wandering_merchant():
    t = json.loads((d.CONFIG / "Templates" / "Merchants" / "Temauir" / "Events" / "Halloween" / "theatre_ghost.json")
                   .read_text(encoding="utf-8"))
    assert t["templateKey"] == "theatre_ghost" and t["sprite"] == 310
    assert t["scriptKeys"] == ["wander"] and t["wanderIntervalMs"] == 1500
    assert t["itemsForSale"] == [] and t["skillsToTeach"] == [] and t["spellsToTeach"] == []


def test_the_normal_theatre_no_longer_has_the_mirror():
    reactors = json.loads((d.NORMAL_DIR / "reactors.json").read_text(encoding="utf-8"))
    assert not any("mirrorDoor" in r["scriptKeys"] for r in reactors)
    assert json.loads((d.NORMAL_DIR / "mirrors.json").read_text(encoding="utf-8"))["segments"] == []
```

Run: `python -m pytest Tools/HauntedTheatre -q`
Expected: failures, `build_instance` does not exist.

- [ ] **Step 2: Create the ghost template**

Create `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/theatre_ghost.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Restless Spirit",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": [
    "wander"
  ],
  "scriptVars": {},
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 310,
  "templateKey": "theatre_ghost",
  "wanderIntervalMs": 1500
}
```

- [ ] **Step 3: Add `build_instance` and write it**

In `decorate_theatre.py`, add `INSTANCE_DIR` and the constants after `NORMAL_DIR`:

```python
INSTANCE_DIR = CONFIG / "MapInstances" / "Temuair" / "Events" / "Halloween" / "Theatre_Halloween"
INSTANCE_ID = "suomi_theatre_halloween"
GHOST_TEMPLATE = "theatre_ghost"
GHOST_SPAWNS = [(15, 5), (16, 14), (4, 25), (14, 27)]  # open floor away from the stage and the doors
MIRROR_DOOR = {
    "scriptKeys": ["mirrorDoor"],
    "scriptVars": {"mirrorDoor": {"facing": "Up", "destination": "mirror_maze:(29, 2)", "halloweenOnly": True}},
    "shouldBlockPathfinding": True,
    "source": "(5, 3)",
}
MIRROR_SEGMENTS = {"segments": [{"id": "theatre-mirror", "x": 5, "y": 2, "side": "north", "length": 1, "style": "glass"}],
                   "darkStretches": []}
```

Add the builder and extend `write` and `main`:

```python
def build_instance() -> dict:
    """The haunted theatre's instance files: the normal theatre's doors and Thulin, plus the mirror and the ghosts."""
    reactors = [r for r in json.loads((NORMAL_DIR / "reactors.json").read_text(encoding="utf-8"))
                if "mirrorDoor" not in r["scriptKeys"]]
    merchants = json.loads((NORMAL_DIR / "merchants.json").read_text(encoding="utf-8"))
    merchants += [{"blackList": [], "direction": "Down", "extraScriptKeys": [], "merchantTemplateKey": GHOST_TEMPLATE,
                   "spawnPoint": f"({x}, {y})"} for x, y in GHOST_SPAWNS]
    return {
        "instance.json": {"flags": "Ghosts, HarvestMoon", "instanceId": INSTANCE_ID, "music": 22,
                          "name": "Garamonde Theatre", "scriptKeys": ["suomitheatremap", "mirrormap"],
                          "templateKey": str(MAP_ID)},
        "reactors.json": reactors + [MIRROR_DOOR],
        "merchants.json": merchants,
        "monsters.json": [],
        "mirrors.json": MIRROR_SEGMENTS,
    }
```

In `write`, after the template line, add:

```python
    INSTANCE_DIR.mkdir(parents=True, exist_ok=True)
    for name, content in build_instance().items():
        (INSTANCE_DIR / name).write_text(json.dumps(content, indent=2) + "\n", encoding="utf-8")
```

and change the `main` print to `print(f"wrote lod{MAP_ID}.map, Templates/Maps/{MAP_ID}.json and {INSTANCE_DIR.relative_to(ROOT)}")`.

Also add a ghost check to `check` (after the placed-props loop):

```python
    for gx, gy in GHOST_SPAWNS:
        if (gx, gy) in protected or is_wall(theatre.tiles[gy][gx]):
            problems.append(f"ghost spawn ({gx}, {gy}) is on a protected tile or a wall")
```

- [ ] **Step 4: Take the mirror out of the normal theatre**

In `Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json`, delete the last reactor (the one with `"mirrorDoor"`, source `(5, 3)`), including the comma on the reactor before it. Replace `mirrors.json` with:

```json
{
  "segments": [],
  "darkStretches": []
}
```

Check: `python -c "import json; json.load(open('Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json'))"` exits 0.

- [ ] **Step 5: Write the instance and run the tests**

Run: `python Tools/HauntedTheatre/decorate_theatre.py` → prints `no problems` and `wrote lod10269.map, Templates/Maps/10269.json and ...Theatre_Halloween`.
Run: `python -m pytest Tools/HauntedTheatre -q` → all passed.

- [ ] **Step 6: Commit**

```bash
git add Tools/HauntedTheatre Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/theatre_ghost.json Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre Data/Configuration/MapData/lod10269.map Data/Configuration/Templates/Maps/10269.json
git commit -m "Add the haunted theatre instance and its ghosts

Same doors and Thulin as the normal theatre, the maze mirror moved here, four
harmless wandering ghosts, and Ghosts and Harvest Moon on by default.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

```json:metadata
{"files": ["Tools/HauntedTheatre/decorate_theatre.py", "Tools/HauntedTheatre/test_decorate_theatre.py", "Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/theatre_ghost.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/instance.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/reactors.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/merchants.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/monsters.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/mirrors.json", "Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json", "Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/mirrors.json"], "verifyCommand": "python -m pytest Tools/HauntedTheatre -q && python Tools/HauntedTheatre/decorate_theatre.py --check", "acceptanceCriteria": ["instance.json has the documented id, name, flags, scriptKeys, templateKey and music", "haunted reactors equal the normal ones plus one mirrorDoor at (5, 3)", "four theatre_ghost spawns on open non-protected floor plus Thulin at (1, 9)", "normal theatre has no mirrorDoor reactor and empty mirrors segments"], "modelTier": "standard"}
```

---

### Task 7: Docs, the spec's as-shipped notes, and the final check

**Goal:** The behaviour is written down for the next person, the spec records where the build differs from it, and everything is verified before the user merges.

**Files:**
- Create: `docs/haunted-theatre.md` (Unora worktree)
- Modify: `docs/README.md` (Systems table; add one row)
- Modify: `docs/ambient-map-effects.md` (the Suomi theatre section: one added paragraph)
- Modify: `Tools/README.md` (Folder table: add `HauntedTheatre/`)
- Modify: `Chaos.Client/docs/superpowers/specs/2026-10-01-haunted-theatre-design.md` (as-shipped notes)

**Acceptance Criteria:**
- [ ] `docs/haunted-theatre.md` explains the swap, the helper, the map tool, the ghosts, the baseline effects and how to change any of them, in plain short sentences.
- [ ] `docs/README.md` links it, `docs/ambient-map-effects.md` says the haunted theatre starts with `Ghosts` and `HarvestMoon` on, and `Tools/README.md` lists `HauntedTheatre/`.
- [ ] The spec carries **As shipped:** notes for the four differences: the helper lives in `Chaos/Services/Theatre/`; the maze's return warp needed no generator change because the warp is resolved when used; ghosts have no click text; `Resolve` redirects every fixed `suomi_theatre` destination (backstage exits, town door) instead of per-reactor scripts.
- [ ] Server: the solution builds and `SuomiTheatreTests`, `MirrorDoorDialogKeyTests`, `SuomiTheatreMapScriptTests`, `TheatreStageEffectsTests`, `TheatreLanternsTests`, `TheatreLightingScenesTests`, `TheatreDarknessSyncTests`, `StageLightingTests`, `MirrorMapScriptTests` pass. Unora: `python -m pytest Tools/HauntedTheatre Tools/MirrorMaze -q` passes.
- [ ] A preview of the final map is rendered, and the user has the in-game checklist.

**Verify:** server worktree: `dotnet build Chaos.sln --no-restore -v q && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SuomiTheatreTests)|(MirrorDoorDialogKeyTests)|(SuomiTheatreMapScriptTests)|(TheatreStageEffectsTests)|(TheatreLanternsTests)|(TheatreLightingScenesTests)|(TheatreDarknessSyncTests)|(StageLightingTests)|(MirrorMapScriptTests)/*"` → 0 failed; Unora worktree: `python -m pytest Tools/HauntedTheatre Tools/MirrorMaze -q` → all passed.

**Steps:**

- [ ] **Step 1: Write `docs/haunted-theatre.md`**

Create it in the Unora worktree with these sections, in plain short sentences: what players see (the haunted copy replaces the theatre Oct 4 to Nov 4, with a dark floor, props, ghosts, Ghosts and Harvest Moon on, the maze mirror); how it switches (the `SuomiTheatre` helper in `Chaos/Services/Theatre/SuomiTheatre.cs`: `IsTheatre`, `OpenInstanceId`, `Resolve`, `MoveTarget`; the window comes from `EventPeriod`; the theatre map script moves players each second); where things live (the map `lod10269`, the instance folder `Events/Halloween/Theatre_Halloween/`, the ghost template `theatre_ghost`); how to change things (re-decorate with `Tools/HauntedTheatre/decorate_theatre.py` once then hand-edit, change the ghosts in `GHOST_SPAWNS`, change the baseline effects in `instance.json` `flags`, stage effects the director can start are on Thulin's menu); limits (a director's "Clear All Effects" also clears the baseline until the server restarts; the layout must stay the same as `lod346` for the tile-for-tile move; the tool overwrites hand edits).

- [ ] **Step 2: Link it from the other docs**

`docs/README.md`, add a row to the Systems table:

```markdown
| [`haunted-theatre.md`](haunted-theatre.md) | The Halloween copy of the Suomi theatre — how it replaces the normal one, its map and ghosts, and how to change them |
```

`docs/ambient-map-effects.md`, at the end of the "The Suomi theatre" section, add:

```markdown
During the Halloween window the haunted copy of the theatre (`suomi_theatre_halloween`) is the one that is open. Its map
flags start with `Ghosts` and `HarvestMoon` on. "Clear All Effects" clears them too, until the server restarts. See
[`haunted-theatre.md`](haunted-theatre.md).
```

`Tools/README.md`, add a row to the folder table:

```markdown
| `HauntedTheatre/` | Builds the Halloween copy of the Suomi theatre: copies `lod346`, swaps the floor, places props, writes the instance files. `tile_sheet.py` lays out candidate tiles so you can look at them. Run `python Tools/HauntedTheatre/decorate_theatre.py --check` first. |
```

- [ ] **Step 3: Add the as-shipped notes to the spec**

In `Chaos.Client/docs/superpowers/specs/2026-10-01-haunted-theatre-design.md`, add a short section at the end, headed `## 10. As shipped`, with these four items:

```markdown
## 10. As shipped

1. **Helper location.** `SuomiTheatre` is in `Chaos/Services/Theatre/SuomiTheatre.cs`, not under `DialogScripts`, because warp, effect and map scripts use it too. It adds `Resolve` and `MoveTarget`.
2. **Fixed warps.** The stock `WarpScript` and `MirrorDoorDialogScript` call `SuomiTheatre.Resolve` when a player walks on or answers, so the ten backstage exits, the Suomi town door and the maze's return mirror reach the open theatre with no reactor changes. The maze generator and `Mirror_Maze/reactors.json` are unchanged.
3. **Ghosts.** They wander and cannot be fought, but clicking one does nothing. Flavour text is not built.
4. **Window test.** `EventPeriod` reads the real clock for its start date, so tests use a plain `bool` for the window and an 800-day sweep to prove the theatre shares the maze's period.
```

This file is in the `Chaos.Client` repo (`docs/` is ignored there, so use `git add -f`).

- [ ] **Step 4: Run all the checks**

Server worktree: run the Verify command above. Expected: Build succeeded, 0 failed.
Unora worktree: `python -m pytest Tools/HauntedTheatre Tools/MirrorMaze -q` → all passed; `python Tools/HauntedTheatre/decorate_theatre.py --check` → ends with `no problems`.
Check no hard-coded id is left: `rg -n -F '"suomi_theatre"' Chaos --glob "*.cs"` (server worktree) → only `SuomiTheatre.cs` and `DisplayMapListCommand.cs`.
Render the final map: `dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10269.map 20 31 theatre-halloween.png`, and show it to the user.

- [ ] **Step 5: Commit each repo**

Unora worktree:

```bash
git add docs/haunted-theatre.md docs/README.md docs/ambient-map-effects.md Tools/README.md
git commit -m "Document the haunted theatre

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Chaos.Client:

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client add -f docs/superpowers/specs/2026-10-01-haunted-theatre-design.md
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client commit -m "Record how the haunted theatre differs from its spec" -- docs/superpowers/specs/2026-10-01-haunted-theatre-design.md
```

- [ ] **Step 6: Hand the in-game check to the user**

Do not merge or push. Give the user this list (the spec's section 7) and the merge order (server `feat/haunted-theatre`, then Unora `feat/haunted-theatre`; no client change):

1. Before the window (or with the dates moved in a test build): only the normal theatre opens from every door.
2. Inside the window: the Suomi door, Terminus and invites go to the haunted theatre.
3. Stand in the normal theatre when the window opens: you are moved to the same tile in the haunted one.
4. Run the director tools in the haunted theatre: lights, scenes, effects, silence, lanterns.
5. See the ghosts wander. Check you cannot attack them.
6. Use the mirror door to the maze and the maze exit back: you land in the haunted theatre.
7. When the window closes, players inside are moved back to the normal theatre.

```json:metadata
{"files": ["docs/haunted-theatre.md", "docs/README.md", "docs/ambient-map-effects.md", "Tools/README.md", "docs/superpowers/specs/2026-10-01-haunted-theatre-design.md"], "verifyCommand": "python -m pytest Tools/HauntedTheatre Tools/MirrorMaze -q", "acceptanceCriteria": ["haunted-theatre.md exists and is linked from docs/README.md, ambient-map-effects.md and Tools/README.md", "spec has As shipped notes for helper location, fixed warps, ghost click text and window test", "server build and listed theatre and mirror tests pass; Unora tool tests pass", "final preview rendered and in-game checklist given to the user"], "modelTier": "mechanical"}
```
